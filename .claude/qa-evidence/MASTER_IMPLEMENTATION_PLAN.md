# Master implementation plan — MAUI iOS audit 2026-11-10

Planning gate output (read-only review). Worktree `.agent-worktrees/qa-20261109`, branch `product/qa-fixes-20261109`,
HEAD `831682d2`. Inputs: IMPLEMENTATION_INPUT_FINDINGS.md, ORCHESTRATOR_CHECKPOINT.md, CLAUDE.md, docs/SAFETY.md,
docs/VERIFICATION_GATES.md, docs/beta-readiness/RELEASE_READINESS.md, plus the source files cited below (read at HEAD +
worktree diff). Nothing was built, tested, run or committed for this plan. This is a plan, not verification evidence.

## 0. Baseline facts (G0)

- Uncommitted worktree edits: `src/MuscleCuties.UI/Services/Workout/WorkoutSessionPresentationMapper.cs` (F01 interim),
  `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.RuntimeText.cs` (+1 entry).
- **36 staged deletions** of `docs/architecture/github-transition/**` (including the target-architecture and migration
  docs CLAUDE.md requires for platform/schema work). Origin not established by this audit. They must NOT ride along in
  any fix PR. Until the integration authority restores or deliberately retires them, any package that touches a shared
  DTO or schema is BLOCKED on governance reading (CLAUDE.md "Orientation" 2).
- Recent commit `bd185f5d` "activate multi-metric HealthKit sync" conflicts with RELEASE_READINESS OD-4 ("HealthKit off")
  and CLAUDE.md ("Health synchronization stays dormant unless the owner authorizes its scope"). Owner must reconcile.

## 1. Final finding list (severity-ranked, de-duplicated)

| Rank | ID | Finding | Status | Evidence / correction |
|---|---|---|---|---|
| P1 | F01 | Bodyweight exercise: weight box shows placeholder "0" (`Common_Placeholder_Zero`, WorkoutSessionPage.xaml:344) while `LoggedWeightText` is empty; `ValidateInputs` (WorkoutSessionViewModel.cs:1810) rejects with `Ui_Workout_Error_Weight`; banner hides the grid | **confirmed** (sim + code) | **Correction to the interim fix:** its comment says "a lift with no prescribed load still requires an explicit weight". False. `BuildExerciseDetailDto` (PostgresWorkoutPlanAndSessionStore.cs ~1535-1543) emits `WeightKg = 0.0` on every set whenever there is no progressed, current or previous load, so for a new user **every first-session weighted lift** (e.g. Hack Squat) now pre-fills "0,0" and passes validation. Result: silent 0 kg logs feed progression (`StartingWeight` forever) and volume. Not intensity-increasing, but it is a data-integrity regression. See §4 |
| P1 | F06 | Today card says "Начать тренировку" while a session is in progress or completed | **confirmed** (sim + code) | DashboardWorkoutCard.xaml:250 has static `Dashboard_Workout_StartWorkout`. `DashboardOverviewResponse` has no workout-state field. The state exists only as English display text "In Progress"/"Completed Today" (PostgresDashboardOverviewStore.cs:595). The state-aware label lives in WorkoutViewModel.cs:719-724 only |
| P1 | F05 | Today nutrition 0 kcal while Nutrition tab shows 130 | **confirmed** (code) | Dashboard reads `DailyNutritionLogs`, falling back to `FoodItemEntries` (PostgresDashboardOverviewStore.cs:157-186). The Nutrition tab reads `LoggedMeals`/`LoggedMealEntries` (PostgresNutritionOverviewStore.cs:121-192). Second variant: a hydration-only `DailyNutritionLogs` row (PostgresHydrationStore.cs:225) with 0 kcal masks any food entries. Two meal-logging models coexist, which is a duplicate-path issue |
| P1 | F04 | Workouts tab spinner >30 s after "Завершить сеанс", no follow-up API call | **needs-runtime-trace** (strong code hypothesis) | Spinner = `IsPageLoading => IsBusy \|\| IsPlanStale` (WorkoutViewModel.cs:308). `IsPlanStale` is set by `Invalidate()` (WorkoutSessionPage.OnNavigatingFrom:62) and cleared **only** in `LoadDataCoreAsync`'s `finally` (:459). Hypothesis: WorkoutPage.OnAppearing load runs first and is short-circuited by the 2-min freshness gate (AppPageLoadGate.RunAsync, no API call). `Invalidate()` then sets `IsPlanStale=true` and nothing reloads until the next appearing (the observed recovery came from a tab switch). Confirm with existing `[PageLoad]`/`[Performance][WorkoutPage]` trace lines |
| P1 | C01 | Cycle card states "Овуляция 11 нояб" and "День 14 из 28" as fact when cycle_length is unset (assumed 28) and the phase answer was "Не знаю" | **confirmed** (sim + DB); rule from SAFETY.md | Raised from P2: SAFETY.md says "Do not fabricate cycle dates … Represent uncertainty explicitly". The day count derives from the entered date (legitimate). The ovulation date and phase are estimates presented as facts. Copy/labelling fix only, no rule change |
| P1 (owner) | F03 | Injury cannot be saved while a session is open | **confirmed, by design** | Server guard PostgresCheckInAndInjuryStore.cs:274-277 (`ActiveWorkoutPreventsInjuryException`). The VM documents the intent (WorkoutSessionViewModel.cs:825-826). **Correction:** the user is not stranded. "Больно" → `ReportPain` offers swap or stop-exercise, and abandon exists. The stranding claim depended on F01. Remaining gap = owner decision **S-04** (mid-session pain/injury surface). This is not a code defect |
| P2-high | F08 | "Изменение травмы сохранено, но план тренировок не обновился" shown although reconcile returned 200 and the plan changed | confirmed (sim) | A misleading safety message can lead the user to distrust the applied restriction. Root cause unverified (message likely evaluated before reconcile completes) |
| P2-high | F07r | Unreachable/misconfigured backend leads to an English profile-setup form with defaults, not an offline/error state | confirmed (harness, stale bundle id) | F07 as a product bug is **retracted**. The resilience gap remains. Unverified: whether submitting that form could overwrite a profile; trace before any fix |
| P2 | F09 | 3 weekdays selected, plan shows "2 training days" | needs-runtime-trace | Dropped from the consolidated list but never resolved. PRODUCT_WORKFLOWS allows withheld sessions only *with disclosure* (G05); check whether a disclosure was shown |
| P2 | L10n | EN leaks in RU UI (see §3), raw enums `Hiit/RockClimbing/Running`, garbled "Активности: 2 training days - 5 full rest days - 4", 12 h time, "1,7 t", English ProfileSetupPage | confirmed | Root cause is systemic (server emits English prose). Handle in §3, not per string |
| P2 | UX | Check-in Save disabled with no hint (sleep slider needs drag); stale injury error after Cancel; no confirmation after symptom quick-log PUT 200; no password-rule hint on registration; empty-login has no field hint | confirmed | — |
| P2 | Layout/copy | Mid-word wraps ("Мышечная болезненность", "Овуляторная", "Обильные"), truncated primary buttons (meal sheet, cycle), custom-food fields under sticky Save, set list clipped, duplicate "Назад", imperative titles, "Обменять", "Фотоальбом путешествия", triplicated "Питание" header | confirmed | DESIGN.md shared page system only |
| P3 (product) | F10 | Starting a workout on a rest day consumes the next planned strength day (30 Nov) | observation | Product decision, no fix planned |
| — | F02 | 30 Nov strength day not suppressed after acute knee injury | **retracted** | Verified in code: `SuppressFutureStrengthForInjuryAsync` (PostgresCheckInAndInjuryStore.cs:411-426) deliberately excludes days with a completed session (`PlannedWorkoutDayId == day.Id && CompletedAt != null`). The 30 Nov day was the session completed at 11:43. Keep a regression test (WP4) so the exclusion stays limited to completed days |
| — | F07 | Relaunch routes to onboarding | **retracted** | Stale second install `com.denispetruk.musclecuties` launched without endpoint config. The residual gap is tracked as F07r |

## 2. Work packages (ordered)

Conventions: one owner per package. Branch `product/<id>-<topic>` from the reviewed base. Shared DTO and schema changes
need an **integration-authority reservation** (flagged ⚑). No package needs a schema migration. Test descriptions only:
nothing in this plan was run. Simulator retests need task-specific owner authorization (G5/G12).

### WP0 — Baseline hygiene (integration authority)
- Unstage or restore the 36 `docs/architecture/github-transition/**` deletions, or record an explicit retirement decision.
- Decide the F01 interim edit: **revert it** (see §4) before any PR. Keep the RuntimeText entry only if WP7 does not supersede it.
- Record package IDs, base SHA, owned paths and reservations in the repository handoff (STATUS.md).
- Acceptance: `git status` shows only the intended package files; reservations are recorded.

### WP1 — F01 bodyweight weight input  ⚑ (additive DTO field)
- Layers: UseCases (contract), Infrastructure.PostgreSql (producer), UI (consumer).
- Files: `UseCases/Features/Workout/Session/Contracts/WorkoutSessionContracts.cs` (add `public bool IsBodyweight { get; init; }`
  to `WorkoutExerciseDetailDto`, default false); `Infrastructure.PostgreSql/Workout/Session/PostgresWorkoutPlanAndSessionStore.cs`
  `BuildExerciseDetailDto`, which resolves `WorkoutExerciseDefinition.IsBodyweight` via `SourceExerciseId` (the session
  path currently reads the legacy `Exercise` model, which has no bodyweight flag); `UI/Services/Workout/WorkoutSessionPresentationMapper.cs`
  (when `IsBodyweight` and weight is empty, set `LoggedWeightText = "0"`); `UI/ViewModels/Workout/WorkoutSessionViewModel.cs`
  ValidateInputs stays unchanged. Optional: replace the `Common_Placeholder_Zero` placeholder with a unit hint so an empty box never looks filled.
- Acceptance: bodyweight exercise advances with no typing and stores 0 kg. A first-session weighted lift with no history
  still shows an empty box and requires an explicit weight. A client running the old build ignores the new field.
- Tests: Mobile.Contracts JSON round-trip with and without the field (old payload deserializes to false); UI mapper unit
  for bodyweight-prefill and weighted-no-history-stays-empty; PostgreSQL integration that Dead Bug → `IsBodyweight=true`
  and Hack Squat → false.
- Sim retest: new synthetic user, first session, Hack Squat (empty, must type) then Dead Bug (pre-filled 0, Далее logs). RU and EN.
- Risk: low. The legacy `Infrastructure/Services/Workout/WorkoutExercisePresentationMapper.cs` is a second path: characterize it and do not extend it.

### WP2 — F06 Today workout state  ⚑ (additive DTO field)
- Layers: Infrastructure.PostgreSql, UseCases contract, UI.
- Files: `DashboardOverviewContracts.cs` (add `string WorkoutStateCode` = `none|planned|in_progress|completed`, default `""`);
  `PostgresDashboardOverviewStore.cs` (~585-600: emit the code; stop emitting English status prose, see WP7);
  `UI/ViewModels/Dashboard/DashboardViewModel.cs` (expose `WorkoutActionTextKey`, mirroring WorkoutViewModel.cs:719-724);
  `UI/Controls/Dashboard/DashboardWorkoutCard.xaml:250` (bind the button text to the VM key via the localized converter).
- Fallback without a DTO change (if the reservation is denied): DashboardViewModel reads `InProgressWorkout` from the same
  overview client WorkoutViewModel uses. That adds a second call, so prefer the DTO.
- Acceptance: Today and Workouts show identical actions (Start / Resume / Edit) for planned, in-progress and completed days. No English badge in RU.
- Tests: Core/UseCases mapping table for state → key; PostgreSQL integration for each session state; XAML binding guard (key exists EN+RU).
- Sim retest: start a session, background it, open Today (Продолжить); finish (Изменить/Завершено).

### WP3 — F04 post-finish spinner (UI)
- Step 1 (trace, no code): capture `[PageLoad]` and `[Performance][WorkoutPage]` lines across finish → pop. Confirm the order "OnAppearing load skipped as fresh → Invalidate sets IsPlanStale".
- Fix direction: invalidate **before** navigation (in `CompletePlannedSessionAsync` on success, WorkoutSessionViewModel.cs
  ~1178-1260) instead of in `WorkoutSessionPage.OnNavigatingFrom`, **and** make `Invalidate()` reload when the workout
  page is current, or set `IsPlanStale` only when a load is actually queued. Files: `UI/Pages/Workout/WorkoutSessionPage.xaml.cs`,
  `UI/ViewModels/Workout/WorkoutViewModel.cs` (Invalidate/LoadDataCoreAsync), `UI/Services/Profile/AppPageLoadGate.cs` if needed.
- Acceptance: after finish, the Workouts tab shows the completed day within one overview call (≤ ~2 s locally). The spinner never outlives an idle gate.
- Tests: VM unit with a fake gate: Invalidate after a fresh load still clears IsPlanStale via a reload; finish → one overview call.
- Sim retest: finish from the Workouts tab and from Today; repeat within 2 min (freshness window).

### WP4 — F05 single nutrition-consumed source + F02 regression (Infrastructure.PostgreSql)
- Characterize first: which UI flows write `FoodItemEntries`/`DailyNutritionLogs` (Diary store) versus `LoggedMeals` (Overview store). Pick the canonical source with the integration authority. Do not delete either path in this package.
- Fix: extract one internal query (e.g. `ConsumedNutritionQuery` in Infrastructure.PostgreSql/Nutrition) used by both
  PostgresDashboardOverviewStore (157-186) and PostgresNutritionOverviewStore (consumed totals). Hydration keeps its own column.
- Acceptance: for any mix of diary entries, logged meals and hydration rows, Today totals equal Nutrition-tab totals for the account's local date.
- Tests: disposable PostgreSQL: (a) logged meal only, (b) diary entry only, (c) hydration row plus meal (the masking
  case), (d) soft-deleted rows excluded, (e) tenant isolation. Plus F02 regression: future strength days are suppressed
  except a day bound to a **completed** session; an in-progress or abandoned bound day is still suppressed.
- Sim retest: log a custom food → Today equals the Nutrition tab; add water → still equal.
- Risk: medium (persistence read path; no schema change). R2 gates G3/G4.

### WP5 — C01 cycle uncertainty labelling (UI + Core copy; no rule change)
- Files: cycle card and Today cycle subtitle view models (CycleViewModel and the Dashboard cycle section), plus catalogs. When `PredictionSource` indicates assumed length or there is no logged history, label phase, ovulation and next period as estimates ("≈", "предположительно") and state that 28 days was assumed. Never show the ovulation date as fact.
- Acceptance: no ovulation/phase statement without an estimate marker when cycle length is unset. The phase answer "Не знаю" is never displayed as user-reported.
- Tests: Core CyclePrediction boundary (length 0/unset → estimate flag); UI mapping unit for the marker; EN/RU parity.
- Owner input: confirm that inferring the phase from the last-period date when the answer was "Не знаю" is intended (product spec).

### WP6 — Injury messaging (UI) + S-04-conditional session path
- 6a (now): fix F08 ordering in `UI/ViewModels/Workout/InjuryLogViewModel.cs` (show the reconcile result only after it
  completes; clear ErrorText on Cancel). Replace the hard-coded English `EmptyStateTitle/Detail` (:160-166) with catalog
  keys and drop the RuntimeText patch.
- 6b (BLOCKED on owner S-04): if approved, add "End session and record injury" from the pain sheet. It must finish or
  abandon the session through the existing path, then open the injury form prefilled with the exercise context. The
  server guard stays, so the restriction is never relaxed. No server change.
- Tests: VM unit for message ordering; Cancel clears the error; for 6b, sequence finish → PUT /injuries → reconcile.
- Sim retest: save an acute knee injury after a session, so no red error appears; for 6b, run the in-session pain → end → injury path.

### WP7 — Localization by codes (see §3)  ⚑ for DTO-carried codes
### WP8 — P2 layout/copy sweep (UI; DESIGN.md)
- Fix wraps/truncation with the shared styles (`LineBreakMode`, min widths, button text keys shortened in the RU catalog). Replace imperative sheet titles. Remove the duplicate "Назад" and the duplicate "Питание" card. Make the check-in required-field hint visible. Add a toast after symptom save. Add password-rule hints. Use 24 h time via the current culture.
- Tests: catalog length/parity checks; XAML binding guards. Sim: smallest iPhone and 17 Pro, light/dark, EN/RU.
### WP9 — F07r unreachable backend (UI startup)
- Trace startup routing (App/Shell, token store) for the endpoint-unreachable and refresh-failed cases. Route to a retryable offline/error state, never to an empty profile-setup form for an existing session. Prove that the form cannot overwrite an existing profile (server-side ownership check) before shipping.
### WP10 — F09 days mismatch trace (UseCases/Core planning)
- Reproduce with a synthetic profile (Mon/Wed/Thu, beginner, 45 min, full gym). If a session is withheld, the disclosure must be visible; otherwise it is a defect for Core planning with characterization tests first.

Order: WP0 → WP1, WP3, WP6a (parallel, disjoint files) → WP2 + WP4 (reservations) → WP5 → WP7 → WP8/WP9/WP10.

## 3. Localization strategy

- Root cause: server and Core return English prose (PostgresWorkoutInjuryStore.cs:82 "exercises blocked";
  PostgresDashboardOverviewStore.cs:595-600 "Completed Today/In Progress/min/sets completed/Moderate/Strength";
  DashboardSupportRule.cs and duplicate DashboardPlanner.cs readiness labels; session DTO "Supports today's workout focus…",
  "Brace your core…", "{n} sets x {m} reps", "No previous log"). The UI translates only where a binding passes through
  `UiTextCatalog.TranslateRuntimeText`, which is an English→RU dictionary that silently misses formatted strings.
- Target: **message code + arguments**. A precedent already exists: `RecommendationCode` in `WorkoutExerciseDetailDto`.
  - Server emits stable codes (`workout.state.in_progress`, `injury.summary.blocked_count` + `{count}`,
    `readiness.label.keep_moderate`, `activity.hiit`) as **additive** fields next to the existing text (⚑ per DTO).
    Old clients keep reading the text.
  - UI resolves codes through `UiTextCatalog` keys (`UiTextCatalog.<Family>.cs` EN / `RussianUiText.<Family>.cs` RU)
    with format arguments and culture-aware numbers, units and time (24 h in RU, "т"/"кг").
  - Enum display (`Hiit`, `RockClimbing`, weekday abbreviations) is mapped in UI via one `ActivityDisplay` key family. Raw enum names are never bound.
  - Freeze `UiTextCatalog.RuntimeText.cs`: no new entries. Remove each entry once its code path ships.
- Guards: extend LocalizationCatalogTests (EN/RU key parity, format-argument parity, every code emitted by the server has a key). Add a contract test that dashboard/session/injury DTOs carry codes for every displayed status.
- Delete the duplicate `Infrastructure/Services/Dashboard/Planning/DashboardPlanner.cs` labels only after characterization proves DashboardSupportRule is the live path.
- Order: highest-traffic surfaces first (Today card, session screen, injury banner, nutrition macros), then ProfileSetupPage and the quiz.

## 4. F01 decision

| Option | Pros | Cons |
|---|---|---|
| A. Current mapper edit (mirror all-0 sets) | UI-only, no reservation | **Unsafe heuristic.** The server sends 0.0 for every no-history lift, so weighted first sessions silently log 0 kg. The code comment is factually wrong |
| A′. UI-only placeholder fix (unit hint instead of "0", plus inline "0 для собственного веса") | No reservation; keeps the explicit-weight guard | Bodyweight users must still type 0 every time |
| B. Additive `IsBodyweight` on `WorkoutExerciseDetailDto` | Exact semantics from the catalog (`WorkoutExerciseDefinition.IsBodyweight`); backward compatible | Needs an integration-authority reservation and a server deploy before the client benefits |

**Recommendation: B, with A′ as the interim. Revert A now.** If the TestFlight timeline cannot wait for B, ship A′
(no data-integrity risk). Do not ship A.

## 5. Release gates and owner-only items

Agent-executable (after authorization of each package): G0, G1, G2 (WP4/WP5 tests), G3 (disposable PostgreSQL for WP4),
G4 (contract tests WP1/WP2/WP7), G8 (non-UI builds/tests), G9 (STATUS/handoff).

Owner-only / BLOCKED until the owner acts:
1. Authorize simulator build/run for retests (G5/G12). Re-run the journeys after WP1-WP6a land; attach dated evidence.
2. Decisions S-03, **S-04** (gates WP6b), S-05; C01 phase-inference intent; F10 rest-day consumption.
3. **HealthKit scope:** reconcile commit `bd185f5d` with OD-4 and CLAUDE.md. Either authorize the scope and make the entitlement and usage strings match shipped behaviour (NSHealthUpdateUsageDescription currently describes reading only), or disable the sync and remove the capability before archive.
4. Whoop URL scheme and Sign in with Apple entitlements: confirm both flows ship, or remove them.
5. Bundle id/provisioning: `com.musclecuties.app` (csproj, keychain group) is the release identity. Delete the stale `com.denispetruk.musclecuties` simulator install and confirm the run-script override is debug-only.
6. Privacy row copy ("Данные о здоровье синхронизируются с аккаунтом") vs the actual policy; App Privacy answers vs PrivacyInfo.xcprivacy.
7. Restore or retire the staged-deleted governance docs (WP0); merge the reviewed PR(s); archive/sign/upload (internal group).
8. External testers additionally need AWS A1-A6 (unchanged; out of scope here).

## 6. Verdict: **NO-GO for internal TestFlight** (consistent with RELEASE_READINESS 2026-11-09)

Exit criteria for internal GO (all required):
1. WP0 done; the F01 interim edit is reverted; the PR diff contains only package files.
2. F01 resolved via B or A′, with a simulator retest showing bodyweight logging and a weighted first session requiring explicit weight.
3. F06, F04, F05 fixed with tests passing locally (`dotnet test` on affected suites; PostgreSQL integration on a disposable DB) and simulator evidence.
4. C01 estimate labelling and F08 messaging shipped; injury journey re-run (restriction applied, no misleading error).
5. RU smoke pass on the Today, session, injury and nutrition surfaces with no English status/badge strings (full §3 migration may follow).
6. HealthKit scope, entitlements and bundle identity reconciled by the owner (§5 items 3-5).
7. S-04 decided (WP6b implemented, or explicitly deferred with the in-session pain/abandon path documented to testers).
8. CI green on the candidate SHA; evidence recorded per CLAUDE.md (SHA, commands/results, unrun gates).
F07r, F09 and the P2 sweep may follow in a later internal build if documented as known issues. They are not optional for external testers.

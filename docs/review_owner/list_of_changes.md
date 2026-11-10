# List of changes — awaiting owner review

| Field | Value |
| --- | --- |
| Branch | `product/qa-fixes-20261109` (local only, not pushed, no PR) |
| Base | `integration/beta-polish-20261109-v2` @ `f1918e138634ba39c2483bde3e1194b6d346f2a8` |
| Worktree | `.agent-worktrees/qa-20261109` |
| Date | 2026-11-09 |
| Source plan | [REMAINING_WORK_PLAN_2026-11-09.md](../beta-readiness/REMAINING_WORK_PLAN_2026-11-09.md) |
| Verification | **Agent checks run on 2026-11-09:** UI build (Debug, `net10.0-ios`, `iossimulator-arm64`) succeeded with 0 warnings/0 errors; the app installed and launched on the iPhone 17 simulator and reached the local API; Core 1324/1324, Mobile Contracts 183/183, Registration 54/54 and the full PostgreSQL integration suite 105/105 (104 + the 3-minute PG09 load test) passed; the QA wizard passed `bash -n`. Screen judgement, VoiceOver, protected CI and device acceptance remain owner gates. See **Agent coverage pass** below. |

Release status was not changed: G12 and the NO-GO verdict stand. The single edit to `RELEASE_READINESS.md` records the
owner's phone-QA deprecation (item 9).

## 1. C-1 — Caption contrast and dark-mode parity (P1, styling S-01)

- `src/MuscleCuties.UI/Controls/Workout/FeaturedWorkoutCard.xaml` (exercise preview count) and
  `src/MuscleCuties.UI/Pages/Workout/StrengthPlanGenerationPage.xaml` ("Required" tag): `TextColor="{StaticResource
  Primary}"` (#C85A87: 3.99:1 light, 3.42:1 dark) replaced with the existing accent pair
  `AppThemeBinding Light=PrimaryDark (#A6446D), Dark=SecondaryDarkText (#F0B8CF)` already used by `Styles.xaml`.
- No new token or style.
- **Check:** both captions in light and dark; contrast ≥ 4.5:1; `UiStyleGateTests`.

## 2. C-4 — Validation errors are no longer announced as headings (P2, styling S-03)

- `Pages/Onboarding/ProfileSetupPage.xaml` (2 labels) and `Pages/Onboarding/QuizPage.xaml` (1 label): removed
  `SemanticProperties.HeadingLevel="Level3"` from `ErrorLabel`s.
  - Correction to the audit: 3 error labels had it, not 7.
- Those `ErrorMessage` labels now use the existing `ScreenReaderStatusAnnouncementBehavior`, so VoiceOver announces the
  reason when it appears. Added `xmlns:sharedControls` to both pages.
- `ProfileSetupPage.xaml.cs`: announces the static wellness-acknowledgment error when Continue reveals it
  (`SemanticScreenReader.Default.Announce`).
- **Check:** VoiceOver on invalid Continue (profile setup and quiz): reason announced once, not as a heading. Related to
  the E-04 layout.

## 3. C-5 — Russian mistranslation (P3, styling S-05)

- `Services/Localization/RussianUiText.CycleWorkout.cs`: `Cycle_QuickLog_LoadingSavedEntry` changed from
  "Загрузка сохраненного входа…" ("saved login") to "Загрузка сохранённой записи…".

## 4. C-2 — Stale age error never cleared (P2, Q-02, E-01 area)

- `ViewModels/Profile/ProfileSetupViewModel.cs`: the age handler set the key `Ui_Onboarding_Error_Age` but cleared only
  when the message equalled the old English sentence.
  - It now uses a `AgeErrorKey` constant for both, so a corrected date clears the error.
  - Other errors are untouched.
- **Check:** pick an under-18 date if reachable (picker max is today−18y), then an adult date; the message disappears.

## 5. C-3 — Confirming the pre-filled birthday (P2, Q-03, E-01 area)

- `ProfileSetupViewModel.cs`: new `MarkBirthDateConfirmedFromPicker()`.
- `ProfileSetupPage.xaml`: the `DatePicker` now has `Unfocused="OnBirthDatePickerUnfocused"`, and
  `ProfileSetupPage.xaml.cs` adds that handler.
- Opening the picker and closing it with Done now counts as answering, even when the date is unchanged. Before this, a
  user turning 18 today could never confirm.
- An untouched picker is still rejected (E-01 fix preserved). The server age gate is unchanged.
- **Check:** E-01 regression:
  - an untouched picker + Continue must still show "birth date required";
  - open → Done on the default date must allow Continue;
  - `date_of_birth` must never be saved without interaction.
- **Risk:** on iOS, if `Unfocused` fires without the picker having been opened, an untouched date would count as
  confirmed. Verify on the simulator.

## 6. C-7 — T-21 analyzer warnings (P2)

- `tests/MuscleCuties.Core.Tests/BackendApiRefreshFailureTests.cs`: dropped `ConfigureAwait(false)` (xUnit1030).
- `tests/MuscleCuties.Core.Tests/CycleOnboardingAnchorTests.cs`: two tests made `async Task`; they now `await` instead
  of `.GetAwaiter().GetResult()` (xUnit1031 ×2).
- `tests/MuscleCuties.Core.Tests/TodayCheckInScoreEnergyTests.cs`: removed `[InlineData(1)]`, which duplicated
  `[InlineData(Minimum)]` (= 1) (xUnit1025).
- **Check:** `dotnet build tests/MuscleCuties.Core.Tests` shows 0 xUnit analyzer warnings; test count drops by 1, the
  duplicate row.

## 7. C-10 — S-05: seven-day post-clearance window removed (owner decision D-4, taken at recommendation)

- `src/MuscleCuties.Core/Domain/Workout/Planning/WorkoutInjuryClearanceRule.cs`:
  `OptionalCalendarDaysIncludingClearance` changed from 7 to 0, with the rationale documented on the class.
  - A cleared exercise is restored as prescribed from its clearance date, and only when current eligibility permits.
  - Unknown, active or future clearance still blocks.
  - The window remains one constant, so reverting is a one-line change.
- Effects that flow through existing code:
  - `InjuryProgressionPhaseRule` now returns `ClearedAfterWindow` from the clearance day, so `ClearedOptionalWindow`
    becomes unreachable.
  - `RequiresConservativeRestriction` releases on the clearance day.
  - Reconciler `RequiredFrom` dates equal the clearance date.
- Tests updated to the new semantics:
  - `tests/MuscleCuties.Core.Tests/WorkoutInjuryClearanceRuleTests.cs` (rewritten);
  - `tests/MuscleCuties.Core.Tests/InjuryProgressionPhaseRuleTests.cs` (first case);
  - the remaining seven-day expectations in `ExerciseEligibilityRuleTests`, `InjuryPrescriptionCandidateRuleTests`,
    `InjuryRestorationDecisionTests`, `ReviewedInjuryCandidateSelectionRuleTests`,
    `WorkoutInjuryProvenanceRuleTests` and `WorkoutPlanningSyntheticReviewTests` were migrated to the zero-window
    semantics.
  - `InjuryCanonicalReconciliationTests` (integration) references the constant and needed no edit; it passed.
- **Check:**
  - Core suite and the full PostgreSQL integration suite; the reconciler/preview tests passed without edits;
  - J04 on the simulator: a cleared exercise shows as required on the clearance day, in preview and in plan (owner
    gate: signed-in local account and the simulator).
- **Owner:** ratify policy amendment A-7, or revert the constant to 7.

## 8. C-9 — S-04: in-session pain action (owner decision D-3, taken at recommendation)

- `ViewModels/Workout/WorkoutSessionViewModel.cs`: session-scoped `_painReportedIds`, `IsPainReportVisible` and
  `HasPainReports`, plus four commands:
  - `ReportPainCommand` opens the inline report;
  - `PainSwapCommand` opens the existing swap panel, whose candidates come from canonical eligibility (reviewed
    alternatives only);
  - `PainStopExerciseCommand` uses the existing skip path, never erasing logged sets;
  - `CancelPainReportCommand` handles a mis-tap.
  - State is cleared with the other per-session state on load.
- `Pages/Workout/WorkoutSessionPage.xaml`:
  - The bottom action bar is now two columns: Swap | Note, Skip | **This hurts**, then Next.
  - An inline `CardSecondary` pain report (title, explanation, Switch / Stop / Cancel) sits above it. It has no new
    modal, no scoring, and no load added to make up for the stopped exercise.
  - After the activity, the summary shows a reminder to add the pain to the injury log.
- Copy (EN/RU, key-identical), in `UiTextCatalog.CycleWorkout.cs` and `RussianUiText.CycleWorkout.cs`:
  - `Workout_Session_PainButton`, `…PainAccessibility`, `…PainTitle`, `…PainBody`, `…PainSwap`, `…PainStop`,
    `…PainLogReminder`;
  - Cancel reuses `Common_Cancel`.
- **Not changed (deliberate):**
  - The server still refuses injury saves while a session is open (`ActiveWorkoutPreventsInjuryException`). The pain
    report doesn't write an injury mid-session; it stops the movement and reminds the user to log afterwards.
  - Recording the pain server-side during the session would need a new session-scoped endpoint and store. That is a
    follow-up if the owner wants it.
- **Check:**
  - `LocalizationCatalogTests` (EN/RU parity);
  - J04/J08 on the simulator: "This hurts" → Switch shows only eligible swaps; Stop advances without erasing logged
    sets; the summary reminder appears; RU at the largest Dynamic Type (two-column bar) shows no clipping.
- **Owner:** ratify policy amendment A-6.

## 9. Physical-device QA deprecated; simulator wired through Rider (owner request, 2026-11-09)

- `scripts/run-ios-simulator.sh` (new):
  - resolves a **simulator only**, by name or UDID (simctl never lists physical devices), then boots it and opens
    Simulator;
  - optionally sets appearance (`MC_SIM_APPEARANCE`) and language/locale (`MC_SIM_LANGUAGE`);
  - refuses a non-loopback `MC_LOCAL_API` and warns if the API is down;
  - runs `dotnet build … -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=:v2:udid=<UDID> -t:Run`.
- `scripts/run-local-api.sh` (new):
  - runs the API on `127.0.0.1:5080` (Development) against a loopback-only PostgreSQL (`MC_LOCAL_PG`, default
    `127.0.0.1:55461/mc_local`);
  - generates throwaway JWT, receipt RSA and HMAC keys in a temp folder that is deleted on exit;
  - `--migrate` runs DbMaintenance `migrate`, `seed` and `dbcheck`;
  - OTP codes print in the terminal (Development delivery).
  - This captures the keys the QA pass had to discover by hand.
- `.run/` (new, versioned per `.gitignore` policy): Rider shell run configurations:
  - `Local API (loopback)`;
  - `iOS Simulator - iPhone 17 Pro`;
  - `iOS Simulator - iPhone 17 Pro (dark, RU)`;
  - `iOS Simulator - iPhone SE (3rd gen)`.
- **Debugger attach:** these configurations build and launch only. For breakpoints, run Rider's own `MuscleCuties.UI`
  project configuration with the same simulator selected in the device list.
- Docs:
  - `docs/beta-readiness/RELEASE_READINESS.md`, owner action 8: QA01 runs on the Rider-attached simulator; physical
    device deprecated.
  - `docs/beta-readiness/REMAINING_WORK_PLAN_2026-11-09.md`, P-1: marked deprecated.
- **Check:** run both configurations from Rider on this Mac:
  - app launches on the named simulator and reaches the local API;
  - the dark/RU configuration renders Russian in dark appearance.
- **Note:** TestFlight testers still install on real phones; only the QA acceptance gate moved.

## 10. C-8 — S-03 policy amendments (owner decisions D-1/D-2, taken at recommendation)

- `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`: new section "Proposed amendments — 2026-11-09 (pending owner
  ratification)". The adopted text stays authoritative until ratified:
  - A-4: readiness reduces one optional set only for Beta; the load step is deferred until a prescription load datum
    exists. This matches the current production path, so no code change.
  - A-5: cycle estimates never change reps, sets or load by themselves; the +2 reps / extra set is withdrawn.
    `ComposedReadinessCycleDoseRule` stays test-only, as today.
  - A-6: in-session pain (item 8).
  - A-7: no post-clearance window (item 7).

## Previously produced in this session (reports, uncommitted on the same branch)

- `docs/release-audit/2026-11-09/OWNER_SIMULATOR_QA_2026-11-09.md` and `evidence-owner-qa-2026-11-09/`
- `docs/beta-readiness/SIMULATOR_TEST_REPORT_2026-11-09.md`
- `docs/beta-readiness/STYLING_AUDIT_2026-11-09.md`
- `docs/beta-readiness/REMAINING_WORK_PLAN_2026-11-09.md`

## Not done (and why)

| Item | Reason |
| --- | --- |
| C-6 native alert migration | Needs a DESIGN.md proposal approved by the owner first (design authority changes by proposal) |
| S-03 load step-down implementation | Deferred by amendment A-4; needs a load datum and an expand-only migration (B owns numbering) |
| Server-side session-scoped pain record | Out of scope for the Beta surface; listed as a follow-up in item 8 |
| Live AWS A1–A6, A11 | Needs a concrete, owner-approved change set and target; not authorized per action |
| Device/visual/VoiceOver acceptance | Owner runs these; agent build, unit-test, database and simulator-launch evidence is recorded below |

## Suggested owner verification order

1. Build the UI and Core.Tests (expect 0 xUnit warnings).
2. Re-run the Core, Mobile Contracts, Registration and PostgreSQL integration suites in CI; the 2026-11-09 local
   results are recorded below.
3. From Rider: `Local API (loopback)` (with `--migrate` the first time), then `iOS Simulator - iPhone 17 Pro`. Walk
   E-01…E-07, J04 and J08 (pain action), CR01, then the dark/RU and iPhone SE configurations for J14.
4. Ratify or reject A-4…A-7. A rejected A-7 is a one-line revert (`OptionalCalendarDaysIncludingClearance = 7`) plus
   restoring the clearance test expectations from the base.

## Simulator verification of these changes (run 2, 2026-11-09)

Rendered on the iPhone 17 simulator via Rider's native configuration. The full record is
`docs/release-audit/2026-11-09/OWNER_SIMULATOR_QA_2026-11-09_RUN2.md`.

**Historical record:** this table captures run 2 before the agent coverage pass. C-3 and the C-9 follow-ups were fixed and
the build/tests were rerun afterwards; the current state is recorded in **Agent coverage pass** below. Rows marked
"unverified" are superseded by that pass and are retained only as the run-2 record.

| Change | Observed |
| --- | --- |
| C-1 contrast | Not reached on screen (preview count / "Required" tag); unverified |
| C-2 stale age error | Not reachable (picker max); unverified |
| C-3 confirm pre-filled birthday | **Regression or no protection:** an untouched picker still let Continue proceed, and DB saved 2008-11-09. Revert or replace (plan R-1) |
| C-4 error semantics | Errors render above Continue/Next; VoiceOver itself not driven |
| C-5 RU copy | Not reached |
| C-7 test warnings | Not built by the agent |
| C-9 pain action | Works (card, Switch → swap panel, Stop → skipped, nothing logged). Follow-ups: pain-context copy in the empty swap panel, layout squeeze, clipped set rows (plan R-9) |
| C-10 no clearance window | Not exercised in UI (no cleared injury) |
| Rider wiring | Owner's native Rider configuration works; the agent's shell `.run` configuration failed (F-17). `run-local-api.sh` was not used; the agent ran the API with the same settings plus a dummy `Storage__S3__BucketName` |

## Agent coverage pass (2026-11-09, after run 2)

Every numbered item is accounted for. The in-scope C-1…C-5, C-7…C-10 and Rider work was re-checked; C-6 and S-03
remain explicitly not done for the reasons below. The C-3 regression and the C-9 follow-ups were fixed; the C-10
policy now has no stale tests. The checks below were actually run; screen judgement, VoiceOver, protected CI and
device acceptance remain owner gates, with their exact prerequisites listed at the end.

| Item | Coverage in this pass |
| --- | --- |
| C-1 contrast | `StrengthPlanGenerationPage.xaml` keeps the accent pair `PrimaryDark`/`SecondaryDarkText`; the site is now recorded in the DESIGN.md exception table and its guard mirror. The UI contract guard passes. Light/dark rendering not viewed |
| C-2 stale age error | Code still uses `AgeErrorKey`; no change needed. Compiled; the under-18 path is unreachable because the picker max is today−18y, so the key-clearing logic is verified by inspection |
| C-3 birthday regression | Removed the unfocused-picker auto-accept. A birthday is confirmed only after the picker received focus or the explicit **Confirm birthday** action; height and weight start unanswered at 0 and Continue blocks with the required-field errors. Compiled; the signed-in screen flow must be walked with the wizard |
| C-4 error semantics | The error label has no `HeadingLevel`; it still renders above Continue/Next. VoiceOver not driven |
| C-5 RU copy | RU catalog now reads `Загрузка сохранённой записи…`; key parity preserved. Compiled |
| C-6 native alerts | Not implemented; a DESIGN.md proposal and owner approval are required before any migration. No code change; see **Not done** |
| C-7 test warnings | `dotnet build tests/MuscleCuties.Core.Tests` reports 0 warnings, 0 errors |
| C-9 pain action | The inline card is now an `AnimatedModalView` sheet, so the exercise and set rows keep their space; no reviewed strength swap shows pain-specific empty copy; the celebration banner is cleared before the first action; logged sets are untouched by Stop. Compiled; the sheet was not viewed on screen |
| C-10 no clearance window | `OptionalCalendarDaysIncludingClearance = 0`. Ten remaining tests that encoded the seven-day window were migrated (clearance day is required; latest clearance is the deadline; MaxValue no longer overflows). Full Core suite passes; the PostgreSQL integration suite also passes 105/105, including the clearance persistence checks |
| C-8 policy amendments | A-4 and A-5 remain documentation-only (no code change); A-6 is the pain action above; A-7 is implemented in C-10 and awaits the owner's ratification. No production policy text was changed |
| Modal accessibility | Legacy `AnimatedModalView` sheets in `WorkoutSessionPage`, `WorkoutPage` and `FoodLogPage` now bind `IsOpen` (FoodLog's duplicate `IsVisible` removed), so focus capture, title announcement and covered-page isolation run. The style guard passes; VoiceOver itself was not driven |
| Rider wiring | The agent used the equivalent of the Rider `MuscleCuties.UI` configuration: Debug, `net10.0-ios`, `iossimulator-arm64`, `:v2:udid=13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB` |

**Evidence actually collected**

- UI build: `Build succeeded. 0 Warning(s), 0 Error(s)`; `-t:Run` installed and launched the app on the booted
iPhone 17 (iOS 26.3) simulator. Screenshot: `docs/release-audit/2026-11-09/screenshots-qa-2026-11-09/sim-proof-new-build.png`
(real login screen; the final modal bindings were compiled afterwards and are not visible on this screen).
- Core suite: `Passed! - Failed: 0, Passed: 1324, Skipped: 0, Total: 1324`.
- Mobile Contracts: `Passed! - Failed: 0, Passed: 183, Skipped: 0, Total: 183`.
- Registration: `Passed! - Failed: 0, Passed: 54, Skipped: 0, Total: 54`.
- PostgreSQL integration: `Passed! - Failed: 0, Passed: 104, Skipped: 0, Total: 104` (everything except the load test,
  5 s) and `Passed! - Failed: 0, Passed: 1, Skipped: 0, Total: 1` for `Pg09LeastPrivilegeAndLoadTests` (3 m), against
the disposable databases below. The schema setup test passed 1/1 and produced app 55 / catalog 17 / ops 3 tables.
- Local API: loops on `127.0.0.1:5080` against `mc_qa_20261109`; the app's requests reach it.
- Human-only path: `scripts/qa-simulator-wizard.sh` (5 stages: stack, launch, onboarding, pain sheet,
appearance/RU/VoiceOver), syntax-checked with `bash -n` and executable. It refuses non-loopback PostgreSQL
and captures a screenshot at each judgement gate.

**Recorded database/contract runs (2026-11-09)**

```bash
# The integration database must match mc_test_nutrition_*; GeneratedMealPostgresTests asserts that prefix.
export MC_REGISTRATION_WRITE_TEST_POSTGRES='Host=127.0.0.1;Port=55461;Database=mc_test_nutrition_qa_20261109_run3;Username=postgres'
export MC_REGISTRATION_TEST_POSTGRES="$MC_REGISTRATION_WRITE_TEST_POSTGRES"
export MC_SCHEMA_COMPATIBILITY_POSTGRES='Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres'
export MC_RETRY_TEST_POSTGRES='Host=127.0.0.1;Port=55461;Database=mc_retry_disposable;Username=postgres'
export MC_PG09_LOAD_POSTGRES='Host=127.0.0.1;Port=55461;Database=mc_pg09_disposable;Username=mc_ci_migrator'

MC_PREPARE_DISPOSABLE_WORKOUT_DATABASE=True dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj \
  --filter 'FullyQualifiedName~DisposableWorkoutDatabaseSetupTests'
dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj \
  --filter 'FullyQualifiedName!~Pg09LeastPrivilegeAndLoadTests'
dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj \
  --filter 'FullyQualifiedName~Pg09LeastPrivilegeAndLoadTests'
```

Prerequisites are local: PostgreSQL 16 on `127.0.0.1:55461`, the disposable `mc_test_nutrition_*` database above,
`mc_retry_disposable`, `mc_pg09_disposable`, and the restricted `mc_ci_migrator` role (CREATEROLE, not superuser,
no BYPASSRLS). Docker is not needed. The integration project builds with three pre-existing xUnit analyzer
warnings (xUnit1031 ×1, xUnit2031 ×2), outside C-7's Core.Tests scope; they do not affect the pass.

**Not verified here:** on-screen C-3/C-9 behaviour, light/dark contrast, RU/XXXL layout and VoiceOver (owner via
the wizard); protected CI and Beta/TestFlight/device acceptance. The Core, Mobile Contracts, Registration and
PostgreSQL integration suites now have recorded local runs above; protected CI evidence still needs an authorized
PR/runner. A-7 still needs the owner's ratification (or the documented one-line revert). C-6 and S-03 remain out of
scope as stated above.

**Owner-only gates and exact prerequisites.** The local evidence above does not cover human judgement:

- C-3 screen flow and the C-9 sheet: the owner, a signed-in local test account (the API prints its OTP), and
  `scripts/qa-simulator-wizard.sh` stages 3–4 on the booted iPhone 17
  (`13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`).
- C-1 light/dark, C-4 VoiceOver, C-5 RU and XXXL layout: the owner, wizard stage 5, the simulator set to dark/RU,
  and VoiceOver enabled.
- C-10 J04: the owner, a cleared-injury fixture or account, and the same simulator flow; the database assertions are
  already covered by the integration suite.
- Item 9 Rider UI: the owner, Rider's native `iOS Simulator - iPhone 17 Pro` and `iOS Simulator - iPhone 17 Pro
  (dark, RU)` configurations, and the same simulator; the agent used the equivalent CLI build, not the Rider UI.
- Device/TestFlight/Beta acceptance: the owner's physical device and an authorized distribution; no device run is
  claimed here.
- Protected CI: an authorized PR/push and a GitHub runner; this branch is local only.

## Agent orchestration and Simulator verification pass (2026-11-09 / 2026-11-10)

Orchestrated 3 specialized agents across lifecycle, data truth, and resilience/polish, then drove automated end-to-end verification via Maestro on the booted iPhone 17 (UDID `13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`) connected to local PostgreSQL (`127.0.0.1:55461`, database `mc_qa_20261109`) and the local API on `http://127.0.0.1:5080`.

| Work Package | Changes Implemented | Verified On Simulator |
| --- | --- | --- |
| **WP-1: Session Lifecycle** | Guarded optional exercise toggle & set count against MAUI binding false triggers; resumed in-progress session directly at first unlogged/unskipped exercise; added Abandon confirmation modal | **VERIFIED:** Session resumed directly at exercise 2/6 (Dead Bug); Abandon modal rendered with explicit Confirm/Cancel and clean dismissal |
| **WP-2: Modal Architecture** | Idempotent `ModalAccessibilityScope` with `HashSet<VisualElement>` tracking, unloading hooks, and explicit `SetValue(..., previous ?? false)` restoration for iOS accessibility | **VERIFIED:** Modals dismiss cleanly without trapping UI touches or leaking disabled state |
| **WP-4: Logging Integrity & Archive Truth** | Per-row `WasEdited` tracking prevents unedited default placeholders from saving as observed sets; added `CompletedAt != null` across all archive/month queries | **VERIFIED:** Workout Archive displays 0 completed sessions for user with in-progress workout |
| **WP-5: Dashboard & Readiness Truth** | In-progress strength workout intensity mapped to "Moderate"; unrated readiness suppresses 65/70 defaults and prompts "Complete a check-in to see readiness" | **VERIFIED:** Today screen displays "Moderate" intensity and "Complete a check-in to see readiness" |
| **WP-7: Resilience & S3 Fallback** | Added iOS Keychain wipe on fresh install via non-sensitive `Preferences`; added resilient S3 fallback service so Profile endpoint does not fail when S3 is unconfigured; replaced raw infrastructure copy | **VERIFIED:** You / Profile screen loaded 200 OK without S3 bucket configured; displays user-friendly privacy copy |
| **WP-9: UI Polish** | Removed duplicate Notes button from workout session bottom bar; removed "Order {0} · " prefix from day titles; unified single "Resume workout" CTA | **VERIFIED:** Train screen shows clean title and single CTA; session screen has single Notes button |

### Automated Test Pass Summary
- `MuscleCuties.UI` build (`net10.0-ios`, `iossimulator-arm64`): **Build succeeded, 0 warnings, 0 errors**.
- `MuscleCuties.Core.Tests`: **1330 passed, 0 failed**.
- `MuscleCuties.Mobile.Contracts.Tests`: **183 passed, 0 failed**.
- `MuscleCuties.Registration.Tests`: **54 passed, 0 failed**.
- `MuscleCuties.Registration.Integration.Tests`: **104 passed, 0 failed** (on `mc_test_beta_rc` and `mc_test_nutrition_qa_20261109_run3`).
- `MuscleCuties.Server.Tests`: **117 passed, 0 failed** (functional suite).

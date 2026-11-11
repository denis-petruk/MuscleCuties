# Handoff prompt — complete every step before Beta (MuscleCuties iOS)

Paste this whole file as the first message of a new session. Written 2026-11-10 after the simulator audit and implementation pass.

## 0. Who you are and the hard rules
You continue an autonomous QA-and-fix loop for the MuscleCuties MAUI iOS app. The owner wants every step finished before Beta, without routine questions. Stop only for the owner-only items in section 6.

Hard constraints (unchanged from the owner's original prompt):
- Work only in `/Users/denispetruk/RiderProjects/MuscleCuties/.agent-worktrees/qa-20261109/` (branch `product/qa-fixes-20261109`, HEAD `e9b2a5db` plus ~32 uncommitted files). Never `cd` to the primary checkout. Note: a `cd /tmp` once reset the shell cwd to the primary checkout — prefix every command with `cd <worktree> &&`.
- Do NOT run `dotnet test`. Do NOT read or modify `tests/`. Builds are allowed and required.
- No commit, push, reset, rebase, merge or stash unless the owner says so. No AWS mutations, no Beta/production/shared DB writes, no TestFlight submission, no credential or security changes, no authentication bypass.
- Synthetic data only. Local DB `mc_local` (Postgres 127.0.0.1:55461, user `postgres`) is disposable and may be edited for QA; back up any row you delete into `.claude/qa-evidence/`.
- CLAUDE.md applies: shared DTO/schema/policy-approval changes belong to the integration authority; flag them, do not invent approvals.
- Never claim a result you did not observe. Mark unverified items "pending simulator verification".
- Report in the concise style; lead with the result.

## 1. Where everything is
Evidence folder (read first): `.claude/qa-evidence/`
- `ORCHESTRATOR_CHECKPOINT.md` — chronological log of every action, result and open item (authoritative).
- `IMPLEMENTATION_INPUT_FINDINGS.md` — findings F01–F06, P2 list, retractions.
- `MASTER_IMPLEMENTATION_PLAN.md` — Opus plan WP0–WP10, localization strategy, verdict NO-GO.
- `DECISIONS_INJECTION_PLAN.md` — Opus plan DK1–DK8 for the owner's ratified decisions.
- `screenshots/` — evidence images. `deleted-checkin-row-backup.txt` — backup of the check-in row I deleted.
Also read: `CLAUDE.md`, `docs/SAFETY.md`, `docs/PRODUCT_WORKFLOWS.md`, `docs/VERIFICATION_GATES.md`, `docs/beta-readiness/RELEASE_READINESS.md`, `docs/release-audit/2026-11-09/OWNER_DECISIONS_AND_QA_GAP_CLOSURES.md`.

Owner decisions (ratified; do not re-litigate): S-03 Decision 4 = set-only readiness reduction; S-03 Decision 5 = a cycle estimate alone never changes sets/reps/load (symptoms + readiness only); S-05 = no 7-day post-clearance window; S-04 = in-session pain button deferred; HealthKit A/A/A on (contextual prompt, rolling step baseline, SleepHours auto-fill + "Suggest from Apple Health" chip); step/sleep averages MAY be stored on the backend; the cycle-phase calorie factor 0.95/1.05 stays.

## 2. Environment and how to run
- Simulator: **iPhone 17 Pro, UDID `678048F6-C4C7-406C-A970-7104AED3E00A`** (the audit device). The `control` tool defaults to the other sim (iPhone 17, `13EDBFDC…`); always pass `udid`.
- App bundle id on the sim: `com.musclecuties.app`. A stale `com.denispetruk.musclecuties` install also exists — do not launch it (it has no endpoint config and shows onboarding).
- Build UI: `cd <worktree> && DOTNET_CLI_USE_MSBUILD_SERVER=0 MSBUILDDISABLENODEREUSE=1 dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 -m:1 -nodeReuse:false` (~3–11 min). Build server: `dotnet build services/MuscleCuties.Server/MuscleCuties.Server.csproj`.
- Install + launch with synthetic Health: `xcrun simctl install $S <app>; SIMCTL_CHILD_MC_SYNTHETIC_HEALTH=1 xcrun simctl launch $S com.musclecuties.app` where app = `src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app`. Verify the binary timestamp is newer than your last source edit (`stat -f %Sm`).
- Local API: terminal tab `c3` runs `bash scripts/run-local-api.sh 2>&1 | tee /tmp/mc-local-api.log` (loopback 5080; OTP codes appear in the log as `[local-dev-email] registration code for <email>: <code>`). Restarting it regenerates JWT keys; the app's refresh token survived the last restart.
- Synthetic user: `qa.tester.20261110b@example.test` (display `qa_tester_0110b`), id `01a1c633-7086-7b97-aa2f-cb37f173d765` in `mc_local`. Password was never recorded; use the in-app password recovery with the dev-log code if you must re-login.
- Coordinates: displayed-pixel × 0.437 = device points (402×874). Today/Profile lists keep scrolling after a swipe — take a screenshot to confirm the list is still before tapping, and use short fast swipes (duration ~0.15). Two accidental taps opened "Delete account"; both were cancelled. Never tap near the bottom of the Profile list without a fresh screenshot.
- Useful: `psql -h 127.0.0.1 -p 55461 -U postgres -d mc_local`; define a function `p(){ psql ... "$@"; }` (do not store the command in a variable).

## 3. What is already changed (uncommitted, 32 files — do not lose)
Compiled OK (UI + Server stack). Pending simulator verification unless marked ✔.
- DK1 cycle dose: `CycleReadinessPrescriptionRule.cs` (phase/pattern reductions removed), `PostgresCycleReadinessPrescription.cs` (no baseline/pattern loading), `PostgresCycleReadinessReconciler.cs` (accepts only DailyReadinessAdjustment/Current). PolicyVersion deliberately NOT bumped (needs an owner approval record).
- DK2: `WorkoutInjuryClearanceRule.cs` comment. Legacy-row behaviour reviewed in code only.
- DK3: `ProfileHealthSyncViewModel.cs` + `ProfilePrivacyPage.xaml(.cs)` card ✔ (Connect/Refresh/Disconnect verified with the synthetic provider; a missing `Error` resource crashed the page and was fixed to `ErrorLabel`); check-in rationale card in `DailyCheckInViewModel.cs` + `DailyCheckInContent.xaml` (NOT verified).
- DK4: no overwrite of typed sleep; unknown sleep ≠ 0 h; hidden sleep substitution removed (`CloudDailyCheckInAndCycleStores.cs`).
- DK5: 7 completed-day step window, min 4 days, energy window aligned (`AppleHealthDataProvider.cs`); no 65 kg/28 y defaults (`NutritionViewModel.cs`); Today target now applies the same Health adjustment (`DashboardViewModel.cs`) ✔ (Today and Nutrition agreed at 2,370 connected / 2,320 disconnected).
- DK7: unused `NSHealthUpdateUsageDescription` removed; privacy copy EN/RU; Health cache version 2 (`HealthSyncService.cs`).
- DK8: Debug-only `Services/Health/SyntheticHealthDataProvider.cs`, selected by env `MC_SYNTHETIC_HEALTH=1` in `PlatformCompositionExtensions.cs`.
- F01 server text (`PostgresWorkoutPlanAndSessionStore.cs`, bodyweight via `WorkoutExerciseDefinitions.Required == None`) — NOT verified.
- F04 spinner (`WorkoutViewModel.cs`: `IsPageLoading = IsBusy`, `Invalidate()` no longer sets IsPlanStale) — NOT verified.
- F05: Today consumed calories now from food entries + logged meals (`PostgresDashboardOverviewStore.cs`) ✔ (130 kcal matches Nutrition).
- F06: Today action label (`DashboardViewModel.WorkoutActionText`) ✔ ("Открыть тренировку" for a completed session; matches real store strings "Completed (…)" / "Active Session").
- Ring fill bug (owner report): `DashboardProgressRing.cs` end angle `(90 − 360p + 360) % 360` ✔ on Today; owner confirmed all rings (readiness and FoodLog included) work.
- Docs amended: PRODUCT_WORKFLOWS, SAFETY, WORKOUT_POLICY (A-4/A-5 rows), OWNER_DECISIONS ledger (status BLOCKED), RELEASE_READINESS verdict (NO-GO).

## 4. Work remaining before Beta (do in this order)
A. Simulator verification of unverified items (needs a user state that allows it):
   1. Check-in rationale card + "Не сейчас" + Connect; pre-fill (SleepHours auto-fill, "Suggest from Apple Health" chip, no overwrite of a typed value, unknown sleep gives no rating). State now: today's check-in row was deleted and Health is disconnected, so the card "Заполнить отметку" should open the sheet — it was not reachable by touch; retry (screenshot first; tap when still), or call it from the dashboard flow.
   2. F01: need an unlogged session with a bodyweight exercise (Dead Bug). Create a fresh planned day for the synthetic user in `mc_local` (insert via the app's flow: Workouts → start a different day, or reset the completed session: back up `workout_session_logs` rows first) and confirm the kg box shows a real `0` and "Далее" passes.
   3. F04: finish a session and confirm the Workouts tab does not hang on the spinner.
   4. (Done: owner confirmed all rings work.)
   5. Cycle: with a phase set and no check-in, no set is removed; after a check-in with <7 h sleep, exactly one optional accessory set is removed (DK1). Re-check the injury flow still works (knee/acute blocks squats).
B. Code still open:
   - Take workout days per week for the calorie rule from the active plan (both `NutritionViewModel` and `DashboardViewModel` hard-code 3).
   - Localize remaining English in the RU UI via message codes + catalog keys (MASTER §3), not RuntimeText patches: "Completed Today", "General strength training", "TRAINING", "Keep it moderate", "Mostly recovered", "In Progress", `Next period in 15d · Ovulation Nov 11`, `+50 kcal activity`, `Protein/Carbs/Fats`, `g`, injury banner "Knee (Acute) – N exercises blocked", enum labels (Hiit/RockClimbing/Running), onboarding page, Health provider messages (`AppleHealthDataProvider`, `DisabledHealthSyncService`).
   - Cycle UI honesty (Opus rated P1): when cycle length is unset ("Не знаю"), label the phase/ovulation date as an estimate with the assumption visible (`cycle_logs.cycle_length = 0` currently shows "День 14 / 28" as fact).
   - Misleading injury message "plan did not update" after a successful reconcile; check-in "Сохранить отметку" disabled with no hint; layout truncations (Cycle quick-log chips, "Мышечная болезненность" tile, custom-food form under sticky Save, duplicate "Назад" buttons).
   - Replace the Health sleep/average windows consistently if product wants them aligned; add the `IsBodyweight` DTO flag only if the server can send 0 kg sets for unknown loads (integration authority).
   - F09 follow-up: the Food Diary now uses the server target (verified 2,320) but not the on-device Health adjustment, so with Health connected Today/Nutrition show +50 kcal and the diary does not. Extract a shared UI service for the adjustment and use it in Dashboard, Nutrition and FoodLog view models.
   - F03/S-04 is deferred by the owner: keep the doc note; the "Больно" button exists in the session.
C. Resilience (P2-high): an unreachable/misconfigured backend currently lands on a blank English onboarding form with zero API calls; show an error/offline state instead.
D. Tests (a separate owner-approved package; do not touch `tests/` yourself): list the tests that must exist for DK1 decision table, DK2 legacy rows (seed `IsOptional=true`, `RequiredFromDate=clearance+7`), DK4 pre-fill, DK5 boundaries, F01/F05, localization parity, XAML resource-key guard (the privacy crash was not caught by compile), `MergedHours`.
E. Docs: `STATUS.md`, `ROADMAP.md`, `docs/beta-readiness/AUDIT.md`, `HEALTHKIT_AND_TESTFLIGHT_IMPLEMENTATION_PLAN.md` (mark WP-HK4/5 partial), then regenerate `UX_FUNCTIONAL_READINESS_REPORT.md` with final counts and evidence.
F. Independent review: have a Sonnet subagent review the full diff (`git diff` of the 32 files) read-only for correctness/regressions, fix confirmed findings, rebuild, re-verify. Then an Opus final verdict pass; keep the verdict NO-GO until section 5 is satisfied.

## 5. Exit criteria for a GO to internal TestFlight (all must hold)
1. UI and Server projects build with 0 errors; no new warnings.
2. Every item in A is simulator-verified with screenshots saved under `.claude/qa-evidence/screenshots/`.
3. No P1 open (F01, F04, F05, F06, cycle labelling) and the DK1 contradiction closed.
4. Today and Nutrition agree on consumed calories, macros and target in connected and disconnected Health states.
5. Release docs consistent with the code (no doc claims GO while criteria are open).
6. The Opus final verdict recorded with an explicit list of unrun gates.
Do not state SAFE TO PROMOTE; promotion needs target checks and owner approval (CLAUDE.md).

## 6. Owner-only — stop and ask, do not attempt
- Cycle policy version bump / new approval record (`PlanningReleaseApproval`, `MC-CYCLE-READINESS-0.3`).
- Bundle id and signing: csproj `ApplicationId` is `com.musclecuties.app`; confirm the release provisioning and the `keychain-access-groups` entitlement match; HealthKit/Whoop URL scheme/Sign in with Apple entitlements vs shipped behaviour; App Privacy declaration and `Profile_Privacy_*` copy for stored step/sleep averages.
- Archive, sign, upload, TestFlight groups; any AWS/Beta DB action; commits, pushes, PRs and merges; deleting `ComposedReadinessCycleDoseRule.cs` and `PostgresCyclePersonalizationEvidence.cs` (test-owned; check callers first); device QA01.
- Any password for the synthetic account (none exists in notes).

## 7. Known hazards (learned the hard way)
- Compile success does not validate XAML resource keys: always open every changed page on the simulator after a build.
- A stale installed binary hides fixes: compare `stat` times; reinstall before testing.
- String-equality checks on server status text broke the Today button once; prefer shared constants/codes.
- The shell can silently drop the worktree cwd; the primary checkout must never be modified.
- `timeout` is not installed on this Mac; do not wrap commands with it.
- The staged deletions of `docs/architecture/github-transition/**` seen at the start of the audit are resolved (owner-confirmed); still verify `git status` before any PR.

## 8. First actions for the new session
1. `cd <worktree> && git status --short | head -40` and confirm the 32 changed files match section 3.
2. Read the checkpoint tail and sections 1–2 of `MASTER_IMPLEMENTATION_PLAN.md`.
3. Confirm the API (tab `c3`) and simulator are up; relaunch the app with the synthetic Health flag.
4. Start with A.1 (check-in rationale), then A.2–A.5, then B–F, appending every result to `ORCHESTRATOR_CHECKPOINT.md`.

## 9. Update 2026-11-10 18:00 (supersedes conflicting lines above)
Changes since section 3 was written (all uncommitted; builds OK for UI + Server):
- `DailyCheckInViewModel.cs`: manual sleep edit clears the "Заполнено из Apple Health" badge (built, NOT re-verified on the sim).
- `PostgresNutritionDiaryStore.cs`: diary falls back to `IServerNutritionTargetProjectionStore` (F09) — verified: diary shows 2,320 kcal, 112/324/64 g like Today/Nutrition. Server API restarted in terminal tab `c4`.
- Ring fix confirmed by owner on all rings.

Simulator results (iPhone 17 Pro, synthetic Health `SIMCTL_CHILD_MC_SYNTHETIC_HEALTH=1`):
- PASS: check-in rationale card shown before any Apple prompt; Connect auto-fills sleep 6,2 ч; chip sets quality 3 / energy 3; manual 10,0 ч survives close/reopen; privacy Health card Connect/Disconnect; Today consumed 130 kcal; Today and Nutrition and Diary target agree (2,370 connected / 2,320 disconnected on Today+Nutrition; diary 2,320); F06 labels (completed "Открыть тренировку", in progress "Продолжить тренировку"); F01 Dead Bug shows real "0" and logs 0 kg.
- INCONCLUSIVE F04: Workouts tab showed no spinner after returning, but POST `/workouts/sessions/{id}/finish` returned **409** on the reopened session (I reopened it via DB: deleted Dead Bug rows, `completed_at=NULL`; timing_interval_* are NULL). Backup: `deadbug-reopen-backup.txt`. Next: read the finish endpoint's 409 conditions in `PostgresWorkoutActiveSessionStore.cs`, then start a fresh session through the app (needs a planned strength day: future days are all `is_removed_by_safety` because of the acute-knee episode; clear the injury in-app or use a different synthetic user) and finish it.
- NOT verified: "Не сейчас" persistence, save with the pre-filled value, unknown-sleep behaviour, cycle dose after a <7 h check-in, the badge fix, Health adjustment on the diary page (not implemented; F09 follow-up).

Updated remaining order: (1) explain the 409 and finish a session cleanly (F04); (2) re-verify badge fix; (3) "Не сейчас" + save path; (4) cycle dose check; (5) F09 follow-up shared Health adjustment service; (6) workout-days-from-plan for calories; (7) localization sweep; (8) cycle labelling honesty; (9) remaining P2 layout/copy; (10) docs + Sonnet review + Opus verdict. Owner-only list in section 6 is unchanged.

Harness tips added: use slow swipes (duration 2 s) to scroll without fling; wait ~3 s after taps that open sheets; screenshots may lag one frame.

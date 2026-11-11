# QA checkpoint — 2026-11-10 ~11:03

Device: iPhone 17 Pro (678048F6-C4C7-406C-A970-7104AED3E00A). API: 127.0.0.1:5080, DB mc_qa_20261109, terminal tab c1, log /tmp/mc-local-api.log.
Synthetic user: qa.tester.20261110b@example.test (display name qa_tester_0110b).

## Completed
- Launch, registration, email verification (code from dev log).
- Onboarding step 1: height 175, weight 62, birthday 10.11.2008, metric, wellness ack, activity "General strength training".
- Quiz (11 questions; progress counter shown as 1/12 then 3/11+):
  - Q1 cycle phase: Не знаю
  - Q2 fitness goal: Build muscle tone
  - Q3 experience: Beginner
  - Q4 days: Понедельник, Среда, Четверг (3 selected)
  - Q5 diet: No preference
  - Q6 session length: 45 мин
  - Q7 equipment: Полный тренажёрный зал
  - Q8–Q11 menstrual/cycle sliders: defaults kept (no values invented)
- Plan generated: "Расписание: 2 training days, 5 full rest days, 4 weeks". Home shows Rest day today.

## Findings (open)
1. Days mismatch: 3 days selected (Mon/Wed/Thu), plan shows 2 training days. Verify rule in docs/PRODUCT_WORKFLOWS.md before calling it a defect.
2. Mixed English/Russian: quiz questions Q1–Q2, Q4, Q5, Q6, Q7 and "Finish" button; activity titles.
3. Height/weight wheel pickers ignore typed input.
4. Cycle questions appear for a profile with no sex/eligibility gate — check spec.
5. Quiz counter jumps 2/12 -> 3/11 after Q1 "Не знаю" (skip logic).
6. Resend-code 400 after API restart (dev-only cause: per-process keys).

## Remaining (not verified)
Plan content vs profile (exercises, duration, split), workout execution/logging, readiness, injury/safety, cycle personalization, returning-user relaunch, nutrition, negative cases, fixes + reverification, Sonnet review, Opus gates, final report.

## Next action
Open Тренировки plan day cards; compare training days with selection; then continue documented journeys.

## Update 2026-11-10 ~11:18
- Workout: Hack Squat logged. DB (mc_local, app.exercise_set_logs): 2 rows, set 1 and set 2, 20 kg x 8, created 11:17:05. PASS for persistence.
- The earlier "0,0 kg" row was a display placeholder; no zero-weight row was stored. Not a confirmed persistence defect (downgraded).
- Exercise 2 of 6 (Machine Chest Press) open.
- Environment note: the restarted API (script without MC_LOCAL_PG) uses database mc_local, not mc_qa_20261109 as this checkpoint previously stated. The synthetic user is in mc_local.
- Open: remaining exercises 2–6, session completion, history, readiness, injury, cycle, returning user, nutrition, negative cases, fix loop, Opus gates.

## Update 2026-11-10 ~11:20 — workout exercises 1–5 verified
- Hack Squat, Machine Chest Press, Seated Leg Curl, Chest Supported Row, Machine Hip Thrust: each saved at 20 kg, rows verified in mc_local (app.exercise_set_logs). No duplicates after advancing.
- Exercise 6 Dead Bug (2 x 10, bodyweight): BLOCKED. Error "Укажите корректный вес в кг. Для собственного веса используйте 0 кг." appears; set inputs are not visible; "Повторить" and "Пропустить" produced no visible change. Session cannot be completed from the UI. Root-cause investigation not started.
- Candidate P0/P1: bodyweight exercise blocks workout completion. Validation requires weight, UI does not render the weight input for this exercise.
- Open: session completion, history, all other journeys, fix loop, Opus gates, final report.

## Update 2026-11-10 ~11:40 — returning user, readiness
- Relaunch (app killed by stopped build): session restored; Today shows workout "In Progress", 10 sets. PASS (Journey G persistence, partial). Notes: button still "Начать тренировку" for in-progress; badges "In Progress"/"TRAINING" in English.
- Daily check-in: Save disabled until sleep hours set; tap on slider does not set value, drag required; no hint of the required field (UX gap). After sleep 7.3h, quality 3, energy 3, soreness 4/5, pain none: PUT /check-ins 200; readiness card "Рассмотрите более лёгкий день" with Сон 3 / Энергия 3 / Мышечная болезненность 4 and a non-medical disclaimer. PASS for documented adjustment-and-explanation.
- Layout defect (P2): "Мышечная болезненность" tile wraps mid-word.
- Observation: Today cycle card shows Овуляторная, day 14/28 although quiz answer was "Не знаю"; subtitle "Next period in 15d · Ovulation Nov 11" is English. Needs spec check.
- Probe edit in WorkoutSessionViewModel.cs reverted; no source changes remain from this audit.

## Nutrition journey (2026-11-10 11:42)
- PASS: custom food (130 kcal / P3 / C28 / F1) -> add to Lunch -> save; Nutrition totals 130/2320 kcal, macros 3/28/1 g, lunch "6% дневной цели" (arithmetic correct). Evidence: screenshots/nutrition-logged.png
- NOTE: food search "rice" -> "Нет совпадений" (local catalog likely empty; product catalog population unverified).
- P2 L10n: "Protein/Carbs/Fats", "g", "P/C/F ... g", "1 serving ... kcal", sheet title "Portion" are English in RU UI; time "11:40 AM" 12h in RU; triplicated "Питание" header card.
- P2 Copy: sheet title "Запишите прием пищи" (imperative as title) and its primary button truncated ("Запишите прием пиш"); "приём/прием" inconsistent.
- P2 Layout: custom-food form shows Carbs/Fat fields clipped under sticky Save button until scrolled.
- Harness note: sim text injection drops "," / maps "." to "ю" in RU layout; used integers. Not an app defect.

## Injury journey (2026-11-10 11:43)
- Entry: Workouts tab, heart-pulse icon (top right) -> "Журнал травм" -> Добавить (body area + status chips, required). No injury entry in Profile.
- FINDING (P1 safety UX): with a workout in progress, Save (Колено / Острая) is rejected: "Завершите или прервите активную тренировку перед записью травмы. Записанные подходы сохранятся." A user hurt mid-session cannot record it without ending the session first. Combined with the Dead Bug input blocker (cannot complete exercise 6) this strands the user. Evidence: screenshots/injury-blocked-active-workout.png. Need to confirm whether an abort/finish control exists in the session UI.
- P2 L10n: injury empty state subtitle "No active injuries are recorded." English in RU UI; Workouts tab shows "General strength training", "TUE", "Rest day", "Активности: 2 training days - 5 full rest days - 4" (mixed EN/RU, garbled).

## Dead Bug blocker RESOLVED (2026-11-10 11:43) - root cause found without probe build
- Earlier "grid not rendered" conclusion was WRONG: the set grid (Подходы/Повторы/кг) IS rendered for Dead Bug. The kg box displays a grey "0" that is a PLACEHOLDER (LoggedWeightText empty). Tapping Далее -> ValidateInputs -> "Укажите корректный вес в кг. Для собственного веса используйте 0 кг." The error banner pushes content down/replaces the grid until "Повторить" is tapped.
- Workaround verified: tap kg, type real "0", Далее -> exercise logged, summary "Активность завершена! Записано 6 из 6", volume 1,7 t. Evidence: screenshots/activity-complete.png
- FINDING (P1 UX/functional): bodyweight exercise (Dead Bug, isBodyweight:true, UsesWeight=true since not endurance) shows a "0" that looks filled but is empty; the primary action fails with an error that tells the user to enter 0, which they believe is already there. Fix candidates: pre-fill "0" for bodyweight exercises (WorkoutExercisePresentationMapper UsesWeight/LoggedWeightText) or treat empty weight as 0 when exercise is bodyweight in ValidateInputs.
- P2: two "Назад" buttons on the exercise screen (top-left and next to "Упражнение 6 из 6"); set-row list "Записанные подходы" clipped to ~1 row by the action buttons; "Strength & Sculpt", "General strength training", "2 sets x 10 reps" English; "Начните 60-секундный отдых" imperative; "Обменять" for swap (reads as "exchange"); "1,7 t" unit English.
- Safety path present in session: "Больно" button on each exercise (to test).

## Post-finish checks (2026-11-10 11:44)
- FINDING (P1 consistency, needs recheck): after logging a 130 kcal Lunch (DB mc_local app.logged_meals + logged_meal_entries rows present, Nutrition tab shows 130/2320), the Today tab "Сегодняшнее питание" card still shows 0 ккал / 0g after GET /dashboard/overview 200. app.daily_nutrition_logs is empty. Today and Nutrition use different sources.
- FINDING (P1): after "Завершить сеанс" (POST /sessions/{id}/finish 200, 68 ms) the Workouts tab sat on "Загрузка ваших данных" spinner >30 s with no follow-up API call; recovered only by switching tabs. 
- NOTE: shell cwd was reset to primary checkout by a `cd /tmp` psql call (read-only, no files touched); all further commands run from the QA worktree.

## Injury journey result (2026-11-10 11:45)
- After finishing the workout, injury log accepts Колено/Острая: PUT /injuries 200, POST /workouts/plan/reconcile 200 (140 ms). Week plan changed 2 -> 1 training day. Banner + "Детали": 10 blocked exercises (Back/Goblet/Hack/Bulgarian Split Squat, Leg Press, Lunges, Step-Up, Nordic Curl, Kneeling Cable Kickback), rehab suggestions (Lying/Seated Leg Curl, Seated Hip Abduction Lean, Single-Leg Glute Bridge), blocked activities (Hiit, RockClimbing, Running). Evidence: screenshots/injury-saved-plan-not-updated.png, injury-impact-details.png
- PASS (safety): acute knee injury removes squat/lunge pattern and impact cardio; restriction not relaxed.
- P2: transient red error "Изменение травмы сохранено, но план тренировок не обновился" shown although reconcile succeeded (plan DID update) - misleading; likely message shown before reconcile completes.
- P2 L10n: banner "Knee (Acute) -- 10 exercises blocked", exercise names English (acceptable?) and raw enum labels "Hiit", "RockClimbing", "Running"; "Дней полного отдыха: 1 training day - 6" mixes RU/EN and appears label/value swapped.
- P1 (earlier): injury cannot be logged while workout in progress (user must finish/abort first).

## P1 CANDIDATE - injury restriction gap in plan (DB evidence, UI exposure NOT yet confirmed) (2026-11-10 11:47)
- Today's synthetic user 01a1c633..., active plan 01a1c633-7728..., acute knee episode started 2026-11-10 (cleared_on NULL).
- app.workout_days Strength rows: 11-11, 11-16, 11-18, 11-23, 11-25, 12-02 have is_removed_by_safety=true (safety_injury_id set). 2026-11-30 (week_index 3, Mon) has is_removed_by_safety=false and still contains Hack Squat + Seated Leg Curl + Machine Hip Thrust... Hack Squat is on the UI's own "Заблокированные упражнения" list for this injury.
- Result: the only remaining visible training day (UI says "1 training day") contains an exercise the app itself lists as blocked. Possibly a plan-horizon/reconcile gap for the 3rd week Monday. Needs: confirm in UI (Week 4 / 30 Nov) and root-cause in PostgresWorkoutPlanReconciler. No product-doc rule for restriction horizon found in docs/SAFETY.md or PRODUCT_WORKFLOWS.md.
- DB note: mc_local holds two synthetic users (owner's earlier 01a1c23e... and today's 01a1c633...); filter by user_id when querying.

## STATUS: MARKED COMPLETED by owner (2026-11-10 ~12:30)
Audit closed at owner request. Deliverables: IMPLEMENTATION_INPUT_FINDINGS.md (consolidated findings + proposed fix order), this checkpoint, screenshots/. Worktree edits (uncommitted): WorkoutSessionPresentationMapper.cs (F01), UiTextCatalog.RuntimeText.cs (1 string). Not done: F01 retest, negative cases, F04/F05/F06 fixes, Opus master plan, final verdict, TestFlight (NO-GO stands).

## Opus gate result (2026-11-10)
MASTER_IMPLEMENTATION_PLAN.md written. Verdict NO-GO internal TestFlight. Per Opus: interim F01 mapper edit and the RuntimeText dictionary entry REVERTED in worktree (src has no net changes from this audit). F01 -> IsBodyweight DTO flag (integration-authority reservation). F03 downgraded (deliberate server guard), cycle labelling raised to P1, F02/F07 retracted.

## Decisions injection (2026-11-10 ~13:00) - DECISIONS_INJECTION_PLAN.md from Opus
Applied in worktree (uncommitted):
- DK1 (partial): CycleReadinessPrescriptionRule.cs - removed phaseFeeling/repeatedFeeling/HasRepeatedPattern; dose reduction now only from current check-in (short sleep, lighter/easier band) or routine symptoms. PolicyVersion NOT bumped (approvals are matched on "MC-CYCLE-READINESS-0.2"; a bump needs a new approval record - owner/integration). Core builds clean.
- DK2 (partial): WorkoutInjuryClearanceRule.cs comment updated to "owner ratified".
Not yet: DK1 persistence cleanups, ComposedReadinessCycleDoseRule deletion (test-owned), DK2 legacy-row characterization, HealthKit packages DK3+, doc amendments.
Open owner questions: HealthKit averages stored on backend?; cycle-phase calorie factor 0.95/1.05 keep?

## HealthKit packages (2026-11-10 ~13:30) - owner: backend storage of averages APPROVED; phase calorie factor 0.95/1.05 KEPT
Applied (uncommitted, UI not yet MAUI-built/retested):
- DK4: DailyCheckInViewModel (no overwrite of typed sleep; unknown sleep != 0h; apply-suggestion ignores 0 rating), CloudDailyCheckInAndCycleStores (hidden sleep substitution removed). Infrastructure builds clean.
- DK5 (partial): NutritionViewModel - no 65 kg / age 28 defaults; unknown weight/age => no adjustment. NOT done: 7-completed-day window / min coverage in AppleHealthDataProvider (needs per-day stats + device/sim), plan-derived workout days, Today==Nutrition target (F05).
- DK6: no change needed (storage approved); privacy copy must be updated (DK7).
Pending: DK3 (Profile sync settings, rationale card, disconnect), DK7 (remove NSHealthUpdateUsageDescription, cache schema, localization, privacy copy), DK8 (Debug synthetic Health provider), interval-merge for overlapping sleep samples, doc amendments, MAUI build + sim retest.

## Implementation started (2026-11-10, owner: "start implementing, don't wait")
- DK7 (part): removed NSHealthUpdateUsageDescription from Info.plist and both InfoPlist.strings (plutil OK). Profile_Privacy_HealthBody EN/RU updated to list the 4 read types (+ sleep) and read-only statement.
- Build in progress at time of note (dotnet build of MuscleCuties.UI from earlier batch); last run was killed by user, result unknown.

## Step 2 done (2026-11-10): Apple Health sync on privacy page (DK3 part)
- NEW ViewModels/Profile/ProfileHealthSyncViewModel.cs: status on appear (no HealthKit prompt), Connect (explicit tap only -> SyncAsync), Disconnect (DisconnectAsync). Refresh reuses Connect command.
- ProfilePrivacyPage.xaml/.xaml.cs: health card (CardPrimary, ButtonPrimary/ButtonSecondary); VM injected via DI (MauiProgram.cs).
- EN/RU keys Profile_HealthSync_* added to NutritionProfileAuth catalogs (10 keys).
- Full MAUI UI compile (net10.0-ios, Debug, iossimulator-arm64): Build succeeded, 0 errors. Simulator retest NOT done.
- Not done: rationale card before the first system sheet (check-in path), Debug synthetic Health provider (DK8), 7-day window + sleep-interval merge, F01/F04/F05/F06, doc amendments.

## Files changed without build (2026-11-10, owner: "no builds for now")
- AppleHealthDataProvider.cs: overlapping asleep samples merged (MergedHours, internal pure helper). Clipped to window.
- Services/Health/SyntheticHealthDataProvider.cs (NEW, #if DEBUG): fixed fake values (9,400 avg steps, 8,100 yesterday, 6.2 h last night, RHR 58, HRV 55, 420 kcal). Selected in PlatformCompositionExtensions only when env MC_SYNTHETIC_HEALTH=1 (launch with SIMCTL_CHILD_MC_SYNTHETIC_HEALTH=1 xcrun simctl launch).
- F01 (server-side, no shared DTO change): PostgresWorkoutPlanAndSessionStore.BuildExerciseDetailDto LoggedWeightText falls back to "0" when the catalog exercise Required == EquipmentSet.None and there is no weight history. Weighted lifts unchanged (empty -> explicit weight required). Opus option 1 (IsBodyweight DTO flag) NOT done - not needed for this fix; revisit if the sets carry 0 kg for unknown loads.
- F06 (UI): DashboardViewModel.WorkoutActionText (rest -> view rest day; completed -> view workout; in progress -> resume; else start), bound from DashboardWorkoutCard.xaml (trigger removed). New key Dashboard_Workout_ViewCompleted EN/RU. CAVEAT: in-progress detection relies on SessionProgressText == "In progress"; the dashboard store's exact string for an active session was NOT confirmed in code (PostgresDashboardOverviewStore:595 returns "In Progress" in a different tuple slot). Verify in simulator before relying on it.
- DOCS: docs/beta-readiness/RELEASE_READINESS.md verdict changed GO -> NO-GO until P1s close (per Opus DECISIONS_INJECTION_PLAN item 10). Note the earlier owner-pasted GO text is superseded by this.

## STATE AT END OF THIS PASS (no builds run)
Changed (uncommitted): cycle rule (DK1), clearance comment (DK2), check-in pre-fill (DK4), calorie defaults removed (DK5 part), sleep interval merge, HealthKit privacy/plist (DK7 part), Apple Health sync card VM+XAML+DI+keys (DK3 part), synthetic Health provider (DK8, Debug, env-gated), F01 server text, F06 Today action label, RELEASE_READINESS verdict.
NOT done: DK3 rationale card before first system sheet; DK5 7-day window + min coverage in AppleHealthDataProvider; DK7 cache schema bump + HealthSyncService message keys; F04 trace; F05 unification; cycle persistence cleanup (PostgresCycleReadinessPrescription/Reconciler); legacy injury row characterization; ComposedReadinessCycleDoseRule deletion (tests owned elsewhere); policy version bump (needs approval record); STATUS/ROADMAP/AUDIT doc amendments; all builds and simulator retests.
- DK5 (step window, NO BUILD): AppleHealthDataProvider.ReadWeeklySummaryAsync now queries seven COMPLETED days (today-7..today-1) separately; average over days with data; fewer than 4 days -> AverageSteps 0 (no adjustment). Yesterday steps unchanged. Energy and sleep windows still use the old today-inclusive 7-day window -> NOT yet aligned (follow-up).
- Energy window aligned to the completed-day window (today-7..today-1), same as calorie steps. Sleep window (7-day, today-inclusive) left as-is: it is not in the calorie formula, and changing it touches check-in data (follow-up).
- DK3 rationale (NO BUILD): DailyCheckInViewModel - first-time users see a rationale card (ShowHealthRationale); system Apple sheet only on "Connect Apple Health" tap (ConnectHealthCommand -> SyncAsync, then reload). "Not now" -> DismissPromptAsync. Already-connected users refresh silently (no sheet). XAML card in DailyCheckInContent.xaml; keys Dashboard_CheckIn_HealthRationale*/HealthConnect/HealthNotNow EN/RU.
- Note: the "Suggest from Apple Health" chip and check-in pre-fill still run on reopen; unchanged.
- DOCS (NO BUILD, unrelated-to-code first task per plan §3): PRODUCT_WORKFLOWS HealthKit scope/permission/coverage/factor lines; SAFETY.md sync authorization; OWNER_DECISIONS status BLOCKED + Decision 2/4 implementation notes; WORKOUT_POLICY acceptance rows A-4/A-5; RELEASE_READINESS verdict wording + stale OD-4/HealthKit-removal lines. NOT amended (left for owner review): HEALTHKIT_AND_TESTFLIGHT_IMPLEMENTATION_PLAN mark WP-HK4/HK5, and STATUS/ROADMAP/AUDIT.

## BUILD (2026-11-10): MAUI UI project, Debug net10.0-ios iossimulator-arm64
- First build: 4 errors (DashboardViewModel missing `using MuscleCuties.UI.Services.Localization` for UiTextCatalog). Fixed.
- Second build: Build succeeded, 0 errors. Warnings listed in /tmp/mc-build-full2.log.
- Not run: simulator launch, retest of any change, Infrastructure/Core tests (tests/ not touched by rule).

## SIM VERIFICATION (2026-11-10 15:49, iPhone 17 Pro, build after fixes)
- F06 PASS: completed workout -> Today button "Открыть тренировку" (was "Начать тренировку"). Root cause of the first miss: store returns "Completed (<kg> kg volume)" and "Active Session"; VM matched exact strings. Fixed to prefix/actual values. Screenshot: screenshots/final-today-after-fix.png
- Build: MuscleCuties.UI Debug net10.0-ios, 0 errors. Installed via simctl (com.musclecuties.app) with SIMCTL_CHILD_MC_SYNTHETIC_HEALTH=1.
- Still English on this card: "Completed Today", "General strength training", "TRAINING", "Keep it moderate", "Mostly recovered" (P2 L10n).
- NOT verified this pass: F01, check-in rationale, privacy health card, calorie target, synthetic Health values.
- F05 (code, NO BUILD, sim verification PENDING): PostgresDashboardOverviewStore consumed macros now = food snapshots + logged meal rows (grams/100), same sources as PostgresNutritionOverviewStore; daily log used only for water. Previously Today read the (empty) daily log and showed 0 kcal while Nutrition showed 130.
- F05 target (NO BUILD, PENDING): Nutrition adds the Health calorie adjustment client-side; Today shows the server target without it. Not yet unified (needs weight/age on Today: DashboardViewModel has no personal-info dependency). Open.
- F05 target (NO BUILD, sim verification PENDING): DashboardViewModel now applies HealthEnergyAdjustment on top of the server target (same weight/age rules as Nutrition: no defaults, unknown -> no adjustment; 3 workout days as Nutrition does). New optional ctor param IProfilePersonalInfoUseCase, wired in MauiProgram. Today and Nutrition share the same target formula. Follow-up: derive workout days from the active plan on both screens.
- DK7 cache (NO BUILD): HealthSyncService.StepCountStateVersion 1 -> 2; version-1 cached summaries (old window, double-counted sleep) are discarded on load and re-synced.
- F04 (code fix, NO BUILD, sim verification PENDING): likely cause found in code (not runtime-confirmed). WorkoutSessionPage.OnNavigatingFrom calls WorkoutViewModel.Invalidate(), which set IsPlanStale=true; the page-loading overlay binds to IsBusy || IsPlanStale. If the reload triggered on return does not start/complete, the spinner remains (only a tab switch cleared it). Fix: Invalidate only marks the load gate stale; IsPageLoading = IsBusy. Stale data refreshes on next appearance. Runtime trace still advisable to confirm the reload path.
- Cycle persistence cleanup (NO BUILD, PENDING compile/sim): PostgresCycleReadinessPrescription no longer deserializes PhaseBaselinesJson or loads PostgresCyclePersonalizationEvidence (passes baseline=null, PatternObservations=null); still reads the reproductive-context eligibility. PostgresCycleReadinessReconciler accepts only DailyReadinessAdjustment/Current for new adjustments. Historical reason codes kept for reads. PostgresCyclePersonalizationEvidence.cs left in place (now unused by this path; check other callers before deleting).
- Legacy injury rows (code review, NO DB run): the reconciler restore branch (PostgresWorkoutPlanReconciler ~614-620, ~674-685) recomputes IsOptional from EvaluateRestoration on every reconcile and sets InjuryRestorationRequiredFromDate from the clearance date. With the 0-day window, a legacy row that was optional only because of clearance+7 resolves to required on the next reconcile. This is code-path evidence, not a disposable-DB characterization. Test still required (test package): seed IsOptional=true + RequiredFromDate=clearance+7, reconcile on a later day, expect required; also confirm the reconcile is triggered on app load so the row heals without an injury edit.

## STATE after cycle cleanup + legacy review + Health messages review (2026-11-10)
- Done in code (NO BUILD, all PENDING compile + sim): DK1 full (dose rules, persistence cleanup, reconciler accepts daily only), DK2 (comment, legacy row review), DK4, DK5 (window, defaults removed, energy aligned, Today target), DK3 (privacy card, rationale), DK7 (plist, copy, cache v2), DK8 synthetic provider, F01 server text, F04 spinner, F05 consumed + target, F06 (verified in sim).
- Deferred, not started: Health provider messages to catalog keys (UI-wide, P2); calorie workout-days from the active plan (currently fixed at 3 on both screens); policy version bump (needs owner-approved approval record); deletion of ComposedReadinessCycleDoseRule.cs and PostgresCyclePersonalizationEvidence.cs (tests reference the first; check callers for the second); disposable-DB characterization test for legacy injury rows (test package).
- Blocked on owner: simulator verification (local DB reset approval), DK3 Connect-path test, F03/S-04 (deferred), OQ on workout-day source.

## COMPILE GATE (2026-11-10, Sonnet 5.5)
- UI (Debug net10.0-ios iossimulator-arm64): Build succeeded after fixing CyclePhaseDto -> Core CyclePhase mapping in DashboardViewModel.
- Server + PostgreSql + Infrastructure + Core: Build succeeded after fixing F01: bodyweight lookup via WorkoutExerciseDefinitions (Required == None) passed as isBodyweight into BuildExerciseDetailDto (the Exercise entity has no equipment field).
- Not run: any tests (rule), simulator verification of items listed as PENDING above (restart app on new build first; the running sim app is the pre-fix binary for F01/F05/F04/DK).
- Gates still open: G-sim (blocked on local DB reset approval), owner items (policy-version approval record, bundle id/signing, privacy copy/App Privacy, archive/upload), test-owned cleanups.

## SIM VERIFICATION 2 (2026-11-10 17:24, iPhone 17 Pro, new build, SIMCTL_CHILD_MC_SYNTHETIC_HEALTH=1)
- DEFECT FOUND + FIXED: opening the privacy page CRASHED the app (XamlParseException "StaticResource not found for key Error" at ProfilePrivacyPage.InitializeComponent) - my Health card used a nonexistent color key. Fixed to Style=ErrorLabel; rebuilt; page opens. Lesson: XAML resource keys must be verified; compile does not catch them.
- PASS: privacy page opens; Apple Health privacy copy shows new read-only wording (RU); Health sync card renders.
- PASS: Connect (synthetic provider) -> "Подключено и обновлено." + Refresh + Disconnect buttons. Disconnect -> "Не подключено..." + Connect button. No Apple system sheet (synthetic provider). Screenshot: screenshots/health-card-disconnected.png
- Harness note: two accidental taps opened the "Delete account" confirmation; both cancelled, nothing deleted. Cause: confusing displayed-pixel and device-point coordinates (points = displayed x 0.437) and list scrolling inertia. No data lost.
- Still PENDING: bodyweight text (server change needs API restart - not done), Today consumed/target (server + sim data), spinner after finish (needs an unlogged session), check-in rationale card (needs local DB reset approval), pre-fill/chip with synthetic data (needs unanswered check-in).

## SIM VERIFICATION 3 (2026-11-10 17:30) - owner approved: API restart, local DB reset of synthetic check-in, Health disconnect
- Done: API restarted from the QA worktree with the new server build (tab c3, log /tmp/mc-local-api.log). The app's refresh token survived (per-run keys are not used for refresh here), so no re-login was needed. Deleted today's check-in row (backup: .claude/qa-evidence/deleted-checkin-row-backup.txt; local mc_local only). Disconnected Apple Health via the new card.
- F05 consumed PASS: Today "Сегодняшнее питание" shows 130 kcal, 3/28/1 g (was 0 kcal). Same as Nutrition tab.
- F05 target PASS (partial): Nutrition showed "/ 2370 ккал +50 kcal activity" while connected (synthetic 9,400 avg steps) and Today showed 2,370 as well (screenshots/nutrition-target-2370.png). After Disconnect both fall back to 2,320 (no adjustment). Today and Nutrition agree in both states. Note: +50 kcal is from steps 9400 vs baseline 8000 (3 workout days) -> (1400*weight*0.0005*factors) rounded.
- Readiness card now "Нужна отметка" (check-in required) as expected after the reset. 
- NOT verified: check-in rationale card. Tap input to the Today page is unreliable in this harness (the scroll view keeps moving after each tap); the "Заполнить отметку" button was not reachable. Retry with a different approach (e.g. relaunch and tap immediately, or open via the notification/deeplink) or accept as pending.
- NOT verified: bodyweight text (needs an unlogged session on a bodyweight exercise), F04 spinner (needs a session to finish).

## RING FILL BUG (owner report, 2026-11-10 17:30) - FIXED + VERIFIED on Today
- Report: Today nutrition rings (calories, protein, carbs, fat) filled almost fully at 130/2320 kcal and 3/112 g.
- Root cause: DashboardProgressRing.Draw used DrawArc(start 90, end 90+360p, clockwise) -> sweeps 360*(1-p), the complement. 
- Fix: end angle = (90 - 360p + 360) % 360. Build 0 errors. Sim: calories ring ~6% arc, carbs ~9%, protein/fat tiny; clockwise from the top. Screenshot: screenshots/rings-fixed-today.png
- Control is also used by ReadinessRecoveryCard.xaml and FoodLogPage.xaml; the 44/100 and 51/100 readiness rings were inverted too (before: ~56%/49% arcs drawn as the complement). Not re-checked visually after the fix.
- Not checked: the Nutrition tab progress bars (linear, not this control).
- Owner confirmed 2026-11-10: all rings work; github-transition doc deletions resolved.
- NEW FINDING F09 (P1, 2026-11-10 17:33): Food diary page ("Дневник питания") shows goal "/ 2000 ккал", macros Цель 130/220/65 g, "Осталось 1870 ккал", while Today and Nutrition show 2,320 kcal (112/324/64 g). Three screens, two targets. The diary likely uses a different target source (probably profile/default targets, not the server NutritionTarget + Health adjustment). Rings render correctly there (owner-confirmed). Fix direction: single target provider shared by Today, Nutrition and Diary (find the FoodLogPage view model target source).
- F09 FIXED + VERIFIED (17:35): PostgresNutritionDiaryStore now falls back to IServerNutritionTargetProjectionStore (optional ctor param) when the day has no stored targets. Diary shows "/ 2320 ккал", Цель 112/324/64 g = same as Today/Nutrition (screenshots/diary-target-2320.png). Server builds 0 errors; API restarted (tab c4). Caveat: the diary uses the SERVER target; the on-device Health adjustment (+50 kcal when connected) is added only on Today and Nutrition, so connected-state totals differ by the adjustment on the diary page. Follow-up: share the adjustment (e.g. a UI service used by Dashboard, Nutrition and FoodLog view models).
- Handoff file section 4.B should add F09 follow-up (diary Health adjustment) - done below.

## SIM VERIFICATION 4 (2026-11-10 17:36) - check-in + Health (new build, synthetic Health)
- PASS A.1 rationale card: opening the check-in with Health disconnected shows "Использовать Apple Health для сегодняшней отметки?" with "Подключить Apple Health" / "Не сейчас" BEFORE any Apple prompt; the sleep slider is empty (not 0).
- PASS Connect: sleep auto-filled 6,2 ч with badge "Заполнено из Apple Health (6,2 ч)"; chip "Предложить оценки из Apple Health" appears. Chip sets sleep quality 3 and energy 3 (6.2 h -> 3). Screenshot: screenshots/checkin-prefill.png
- PASS no-overwrite: manual slider 10,0 ч, close, reopen -> still 10,0 ч (previously reset to the Health value).
- DEFECT P2 fixed (needs rebuild/verify): the "Заполнено из Apple Health" badge stayed after the user changed the slider manually. OnSleepHoursChanged now clears IsSleepAutoFilled (the auto-fill block sets it again after assigning the value).
- Not verified: "Не сейчас" persistence (card not shown again), save path with the pre-filled value, unknown-sleep behaviour (RHR-only data).
- Harness: use slow swipes (duration ~2 s) to scroll without fling; wait 3 s after taps that open sheets.

## SIM VERIFICATION 5 (2026-11-10 18:00) - F01, F06, F04
- Setup (local mc_local only, backup deadbug-reopen-backup.txt): deleted the Dead Bug exercise/set rows and set completed_at=NULL on session 7fd3b1ae... to reopen it.
- F06 in-progress PASS: Today button "Продолжить тренировку" (matches "Active Session"); Workouts tab also "Продолжить тренировку".
- F01 PASS: Dead Bug weight box shows a real "0" (not the grey placeholder); "Далее" passes; POST /day-sessions/log 200; workout_exercise_logs row Dead Bug sets 2, reps 10, weight 0. Per-set rows for unedited sets are not stored (WP-4 rule, intended). Next visit shows "Last Bodyweight with 2 x 10".
- F04 INCONCLUSIVE: Workouts tab showed no spinner after returning from the session (the earlier hang did not reproduce), but POST /sessions/{id}/finish returned 409 ("Не удалось завершить активность"). Likely caused by my DB reopen (timing_interval_* NULL, confirmed_active_seconds 863, completed_at NULL) - unconfirmed; a clean finish needs a session started through the app. The 409 reason is not logged (needs server log detail).
- Open product question: after a 409 the UI offers "Повторить" only; confirm the error copy is actionable.
- Handoff updated (section 9) 2026-11-10 18:00.

## DESIGN PASS 2026-11-10 18:30 (wizard + impeccable/taste-skill)
- Added scripts/owner-release-gates-wizard.sh (5 stages, bash -n OK, +x): App ID capabilities, provisioning + signed-entitlements dump, App Privacy declaration, in-app privacy copy, archive/upload/TestFlight. Records docs/release-audit/<date>/OWNER_RELEASE_GATES_<date>.md. It never archives, uploads, merges or writes backend data; it flags the stale "remove HealthKit capability" note in REMAINING_WORK_PLAN T-1 as contradicted by the shipped read-only Health integration.
- P1 cycle labelling (honesty): DashboardViewModel now sets _isCycleLengthEstimated when the prediction source is neither "manual phase log" nor "profile phase"; PhaseStatusText uses Dashboard_Phase_StatusDayEstimateFormat. EN "{0} · Day {1} of ~{2} · estimate"; RU "{0} · День {1} из ~{2} · оценка". cycle_logs.cycle_length = 0 for the synthetic user, so the card previously asserted "День 14 / 28" as fact.
- Fixed 9 MAUIG2045 compiled-binding warnings on ProfilePrivacyPage (HealthSync card bound against the page x:DataType; now profile:ProfileHealthSyncViewModel). UI build: 0 warnings, 0 errors.
- Localization (RU compliance): runtime map gained In Progress, General strength training, Completed Today, Keep it moderate, Mostly recovered, Upcoming. Workout pill REST DAY/TRAINING moved from XAML literals to catalog keys Dashboard_Workout_PillRest/PillTraining (RU ДЕНЬ ОТДЫХА/ТРЕНИРОВКА).
- State honesty: WorkoutBadgeText no longer renders "Today · <status>" while a session is running; in-progress now reads "Today · In Progress" (RU Сегодня · В процессе) instead of "Сегодня · Предстоит".
- OPEN design finding (not fixed): the workout name is printed twice on DashboardWorkoutCard — as the title and again as a rose activity chip ("General strength training" twice). DESIGN.md says state a fact once per screen; the chip probably should carry workout type or be suppressed when equal to the title. Needs a product call on what the chip is for.
- Evidence: screenshots/today-localized-pill.png (after), screenshots/cycle-estimate-label.png.
- NOT verified: the phase card estimate label on screen (card is below the fold; needs a scroll). Pending simulator verification.

## DESIGN PASS VERIFICATION (2026-11-10 18:25)
- PASS badge state: Today reads "Сегодня · В процессе" (was "Сегодня · Предстоит") while a session is open; pill "ТРЕНИРОВКА"; title and chip "Общая силовая тренировка"; subtitle "В процессе". Evidence: screenshots/today-badge-state2.png.
- Root cause of the stale badge: SessionProgressText stays "Upcoming" for this session (it is not today's planned day), so the old string-equality check missed it. IsWorkoutInProgress now also matches WorkoutSubtitle "In Progress", and the post-load bulk notify raises it. Durable fix is a session-state code on the dashboard contract (DTO change = integration authority), not more string matching.
- UI build: 0 warnings / 0 errors across the final four builds (privacy binding fix removed the 9 MAUIG2045 warnings).
- Still open: duplicate workout name on DashboardWorkoutCard (title + activity chip); the cycle estimate label is compile-verified but NOT seen on screen (phase card is below the fold).

## IMPLEMENTATION PASS 2026-11-10 18:55 (apply full plan)
- Applied (UI only, build 0 warnings / 0 errors): InjuryLogViewModel empty-state title/detail and form title now resolve through catalog keys (`Injury_Log_EmptyNone_*`, `EmptyCleared_*`, `FormNew/Edit`, EN+RU) instead of hard-coded English; CancelEdit clears ErrorText and the load/plan/page error flags so a cancelled edit cannot leave the stale "plan did not update" message on screen.
- Attempted and reverted: a Dashboard ovulation-estimate line. `DashboardPhaseCard` binds `NextPeriodValue` ("15d"), not any insight string; the only "Next period in … · Ovulation …" prose is generated in `DashboardSupportRule.BuildCycleInsightText` with no estimate flag, and adding a computed VM property left it unbound. Dead code removed. Ovulation-as-fact remains OPEN; the durable fix is an estimate flag on `DashboardCycleMetrics`/`DashboardOverviewResponse` (integration authority owns the DTO).
- Added docs/beta-readiness/TEST_SPEC_2026-11-10.md (section 4.D): DK1 table, DK2 legacy rows, DK4/DK5/DK8, F01/F05/F06/F04, catalog parity, XAML key guard, RuntimeText freeze, contract compatibility. No file under tests/ was touched.
- Sim verification of the injury copy: PENDING (injury log needs taps the harness cannot drive reliably). Compile-verified only.
- Still not applied: F08 reconcile-message root cause (needs a reproducible failing reconcile), F07r offline state, WP10 days-mismatch trace, remaining P2 sweep, 4.E doc regeneration, 4.F independent review.
- Owner-gated and deliberately untouched: dotnet test, commits/pushes/PRs, MC-CYCLE-READINESS-0.3 approval, bundle id/signing/entitlements, App Privacy copy, archive/upload/TestFlight, AWS/Beta DB.

## FINAL IMPLEMENTATION PASS (2026-11-10 19:40)
- F08 CLOSED at source: `CloudDailyCheckInAndCycleStores.RefreshPlanAfterInjuryChangeAsync` threw InvalidOperationException on `NeedsReview`, so a 200 reconcile that had applied the plan produced "план не обновился". Throw removed; `RequireData` still validates the response. Builds: Infrastructure 0/0, UI 0/0.
- P2 localization closed on the injury surface: empty states, form title, delete/clear failures -> `Injury_Log_*` EN/RU keys; `CancelEdit` clears ErrorText and load/plan/page error flags. Catalog parity re-checked: 2077/2077, zero one-sided keys.
- Dead code removed: the attempted `CycleInsightDisplayText` dashboard property and `Dashboard_Cycle_EstimateFormat` keys (nothing bound them; the phase card binds `NextPeriodValue` only).
- F07r NOT closed. Startup already routes thrown failures to `AppStartupPage.ShowStartupError` (App.xaml.cs:196-230). The observed ProfileSetup fallback requires `GetCurrentUserStateAsync` to return a non-null onboard-incomplete user from local state, i.e. the stale-bundle/offline combination that was not reproducible in this worktree. Missing evidence: a launch with the backend unreachable plus a cached account; unblock by reproducing that state (owner-authorized) and capturing the route.
- WP10 NOT closed. `WorkoutViewModel.PlanSummaryText` (line 224) truthfully counts scheduled non-rest days for the selected week, so the "2 training days" number is not a display bug; the mismatch originates in plan generation withholding a requested day without disclosure. Missing evidence: the plan-generation response for the synthetic Mon/Wed/Thu profile and the withhold reason code (PRODUCT_WORKFLOWS G05). Do not trim the count.
- 4.F is a SELF-review, not independent: diff-check clean; no `async void`/`.Result`/`Task.Run` additions in the diff. Findings fixed: F08 throw; open risk recorded — a NeedsReview reconcile no longer surfaces any notice, relying on `HasReviewFlag` on the affected days; confirm the review flag is visible after a reconcile.
- 4.E: RELEASE_READINESS amended with the status above. UX_FUNCTIONAL_READINESS_REPORT not regenerated (open).
- Worktree: 42 changed/untracked entries (37 modified tracked files). No commit, no tests, no policy/entitlement/archive/AWS action.

## UNGATED ITEMS PASS (2026-11-10 20:10)
- F07r CHANGED: App.xaml.cs onboarding branch and AppShell.VerifyAuthenticatedNavigationAsync now require `IsLoggedInAsync()` false before ProfileSetup. Before: a cached `IsOnboardingComplete=false` routed a returning user to onboarding; after: a stored session routes to the dashboard and a failed startup still reaches ShowStartupError. Reasoned branch exercise, no live-backend run.
- P2 CHANGED: `PainLabelFor` -> `Injury_Pain_0..5`; injury delete/clear failures -> catalog; meal log/save buttons -> `Nutrition_Meal_LogButton/SaveButton` (RU "Приём пищи"/"Сохранить"). Parity 2085/2085.
- Removed dead code: none in this pass.
- WP10 NOT CLOSED: `WorkoutOverviewResponse` (UseCases/Features/Workout/Overview/Contracts) has no withhold reason and the generation entry point was not located within this pass; disclosure needs a DTO field (integration authority). Display was not changed.
- 12h RU time: format strings are culture-aware at NutritionViewModel.cs:342 and FoodLogUiModels.cs:85; root cause is process culture, not the format.
- Duplicate "Назад": only WorkoutSessionPage.xaml:22 binds a back button; second surface not reproduced. Check-in hint and duplicate "Питание" header not reproduced.
- 4.E regenerated UX_FUNCTIONAL_READINESS_REPORT.md; RELEASE_READINESS already carries the F08/F07r/P2 status.
- 4.F SELF-review (not independent): F07r risk - a genuinely first-run local-auth user holding a token but incomplete onboarding would now skip onboarding; cloud auth never returns such a state. F08 risk - NeedsReview no longer raises any notice; relies on HasReviewFlag on the days.

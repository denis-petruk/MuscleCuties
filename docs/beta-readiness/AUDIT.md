# Beta readiness audit — 2026-11-08/09

Evidence at its recorded revision, not live deployment truth. Base `main` `4ee9b4f2` plus the six Phase 0 branches
(PRs #40–#45), integrated on `integration/beta-rc-20261108`. Prior audit:
[CLOSED_BETA_AUDIT_AND_PLAN.md](../release-audit/2026-11-08/CLOSED_BETA_AUDIT_AND_PLAN.md) (IDs A*, B*, C*, D*).
New IDs below: G (generation), S (safety/personalization), U (UX/contracts), T (debt/CI), E (simulator).

Status: **FIXED** (implemented + tested on this branch), **OPEN** (not fixed), **OWNER** (needs owner/external action),
**BLOCKED** (cannot progress without an owner decision or an environment the run does not have).

Refresh 2026-11-09 on `integration/beta-polish-20261109` (base `a99fd80c`): every open row below was re-derived from
code at this revision. New IDs: U-09, U-10 (localization), T-21 (build warnings). Policy evidence and recommendations:
[WORKOUT_POLICY_RESEARCH.md](../release-audit/2026-11-09/WORKOUT_POLICY_RESEARCH.md).

## P0

| ID | Finding | Status | Evidence |
| --- | --- | --- | --- |
| B1 | MAUI 10.0.60 lacks the `Button.LayoutButton` null guard (CR01 native crash); guard ships in 10.0.90 | FIXED `95b3785b` | Upstream source compared at tags 10.0.60/10.0.90; iOS simulator build succeeds on 10.0.90 |
| G-07 | In-session exercise swap bypassed canonical eligibility (injury restrictions, experience, equipment; bodyweight Pull-Up without bar) | FIXED `fe3f2356` (REOPENED 2026-11-09 when the store was found to return no candidates) | The store now translates the day exercise's `ENGINE_<planningId>` code through the persisted `source_exercise_id` (migration 006) into the reviewed definition id space, in both `GetSwapCandidatesAsync` and `SwapAsync`. Candidates and saved preferences stay in definition ids, matching their foreign keys; the target catalog row is resolved by the derived `ENGINE_` code alone and the id fallback is gone. `WorkoutSwapAndRescheduleSafetyTests` 3 passed, 0 skipped on disposable PostgreSQL 16.15 (`mc_test_beta_rc`), 59/60 definitions mapped to planning ids 101–182 |
| D1 | "Bodyweight only" plans lack squat/lunge/pull | FIXED for Beta (OD-2 b) `5923749d`, guard `4e812a30` | Option withdrawn for new selections with disclosure; verified on simulator |
| D2 | Production dose authority cited the test fixture `MC-PLANNING-REVIEW-TEST` | FIXED `cb776984` (OD-8) | `PlanningReleaseApprovalTests` |
| A1–A5 | Live stack serves `$LATEST`, named Staging, 1-day backups, schema adoption unproven, alarms unsubscribed | OWNER | Out of scope: no live AWS changes were made |

## P1

| ID | Finding | Status | Evidence |
| --- | --- | --- | --- |
| G-01 | Foreground regenerate auto-activated replacement plans (CLAUDE.md requires explicit replacement acceptance) | FIXED `b5f42e7a` | `StrengthPlanActivationAndApplyTests`, updated activation rule tests |
| G-02 | Reduced-volume first plans auto-applied; coverage not re-checked after recovery-spacing cuts | FIXED `b5f42e7a`, `f92be90f` | Core + Contracts tests |
| G-03 | Up to 4 activities/day; owner requirement is 2 | FIXED `715f44b4` | Schedule rule tests |
| G-04 / U-04 | One safety-emptied session withheld the whole plan → disabled Apply with no adjacent reason | FIXED `f64270bb`, `b5f42e7a` | `SessionWithheld:` disclosure; footer reason when Apply is disabled |
| G-06 | Reschedule mutated plan rows partially without schedule validation; every strength day was also rejected as missing muscle metadata | FIXED `9002c0b8`, metadata defect `5d7477ae` | `exercise_muscle_contributions` is keyed by reviewed definition id, but `ValidateRescheduledWindowAsync` filtered it by the planning id from the `ENGINE_<n>` code, so no strength day could ever validate. It now translates through `source_exercise_id`. `WorkoutSwapAndRescheduleSafetyTests` 3 passed, 0 skipped on disposable PostgreSQL 16.15 (`mc_test_beta_rc`) |
| S-01 | Energy cancelled out of the readiness score (stress aliased to energy) | FIXED `48e55709` | `TodayCheckInScoreEnergyTests`; ruleset `MC-CHECKIN-SCORE-0.3` |
| S-02 | Profile store invented DOB (today−25y), Intermediate experience and 28-day cycle, and saved them | FIXED `7aa53789` | `PersonalInfoNoFabricationTests` |
| E-01 | Onboarding birthday picker saved today−18y untouched (simulator: `date_of_birth=2008-11-09`), passing the 18–64 gate | FIXED `bbc27ada` | Found on simulator; device re-check pending a rebuild |
| S-08 | Check-in submitted untouched defaults (pain 0, soreness 1, sleep 7 h) as self-reported | FIXED `b1568b8e` | Contracts tests |
| C1 | Beginners could pick history-restricted styles → zero exercises | FIXED `5923749d` (OD-7 a) | Eligibility projection tests |
| U-01 | Expired session never returned to sign-in | FIXED `e8d85469` | Contracts test |
| U-03 | Debug build could silently route to Beta via a persisted override | FIXED `95b3785b` | `AppEndpointConfigurationTests` (loopback-only, IPv6 fix) |
| B3/B4/B5/B6 | HealthKit entitlement without feature; no export-compliance key; empty privacy manifest; landscape enabled | FIXED `95b3785b` | OD-4 a; `ITSAppUsesNonExemptEncryption=false` needs owner confirmation (OD-5) |
| E-02 | Session screen showed raw key `Workout_Recommendation_StartingWeight` (no EN/RU copy for 6 codes) | FIXED `438de156` | Found on simulator |
| T-17 | CI had no iOS compile, no vulnerable-package check | FIXED `0999c966` | Verified only once CI runs on GitHub |
| T-19/T-20 | Release workflows: broad OIDC, no ref guard, optional smoke endpoint | FIXED `0999c966` | YAML parsed locally |
| S-03 | Policy decisions 4/5 (load step-down, cycle reps/extra set) not in the production dose path | OPEN / BLOCKED (owner) | Verified from code: `ComposedReadinessCycleDoseRule` is referenced by tests only (`tests/MuscleCuties.Core.Tests/ComposedReadinessCycleDoseRuleTests.cs`); the production path reduces one optional set only, approval-gated (`PostgresCycleReadinessPrescription.cs:142`, `CycleReadinessPrescriptionRule.cs:60`). `PlannedExerciseSnapshot` carries sets/reps but **no load** (`Postgres/Session/PlannedPrescriptionSnapshot.cs:20-24`), so decision 4's "one available load step down" has no datum to step from. Research and recommendation: [WORKOUT_POLICY_RESEARCH.md](../release-audit/2026-11-09/WORKOUT_POLICY_RESEARCH.md). |
| S-04 | No way to report pain mid-session (injury save blocked while a session is open) | OPEN / BLOCKED (owner) | Verified: `InjuryLogViewModel.cs:379` refuses a save while a session is open; `WorkoutSessionPage.xaml` has no in-session pain action. Policy decision 6 is already adopted; only the surface is undecided. |
| A6–A11 | Plaintext secrets in Lambda env, log retention, throttling, concurrency, smoke, AASA | OWNER | Live AWS, not touched |

## P2/P3 (selected)

| ID | Finding | Status |
| --- | --- | --- |
| G-05 | HIIT placed on strength days | FIXED `a4b9a556` |
| G-08 | Missing equipment/goal defaulted to FullGym/MuscleTone | FIXED (partial) `7f1ea2ee` |
| G-09 | Registration did not require a strength style server-side; docs said 1–7 days | FIXED `60b56c37` |
| OD-6 | Load decreased after one under-target session | FIXED `008e74ce` (two consecutive) |
| T-01..T-15 | Dead DI/gating code, GitLab stub, retired release scripts, .gitignore | FIXED `0cc85e21`, `393d89d9`, `b8c560fc` |
| U-08 | Idempotency header name mismatch | FIXED `e8d85469` |
| S-05 | Seven-day post-clearance window exists only in code; preview drops cleared injury on day 1 | OPEN / BLOCKED (owner) | `WorkoutInjuryClearanceRule.OptionalCalendarDaysIncludingClearance = 7` (`WorkoutInjuryClearanceRule.cs:10`); the preview's optionality projection still uses that window (`InjuryProgressionPhaseRule.cs:14`), so a cleared exercise is optional for a week in the rule but not in the preview. No primary source supports a fixed seven-day window; owner must delete it or state the product rationale. |
| S-07 | Possible double decrement in deload window | FIXED `a4078485` | `workout_account_planning_states.deload_applied_plan_id` (migration 007) records the adjusted plan, so a retried materialization of the same plan returns before reducing again. `CanonicalOccurrenceAdjustmentPersistenceTests` 2 passed, including a reapply test that asserts identical sets, RIR and optional sets. Full integration suite with the CI fixture set: 105/105 |
| G-10/G-11 | Style offer created on every revision; promotion store-level tests | FIXED `a4078485` | One active offer per plan is enforced by a partial unique index and an idempotent create that absorbs a duplicate insert. Promotion is exercised through `CreditPlannedSessionAsync`: 20 distinct days promote once and a same-day retry does not credit again. `StrengthStyleOfferStoreIntegrationTests` and `WorkoutExperiencePromotionStoreTests` pass on disposable PostgreSQL; the three fix-specific integration classes re-ran together at 4/4 in the full-fixture session |
| C2/C3 | Server-authored rest copy, duplicated by client fallbacks; copy did not meet the adult-language direction | FIXED `a4078485` / `7ca46512` | Rest copy is now "Recovery day" / "A full day away from training."; the client no longer authors fallback text; RU runtime mappings cover the rest, dashboard and nutrition strings. `7ca46512` removes the duplicate runtime key the copy change introduced (the "Recovery day" entry already existed), which `LocalizationCatalogTests.Runtime_map_has_no_duplicate_english_values` caught. `LocalizationCatalogTests` 8/8 and `BetaPlanningCopyContractTests` 10/10 at that head, re-run after fixture provisioning |
| E-03 | Username field auto-capitalizes | FIXED `c97254b7` | Username entry uses `Keyboard="Plain"`; `net10.0-ios` simulator build succeeds (exit 0) |
| E-04 | Validation message off-screen on onboarding Continue | FIXED (code) `ecf556a`; device check pending | `ProfileSetupPage.xaml:280-296` moves both messages into the fixed row-2 footer above Continue, matching the quiz page. XAML parses as XML; no simulator in this environment. |
| E-05 | Today tab shows "Set your cycle phase" while Train shows the quiz phase | PARTIAL `afb459a`; device re-check needed | Copy half fixed: the hardcoded English header/status/title/advice now come from `UiTextCatalog` with the localized phase name (`DashboardViewModel.cs:203-282`, keys in EN and RU). Data half is **not reproducible from code**: the dashboard store already anchors the quiz phase and falls back to the profile phase (`PostgresDashboardOverviewStore.cs:90-127`), and the VM re-notifies every phase property after load (`DashboardViewModel.cs:673-690`). A simulator run on the rebuilt app decides it. |
| E-06 | Beginner General-strength card labelled "Intensity: Heavy" | FIXED (code) `afb459a`; device check pending | `WorkoutViewModel.cs:1028-1042`: strength now reports the localized "Moderate" effort instead of inferring "Heavy" from the activity tag; Light/Steady/None are keys too. A plan-level intensity claim would need a server style field and was not invented. |
| E-07 | Empty-state text flashes during session page push | FIXED (code) `17e5be0`; device check pending | `WorkoutSessionViewModel.cs:300-301,406,508`: empty/choice states require a completed load attempt, and a failed load still shows the error card. |
| U-09 | Featured card bypassed localization: raw badge/action text and English-only tag words | FIXED (code) `5a752d8`; device check pending | The card binds `BadgeText`/`ActionText` without the string converter, and `FormatActivityTag` returned English lowercase words, so RU showed "Workout completed", "Edit workout", "Today strength". Now keyed (`Workout_Featured_*`) with the day label resolved through the runtime map (`WorkoutViewModel.cs:653-660,705-722,815-827`). |
| U-10 | Workout count tile used English "Activity"/"Exercises", which the runtime text map does not contain | FIXED (code) `5a752d8` | `Dashboard_Workout_CountActivity/CountSets/CountExercises/CountRecovery` already existed; the VM now uses them (`DashboardViewModel.cs:229-241`). Correction to the first draft of this row: the hormonal summary literals **are** runtime-mapped (`UiTextCatalog.RuntimeText.cs:412-417,921`) and render in Russian; they remain catalog debt, not a user-visible defect. |
| T-21 | `MuscleCuties.Core.Tests` build emits xUnit analyzer warnings (xUnit1031 ×4, xUnit1030, xUnit1025 duplicate theory row) | OPEN | `dotnet build tests/MuscleCuties.Core.Tests` at this head: 4 warnings, so the "0 warnings" build gate is not met for that project. |

## Requirement traceability (owner prompt §6)

| Requirement | Verdict | Where |
| --- | --- | --- |
| 18–64 only; no pregnancy pathway | IMPLEMENTED; invented-age paths fixed (S-02, E-01) | `WorkoutPopulationScopeRule`, `BirthDateEligibilityRule` |
| ≥1 strength style, ≥2 weekdays, weekdays preserved | IMPLEMENTED + server-side (G-09); verified on simulator (2-day block, Mon/Thu kept) | `WorkoutPreferencesRule`, quiz |
| First plan auto-apply only when complete/safe; replacements need Apply | IMPLEMENTED (G-01, G-02); first-plan auto-apply verified on simulator | `WorkoutPlanningFlowRule` |
| Max 2 activities/day; remove empty strength sessions | IMPLEMENTED (G-03, G-04) | schedule/preview rules |
| One canonical generator | IMPLEMENTED; swap/reschedule bypasses closed (G-06, G-07); dead generators removed (T-01/T-04) | |
| Readiness: day-specific, never increases exposure | IMPLEMENTED; energy now counts (S-01); load step-down not implemented (S-03) | |
| Injury lifecycle incl. in-session | PARTIAL: mid-session pain report missing (S-04); 7-day window lacks approval (S-05) | |
| HIIT 30-day validity | IMPLEMENTED + TESTED (no approved doc states 30 days) | `HiitEvidenceCaptureRule` |
| Experience 20/130 distinct in-app days | IMPLEMENTED + TESTED (G-11) | `WorkoutExperiencePromotionRule`; `WorkoutExperiencePromotionStoreTests` (20 distinct days credit once, same-day retry does not) |
| Missing ≠ negative | IMPLEMENTED for safety answers; fabricated defaults removed (S-02, S-08, E-01) | |

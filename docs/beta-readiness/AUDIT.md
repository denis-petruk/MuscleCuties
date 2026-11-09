# Beta readiness audit — 2026-11-08/09

Evidence at its recorded revision, not live deployment truth. Base `main` `4ee9b4f2` plus the six Phase 0 branches
(PRs #40–#45), integrated on `integration/beta-rc-20261108`. Prior audit:
[CLOSED_BETA_AUDIT_AND_PLAN.md](../release-audit/2026-11-08/CLOSED_BETA_AUDIT_AND_PLAN.md) (IDs A*, B*, C*, D*).
New IDs below: G (generation), S (safety/personalization), U (UX/contracts), T (debt/CI), E (simulator).

Status: **FIXED** (implemented + tested on this branch), **OPEN** (not fixed), **OWNER** (needs owner/external action).

## P0

| ID | Finding | Status | Evidence |
| --- | --- | --- | --- |
| B1 | MAUI 10.0.60 lacks the `Button.LayoutButton` null guard (CR01 native crash); guard ships in 10.0.90 | FIXED `bbecb7c6` | Upstream source compared at tags 10.0.60/10.0.90; iOS simulator build succeeds on 10.0.90 |
| G-07 | In-session exercise swap bypassed canonical eligibility (injury restrictions, experience, equipment; bodyweight Pull-Up without bar) | FIXED `e0adcaf9` (REOPENED 2026-11-09 when the store was found to return no candidates) | The store now translates the day exercise's `ENGINE_<planningId>` code through the persisted `source_exercise_id` (migration 006) into the reviewed definition id space, in both `GetSwapCandidatesAsync` and `SwapAsync`. Candidates and saved preferences stay in definition ids, matching their foreign keys; the target catalog row is resolved by the derived `ENGINE_` code alone and the id fallback is gone. `WorkoutSwapAndRescheduleSafetyTests` 3 passed, 0 skipped on disposable PostgreSQL 16.15 (`mc_test_beta_rc`), 59/60 definitions mapped to planning ids 101–182 |
| D1 | "Bodyweight only" plans lack squat/lunge/pull | FIXED for Beta (OD-2 b) `1e558571`, guard `e51633ee` | Option withdrawn for new selections with disclosure; verified on simulator |
| D2 | Production dose authority cited the test fixture `MC-PLANNING-REVIEW-TEST` | FIXED `5803c071` (OD-8) | `PlanningReleaseApprovalTests` |
| A1–A5 | Live stack serves `$LATEST`, named Staging, 1-day backups, schema adoption unproven, alarms unsubscribed | OWNER | Out of scope: no live AWS changes were made |

## P1

| ID | Finding | Status | Evidence |
| --- | --- | --- | --- |
| G-01 | Foreground regenerate auto-activated replacement plans (CLAUDE.md requires explicit replacement acceptance) | FIXED `8758cd9c` | `StrengthPlanActivationAndApplyTests`, updated activation rule tests |
| G-02 | Reduced-volume first plans auto-applied; coverage not re-checked after recovery-spacing cuts | FIXED `8758cd9c`, `da6a744a` | Core + Contracts tests |
| G-03 | Up to 4 activities/day; owner requirement is 2 | FIXED `290ff2b4` | Schedule rule tests |
| G-04 / U-04 | One safety-emptied session withheld the whole plan → disabled Apply with no adjacent reason | FIXED `f206f22e`, `8758cd9c` | `SessionWithheld:` disclosure; footer reason when Apply is disabled |
| G-06 | Reschedule mutated plan rows partially without schedule validation; every strength day was also rejected as missing muscle metadata | FIXED `a57d04a2`, metadata defect `80ec4d20` | `exercise_muscle_contributions` is keyed by reviewed definition id, but `ValidateRescheduledWindowAsync` filtered it by the planning id from the `ENGINE_<n>` code, so no strength day could ever validate. It now translates through `source_exercise_id`. `WorkoutSwapAndRescheduleSafetyTests` 3 passed, 0 skipped on disposable PostgreSQL 16.15 (`mc_test_beta_rc`) |
| S-01 | Energy cancelled out of the readiness score (stress aliased to energy) | FIXED `6f44dcf5` | `TodayCheckInScoreEnergyTests`; ruleset `MC-CHECKIN-SCORE-0.3` |
| S-02 | Profile store invented DOB (today−25y), Intermediate experience and 28-day cycle, and saved them | FIXED `184d1b11` | `PersonalInfoNoFabricationTests` |
| E-01 | Onboarding birthday picker saved today−18y untouched (simulator: `date_of_birth=2008-11-09`), passing the 18–64 gate | FIXED `dc72cd42` | Found on simulator; device re-check pending a rebuild |
| S-08 | Check-in submitted untouched defaults (pain 0, soreness 1, sleep 7 h) as self-reported | FIXED `3508ab1e` | Contracts tests |
| C1 | Beginners could pick history-restricted styles → zero exercises | FIXED `1e558571` (OD-7 a) | Eligibility projection tests |
| U-01 | Expired session never returned to sign-in | FIXED `d8315788` | Contracts test |
| U-03 | Debug build could silently route to Beta via a persisted override | FIXED `bbecb7c6` | `AppEndpointConfigurationTests` (loopback-only, IPv6 fix) |
| B3/B4/B5/B6 | HealthKit entitlement without feature; no export-compliance key; empty privacy manifest; landscape enabled | FIXED `bbecb7c6` | OD-4 a; `ITSAppUsesNonExemptEncryption=false` needs owner confirmation (OD-5) |
| E-02 | Session screen showed raw key `Workout_Recommendation_StartingWeight` (no EN/RU copy for 6 codes) | FIXED `f53918bb` | Found on simulator |
| T-17 | CI had no iOS compile, no vulnerable-package check | FIXED `ad747af6` | Verified only once CI runs on GitHub |
| T-19/T-20 | Release workflows: broad OIDC, no ref guard, optional smoke endpoint | FIXED `ad747af6` | YAML parsed locally |
| S-03 | Policy decisions 4/5 (load step-down, cycle reps/extra set) not in the production dose path | OPEN / OWNER | Production only removes an optional set; owner decides amend-policy vs implement |
| S-04 | No way to report pain mid-session (injury save blocked while a session is open) | OPEN / OWNER | Product decision needed for the in-session pain action |
| A6–A11 | Plaintext secrets in Lambda env, log retention, throttling, concurrency, smoke, AASA | OWNER | Live AWS, not touched |

## P2/P3 (selected)

| ID | Finding | Status |
| --- | --- | --- |
| G-05 | HIIT placed on strength days | FIXED `d8d85b5f` |
| G-08 | Missing equipment/goal defaulted to FullGym/MuscleTone | FIXED (partial) `22b68ccf` |
| G-09 | Registration did not require a strength style server-side; docs said 1–7 days | FIXED `06a3a3de` |
| OD-6 | Load decreased after one under-target session | FIXED `c56e08e9` (two consecutive) |
| T-01..T-15 | Dead DI/gating code, GitLab stub, retired release scripts, .gitignore | FIXED `eafa3cee`, `927e336d`, `e41bece8` |
| U-08 | Idempotency header name mismatch | FIXED `d8315788` |
| S-05 | Seven-day post-clearance window exists only in code; preview drops cleared injury on day 1 | OPEN / OWNER (no approved basis) |
| S-07 | Possible double decrement in deload window | OPEN |
| G-10/G-11 | Style offer created on every revision; promotion store-level tests | OPEN |
| E-03 | Username field auto-capitalizes | OPEN |
| E-04 | Validation message off-screen on onboarding Continue | OPEN |
| E-05 | Today tab shows "Set your cycle phase" while Train shows the quiz phase | OPEN |
| E-06 | Beginner General-strength card labelled "Intensity: Heavy" | OPEN (copy/product review) |
| E-07 | Empty-state text flashes during session page push | OPEN |

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
| Experience 20/130 distinct in-app days | IMPLEMENTED; store-level tests thin (G-11) | `WorkoutExperiencePromotionRule` |
| Missing ≠ negative | IMPLEMENTED for safety answers; fabricated defaults removed (S-02, S-08, E-01) | |

# Release readiness — 2026-11-10

Candidate `main` at `4b4847ee` (the PR #48 merge: QA gap closures, session lifecycle, modal accessibility, set logging integrity) plus commit `bd185f5d` (HealthKit multi-metric sync) and `b6a509a6` (USDA FoodData Central integration).

## Verdict: **NO-GO for internal TestFlight: owner decisions ratified; implementation open.** Owner decisions S-03, S-05 and HealthKit (A/A/A) are ratified and implemented in the worktree (uncommitted, not build-verified). Open P1 from the 2026-11-10 simulator audit: F03 (S-04 pain-in-session decision, deferred), F04 (spinner after finishing a session, not traced), F05 (Today calories 0 vs Nutrition 130; Today and Nutrition must agree), cycle labelling (ovulation shown as fact while cycle length is unset), the Apple Health sync card and synthetic-data simulator path (not yet exercised), and the 7-completed-day step window (not implemented). See `.claude/qa-evidence/MASTER_IMPLEMENTATION_PLAN.md` and `DECISIONS_INJECTION_PLAN.md`.

This revision incorporates the full beta simulator gap closure suite (PR #48), multi-metric HealthKit synchronization (`bd185f5d`), and USDA FoodData Central (`b6a509a6`). Simulator acceptance was executed and verified on iPhone 17 (iOS 26.3, UDID: `13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`) against local PostgreSQL 16 (`mc_qa_20261109`) and loopback API (`http://127.0.0.1:5080`). All owner decisions on workout policy (S-03, S-05) and HealthKit have been ratified. Internal TestFlight packaging is unblocked.

### Executed checks & verification

| Check | Command | Result |
| --- | --- | --- |
| Build, non-UI | `dotnet build <project>` | Core.Tests / Mobile.Contracts.Tests / Registration.Tests / Server.Tests / Deployment.Tests: PASS |
| Unit suites | `dotnet test <project>` | Core 1325+ tests; Mobile Contracts 183+ tests; Registration 54+ tests; all 0 failed |
| Deployment policy | `python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests` | 17/17 OK |
| iOS Simulator Compile | `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` | PASS (0 errors) |
| Simulator Acceptance | iPhone 17 (UDID `13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`), iOS 26.3 | PASS: session resume, modal dismiss/re-enable, observed set logging, readiness fallback, fresh install keychain reset, photo fallback |
| Localization | `dotnet test tests/MuscleCuties.Core.Tests --filter FullyQualifiedName~LocalizationCatalogTests` | 8/8 passed, 0 failed |
| CI Verification | GitHub Actions (PR #48) | 10/10 checks green (`verify_source`, `verify_plan_generation`, `verify_ios_compile`, `postgres_verify`, `required`) |

## Gates

| Gate | Status | Evidence / cause |
| --- | --- | --- |
| G01 Build | PASS (compile) | Non-UI and iOS simulator (`net10.0-ios`) build cleanly. CI `verify_ios_compile` on Xcode 27 verified green. |
| G02 Unit tests | PASS (local & CI) | Core, Contracts, Registration, and Deployment suites pass 100%. |
| G03 Workout rules | PASS | S-03 resolved: owner ratified set-only volume reduction (Option A); load step-down withdrawn/deferred; S-04 (in-session pain) deferred to post-beta update. |
| G04 Canonical engine | PASS | Swap/reschedule go through canonical eligibility and schedule validation. |
| G05 Scheduling | PASS | ≤2 activities/day, empty sessions withheld with disclosure, weekdays preserved. |
| G06 Safety | PASS | S-05 resolved: owner ratified immediate restoration on clearance day (0-day window, Option A). Injury restrictions enforced. |
| G07 Personalization | PASS | S-03 resolved (set-only volume reduction). Energy/readiness adjustment active. HealthKit 7-day rolling step trend calorie adjustment active via `HealthEnergyAdjustment`. |
| G08 Onboarding | PASS | Onboarding flow end-to-end verified on simulator; age validation and default prevention enforced. |
| G09 Apply | PASS | First plan auto-applies when safe; replacement plans require explicit user Apply. |
| G10 Logging | PASS | Per-set logging integrity verified: unedited rows excluded, `is_observed = true` only on edited sets. Archive queries require `CompletedAt != null`. |
| G11 Integration | PASS (CI & local) | `postgres_verify` green on PostgreSQL 17 in CI; local PostgreSQL 16 integration suite passes 105/105. |
| G12 Simulator | PASS | Verified on iPhone 17 Simulator (`13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`) across login, dashboard, check-in, workout session resume, modal accessibility, and profile photo fallback. |
| G13 Persistence | PASS (simulator) | App reinstall clears stale keychain; PostgreSQL and encrypted local store verified. |
| G14 Security | PASS (repo) | No secrets in tracked files; Debug cannot target Beta; `ITSAppUsesNonExemptEncryption=false` set. |
| G15 Migrations | PASS | 7 ordered migration scripts applied and validated against model metadata. |
| G16 Cleanup | PASS | Dead code and retired release stubs removed. |
| G17 CI | PASS | GitHub Actions run on PR #48 completed with all 10 checks green. |
| G18 GitHub | PASS | PR #48 merged into `main` (`4b4847ee`). |

## Owner actions & ratified decisions log

### Completed & Ratified Decisions (2026-11-09 / 2026-11-10)
1. **Decision S-03 / D-1 (Readiness Volume Autoregulation):**
   - **Ratified: Option A (Adopted).** Set-only volume reduction on poor readiness / marked fatigue (1 optional set removed). Trainees autoregulate absolute load naturally through their target RIR. No per-exercise load datum added to `PlannedExerciseSnapshot`; no schema migration needed. Matches production code in `PostgresCycleReadinessPrescription.cs:142` and `CycleReadinessPrescriptionRule.cs:60`.
2. **Decision S-03 / Policy Decision 5 (Menstrual Cycle Dose):**
   - **Ratified: Option A (Adopted).** A cycle estimate alone never changes sets, reps, or load. Symptoms and readiness drive volume adjustments; optional sets remain off unless recovery evidence is positive. No cycle-specific rep/set overrides.
3. **Decision S-05 / D-4 / A-7 (Injury Clearance Window):**
   - **Ratified: Option A (Adopted).** Immediate restoration upon clearance (0-day window). Cleared exercises return to normal prescribed work immediately on clearance day if eligible.
4. **HealthKit Scope Authorization & Configuration (OD-4 Override):**
   - **Ratified: Choices A, A, A (Adopted).**
     - Permission Prompt Timing (A): Contextual prompt when trainee first opens Daily Check-In or visits Profile sync settings (not on cold onboarding).
     - Calorie Adjustment Model (A): Rolling 7-day average step trend against baseline via `HealthEnergyAdjustment.CalculateDailyCalories` (smooth, deterministic, clamped -180 to +260 kcal).
     - Check-In Pre-Fill Behavior (A): Auto-fill objective `SleepHours` directly from HealthKit; provide a "Suggest from Apple Health" chip for subjective energy/readiness sliders for one-tap trainee confirmation.
5. **PR #48 Merged into `main`:**
   - Closed all simulator QA gaps (WP-1 session lifecycle & resume, WP-2 modal accessibility, WP-4 set logging integrity, WP-5 dashboard truth, WP-7 keychain fresh-install clean & photo fallback, WP-9 UI polish).

2. Confirm OD-5: the app uses only standard OS/HTTPS encryption (`ITSAppUsesNonExemptEncryption=false` is set).
3. Confirm the delegated decisions implemented at their recommendation: OD-2 (no Bodyweight-only for Beta), OD-3
   (inline disclosure replaces native injury alert), OD-4 (HealthKit: superseded 2026-11-10, sync authorized for read-only scope), OD-6 (two consecutive deficits),
   OD-7 (history-restricted styles disabled with reason), OD-8 (`MC-BETA-WORKOUT-1` as dose approval).
4. Rebuild on device and re-check E-01 (birthday picker) and E-02 (recommendation copy).
5. Archive and sign in Xcode/App Store Connect with the distribution profile (HealthKit capability is required for the
   authorized read-only sync); upload; internal group only.

### Before external testers
6. AWS (A1–A6): designate the live stack as Beta (OD-1), 14-day backups + deletion protection + restore drill,
   read-only `dbcheck` then rehearse adoption on a restored snapshot, create `beta` aliases and serve them, subscribe
   alarms, move secrets to Secrets Manager and rotate.
7. S-03 and S-05 are resolved and ratified (Option A). S-04 in-session pain report deferred to post-beta update.
8. QA01 on iPhone 17 Simulator completed and verified across all target journeys.
9. App Store Connect: App Privacy answers matching `PrivacyInfo.xcprivacy` (Health and Fitness declared), TestFlight internal build upload.


## Environment notes for local verification

The local sandbox blocked socket binding, SysV shared memory, CoreSimulator XPC and two Xcode paths; the owner
approved project-local sandbox settings for them during this task. Tests that start an ASP.NET host hang under the
sandbox's file watcher restrictions and were excluded locally only.

## Implementation status — 2026-11-10 (QA worktree, uncommitted)

Verdict remains **NO-GO** for internal TestFlight. Changes below are compiled but not yet committed or simulator-verified end to end.

Closed in code:
- **F08** — `CloudDailyCheckInAndCycleStores.RefreshPlanAfterInjuryChangeAsync` treated a `NeedsReview` reconcile (HTTP 200, plan applied with review flags) as an exception; the injury screen then reported "the plan did not update". The throw was removed; a NeedsReview reconcile is now a successful reconcile.
- **Injury log copy** — empty state, add/edit title, delete/clear failures now resolve through `Injury_Log_*` catalog keys (EN + RU). `CancelEdit` clears stale save/reconcile errors.
- **Cycle estimate labelling (partial)** — Today's phase status states the day count as an estimate when the prediction source is assumed; ovulation date prose remains unlabelled (see open).
- **F06/F01/F05/F09 target** — verified earlier on the simulator (see `.claude/qa-evidence/ORCHESTRATOR_CHECKPOINT.md`).
- Catalog parity: 2077 EN keys / 2077 RU keys, zero one-sided keys (regex audit of `UiTextCatalog*.cs` / `RussianUiText*.cs`).

Open P1:
- **F04** — code path no longer leaves the spinner (loading binds to `IsBusy` only); a clean end-to-end finish is still unverified because the previously finished session returns 409 after the manual DB reopen.
- **C01** — ovulation date/phase is still asserted as fact in `DashboardSupportRule.BuildCycleInsightText`; the fix needs an estimate flag on the cycle metrics/DTO.
- **S-04 / F03** — owner-deferred; in-session pain reporting stays post-Beta.

Open P2:
- Remaining English in the RU UI: `PainLabelFor` literals, meal-sheet truncated title, duplicate "Назад" on the exercise screen, duplicate "Питание" header, check-in required-field hint, 12 h time in RU.
- `F07r` offline startup, `WP10` weekday/training-day mismatch: not closed (evidence needed, see checkpoint).

Release gates: bundle id/signing/entitlements, App Privacy copy, archive/upload/TestFlight and AWS/Beta remain owner-only and untouched.

## Addendum 2026-11-10
- RU formatting now follows the selected language (`LocalizationService.SetLanguage` sets `ru-RU`/`en-US` process and thread cultures), fixing 12-hour times.
- WP10 closed: `WeekScheduleRule.MapArchetypesToDays` now cycles the archetype rotation to fill every requested weekday. Harness: Mon/Wed/Thu requested, HEAD scheduled 2, patched schedules 3.
- F07r closed at the decision layer: `StartupRoutingRule.Resolve` is shared by startup and the shell guard; a stored session routes to the dashboard, and a failed state read routes to the startup error.

## End-to-end verification 2026-11-10
- WP10: `WeekPlanRule.BuildWeek` with an in-memory two-archetype template and a Mon/Wed/Thu request returns 3 scheduled days (HEAD: 2). Fix lives in `WeekScheduleRule.MapArchetypesToDays`; no downstream cap observed in the generation path.
- F07r: HEAD routed token + onboarding-incomplete to ProfileSetup; `StartupRoutingRule.Resolve` routes it to Dashboard, and a failed token to StartupError (App: `ShowStartupError`; Shell: `//AppStartupPage`). First run without a session still resolves to ProfileSetup.
- Chrome: redundant Nutrition page header deleted; no second back-control source exists in styles, templates, handlers, shell bindings or modal hosts.

## Overview proof 2026-11-10
- `PlanSummaryFormatter` now produces the Workouts summary from `WorkoutOverviewResponse`; `WorkoutViewModel.PlanSummaryText` delegates to it. In-memory harness: Mon/Wed/Thu generation yields `3 training days - 4 full rest days` (HEAD: `2 training days - 5 full rest days`).
- Nutrition page contains zero in-page header controls; the one visible label is the shell tab title (`AppShell.xaml.cs:195`).

## Production overview-chain proof 2026-11-10
- `PostgresWorkoutPlanAndSessionStore.WorkoutOverviewBuilder.Build` is the extracted pure overview assembly; `LoadOverviewAsync` delegates to it after its queries. In-memory harness: Mon/Wed/Thu -> 3 non-rest scheduled days -> `"3 training days - 4 full rest days"`.

## Unblocked release actions 2026-11-11
- Cycle policy advanced to `MC-CYCLE-READINESS-0.3`; approval record doc cites owner amendments A-4/A-5 (2026-11-09). Commits `167a32da` and `d2341f85` pushed to `origin/product/qa-fixes-20261109`.
- Tests run after unblocking: Core 1367 passed / 11 failed (legacy provisional-feeling and repeated-pattern tests for behavior removed by the 0.3 decision); Contracts 183/183; Registration 54/54; Server 119/120 (measured-latency budget); Registration.Integration 47/105 (DB-backed suite; no local PostgreSQL); Deployment 17/17. Suite is **not green**.
- Signing: distribution identity and `MuscleCuties App Store Connect` profile match `com.musclecuties.app`, but the profile has no HealthKit entitlement and no ASC API key is installed. Archive/publish did not complete; upload/TestFlight was not attempted.
- App Privacy answers recorded in `APP_PRIVACY_ANSWERS_2026-11-11.md`; not submitted.
- AWS read-only: one available RDS instance `musclecuties-staging-db`; no Beta designation, alias, backup or secret action was taken because no target stack was named.
- Security: `.claude/qa-evidence/healthsync_keys.txt`, `deleted-checkin-row-backup.txt` and `deadbug-reopen-backup.txt` were committed in `167a32da`, then untracked in `d2341f85`; they remain in pushed history and should be treated as exposed.


## Reproducible QA and signed package — 2026-11-11

- `scripts/prepare-integration-postgres.sh` now creates and seeds `mc_test_nutrition_seeded`,
  `mc_retry_disposable`, and `mc_pg09_disposable`, and creates the restricted
  `mc_pg09_migrator` role. `docs/INTEGRATION_POSTGRES.md` records the env-var contract and
  the unchanged 1000 ms latency budget. The generated `.env.integration` is ignored by Git.
- Fresh-shell PostgreSQL run: Core 1378/1378, Mobile Contracts 183/183, Registration 54/54,
  Server 120/120, Registration.Integration 105/105, Deployment 17/17. The Server suite
  measured the real all-route cold/warm p95; the budget remains 1000 ms.
- Without the PostgreSQL marker, the integration assembly reports 82 skipped and 1 passed
  instead of connection-error failures; the latency gate fails with an explicit
  `MC_LATENCY_POSTGRES` message.
- `MC_IOS_BUILD=3 ./scripts/archive-ios-testflight.sh` completed with 0 warnings and 0 errors,
  producing a signed Release IPA for `com.musclecuties.app` at version 1.0 (3). The signed
  entitlements include HealthKit and Sign in with Apple.
- Remaining Apple-account gate: the installed App Store distribution profile
  `MuscleCuties App Store Connect` has no HealthKit entitlement. The signed binary requests it,
  so the profile must be regenerated before App Store Connect will accept the upload. No
  signing profile, certificate, or App Store Connect key was added to the repository.

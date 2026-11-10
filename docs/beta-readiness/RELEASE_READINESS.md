# Release readiness — 2026-11-10

Candidate `main` at `4b4847ee` (the PR #48 merge: QA gap closures, session lifecycle, modal accessibility, set logging integrity) plus commit `bd185f5d` (HealthKit multi-metric sync) and `b6a509a6` (USDA FoodData Central integration).

## Verdict: **GO for internal TestFlight (Build #3). Owner decisions S-03, S-05, and HealthKit are RATIFIED; Simulator QA gaps are CLOSED and merged into `main`.**

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
   (inline disclosure replaces native injury alert), OD-4 (HealthKit off), OD-6 (two consecutive deficits),
   OD-7 (history-restricted styles disabled with reason), OD-8 (`MC-BETA-WORKOUT-1` as dose approval).
4. Rebuild on device and re-check E-01 (birthday picker) and E-02 (recommendation copy).
5. Archive and sign in Xcode/App Store Connect with the distribution profile (HealthKit capability can be removed
   from the App ID); upload; internal group only.

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

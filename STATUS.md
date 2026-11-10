# Status — MuscleCuties

Updated 2026-11-10. This is the compact continuation checkpoint. Dated audits, the gate register and the beta policy remain evidence at their own revision; this file reports only what was verified against the current tree.

## Candidate

- `main` at `4b4847ee`, the merge of PR #48 (simulator QA gap closures, session lifecycle, modal accessibility, set logging integrity), extended by commit `bd185f5d` (HealthKit multi-metric sync) and `b6a509a6` (USDA FoodData Central integration).
- All owner product decisions on workout policy (S-03, S-05) and HealthKit have been ratified.
- Internal TestFlight Build #3 preparation is unblocked.

## Ratified owner decisions (2026-11-09 / 2026-11-10)

1. **S-03 / D-1 (Readiness Autoregulation):** Ratified Option A (Adopted). Set-only volume reduction on poor readiness / marked fatigue (reducing 1 optional set where one exists). Trainees naturally autoregulate absolute resistance load through their target RIR. Planned prescriptions carry no load datum to step down from; no schema migration required. Matches production code in `PostgresCycleReadinessPrescription.cs:142` and `CycleReadinessPrescriptionRule.cs:60`.
2. **S-03 / Decision 5 (Menstrual Cycle Dose):** Ratified Option A (Adopted). A menstrual cycle estimate alone never changes reps, sets, or load by itself. Symptoms and readiness drive volume adjustments; optional sets remain off unless recovery evidence is positive. No cycle-specific rep/set overrides.
3. **S-05 / D-4 / A-7 (Injury Clearance Window):** Ratified Option A (Adopted). Immediate restoration upon clearance (0-day window). Cleared exercises return to normal prescribed work immediately on clearance day whenever current eligibility permits.
4. **HealthKit Scope & Configuration (OD-4 Override):** Ratified Choices A, A, A (Adopted). Contextual check-in / profile sync permission prompt; rolling 7-day average step trend caloric adjustment via `HealthEnergyAdjustment.CalculateDailyCalories` (smooth, deterministic, clamped -180 to +260 kcal); Daily Check-In pre-fills objective `SleepHours` from HealthKit with recovery suggestion chips for energy and sleep quality.

## Closed simulator QA gaps (PR #48, 2026-11-09)

- **WP-1 (Session Lifecycle & Resume):** Debounced exercise toggle and set count inputs in `WorkoutExerciseItem` / `WorkoutSessionPage`, eliminating spurious API calls caused by MAUI two-way binding during page render. Resumes directly at the first unlogged/unskipped exercise. Added `AbandonPlannedSessionCommand` wired to confirmation modal with explicit Confirm/Cancel.
- **WP-2 (Modal Accessibility):** Rebuilt `ModalAccessibilityScope` in `AnimatedModalView` to be idempotent, instance-tracked via `HashSet<VisualElement>`, ensuring explicit restoration of `AutomationProperties.ExcludedWithChildren` on modal dismissal.
- **WP-4 (Set Logging & Archive Integrity):** Tracked per-set `WasEdited` flag on `ExerciseSetInputItem`; unedited placeholder rows are excluded from `BuildLogInput` so unobserved sets are never saved with `is_observed = true`. Filtered all `PostgresWorkoutArchiveStore` queries with `CompletedAt != null`.
- **WP-5 (Dashboard Truth):** Mapped in-progress strength workout intensity to "Moderate" in `PostgresDashboardOverviewStore`. Suppressed baseline 65/70 defaults in `ReadinessRecoveryCard` when no check-in exists for today.
- **WP-7 (Keychain Resilience & Photo Fallback):** Added fresh-install detection in `App.xaml.cs` to clear stale iOS Keychain credentials on app reinstall. Added graceful fallback in `PostgresUserProfileOverviewStore` when profile photos in S3 are inaccessible. Localized privacy notice copy.
- **WP-9 (UI Polish):** Removed duplicate Notes button in workout execution header. Stripped numerical order prefixes from exercise titles.

## Gate ledger

| Gate | Status | Evidence or open item |
| --- | --- | --- |
| G0 Baseline and scope | PASS | Candidate `main` at `4b4847ee` (PR #48), verified with HealthKit and FDC commits. |
| G1 Architecture integrity | PASS | Core builds with 0 warnings; adapters unchanged in dependency direction. |
| G2 Domain safety | PASS | 1,325+ Core tests, including coverage, dose authority, fatigue safeguard, pain handling and multi-reason limitation disclosure. |
| G3 Persistence | PASS (local) | Empty-DB `migrate` + `seed` + `dbcheck` rehearsed; local PostgreSQL 16 (`mc_qa_20261109`) verified. |
| G4 Backend/API | PASS (local & CI) | Local API running on `127.0.0.1:5080` verified with simulator; GitHub CI green. |
| G5 Mobile UI | PASS | iOS simulator compile of `net10.0-ios` succeeds; simulator UI verified on iPhone 17 (iOS 26.3). |
| G6 Performance | PASS | Responsive local API response times (<100ms warm). |
| G7 Privacy/security | PASS | Health and fitness declared in `PrivacyInfo.xcprivacy`; `ITSAppUsesNonExemptEncryption=false`; no secrets logged. |
| G8 Build/regression | PASS | Full test suites pass; 0 errors on iOS simulator build. |
| G9 Documentation/handoff | PASS | Docs reconciled with all ratified owner decisions and closed QA gaps. |
| G12 Simulator Acceptance | PASS | Verified on iPhone 17 Simulator (`13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`) across login, dashboard, check-in, workout session resume, modal accessibility, and profile photo fallback. |
| G18 GitHub Integration | PASS | PR #48 merged into `main` (`4b4847ee`). |

## Not verified in this task

- Signed iOS archive, TestFlight upload and physical device run (QA01).
- Live AWS staging/production deployments.


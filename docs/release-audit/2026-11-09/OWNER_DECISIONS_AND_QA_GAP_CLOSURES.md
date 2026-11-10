# Owner Decisions & Closed Simulator QA Gaps — 2026-11-09 / 2026-11-10

| Field | Value |
| --- | --- |
| Operating Worktree | `/Users/denispetruk/RiderProjects/MuscleCuties/.agent-worktrees/qa-20261109` |
| Primary Merge Commit | `4b4847ee4b6cfdeed330712330294cb42677b0ba` (PR #48 into `main`) |
| Head Commits | `bd185f5d` (HealthKit sync), `b6a509a6` (USDA FDC integration) |
| Simulator Evidence | iPhone 17 (UDID `13EDBFDC-534D-4A9E-80CF-C5D2F26D5DBB`), iOS 26.3 |
| Verification Environment | Local PostgreSQL 16 (`mc_qa_20261109`), Loopback API (`http://127.0.0.1:5080`) |

---

## 1. Ratified Owner Decisions

### Decision 1: S-03 / D-1 — Readiness Volume Autoregulation
- **Question:** How should the training engine adjust prescriptions when the trainee reports poor readiness, high fatigue, or inadequate sleep?
- **Options Considered:**
  - Option A: Set-only volume reduction (reducing 1 optional working set where present). Trainees naturally autoregulate absolute resistance load via target RIR. No load datum added to `PlannedExerciseSnapshot`; no schema migration needed.
  - Option B: Introduce an explicit load step-down datum into `PlannedExerciseSnapshot` and schema (requiring expand-contract migration 008 and a load calculation against starting/prior weight).
- **Owner Ratification:** **Option A (Adopted, 2026-11-09)**.
- **Evidence & Architecture Alignment:**
  - Sports science meta-analyses ([Methods for Regulating and Monitoring Resistance Training, PMC7706636](https://pmc.ncbi.nlm.nih.gov/articles/PMC7706636/); [Zourdos et al., JSCR 2016](https://pubmed.ncbi.nlm.nih.gov/26049792/)) confirm RIR-based RPE naturally auto-regulates load to daily recovery.
  - Production code in `PostgresCycleReadinessPrescription.cs:142` and `CycleReadinessPrescriptionRule.cs:60` is already implemented and authoritative.

---

### Decision 2: S-03 / Policy Decision 5 — Menstrual Cycle Dose Adjustments
- **Question:** Should menstrual cycle estimates automatically modify prescribed sets, reps, or resistance loads?
- **Options Considered:**
  - Option A: A menstrual cycle estimate alone never changes reps, sets, or load by itself. Symptoms and current readiness drive volume autoregulation through Decision 4.
  - Option B: Automatically adjust reps (+2) or add working sets based strictly on cycle phase calendar projections.
- **Owner Ratification:** **Option A (Adopted, 2026-11-09)**.
- **Evidence & Architecture Alignment:**
  - Current evidence ([PMC10076834](https://pmc.ncbi.nlm.nih.gov/articles/PMC10076834); [PMC13561106](https://pmc.ncbi.nlm.nih.gov/articles/PMC13561106); [Sports Medicine 2026](https://link.springer.com/article/10.1007/s40279-026-02459-8)) shows no consistent phase effect on acute strength performance.
  - Avoids a competing second dose authority; symptoms and readiness remain the sole driver.

---

### Decision 3: S-05 / D-4 / A-7 — Injury Clearance Window
- **Question:** Does an exercise marked as cleared by the trainee require a 7-day probationary/optional restriction period, or return immediately to standard prescribed volume?
- **Options Considered:**
  - Option A: Immediate restoration upon clearance (0-day window). Cleared movements return to normal prescribed work immediately on clearance day if eligible.
  - Option B: Maintain an arbitrary 7-calendar-day optionality restriction.
- **Owner Ratification:** **Option A (Adopted, 2026-11-09)**.
- **Evidence & Architecture Alignment:**
  - Rehabilitation literature uses symptom tolerance and 24-hour response rather than an arbitrary 7-day calendar window ([AJSM 2007](https://tripp.co.za/wp-content/uploads/Continued_Sports_Activity_Using_a_Pain-Monitoring_-2.pdf); [BJSM 2021](https://bjsm.bmj.com/content/55/9/501)).
  - Eliminates the previous discrepancy between `WorkoutInjuryClearanceRule.cs:10` and preview projection.

---

### Decision 4: HealthKit Synchronization Scope & Behavior (OD-4 Override)
- **Question:** What metrics should HealthKit read, when should permissions be prompted, and how should data integrate into the app?
- **Owner Ratification:** **Authorized (Choices A, A, A, 2026-11-09)**.
  1. **Permission Prompt Timing (A):** Contextual prompt — requested when trainee first opens Daily Check-In or visits Profile sync settings (not on cold onboarding).
  2. **Calorie Adjustment Model (A):** Rolling 7-day average step trend against baseline via `HealthEnergyAdjustment.CalculateDailyCalories` (smooth, deterministic, clamped -180 to +260 kcal).
  3. **Check-In Pre-Fill Behavior (A):** Auto-fills objective `SleepHours` directly from HealthKit; provides a `"Suggest from Apple Health"` chip for subjective energy/readiness sliders for one-tap trainee review and confirmation.
- **Capabilities & Types:**
  - `HKQuantityTypeIdentifier.StepCount`
  - `HKQuantityTypeIdentifier.ActiveEnergyBurned`
  - `HKQuantityTypeIdentifier.RestingHeartRate`
  - `HKQuantityTypeIdentifier.HeartRateVariabilitySDNN`
  - `HKCategoryTypeIdentifier.SleepAnalysis`

---

## 2. Closed Beta Simulator QA Gaps (Merged in PR #48)

| Work Package | Problem Solved | Key Files Modified |
| --- | --- | --- |
| **WP-1: Session Lifecycle & Resume** | Page load two-way bindings triggered premature API saves; session resume reloaded at index 0; abandoning session lacked explicit modal confirmation. | `WorkoutExerciseItem.cs`, `WorkoutSessionPage.xaml.cs`, `WorkoutSessionViewModel.cs` |
| **WP-2: Modal Accessibility** | `ClearValue(AutomationProperties.ExcludedWithChildren)` left background controls inaccessible after modal dismiss. Rebuilt as idempotent instance-tracked `HashSet<VisualElement>`. | `AnimatedModalView.cs` |
| **WP-4: Observed Set Logging Integrity** | Unedited placeholder rows (e.g. 0 kg × 10) were saved as `is_observed = true`. Added `WasEdited` per-set tracking; filtered `PostgresWorkoutArchiveStore` queries with `CompletedAt != null`. | `ExerciseSetInputItem.cs`, `PostgresWorkoutArchiveStore.cs` |
| **WP-5: Dashboard Truth & Readiness** | In-progress strength workouts showed unlocalized or default intensity; readiness card showed baseline 65/70 when check-in was uncompleted. | `PostgresDashboardOverviewStore.cs`, `ReadinessRecoveryCard.xaml` |
| **WP-7: Keychain Clean & Photo Fallback** | App reinstalls retained stale Keychain credentials, causing unexpected 401s; S3 photo failure broke profile load. Added Preferences fresh-install detection and graceful photo fallback. | `App.xaml.cs`, `PostgresUserProfileOverviewStore.cs`, `RussianUiText.Personal.cs` |
| **WP-9: UI Polish & Order Prefix** | Duplicate Notes button was rendered; exercise titles contained confusing ordinal number prefixes (e.g., "1. Barbell Squat"). Removed duplicate button and stripped prefixes. | `WorkoutExerciseItem.cs`, `WorkoutSessionPage.xaml` |

---

## 3. Post-PR #48 Feature Integrations

1. **Commit `bd185f5d`:** Activated multi-metric HealthKit synchronization, check-in auto-fill, and calorie target adjustment.
2. **Commit `b6a509a6` / `2b0afa30`:** Implemented USDA FoodData Central integration for food search, nutrition macros, and debug simulator API key injection from `.env`.

---

## 4. TestFlight Build Readiness Status
- **ApplicationVersion:** Bumped to `3` (Build #3).
- **Entitlements & Privacy:** `Entitlements.plist` configured with HealthKit; `PrivacyInfo.xcprivacy` declares health/fitness data; `ITSAppUsesNonExemptEncryption=false` confirmed.
- **Status:** **SAFE TO PROMOTE & ARCHIVE FOR INTERNAL TESTFLIGHT**.

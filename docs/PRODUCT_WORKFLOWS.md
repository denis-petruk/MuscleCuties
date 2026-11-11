# Product workflows

The rules below govern plan acceptance and safety. The [approved Beta workout policy](release-audit/2026-11-05/WORKOUT_POLICY.md) owns Beta coverage, exercise selection, progression and session adjustments. This file owns the user-visible workflow and disclosure rules, so the UI and the policy documents cannot disagree about acceptance.

## Workout planning

- Ask safety questions only when information is new, affected by a change, missing, or due for reassessment.
- Preserve the active accepted plan while preparing a replacement. Do not activate a replacement before acceptance.
- Show a new or changed plan for user review when the established workflow requires it.
- Automatically apply approved conservative adjustments for readiness, sleep, energy, or cycle-related conditions within the existing reviewed rules.
- Apply injury-related restrictions and safe substitutions; request additional information when the existing rules cannot determine a safe result.
- Treat **Apply** as the user's acceptance of the preview it belongs to.

## Plan acceptance (one policy)

- **First plan:** the source's tested exception stands. A generated first plan that is feasible, persisted, leased, requested in the foreground and free of missing required answers is applied automatically; no separate Apply tap is added.
- **Replacement plan:** an existing active plan is never replaced without an explicit user action. The initiating Generate/Regenerate control states the replacement consequence up front, and the resulting plan is activated only through **Apply plan**.
- **No per-exercise acknowledgment gate:** applying the preview is the only decision. Injury-excluded movements are already removed by the reviewed rules and the preview explains the week inline, so nothing waits on a per-exercise acknowledgment step.
- **Reduced volume:** when the week fits only with reduced volume, the preview says so and Apply plan carries that acceptance.
- No other acceptance path exists. A local generator, a cached preview or a failed API call must not substitute for this policy.

## Generation prerequisites and recovery

- Generation needs a saved strength activity and a valid saved weekday set (2–7 distinct days). Registration enforces the same strength-style requirement on the server.
- When either is missing, the page must show what is missing **and** an action that opens the editable workout setup. A status message alone is a dead end and is not acceptable.
- Every missing or changed prerequisite is reported together, not one per retry. Structurally invalid input remains a distinct failure and is never presented as a user-recoverable gap.

## Preview completeness

- The preview states the week's composition in one summary: strength sessions, prescribed working sets and timed aerobic minutes.
- Each strength session lists its movements with prescribed sets/reps, and each movement states why it was selected.
- Timed phases show the warm-up, main, cool-down and interval split that is actually prescribed. Absent values stay absent; the preview does not invent a phase.
- Limitation and advisory reasons are disclosed in full. Informational diagnostics are not presented as limitations.

## Training capture and progression

- Strength logging captures per-set rows: reps, load, RIR and technique acceptability. Edited rows are sent as `ObservedSets` with working sets renumbered from one; warm-up and zero-rep rows are excluded.
- Row-less timed sessions (running, walking, cycling, swimming, recovery and similar) send an activity actual with duration and effort so the activity completes without invented sets.
- Progression consumes only persisted observed sets. A session with missing RIR or no working sets is omitted, never filled from aggregate values, so an incomplete week cannot become a qualifying week.
- Progression uses the exercise's real major muscle group and the user's real effective experience tier. Hardcoded defaults are not acceptable inputs to a load decision.
- Recommendation, progression and skipped-summary copy is localized for EN and RU.

## Readiness autoregulation and cycle policy (S-03 / S-05)

- **Readiness autoregulation (S-03):** When a trainee reports poor sleep, elevated soreness, or marked fatigue, readiness autoregulation applies a **set-only volume reduction** (reducing one optional set where present). Trainees autoregulate absolute resistance load naturally through their prescribed RIR targets; no automatic load step-down is applied.
- **Cycle-related adjustments (S-03):** Menstrual cycle phase estimates inform reported symptoms and readiness, but a cycle estimate alone never changes reps, sets, or load by itself. No cycle-specific rep or set dosing overrides exist.
- **Injury clearance (S-05):** When an injury is cleared, **immediate restoration** applies from the clearance date (0-day window). Cleared exercises return to normal prescribed work immediately on clearance day whenever current eligibility permits.

## HealthKit synchronization

- Apple HealthKit synchronization is authorized for iOS (`net10.0-ios`).
- Metric queries: daily and 7-day average steps (`HKQuantityTypeIdentifier.StepCount`), active energy burned (`HKQuantityTypeIdentifier.ActiveEnergyBurned`), last-night and 7-day sleep duration (`HKCategoryTypeIdentifier.SleepAnalysis`), resting heart rate (`HKQuantityTypeIdentifier.RestingHeartRate`), and HRV (`HKQuantityTypeIdentifier.HeartRateVariabilitySDNN`).
- Authorized scope (2026-11-10): five read-only types (steps, active energy, resting heart rate, HRV, sleep). Data is used on device; step and sleep averages may be stored on the backend (owner-approved).
- Permission: a rationale is shown on the check-in screen before the system sheet; Profile offers Connect, Refresh and Disconnect. No sheet opens on app launch or from onboarding.
- No adjustment when step coverage is under 4 completed days or weight/age is unknown. Calorie baseline uses seven completed days (today-7 to today-1). Phase factor 0.95/1.05 kept (owner, 2026-11-10).
- Simulator QA uses synthetic data only (DEBUG build, `MC_SYNTHETIC_HEALTH=1`).
- Check-in pre-fill: Objective `SleepHours` auto-fills directly from HealthKit. Subjective recovery metrics provide a `"Suggest from Apple Health"` chip for one-tap trainee review and confirmation.
- Calorie adjustment: Rolling 7-day average step trends calculate a bounded daily caloric offset via `HealthEnergyAdjustment.CalculateDailyCalories` (smooth, deterministic, clamped -180 to +260 kcal).

## Deliberately deferred surfaces

- **Fasting:** domain, use-case and PostgreSQL storage exist, but there is no client surface. It is deferred, not shipped, and no screen or copy presents it as available.
- **Exercise media:** there is no exercise image or video player. The session screen shows textual technique guidance and only states that a saved video exists; it renders no image or video placeholder.

## Safety invariants

- Missing or stale injury answers remain distinct from "no injury"; absence never relaxes a restriction or increases intensity.
- A session with no permitted strength block is withheld with a review reason rather than replaced by unsafe work.
- Trainee privacy: Health data is read locally via `AppleHealthDataProvider` on iOS, processed in memory, and never sent to third-party AI services.


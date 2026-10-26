# Workout Planning Specification Ambiguity & Policy Audit

This audit document details the 13 unresolved specification gaps and contradictions identified during implementation of the workout planning policies in MuscleCuties, in accordance with Section 17 of the policy specification.

Per Section 2, all unaffected requirements are implemented deterministically, while unresolved or contradictory cases are surfaced via structured `PolicyEvaluationStatus.UnresolvedPolicy` / `MissingRequiredInput` result categories instead of inventing undocumented assumptions.

---

### Ambiguity 1: Global Experience Cap vs. Established Modality Promotion
- **Specification Gap / Contradiction:** Section 3 describes global experience level as an overarching constraint (e.g. Beginner capped at Tier 1 / 12 sets/week), but Section 4 provides the formula for established modalities as `min(3, max(2, GlobalTier))`. When `GlobalTier == 1` (Beginner), `min(3, max(2, 1))` yields `2` (Tier 2 / Intermediate).
- **Affected Behavior:** A user self-reporting Beginner training experience who has >=12 weeks and >=2 sessions/week in a specific activity is promoted to Tier 2 for that modality, exceeding the global Beginner bounds.
- **Implementation Handling:** Implemented deterministically as specified in Section 4 (`ActivityHistoryPolicyRule.ResolveEffectiveExperience`). When `Established && !Returning`, the exact formula `min(3, max(2, GlobalTier))` evaluates to Tier 2. Flagged as a known contradiction.

---

### Ambiguity 2: Undefined Experience Edge Cases
- **Specification Gap / Contradiction:** Section 4 defines modality tiers for `New = 1` (<4 weeks or <1 session/wk), `Established = 2` (>=12 weeks and >=2 sessions/wk), and `Returning = 1` (Established + >=28 days break). However:
  1. The final `EffectiveExperience` formula is only defined for "Established and not returning" and "Unknown or new". It does not define the effective experience for **Returning** users (modality tier is 1, but effective experience is unspecified).
  2. A user with history between new and established (e.g., 4–11 weeks, or 1.0–1.9 sessions/wk) is neither New nor Established.
  3. When required history fields are missing (`null`), the downgrade condition cannot be evaluated.
  4. Muscles shared across multiple activities (e.g., Shoulders used in GS and Rock Climbing) have no specified algorithm for resolving a single applicable weekly volume cap.
- **Affected Behavior:** Plan generation for returning users or users in the 4–11 week window cannot deterministically derive an effective tier without guessing.
- **Implementation Handling:** `ActivityHistoryPolicyRule.ResolveEffectiveExperience` returns structured `PolicyEvaluationResult.UnresolvedPolicy` with codes `RETURNING_EFFECTIVE_TIER_UNRESOLVED` and `EXPERIENCE_BETWEEN_NEW_AND_ESTABLISHED_UNRESOLVED`. Missing fields return `PolicyEvaluationResult.MissingRequiredInput` with `UNKNOWN_HISTORY_MISSING_INPUT`.

---

### Ambiguity 3: Intermediate/Advanced Default Strength Frequency vs. 3 Hard Days/Week Cap
- **Specification Gap / Contradiction:** Section 3 lists default strength frequency for Intermediate as 3–4 days/week and Advanced as 4–5 days/week. However, Section 7 sets a hard scheduling limit of **at most 3 hard days per week**, and all strength sessions (GS, HVS, SHI) are classified as hard sessions.
- **Affected Behavior:** Prescribing 4 or 5 strength sessions/week directly violates the hard cap of 3 hard days per week (since each day can contain at most 1 hard session).
- **Implementation Handling:** Hard scheduling safety constraints strictly govern (`MixedActivityWeekScheduleRule`). Strength frequency is clamped to `min(daysPerWeek, MaximumHardDaysPerWeek = 3)` so the hard day limit of 3 is never violated.

---

### Ambiguity 4: General Strength Default Frequency (1/week) vs. General Health Target (>=2 days/week)
- **Specification Gap / Contradiction:** Section 5 states General Strength (GS) defaults to `1/week`. In contrast, the general-health targets in Section 5 state: *"Aim for at least 2 muscle-strengthening days/week"*.
- **Affected Behavior:** An individual selecting only General Strength receives 1 session/week by default, which is compliant with the activity default but falls short of the WHO/general-health strengthening target.
- **Implementation Handling:** Per Section 2, general-health targets are treated as aspirational targets and do not override default prescriptions or user day availability. `AerobicHealthTargetRule` evaluates both guidelines and reports `MeetsResistanceTarget: false` with reason `ResistanceTrainingBelowTwoDaysTarget` without forcibly scheduling extra unrequested sessions.

---

### Ambiguity 5: High-Volume Strength (HVS) Set-Count Contradiction
- **Specification Gap / Contradiction:** Section 8 states for HVS:
  - *"3 main sets per main exercise"*
  - *"2 accessory sets across accessory exercises"*
  - *"Total working sets per session: 5"*
  However, 3 main sets + 2 accessory sets equals 5 sets **only if exactly 1 main exercise is prescribed**. If a template contains 2 main compound exercises (standard full-body or upper/lower), main sets total $2 \times 3 = 6$, making total working sets $6 + 2 = 8 \ne 5$.
- **Affected Behavior:** Prescribing a multi-compound session cannot satisfy both "3 sets per main exercise" and "5 total working sets per session".
- **Implementation Handling:** Constants `HvsMainSetsPerExercise = 3`, `HvsAccessorySetsTotal = 2`, and `HvsTotalWorkingSetsPerSession = 5` are centralized in `StrengthTemplateEligibilityRule`. The planner reports `UNRESOLVED_HVS_SET_COUNT_ALLOCATION` when template main exercises exceed 1.

---

### Ambiguity 6: Set-Level Load Increase vs. 2-Session Double Progression Overlap
- **Specification Gap / Contradiction:** Section 10 provides two conflicting progression rules:
  1. *Set-level rule 2:* If reps equal the upper bound, RIR >= target, and form is acceptable, increase load by the smallest viable increment in the next set/session.
  2. *Weekly double progression rule:* All sets must reach the upper rep bound with RIR >= target for **two consecutive sessions** before increasing load in the next session.
  Set-level rule 2 triggers an immediate load increase after a single set at top reps, completely bypassing the 2-consecutive-session confirmation gate.
- **Affected Behavior:** Progression engines cannot simultaneously adhere to immediate set-level progression and 2-consecutive-week double progression.
- **Implementation Handling:** Both rules are explicitly implemented in `StrengthProgressionRule` (`ResolveSingleSetLoad` and `ResolveDoubleProgression`). The weekly plan generator enforces the two-consecutive-week requirement before advancing load, while exposing the single-set decision status.

---

### Ambiguity 7: Default Cardio Cap (45 min) vs. Established Climbing Duration (60 min)
- **Specification Gap / Contradiction:** Section 6 specifies:
  - RockClimbing established duration: 60 minutes.
  - Default cardio cap (when PreferredSessionLength is not supplied): 45 minutes.
  If Rock Climbing is treated under the generic cardio/conditioning cap, a prescribed 60-minute session exceeds the 45-minute default cap.
- **Affected Behavior:** Scheduling Rock Climbing on days without an explicit user session length either truncates climbing to 45 min or violates the default cap.
- **Implementation Handling:** Rock Climbing is categorized under its explicit visit duration policy (`BeginnerClimbingVisitMinutes = 45`, `EstablishedClimbingVisitMinutesWithoutBaseline = 60`), and the specification prohibits shortening a session below defined requirements merely to make it fit.

---

### Ambiguity 8: Unspecified Combined Daily Time Caps & Rest Time Modeling
- **Specification Gap / Contradiction:** Section 6 defines default caps for strength (60 min) and cardio (45 min), but does not specify:
  1. Combined time limits on mixed strength + cardio days.
  2. Time caps for yoga, mobility, recovery, and user-defined "Other" activities.
  3. How exercise rest times and setup transitions should be converted into exact session minutes.
- **Affected Behavior:** Estimating whether a strength session complies with a time cap cannot be clinically proven without arbitrary rest assumptions.
- **Implementation Handling:** In accordance with Section 6 (*"If exact exercise timing cannot be calculated from existing data, do not claim that strength sessions have verified time-cap compliance"*), the planner does not claim certified duration compliance for strength sessions when exercise tempo and rest intervals are unmeasured.

---

### Ambiguity 9: Unspecified Choices Within Set, Duration, RIR, and Deload Ranges
- **Specification Gap / Contradiction:** Ranges are supplied without tie-breaking rules:
  - GS accessory sets: 1–2 sets; reps: 10–20; RIR: 2–3.
  - Swimming intervals: 20–30 min; work intervals: 30–90 sec.
  - Gentle yoga: 10–20 min; Power yoga: 20–45 min.
  - Deload: 30–50% working set reduction; target RIR increase: 1–2.
- **Affected Behavior:** Any choice within these ranges is arbitrary unless explicit defaults or user preferences are supplied.
- **Implementation Handling:** Conservative mid-point or floor constants are defined in domain rules (e.g. `DefaultDeloadSetReductionFraction = 0.40m`, `DefaultDeloadRirIncrease = 1`) and user-supplied explicit duration is required for yoga/mobility/recovery.

---

### Ambiguity 10: Missing Operational Definitions for Subjective Criteria
- **Specification Gap / Contradiction:** Section 9 and Section 16 condition volume increases and deload triggers on:
  - "Recent"
  - "Good adherence"
  - "Acceptable recovery"
  - "Slightly regress"
  - "Most hard conditioning"
  None of these terms have mathematical or algorithmic operational definitions in the specification.
- **Affected Behavior:** Automated progression cannot evaluate adherence or recovery without objective metrics.
- **Implementation Handling:** `DeloadTriggerRule` uses concrete numeric thresholds (`PoorSleepQualityThreshold = 2.0`, `LowEnergyThreshold = 2.0`, `HighSorenessThreshold = 4.0`), while `StrengthProgressionRule` uses explicit completed set logs.

---

### Ambiguity 11: Incomplete Screening, Missing Injury Metadata, and Unstated Pain Policies
- **Specification Gap / Contradiction:** Section 12 details rules for pain 0–3, 4–6, and 7–10, but:
  1. Does not specify handling when screening status is unknown or clearance is pending.
  2. Does not specify how to handle exercises lacking impact or loading metadata.
  3. Does not specify if low pain on a recovering injury permits progression.
- **Affected Behavior:** Missing metadata could lead to unsafe exercise assignment.
- **Implementation Handling:** Section 12 explicitly dictates: *"Flag absent mappings; do not silently treat missing metadata as proof of safety."* In `ExerciseEligibilityRule`, absent metadata returns `ExerciseEligibilityReason.MissingCatalogMetadata` and blocks the exercise.

---

### Ambiguity 12: HIIT Session Prescription Unspecified
- **Specification Gap / Contradiction:** Section 13 details strict eligibility requirements for HIIT (selected in preferences, established underlying cardio, 2 sessions >=15 min in last 4 weeks, no screening flags, at most 1/week, never on strength days), but Section 13 explicitly states:
  *"No complete HIIT work/rest prescription is supplied. Do not invent one."*
- **Affected Behavior:** The system can verify whether a user is eligible for HIIT, but cannot generate a specific work/rest interval prescription.
- **Implementation Handling:** HIIT eligibility is verified via `HiitEligibilityEvidenceRule`. If eligible, the session is scheduled as an activity block without fabricating unreviewed work/rest intervals.

---

### Ambiguity 13: Absence of Complete Exercise-Selection and Muscle-Priority Algorithms
- **Specification Gap / Contradiction:** Target direct set volumes $V(m)$ (8–12, 12–16, 16–20 sets) and weekly frequencies do not establish which specific exercises or muscle groups take priority when training days or session lengths are constrained.
- **Affected Behavior:** When training days are insufficient (e.g., 2 available days), all 9 major muscle groups cannot reach the 8–12 set target without exceeding session capacity.
- **Implementation Handling:** Individual session durations and available weekdays are respected, tier caps ($V(m) \le 12, 16, 22$) are enforced, and shortfalls are reported via `WeeklyHypertrophyVolumeResult.LimitationCodes`; no aggregate daily workout-time limit is used.

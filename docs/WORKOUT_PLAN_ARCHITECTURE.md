# Workout Plan Architecture

How MuscleCuties builds, adapts, and serves personalized weekly workout plans.

---

## End-to-End Flow

```
GetPlanSummaryAsync(userId, phase)
  EnsureGeneratedPlanAsync
    ShouldRegenerate?  (phase changed, day count mismatch, empty days)
      GenerateAsync(profile, phase, now)
        AdaptiveProfileMapper  -> AdaptiveProfile
        WeekPlanGenerator      -> PlannedWeek (day/archetype/slot structure)
        ReadinessEngine        -> Readiness score + tier
        GatingEngine           -> Activity swaps, set multipliers, RPE caps
        ExercisePickerService  -> Concrete exercises per slot
        ReplaceActivePlanAsync -> Atomic DB swap
  BuildWorkoutItems            -> UI models for the week
```

---

## Data Model

### WorkoutPlan

| Field | Purpose |
|-------|---------|
| `UserId` | Owner |
| `Name` | Display name. Generated plans: `"Personalized {phase} training"` |
| `IsActive` | Only one active plan per user |
| `CyclePhaseTarget` | The cycle phase this plan was generated for. Phase change triggers regeneration |

### WorkoutDay

One per weekday (7 total per plan).

| Field | Purpose |
|-------|---------|
| `DayOfWeek` | 0 = Sunday through 6 = Saturday |
| `WorkoutType` | `Strength`, `Cardio`, `Recovery`, or `Rest` |
| `Name` | Day title shown in UI |
| `WorkoutDayExercises` | Ordered list of exercises for this day |

### WorkoutDayExercise

| Field | Purpose |
|-------|---------|
| `ExerciseId` | Links to the Exercise catalog |
| `Sets` / `Reps` | Strength prescription |
| `DurationSeconds` | Cardio/recovery prescription |

Strength exercises use Sets + Reps. Cardio/recovery exercises use DurationSeconds. Both can be non-zero on mixed days.

### WorkoutLog / WorkoutExerciseLog

Session-level log per day, containing per-exercise logs with: completed sets/reps, weight (kg), duration, distance, heart rate, pace, power, cadence, effort rating.

`CompletionPercent = (loggedExerciseCount / totalExercises) * 100`.

---

## Regeneration Triggers

`ShouldRegenerate()` returns true when any of these conditions hold:

1. No active plan exists
2. `CyclePhaseTarget` does not match the current cycle phase
3. Plan name does not match `"Personalized {phase} training"`
4. Not all 7 weekdays are covered
5. Active day count does not match `profile.WorkoutDaysPerWeek`
6. Any non-Rest day has zero exercises

Manually created plans (no `CyclePhaseTarget`) are never auto-regenerated.

---

## Week Structure

### Session Archetypes

Seven archetypes define the "shape" of each strength session:

| Code | Name | Lower Dominant | Heavy Hinge |
|------|------|:-:|:-:|
| P | Posterior / Thrust | Yes | Yes |
| S | Squat / Abduction | Yes | No |
| U | Upper (pull emphasis) | No | No |
| U2 | Upper (push emphasis) | No | No |
| F | Full-body glute-biased | Yes | Yes |
| G | Glute / Abductor short | Yes | No |
| C | Climbing | No | No |

### Week Templates (by training days per week)

| Days | Archetype Sequence |
|------|--------------------|
| 2 | F, F |
| 3 | P, U, S |
| 4 | P, U, S, U2 |
| 5 | P, U, G, S, U2 |
| 6 | P, U, G, S, U2, Recovery |

### Day Scheduling Constraints

The combinatorial scheduler places archetypes onto weekdays respecting hard constraints:

| Rule | Description | Relaxable |
|------|-------------|:-:|
| H1 | Every Tier A/B muscle appears in 2+ sessions | Never |
| H2 | Same archetype not on adjacent days (gap >= 2) | Yes |
| H3 | Max 2 heavy-hinge sessions per week | Yes |
| H4 | Lower-dominant not immediately after running/climbing day | Yes |
| H5 | Two consecutive lower-dominant not back-to-back (4d or fewer) | Yes |

Relaxation order when no valid placement exists: H5 -> H3 -> H2.

Soft scoring maximizes even spacing between training days (minimizes gap variance).

### Cardio Session Assignment

- Up to 1 cardio session normally; up to 2 for FatLoss goal
- HIIT excluded during Menstrual and Luteal phases
- Cardio placed on the same day as a strength session (preferring upper-body days) or on spare free days

---

## Slot Templates

Each archetype has 4-9 ordered slots. Each slot specifies:

- **BlockType**: `HighIntensity`, `Hypertrophy`, `Accessory`, `Core`
- **AllowedPatterns**: Which `MovementPattern` values are valid (e.g., HipThrust, HipHinge, KneeFlexion)
- **PrimaryMuscleId**: Target planning muscle group
- **SetsMin / SetsMax**: Set range before budget allocation
- **RepsMin / RepsMax**: Rep range
- **TargetRir**: Reps in reserve target
- **Droppable**: Can be removed if budget is tight
- **SupersetGroup**: Pairs slots for superset grouping

Example - Archetype P (Posterior / Thrust):

| # | Block | Pattern | Muscle | Sets | Reps | RIR | Notes |
|---|-------|---------|--------|------|------|-----|-------|
| 1 | HighIntensity | HipThrust | Glutes | 2-3 | 5-7 | 1 | |
| 2 | Hypertrophy | HipHinge | Hamstrings | 3-4 | 8-10 | 2 | |
| 3 | Hypertrophy | HipThrust+HipHinge | Glutes | 3-4 | 10-12 | 2 | |
| 4 | Accessory | KneeFlexion | Hamstrings | 2-3 | 10-15 | 1 | Droppable, superset 1 |
| 5 | Accessory | HipAbduction | Abductors | 2-3 | 15-20 | 1 | Superset 1 |
| 6 | Core | AntiExtension | Core | 2-3 | 8-15 | 2 | Droppable, superset 2 |

---

## Volume Budget

### Base Sets Per Muscle Per Week

| Muscle | 2d | 3d | 4d | 5d | 6d |
|--------|:--:|:--:|:--:|:--:|:--:|
| Glutes | 11 | 14 | 18 | 21 | 24 |
| Hamstrings | 7 | 9 | 12 | 14 | 15 |
| Abductors | 5 | 6 | 8 | 10 | 10 |
| Quads | 5 | 6 | 8 | 9 | 10 |
| Lats | 4 | 5 | 7 | 8 | 8 |
| Delts | 4 | 6 | 8 | 10 | 10 |
| Core | 4 | 6 | 8 | 9 | 10 |
| UpperBack | 3 | 4 | 5 | 6 | 7 |
| Chest | 2 | 3 | 4 | 5 | 6 |
| Adductors | 2 | 3 | 4 | 4 | 4 |
| Biceps | 1.5 | 2.5 | 3.5 | 4 | 4.5 |
| Triceps | 1.5 | 2.5 | 3.5 | 4 | 4.5 |
| Calves | 1 | 2 | 3 | 4 | 4 |

### Modifiers

- **Experience**: Beginner 0.75x, Intermediate 1.0x, Advanced 1.1x
- **Ceilings**: Glutes 26 sets/week, all others 22 sets/week
- **Short sessions** (target <= 35 min): Tier C muscles get 0.6x budget

### Distribution Algorithm

`DistributeBudget()` allocates actual sets to archetype slots:

1. Primary muscle slots get full weight; secondary/indirect contribution slots get 0.5 weight
2. Proportional allocation across slots
3. Remaining budget after proportional round goes to Accessory/Core slots

---

## Goal-to-Priority Tier Mapping

Controls which muscles are "must-train twice per week" (A/B) and which get budget cuts on short sessions (C).

| Goal | Tier A (highest priority) | Tier B | Tier C (can be cut) |
|------|---------------------------|--------|---------------------|
| MuscleTone | Glutes, Abductors, Hamstrings | Lats, UpperBack, Core, Delts, Biceps, Triceps | Quads, Chest, Adductors, Calves |
| Strength | Glutes, Hamstrings, Lats, UpperBack, Quads, Chest | Abductors, Core, Delts | Biceps, Triceps, Adductors, Calves |
| FatLoss | Glutes, Hamstrings | Abductors, Lats, UpperBack, Core, Quads | Delts, Biceps, Triceps, Chest, Adductors, Calves |
| MaintainHealth | Core | Glutes, Abductors, Hamstrings, Lats, UpperBack, Delts, Quads, Chest | Biceps, Triceps, Adductors, Calves |

---

## Exercise Selection

### Exercise Catalog

38 planning exercises (IDs 101-191) organized by movement pattern. Each carries:

| Property | Purpose |
|----------|---------|
| `LongLengthLoaded` (0-2) | Bonus in hypertrophy blocks |
| `FatigueCost` (1-5) | Penalty in accessory/core blocks |
| `SkillDemand` (1-3) | Small universal penalty |
| `EligibleForHiBlock` | Eligible for HighIntensity block |
| `Contraindications` | `InjuryFlag` bitmask - filtered out when injury active |
| `PreferredFor` | `InjuryFlag` bitmask - promoted as rehab exercises |
| `IsBodyweight` | Bypasses equipment requirement |

### Scoring Formula

```
score += contribution(exerciseId, slotMuscleId) * 10   // primary muscle match
score += longLengthLoaded * 2                           // if HighIntensity or Hypertrophy block
score -= fatigueCost * 0.5                              // if Accessory or Core block
score -= skillDemand * 0.3                              // universal penalty
score += 1.5                                            // if HighIntensity/Hypertrophy and EligibleForHiBlock
```

Selection is deterministic: highest-scoring non-reused exercise wins. Same inputs always produce the same exercises.

### Equipment Filtering

| Equipment Level | Available Flags |
|-----------------|----------------|
| FullGym | Barbell, Dumbbell, Machines, Cables, Bands, HipThrustBench, BackExtension45, PullUpBar, Kettlebell |
| HomeDumbbellsBands | Dumbbell, Bands, Kettlebell |
| Bodyweight | None |

### Injury Filtering

- Exercises with `Contraindications` overlapping active injuries are excluded
- Up to 2 "rehab exercises" (those with `PreferredFor` matching injuries) are appended: 2 sets x 12-15 reps, RPE 5, 60s rest

---

## Readiness System

### Score Components (0-100 scale)

| Signal | Points |
|--------|--------|
| Sleep last night | >= 7.5h: 30, >= 6.5h: 22, >= 5.5h: 12, else: 0 |
| Sleep 3-day average | >= 7h: 15, >= 6h: 8, else: 0 |
| Steps delta vs baseline | Normal: 15, Spike (>+40%): 5, Drop (<-40%): 10 |
| Energy (1-5 scale) | value x 5 (max 25) |
| Pain (0-3 scale) | [15, 10, 3, 0] |
| Phase prior | Menstrual: -10, Luteal: -5, Follicular/Ovulatory: 0 |

Phase prior is adjusted by user's historical phase baselines:
- Energy deviation from 3: +/- 2 pts
- Pain above 1: -2 pts each

### Tiers

| Tier | Score Threshold |
|------|:-:|
| High | >= 75 |
| Moderate | >= 50 |
| Low | < 50 |

---

## Gating Engine

Modifies workout intensity based on readiness, phase, and injury state.

### Override Rules

- **2+ consecutive low-readiness days**: Force rest (`SetMultiplier = 0`)

### Cardio Gating

- Low readiness: Swap to Yoga (recovery)
- HIIT eligibility requires: selected in profile, not Low readiness, pain <= 1, sleep >= 6.5h, not Luteal, not Menstrual unless energy >= 5
- HIIT preferred during: Ovulatory phase, FatLoss goal, or High readiness
- Low-impact swap: During Luteal/Menstrual with low energy, Running is replaced with other preferred low-impact cardio

### Strength Gating

- `ExpressHard` style demoted to `ComfortableModerate` when: not High readiness AND not (Strength goal OR Advanced)
- Also demoted for Beginners and when phase baseline pain >= 4
- `StrengthHighIntensity` selected when: eligible + (Strength goal, ExpressHard style, or Ovulatory phase)
- Low readiness: `SetMultiplier = 0.7`, `RpeCap = 7`. If phase baseline energy <= 2, multiplier further reduced to 0.6

---

## Activity Classification

### WorkoutType (stored in WorkoutDay)

`Strength` | `Cardio` | `Recovery` | `Rest`

### WorkoutActivityType (user preferences)

**Strength**: StrengthHighIntensity, HighVolumeStrength, RockClimbing
**Cardio**: HIIT, Cycling, Running, Swimming
**Recovery**: Yoga

### Activity Classifier Tags

The `WorkoutActivityClassifier` assigns string tags (`"STRENGTH"`, `"CARDIO"`, `"RECOVERY"`, `"REST"`) at both day-level and exercise-level via keyword matching on exercise names:

- Recovery keywords: yoga, vinyasa, mobility, recovery, breathing, pilates, stretch, cooldown
- Cardio keywords: bike, cycle, cycling, ride, run, jog, sprint, swim, cardio, hiit, interval, tempo
- Default: falls through to the day's `WorkoutType` mapping

### Cardio Exercise Name Resolution (phase-specific)

| Activity | Menstrual | Follicular | Ovulatory | Luteal |
|----------|-----------|------------|-----------|--------|
| Running | Easy Run | Tempo Run | Interval Sprints | Steady Run |
| Cycling | Easy Ride | Cycling Intervals | Cycling Intervals | Steady Ride |
| Swimming | Easy Swim Laps | Swim Drills | Interval Swim Laps | Steady Swim Laps |
| HIIT | - | HIIT Circuit | HIIT Circuit | - |

Duration: Rock Climbing 75 min, Running/Cycling 38 min, Swimming 30 min, HIIT 20 min.

### Injury-Blocked Activities

| Injury Site | Blocked Activities |
|-------------|-------------------|
| Metatarsal / Ankle | HIIT; if Acute also Running and RockClimbing |
| Knee | HIIT and Running |
| Shoulder (Acute) | Swimming and RockClimbing |

---

## Workout Day Construction

### Strength Days

1. GatingEngine resolves activity, style, set multiplier, RPE cap
2. If `SetMultiplier <= 0` -> rest day instead
3. `ExercisePickerService.PickForSessionAsync()` fills each slot:
   - Filter by allowed movement patterns
   - Filter by equipment availability
   - Filter out contraindicated exercises
   - Score remaining candidates
   - Pick highest-scoring non-reused exercise
4. Apply set multiplier to assigned sets
5. Cap RPE: `min(10 - slot.TargetRir, rpeCap)`
6. Assign rest periods by block type:
   - HighIntensity: 150s
   - Hypertrophy: 120s
   - Accessory: 90s
   - Core: 60s
7. Append up to 2 rehab exercises if injuries are active

### Cardio Days

- Gating resolves the specific activity (may swap to Yoga if low readiness)
- Exercise name resolved per phase (see table above)
- Single `WorkoutDayExercise` with `DurationSeconds`

### Recovery Days

- Single exercise: Yoga Flow or Mobility Flow
- Duration: 38 minutes

### Rest Days

- No exercises
- Name: "Living happy life"

---

## Duration Estimation

- **Strength**: 12 min warmup + work time + rest time (90-150s per set) + 3 min transitions between exercises. Minimum 45 min
- **Cardio/Recovery**: From `DurationSeconds` on the exercise
- **Fallback**: `max(20, exerciseCount * 12)` min

---

## Progression

There is no multi-week periodization or planned progressive overload across weeks.

### What Adapts

| Mechanism | How It Works |
|-----------|-------------|
| **Volume** | Set counts computed fresh each generation from budget + experience modifier + readiness multiplier |
| **Load suggestion** | At logging time: if user completed all target sets x reps, suggest +1 kg (<20 kg) or +2.5 kg (>= 20 kg) |
| **Exercise selection** | Deterministic scoring. Same inputs = same exercises. No rotation mechanism |
| **Phase variation** | Cycle phase change triggers full plan regeneration - shifts intensity, HIIT eligibility, set multipliers, RPE caps |
| **Readiness deload** | Low readiness: sets at 70%, RPE capped at 7. Two consecutive low days: forced rest |

---

## Adaptive Profile

Built from user profile + injury logs via `AdaptiveProfileMapper`:

| Field | Source |
|-------|--------|
| `Goal` | User's selected `UserGoal` |
| `Experience` | `TrainingExperienceLevel` |
| `DaysPerWeek` | Clamped 2-6, default 3 |
| `SessionMinutesCap` | Default 60 |
| `Selected` | Parsed from `PreferredWorkoutActivityTypes` (comma-delimited). At least one strength activity guaranteed |
| `Style` | `StrengthTrainingStyle` (stored as `"StrengthStyle:ComfortableModerate"` token) |
| `Equipment` | Parsed from `EquipmentLevel`. Default: FullGym |
| `Injuries` | From non-cleared `WorkoutInjuryLog` records |
| `BaselineSteps` | Hardcoded 8000 |
| `Baselines` | `CyclePhaseBaselines` from `profile.PhaseBaselinesJson` |

---

## Configuration

All numeric thresholds live in `Resources/workout-planning-config.json`:

```json
{
  "readiness": {
    "sleepLastNight": [[7.5, 30], [6.5, 22], [5.5, 12], [0, 0]],
    "sleep3dAvg": [[7, 15], [6, 8], [0, 0]],
    "stepsDeltaPct": {
      "normal": 15, "spike": 5, "drop": 10,
      "spikeThreshold": 40, "dropThreshold": -40
    },
    "energyMultiplier": 5,
    "pain": [15, 10, 3, 0],
    "phasePrior": {
      "Menstrual": -10, "Luteal": -5,
      "Follicular": 0, "Ovulatory": 0
    },
    "highTier": 75, "moderateTier": 50
  },
  "gating": {
    "hiitMinSleep": 6.5,
    "hiitMaxPain": 1,
    "hiitMenstrualMinEnergy": 5,
    "lowReadinessSetMultiplier": 0.7,
    "lowReadinessRpeCap": 7,
    "consecutiveLowDaysToRest": 2
  }
}
```

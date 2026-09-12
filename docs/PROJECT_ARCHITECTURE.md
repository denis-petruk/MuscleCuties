# MuscleCuties Architecture

MuscleCuties is a local-first .NET MAUI application that adapts cycle tracking,
nutrition targets, and workout planning to one user profile. This document
describes the code that is active in the application now.

## Solution

```text
MuscleCuties.sln
  src/
    MuscleCuties.App/          MAUI pages, controls, platform adapters, resources, DI
    MuscleCuties.Core/         models, EF Core, repositories, services, ViewModels
  tests/
    MuscleCuties.Core.Tests/   unit and SQLite integration tests
  docs/
    PROJECT_ARCHITECTURE.md
    PLANNING_CUSTOMIZATION_GUIDE.md
    resource-organization.md
```

`MuscleCuties.App` owns platform behavior and XAML. `MuscleCuties.Core` owns the
application and domain rules. Core references `Microsoft.Maui.Graphics` only for
color-bearing presentation models; it does not reference pages, Shell, handlers,
HealthKit, or Android APIs.

## Runtime Request Path

The normal MVVM path is:

```text
Page or reusable control
  -> ViewModel command
  -> domain service facade
  -> planner and repository interfaces
  -> AppDatabase (EF Core + SQLite)
  -> observable ViewModel state
  -> XAML binding
```

Pages do not query `AppDatabase` directly. Repositories own EF Core queries and
updates. ViewModels coordinate screen state but do not implement database rules.

## Core Structure

```text
MuscleCuties.Core/
  Data/
    AppDatabase.cs
    Seed/                       deterministic reference-data seeders
  Models/
    Entities/<Domain>/          persisted EF Core entities
    Enums/<Domain>/             domain enums
    Nutrition/                  reusable nutrition value objects
    Nutrition/Inputs/           nutrition command inputs
    Nutrition/Planning/         nutrition plan and meal suggestion contracts
    UI/<Domain>/                non-persisted XAML projection models
    Workout/Logging/            workout logging command inputs
    Workout/Planning/           immutable workout planning inputs and results
  Repositories/<Domain>/        persistence interfaces and implementations
  Services/<Domain>/            application service facades
    Cycle/Planning/
    Dashboard/Planning/
    Nutrition/Planning/
    Workout/Planning/
  ViewModels/<Domain>/          screen state and commands
  Resources/
    workout-planning-config.json
```

Planning code stays inside its domain. There is no separate generic `Engine`
layer and there is no second workout plan store.

## App Structure

```text
MuscleCuties.App/
  Pages/<Domain>/               routable screens
  Controls/<Domain>/            reusable cards, pickers, lists, and modal content
  Resources/
    Images/                     SVG and PNG source assets
    Raw/Animations/Cycle/       packaged cycle animation JSON
    Styles/                     shared colors and component styles
  Services/
    Auth/                       Apple and Google platform adapters
    Health/                     dormant native provider adapters
    Notifications/              platform notification implementation
    Profile/                    native image picker
  Platforms/<Platform>/         manifests, entitlements, delegates, icons
  MauiProgram.cs                composition root
```

See `docs/resource-organization.md` for runtime asset naming and locations.

## Dependency Lifetimes

| Lifetime | Components |
| --- | --- |
| Singleton | `HttpClient`, secure storage, local notifications, planning config, readiness and gating calculators |
| Scoped | `AppDatabase`, repositories, domain services, planners, workout planning services |
| Transient | pages and ViewModels |
| App singleton | `AppShell` |

Services that use `AppDatabase` are scoped. Pure calculators with immutable
configuration are singletons.

## Database

`AppDatabase` is the sole EF Core context. It contains these data groups:

| Domain | Main persisted data |
| --- | --- |
| User | `User`, `UserProfile`, `UserProfileSnapshot` |
| Quiz | `QuizQuestion`, `QuizAnswer`, `UserQuizResponse` |
| Cycle | `CycleLog`, `CyclePhaseLog`, `SymptomLog` |
| Nutrition | `FoodItem`, versions and sync logs, meal templates, logged meals and entries |
| Workout | exercise catalog, plans, activity days, planned exercises, workout logs, exercise logs |
| Workout planning | daily readiness, injuries, muscle groups, contributions, session and week templates, volume budgets |

All schema configuration and seeding use C# and EF Core. There are no direct SQL
commands in the application.

Some cleaned CLR names retain old SQLite table names for database compatibility:

| CLR entity | SQLite table |
| --- | --- |
| `WorkoutInjuryLog` | `EngineInjuryLogs` |
| `WorkoutMuscleGroup` | `EngineMuscleGroups` |
| `WorkoutExerciseDefinition` | `EngineExercises` |

Startup uses `Database.EnsureCreatedAsync()`. That creates a complete database for
a new install, but it does not upgrade an existing database after schema changes.
Before an App Store update changes the schema, add and validate an EF Core migration.
For local development, `ResetAndSeedDebugDatabaseAsync()` recreates and fully seeds
the database and is compiled only in Debug.

Startup seeding is split deliberately:

1. `InitializeStartupAsync()` creates a new schema and seeds quiz questions so
   onboarding can open quickly.
2. `SeedDeferredReferenceDataAsync()` seeds food, ready-meal templates, and workout reference data.
3. `SeedWorkoutPlanningDataAsync()` seeds planning rules and mirrors planning
   exercises into the exercise catalog used by the UI and logs.

## Workout Flow

The active production path is:

```text
WorkoutPage
  -> WorkoutViewModel
  -> IWorkoutService / WorkoutService
  -> IWorkoutPlanGenerator / WorkoutPlanGenerator
  -> IWeekPlanGenerator / WeekPlanGenerator
  -> IVolumeBudgetResolver
  -> IExercisePickerService
  -> WorkoutPlan + WorkoutDay + WorkoutDayExercise
  -> IWorkoutPlanner / WorkoutPlanner
  -> WorkoutItem and TodaysWorkoutSummary
```

The previous template planner and duplicate daily-plan storage were removed. The
main workout tab, dashboard summary, exercise modal, and workout logging now read
the same persisted `WorkoutPlan`.

### Workout Inputs

| Source | Fields used |
| --- | --- |
| `UserProfile` | user id, goal, experience, workout days, session duration, equipment, selected activities, strength style, phase baselines |
| Cycle service | current `CyclePhase` |
| `WorkoutInjuryLog` | active injury site and status |
| `DailyReadinessLog` | sleep, step averages, energy, pain, bloating, body weight, readiness |
| Planning reference tables | week templates, archetypes, slots, muscle priorities, volume budgets, exercise contributions |
| Exercise catalog | user-facing exercise data and logging identity |

Activity preferences are stored in `PreferredWorkoutActivityTypes` as a stable,
comma-separated list. A `StrengthStyle:<value>` token stores the strength style.
Legacy preference labels are accepted during parsing. If no strength option is
present, high-volume strength is added as the required base.

Supported activities are strength high intensity, high-volume strength, rock
climbing, yoga, HIIT, cycling, running, and swimming. Strength style is either
`ComfortableModerate` or `ExpressHard`. Missing session duration defaults to 60
minutes, missing equipment defaults to a full gym, and workout days are clamped to
2 through 6.

### Weekly Generation

`WeekPlanGenerator` selects a 2-to-6-day week template, maps session archetypes to
calendar days, allocates goal-specific weekly muscle volume, and validates exposure.
Tier A and B muscles are targeted at least twice per week when the selected template
can satisfy the constraints. The scheduler avoids repeated archetypes too close
together, limits heavy hinges, protects adjacent lower-body days, and accounts for
running or climbing fatigue.

The seeded strength archetypes are posterior, squat, upper pull, upper push,
full-body, short glute/accessory, and climbing patterns. Volume budgets vary by
goal, experience, available days, and session duration.

Selected supplemental activity does not mean every option must appear every week.
The planner picks at most one supplemental activity for most goals and at most two
for fat loss. HIIT is excluded from menstrual and luteal planning. Rock climbing is
treated as a pull-dominant strength activity.

One `WorkoutDay` is one loggable activity session. Strength and cardio on the same
calendar day remain separate rows with separate workout logs. Weekly active-day and
rest-day calculations use distinct weekdays, so two sessions on Tuesday still count
as one active day. Every otherwise unused weekday receives a `Rest` row named
`Living happy life`. A planned recovery session is a loggable activity and is not a
pure rest row.

### Exercise Selection

`ExercisePickerService` filters candidates by movement pattern, equipment, and
injury flags. It scores the remaining options using their muscle contribution,
fatigue cost, skill demand, long-length loading, setup time, and the requested
session block. The selected exercise receives sets, rep range, RPE, rest time,
superset group, and bodyweight metadata. The production plan stores the selected
sets and upper rep target in `WorkoutDayExercise`.

### Readiness

Readiness comes from `workout-planning-config.json`:

| Input | Points |
| --- | --- |
| Last sleep | 30 at 7.5h, 22 at 6.5h, 12 at 5.5h, otherwise 0 |
| Three-day sleep average | 15 at 7h, 8 at 6h, otherwise 0 |
| Steps versus seven-day average | 15 normal, 5 above +40%, 10 below -40% |
| Energy | selected 1-5 value multiplied by 5 |
| Pain | 15, 10, 3, or 0 for levels 0-3 |
| Phase prior | menstrual -10, luteal -5, follicular/ovulatory 0 |

Phase baseline energy can raise the phase contribution and phase baseline pain can
lower it. The final score is clamped to 0-100: high is 75+, moderate is 50-74, and
low is below 50.

Low readiness reduces strength sets to 70% and caps RPE at 7. Two consecutive low
days produce a rest day. HIIT requires at least 6.5 hours of sleep, pain no higher
than 1, and menstrual energy of at least 4. A low phase-energy baseline can make
that menstrual threshold stricter. High baseline pain blocks HIIT and reduces an
express strength prescription.

### Workout Logging And Progression

`WorkoutService.LogWorkoutSessionAsync()` validates every submitted exercise and
merges it into one `WorkoutLog` for that activity and date. Completion is the number
of valid logged exercises divided by the planned exercise count. A session is
complete only at 100%.

Strength logging requires completed sets, reps, and a non-negative weight. Zero is
valid for bodyweight work. Endurance exercises use only their relevant duration,
distance, pace, heart-rate, power, cadence, effort, or time-under-tension fields.

The next weight suggestion reads the latest prior `WorkoutExerciseLog`. When the
previous set and rep target was completed, it adds 1 kg below 20 kg or 2.5 kg at
20 kg and above, then rounds to the nearest 0.5 kg. Otherwise it repeats the last
weight. Same-day activities have different `WorkoutDayId` values and therefore save
and complete independently.

## Nutrition Flow

```text
NutritionPage
  -> NutritionViewModel partials
  -> INutritionService / NutritionService
  -> NutritionPlanner, SuggestedMealService, and repositories
  -> FoodSyncService -> FdcApiClient when remote food data is needed
```

`NutritionViewModel` is one ViewModel split by responsibility: meals, search,
custom food, breakdown, suggestions, formatting, and notifications. These partial
files share one state object; they are not separate services.

### Daily Nutrition Targets

For a complete profile, `NutritionPlanner` uses:

```text
BMR  = 10 * weightKg + 6.25 * heightCm - 5 * age - 161
TDEE = BMR * activityMultiplier
```

Activity multiplier comes from workout days: 1.2 for none, 1.375 for 1-2, 1.55
for 3-4, 1.725 for 5-6, and 1.8 for 7. Beginner subtracts 0.025 and advanced adds
0.025.

Goal adjustment:

| Goal | Steady | Aggressive |
| --- | --- | --- |
| Fat loss | -12%, capped at -300 kcal | -20%, capped at -500 kcal |
| Strength | +8%, capped at +250 kcal | +12%, capped at +400 kcal |
| Muscle tone | +4%, capped at +150 kcal | same calculation |
| Maintain health | no adjustment | no adjustment |

Phase adjustment is -50 kcal menstrual, 0 follicular, +50 ovulatory, and +150
luteal. Menstrual fat-loss plans use 0 instead of -50. Final calories are clamped
to 1200-4000 and rounded to 10 kcal.

Protein is 2.0 g/kg for fat loss, 1.9 for strength, 1.8 for muscle tone, and 1.6
for general health. Fat starts at 30% of calories in menstrual/luteal phases and
25% otherwise, with a weight-based floor and at least 25% of calories left for
carbohydrate. Carbohydrate receives the remaining calories.

Fiber is the greater of 25 g or 14 g per 1000 kcal, plus 3 g in the luteal phase.
Water is 0.035 L/kg, plus 0.1 L per weekly workout day and 0.2 L in ovulatory or
luteal phases, clamped to 1.8-4.0 L. Breakfast, lunch, dinner, and snack targets use
25%, 30%, 30%, and 15% of daily targets.

Positive values in `UserProfile.NutritionGoalsJson` override calculated goals.
Missing custom values continue to use the calculated plan.

### Food And Meals

The local food catalog is the first source. Food Data Central search is paged,
normalized, deduplicated, and cached through `FoodSyncService`. Custom foods are
stored with nutrients normalized per 100 g plus their serving metadata.

A logged meal owns one or more `LoggedMealEntry` rows. Each entry references a
`FoodItem` and stores grams, so totals are calculated from the current food nutrient
record. Meal templates are separate reusable recipes and do not count as logs until
the user adds them.

`SuggestedMealService` classifies foods as protein, carbohydrate, vegetable, or
sauce; ranks variety over the previous seven days; scores combinations for dietary
tags, phase, goal, and macro fit; solves portions; and returns up to four
suggestions. Its data contracts live under `Models/Nutrition/Planning`; the
scoring, catalog, and portion algorithms live under `Services/Nutrition/Planning`.
Thin component groups can be filled from Food Data Central with a five-second
bound. Suggested portions must normally stay within 8% of meal calories and 5 g of
protein; the fallback pass allows 12% calorie tolerance.

Vitamins A, D, E, and K are counted as absorbable only when at least 5 g of fat was
logged in the same meal or within two hours. Other tracked nutrients, including
fiber, potassium, vitamins, and minerals, are summed normally.

## Cycle Flow

```text
CyclePage
  -> CycleViewModel
  -> ICycleService / CycleService
  -> CyclePredictionPlanner and CyclePhaseRules
  -> ICycleRepository
```

Prediction uses up to six recent measured cycle lengths between 18 and 60 days. If
none exist, it derives lengths from cycle-start gaps, then uses the profile cycle
length, then 28 days.

For an active cycle:

- Current day is days since the start date plus one.
- Next period is start date plus predicted cycle length.
- Ovulation is predicted 14 days before the next period.
- Fertile window is five days before through one day after predicted ovulation.
- Days 1-5 are menstrual.
- Follicular runs through two days before predicted ovulation.
- Ovulatory runs through two days after predicted ovulation.
- Remaining days are luteal.

Manual `CyclePhaseLog` entries override the projected phase from their logged date.
Edits permit the same phase or the next phase in menstrual -> follicular ->
ovulatory -> luteal -> menstrual order. Adjacent records are validated so a date
edit cannot leave inconsistent history. Dates before tracking began remain neutral.

## Dashboard Recovery

Readiness and recovery are related but separate values.

- If a `DailyReadinessLog` exists, the dashboard uses its recorded readiness score.
- Otherwise the dashboard fallback starts at 72 and adjusts for phase, calorie
  progress, planned training days, steps, and sleep quality.
- Recovery starts at 78 and adjusts for phase, calorie progress, whether today's
  workout is complete, and average sleep.
- Hydration is 0.035 L/kg clamped to 2.0-3.8 L, with 0.2 L added above 10,000
  average daily steps.
- Sleep target is 8 hours for four or more workout days and 7.5 hours otherwise.

Apple Health, Health Connect, and Whoop providers are intentionally dormant in the
current composition root. `DisabledHealthSyncService` returns no provider data, so
native authorization cannot block startup or page transitions. The provider files
remain isolated under `MuscleCuties.App/Services/Health` for the planned native
integration pass.

## Streaks

`ProgressSummaryService` reads real meal and workout logs over the requested window.
A completed workout session has 100% completion and is unique by date plus
`WorkoutDayId`. Workout and nutrition streaks use today when today has a log;
otherwise they can continue from yesterday. Missing an earlier day ends the streak.

## Structural Rules

- Keep persisted classes in `Models/Entities/<Domain>`.
- Keep enums in `Models/Enums/<Domain>`.
- Keep XAML-only projection models in `Models/UI/<Domain>`.
- Keep algorithms under `Services/<Domain>/Planning` when they do not perform UI work.
- Keep EF Core access in repositories or deterministic database seeders.
- Keep platform APIs in `MuscleCuties.App/Services` or `Platforms`.
- Add a reusable control when a visual block is used by more than one page or owns
  meaningful interaction; do not create one-file wrappers around a single label.
- Keep comments for section boundaries, non-obvious constraints, and TODOs with a
  real future action. Names and tests should explain ordinary code.
- Add tests beside the matching domain and responsibility.

## Known Release Boundary

The current schema is suitable for fresh installs and debug resets. The next schema
change must introduce versioned EF Core migrations before shipping an update to
existing users. Native health providers must remain disabled until their platform
entitlements, consent flow, provider tests, and timeout behavior are complete.

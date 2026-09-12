# Planning Customization Guide

This guide is a file map for changing the workout and nutrition ideas by hand.
Use it as a set of hints while you experiment, then let tests tell you whether the
rules still hold together.

## Workout Planning

Start with these files when the question is "what workout should the user get?"

| File | What it controls |
| --- | --- |
| `src/MuscleCuties.Core/Services/Workout/Planning/WorkoutPlanGenerator.cs` | Converts a generated weekly structure into persisted `WorkoutPlan`, `WorkoutDay`, and `WorkoutDayExercise` rows. Good place to tune names, duration estimates, rest-day wording, and how one planned activity becomes one loggable session. |
| `src/MuscleCuties.Core/Services/Workout/Planning/WeekPlanGenerator.cs` | The central weekly planner. This is where days, archetypes, fatigue spacing, active-day count, supplemental activities, and exposure validation meet. Change slowly and test often. |
| `src/MuscleCuties.Core/Services/Workout/Planning/ExercisePickerService.cs` | Chooses actual exercises for a slot. If an exercise feels wrong for a session, look here after checking the seed data. |
| `src/MuscleCuties.Core/Services/Workout/Planning/GatingEngine.cs` | Decides whether phase, energy, pain, sleep, injuries, and readiness should allow or reduce an activity. Useful for menstrual/luteal intensity rules. |
| `src/MuscleCuties.Core/Services/Workout/Planning/WorkoutProfileAdapter.cs` | Reads `UserProfile` and turns it into planner-friendly inputs. If quiz/profile choices are not affecting workouts, inspect this bridge first. |
| `src/MuscleCuties.Core/Services/Workout/Planning/WorkoutActivityPreferences.cs` | Parses and serializes selected activities and strength style. Keep backward-compatible parsing here, not inside UI pages. |
| `src/MuscleCuties.Core/Services/Workout/Planning/WorkoutInjuryRules.cs` | Blocks or downgrades activities based on active injury logs. |
| `src/MuscleCuties.Core/Services/Workout/Planning/VolumeBudgetResolver.cs` | Turns goal, experience, days, and session length into weekly muscle volume. |
| `src/MuscleCuties.Core/Resources/workout-planning-config.json` | Readiness scoring weights and thresholds. Tune values here when the rule is configuration, not code. |

The data that powers the planner lives here:

| File | What it contains |
| --- | --- |
| `src/MuscleCuties.Core/Data/Seed/AppDatabaseWorkoutPlanningSeed.cs` | Muscle priorities, volume budgets, exercise definitions, muscle contributions, session archetypes, slots, and week templates. This is the biggest lever for changing the planner without rewriting algorithms. |
| `src/MuscleCuties.Core/Data/Seed/AppDatabaseWorkoutSeed.cs` | User-facing exercise catalog mirrored into workout sessions. Technique explanations and quick tips are seeded here. |
| `src/MuscleCuties.Core/Models/Enums/Workout/WorkoutActivityType.cs` | The supported activity list. Keep this short and stable. |
| `src/MuscleCuties.Core/Models/Workout/Planning/*.cs` | Immutable planner input and result records. These should describe the planner, not contain behavior. |
| `src/MuscleCuties.Core/Models/Workout/Logging/WorkoutExerciseLogInput.cs` | Data submitted when the user logs a session. |

Helpful instincts:

- Tune seed data first when the exercise library is weak.
- Tune `GatingEngine` when workouts feel unsafe for a phase or readiness state.
- Tune `WeekPlanGenerator` only when weekly structure is wrong.
- Keep one `WorkoutDay` as one loggable activity session, even when two activities share the same date.
- Add tests for every rule that could accidentally make a plan impossible.

## Workout Presentation And Logging

These files decide what the user sees and how completion is stored.

| File | What it controls |
| --- | --- |
| `src/MuscleCuties.Core/Services/Workout/WorkoutService.cs` | Facade for summaries, session details, logging, completion, and progression suggestions. If UI data is wrong but the plan rows are correct, look here. |
| `src/MuscleCuties.Core/Services/Workout/WorkoutPlanner.cs` | Converts persisted plan rows into dashboard/workout list presentation models. |
| `src/MuscleCuties.Core/ViewModels/Workout/WorkoutViewModel.cs` | Workout page state, selected session modal, entered logs, and submit commands. |
| `src/MuscleCuties.Core/Models/UI/Workout/WorkoutPresentationModels.cs` | Non-persisted models bound by XAML. |
| `src/MuscleCuties.App/Controls/Workout/*.xaml` | Reusable workout cards, activity blocks, exercise rows, and modal views. |
| `src/MuscleCuties.App/Pages/Workout/WorkoutPage.xaml` | Page composition only. Keep detailed card design inside controls. |

## Nutrition Planning

Start with these files when the question is "what should the user eat?"

| File | What it controls |
| --- | --- |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/NutritionPlanner.cs` | Daily calories, macro split, water, fiber, phase adjustment, meal target split, and custom-goal fallbacks. |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/SuggestedMealService.cs` | Orchestrates ready suggestions from local foods, concepts, scoring, portions, variety, and FDC fallback. |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/SavouryMealCatalog.cs` | Savoury meal concepts and ingredient slots. |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/SweetBreakfastCatalog.cs` | Sweet breakfast concepts. |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/MealComponentScorer.cs` | Scores dietary fit, phase micronutrients, variety, style, and macro potential. |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/PortionSolver.cs` | Converts selected foods into grams that fit one meal target. |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/FoodComponentClassifier.cs` | Classifies foods into protein, carb, vegetable, sauce, or mixed. |
| `src/MuscleCuties.Core/Services/Nutrition/Planning/FatSolubleVitaminAbsorption.cs` | Counts fat-soluble vitamins only when meal timing/fat rules are met. |

Nutrition data and contracts live here:

| File | What it contains |
| --- | --- |
| `src/MuscleCuties.Core/Data/Seed/AppDatabaseNutritionSeed.cs` | Starter food catalog and system ready-meal templates. Good first stop for changing default recommendations. |
| `src/MuscleCuties.Core/Models/Nutrition/MacroNutrients.cs` | Shared macro math helpers. |
| `src/MuscleCuties.Core/Models/Nutrition/Inputs/*.cs` | Command inputs for custom foods and meal logging. |
| `src/MuscleCuties.Core/Models/Nutrition/Planning/*.cs` | Daily plan, micronutrient goals, meal concepts, suggested meal records, and portion solution records. |
| `src/MuscleCuties.Core/Models/Enums/Nutrition/*.cs` | Meal types, dietary tags, serving units, and planning enums. |

Helpful instincts:

- Tune `NutritionPlanner` for calorie and macro philosophy.
- Tune `MealComponentScorer` when suggestions are technically valid but not appetizing or phase-aware enough.
- Tune catalog files when you want better recipes without touching math.
- Tune `PortionSolver` when grams feel unrealistic.
- Keep all nutrient values normalized per 100 g in `FoodItem`.

## Nutrition Presentation And Logging

| File | What it controls |
| --- | --- |
| `src/MuscleCuties.Core/Services/Nutrition/NutritionService.cs` | Facade for daily plan, search, custom foods, logged meals, ready suggestions, and meal updates. |
| `src/MuscleCuties.Core/ViewModels/Nutrition/NutritionViewModel*.cs` | Nutrition page state split by meals, search, custom foods, suggestions, breakdowns, formatting, and notifications. |
| `src/MuscleCuties.Core/Models/UI/Nutrition/*.cs` | Row/card/modal models bound by nutrition XAML. |
| `src/MuscleCuties.App/Controls/Nutrition/*.xaml` | Reusable nutrition visual blocks. |
| `src/MuscleCuties.App/Pages/Nutrition/NutritionPage.xaml` | Page composition only. |

## Cycle And Recovery Influence

| File | What it controls |
| --- | --- |
| `src/MuscleCuties.Core/Services/Cycle/CycleService.cs` | Current phase, phase logs, cycle edits, and predictions. |
| `src/MuscleCuties.Core/Services/Cycle/Planning/*.cs` | Phase order rules and prediction helpers. |
| `src/MuscleCuties.Core/ViewModels/Cycle/CycleViewModel.cs` | Calendar state, current day, phase edits, and warnings. |
| `src/MuscleCuties.Core/Services/Dashboard/Planning/*.cs` | Dashboard readiness/recovery fallback math. |
| `src/MuscleCuties.Core/Services/Progress/ProgressSummaryService.cs` | Workout and nutrition streaks. |
| `src/MuscleCuties.Core/Models/Entities/Workout/Planning/DailyReadinessLog.cs` | Stored sleep, steps, energy, pain, body weight, and readiness inputs. |

Useful mental checks:

- Cycle phase should influence planning through services, not XAML.
- Recovery/readiness should reduce or redirect training before exercise picking happens.
- Streaks should read real logs only, never planned sessions.

## Tests Worth Opening First

| Test file | Why it matters |
| --- | --- |
| `tests/MuscleCuties.Core.Tests/Services/Workout/Planning/WeekPlanGeneratorTests.cs` | Weekly structure, rest days, and activity selection rules. |
| `tests/MuscleCuties.Core.Tests/Services/Workout/Planning/GatingEngineTests.cs` | Phase/readiness/injury gating. |
| `tests/MuscleCuties.Core.Tests/Services/Workout/WorkoutServiceTests.cs` | Persisted plans, logging, completion, and progression. |
| `tests/MuscleCuties.Core.Tests/Services/Nutrition/NutritionPlannerTests.cs` | Daily nutrition formulas. |
| `tests/MuscleCuties.Core.Tests/Services/Nutrition/MealComponentScorerTests.cs` | Suggestion ranking and dietary fit. |
| `tests/MuscleCuties.Core.Tests/Services/Nutrition/PortionSolverTests.cs` | Portion grams and meal target tolerance. |
| `tests/MuscleCuties.Core.Tests/Data/AppDatabaseInitializationTests.cs` | Startup seed expectations. |

When you change the idea of the app, write the expectation as a test first in the
closest file above. That keeps the project teaching you where the rule belongs.

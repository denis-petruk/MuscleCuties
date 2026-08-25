# MuscleCuties Project Context

Use this file as grounding context for future prompts about this repository.

## Core idea

MuscleCuties is a cycle-aware fitness and nutrition companion for women. The app adapts workout intensity, daily nutrition targets, recovery cues, and dashboard guidance to the user's current menstrual-cycle phase: Menstrual, Follicular, Ovulatory, or Luteal.

The product thesis from `README.md` is: fitness and nutrition plans should respect that energy, symptoms, recovery, and appetite can vary across the cycle.

## Tech stack

- .NET 10 solution.
- `src/MuscleCuties.Core`: platform-agnostic models, EF Core context, repositories, services, and ViewModels.
- `src/MuscleCuties.App`: .NET MAUI host, XAML pages, platform services, resources, DI setup, and Shell navigation.
- EF Core SQLite for local persistence.
- CommunityToolkit.Mvvm for observable ViewModels and commands.
- CommunityToolkit.Maui for MAUI app support.
- xUnit, NSubstitute, and SQLite in-memory tests in `tests/MuscleCuties.Core.Tests`.

## Current app surfaces

- Auth: login and registration with email/password.
- Onboarding: quiz plus profile setup.
- Today dashboard: current cycle phase, nutrition targets/consumption, workout summary, and placeholder readiness/recovery/hydration/sleep values.
- Cycle: cycle calendar and phase display.
- Train: active workout plan and workout-day cards filtered by type.
- Nutrition: daily macro/calorie targets and meal data.
- Profile: user summary/preferences and logout.

## Architecture rules to preserve

- Keep business logic in `MuscleCuties.Core`; keep MAUI/platform concerns in `MuscleCuties.App`.
- ViewModels should call services first when business logic exists. Repositories are acceptable where a service layer has not been introduced yet.
- Repositories wrap EF Core access behind interfaces.
- Keep async method boundaries throughout services, repositories, and ViewModels.
- Use constructor injection through `MauiProgram.cs`.
- Prefer extending existing bounded contexts over adding cross-cutting shortcuts.
- Add or update focused tests under `tests/MuscleCuties.Core.Tests` when changing Core behavior.

## Bounded contexts and data model

The current model has 5 bounded contexts and 20 entities.

### User

- `User`: email, SHA-256 password hash, timestamps, onboarding flag, optional `UserProfile` navigation.
- `UserProfile`: name, birth date, height, weight, goal, weight-goal pace, workout days per week, cycle length, dietary tags, updated timestamp.
- `UserProfileSnapshot`: JSON profile snapshot with `SnapshotReason`.

### Quiz

- `QuizQuestion`: question text, order, `QuizQuestionType`, answer collection.
- `QuizAnswer`: answer text, order, mapped integer value.
- `UserQuizResponse`: user/question/answer row, answered timestamp, optional profile snapshot link.

`QuizService.SaveAnswersAsync` maps goal, workout-days-per-week, and dietary preference into `UserProfile`. It writes a snapshot and stores quiz responses, but it does not mark onboarding complete.

### Cycle

- `CycleLog`: user, start date, optional end date, cycle length, created timestamp, symptom logs.
- `SymptomLog`: user, cycle log, date, `SymptomType`, severity 1-5, notes, created timestamp.

`CycleService.GetCurrentPhaseAsync` uses the latest cycle start date and profile cycle length, defaulting to 28 days. If no cycle exists, it returns `Follicular`.

### Nutrition

- `FoodItem`: name, calories, protein, carbs, fats, fiber, iron, B12, C, D, A, B6, folate, calcium, magnesium, zinc, custom/FDC flags, sync timestamps, version history.
- `FoodItemVersion`: nutrient JSON history for changed food records.
- `FoodSyncLog`: sync-run audit status and counts.
- `MealTemplate`: system or user meal template.
- `MealTemplateEntry`: food item plus grams in a template.
- `LoggedMeal`: user/date/exact `LoggedAt` time/meal type, optional template, entries.
- `LoggedMealEntry`: food item plus grams in a logged meal.

`NutritionService` calculates daily targets from profile metrics using Mifflin-St Jeor BMR, activity multiplier, goal adjustment, cycle-phase adjustment, calorie clamping, and macros. It searches food items, logs selected foods with grams and exact meal time, and calculates consumed calories/macros from logged meal entries.

### Workout

- `Exercise`: name, description, optional image, primary muscle, secondary muscles string, joint areas string.
- `WorkoutPlan`: user, name, active flag, optional target cycle phase, days.
- `WorkoutDay`: workout plan, day of week, name, exercises.
- `WorkoutDayExercise`: day/exercise join with sets, reps, optional duration.
- `WorkoutLog`: user, workout day, date, completion percent, notes.

`WorkoutRepository` can load an active plan, plan days, day exercises, and workout logs. There is no workout service yet.

## Key enums

- `CyclePhase`: Menstrual, Follicular, Ovulatory, Luteal.
- `UserGoal`: FatLoss, MuscleTone, Strength, MaintainHealth.
- `WeightGoalPace`: Steady, Aggressive.
- `DietaryTag`: None, Vegetarian, Vegan, GlutenFree, LactoseFree.
- `MealType`: Breakfast, Lunch, Dinner, Snack.
- `WorkoutType`: Strength, Cardio, Recovery.
- `SymptomType`: Cramps, Bloating, Fatigue, Headache, MoodSwings, Spotting, Other.
- `QuizQuestionType`: Goal, ExperienceLevel, WorkoutDaysPerWeek, DietaryPreference, and pain/energy questions per cycle phase.

## Startup and navigation

- `MauiProgram.cs` wires EF Core SQLite, repositories, services, ViewModels, pages, and `AppShell`.
- `App.CreateWindow` returns `AppShell`.
- `App.OnStart` creates a scope, calls `AppDatabase.InitializeAsync()`, then checks auth state.
- `AppShell.xaml` contains `LoginPage` plus a five-tab `TabBar`: Today, Cycle, Train, Nutrition, You.
- Auth state is a stored `current_user_id` via `ITokenStorage`; the MAUI app implements it with `SecureStorageService`.

## Database setup

- `AppDatabase.OnModelCreating` configures relationships, indexes, delete behavior, required fields, max lengths, and basic check constraints.
- `AppDatabase.InitializeAsync()` calls `EnsureCreatedAsync()`, applies local compatibility updates, and seeds quiz questions, system meal templates, and starter foods idempotently.
- EF Core migrations are not configured yet.

## Onboarding behavior

- Registration stores the new user id and routes to the quiz.
- Quiz completion saves responses and profile-derived quiz fields, then routes to profile setup.
- Profile setup updates the same profile if it already exists, creates it if missing, writes a profile setup snapshot, marks the user onboarding-complete, and routes to the dashboard.
- Login uses `IQuizService.IsOnboardingCompleteAsync()` to route incomplete users to the quiz and complete users to the dashboard.

## Current implementation caveats

- Password hashing is plain SHA-256 and should be replaced before production use.
- EF migrations are not in place yet; `EnsureCreatedAsync()` is suitable for the current MVP.
- Several ViewModels still use repositories directly where no service layer exists yet.
- Profile editing screens are not implemented beyond the onboarding profile setup flow.
- FoodData Central sync services are implemented in Core and remain local-first; UI food search falls back to seeded/local foods when no `FDC_API_KEY` is configured.
- Root `Models/` and `ViewModels/` folders are effectively empty; active code lives under `src/MuscleCuties.Core`.

## Useful commands

```bash
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
```

```bash
dotnet build src/MuscleCuties.Core/MuscleCuties.Core.csproj
```

For MAUI app execution, use the platform target from `README.md`, for example Android:

```bash
dotnet build -t:Run -f net10.0-android
```

## Prompting guidance

When asking for future changes, include this context and specify which layer should change:

- Data model/entity changes: update entities, EF model configuration, repositories, and repository tests.
- Business behavior: prefer services in `MuscleCuties.Core/Services` and add service tests.
- UI behavior: update Core ViewModels first, then MAUI XAML/page wiring.
- Nutrition/FDC behavior: align with `STUDYAPI.md`, `FoodItem`, `FoodItemVersion`, and `FoodSyncLog`. USDA sync is local-first through `IFoodSyncService`; keep the API key in `FDC_API_KEY`.

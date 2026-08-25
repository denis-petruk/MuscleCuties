# MuscleCuties

MuscleCuties is a cycle-aware fitness and nutrition companion for women. It adapts workout intensity, calorie and macro targets, recovery cues, and daily guidance to the user's current menstrual-cycle phase: Menstrual, Follicular, Ovulatory, or Luteal.

The core product idea is simple: training and nutrition plans should respect how energy, appetite, symptoms, and recovery can change across the cycle.

## Features

### Onboarding

- Email/password registration with secure local session storage.
- A 12-question quiz captures goal, experience level, training frequency, dietary preference, and per-phase symptom baselines.
- Profile setup captures name, birth date, height, weight, and unit preference.
- Quiz answers create or update a partial `UserProfile` and write a `UserProfileSnapshot`.
- Profile setup completes the same profile, writes a setup snapshot, and marks onboarding complete.

### Today

- Time-aware greeting and date header.
- Active cycle phase badge with contextual guidance.
- Workout summary from the active workout plan.
- Readiness and recovery scores derived from cycle phase, nutrition progress, and workout load.
- Daily calorie and macro targets with consumed totals from logged meals.
- Hydration and sleep targets based on profile metrics and training frequency.

### Cycle

- 28-day cycle calendar colored by phase.
- Current cycle day and current phase display.
- Phase legend cards, phase detail pages, manual phase shifts, and editable day-level phase logs.

### Train

- Active workout plan header.
- Filter chips for All, Strength, Cardio, Climb, Yoga, Pilates, and Recovery.
- Workout cards generated from `WorkoutDay` rows.
- Workout completion logs through `WorkoutLog`, with per-exercise sets, reps, weight, duration, distance, pace, and heart-rate fields.
- Saved activity preferences for strength, climbing, yoga styles, Pilates, cardio, mobility, and active recovery.

### Nutrition

- Phase-aware nutrition focus.
- Daily calorie and macro targets calculated from profile data.
- Logged meals stored as `LoggedMeal` plus `LoggedMealEntry` rows, including exact `LoggedAt` time.
- Add-food flow searches foods, lets the user choose grams, meal type, and meal time, then refreshes daily totals.
- Food items include macro and micronutrient fields, with FoodData Central IDs, sync logs, and version history for USDA-backed food data.

### You

- User profile summary.
- Editable personal info, nutrition settings, unit preferences, privacy page, and private feedback flow.
- Logout.

## Current State

The app is a .NET MAUI MVP with platform-specific code isolated in `MuscleCuties.App` and business logic in `MuscleCuties.Core`.

The current data model has 5 bounded contexts:

- User
- Quiz
- Cycle
- Nutrition
- Workout

The database is local SQLite. `AppDatabase.InitializeAsync()` runs on app start, creates the schema with `EnsureCreatedAsync()`, applies small compatibility updates for existing local databases, and seeds onboarding quiz questions, system meal templates, and starter food items idempotently. EF Core migrations are not configured yet.

Nutrition targets are calculated from profile metrics using Mifflin-St Jeor BMR, activity multiplier, goal adjustment, phase adjustment, calorie clamping, and macro calculation. Manual calorie targets are not currently modeled.

FoodData Central sync is implemented in Core through `IFdcApiClient` and `IFoodSyncService`. The app reads the USDA key from the `FDC_API_KEY` environment variable and keeps search local-first before calling the remote API.

## Requirements

- .NET 10 SDK
- MAUI workload: `dotnet workload install maui`
- Android SDK API 21+ or Xcode 15+ for iOS
- Optional for USDA food sync: `FDC_API_KEY`

## Run

```bash
# Android
dotnet build -t:Run -f net10.0-android

# iOS simulator
dotnet build -t:Run -f net10.0-ios

# Mac Catalyst
dotnet build -t:Run -f net10.0-maccatalyst
```

## Tests

```bash
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
```

## Project Layout

```text
MuscleCuties/
├── src/
│   ├── MuscleCuties.Core/
│   │   ├── Data/
│   │   │   ├── AppDatabase.cs
│   │   │   └── IDbPathProvider.cs
│   │   ├── Models/
│   │   │   ├── Entities/
│   │   │   └── Enums/
│   │   ├── Repositories/
│   │   ├── Services/
│   │   └── ViewModels/
│   └── MuscleCuties.App/
│       ├── Pages/
│       ├── Resources/
│       ├── Services/
│       ├── App.xaml(.cs)
│       ├── AppShell.xaml(.cs)
│       └── MauiProgram.cs
└── tests/
    └── MuscleCuties.Core.Tests/
```

## Architecture

MuscleCuties follows MVVM with constructor-injected dependencies.

- `MuscleCuties.Core` owns entities, enums, repositories, services, and ViewModels.
- `MuscleCuties.App` owns MAUI startup, platform services, pages, Shell routes, and resources.
- Repositories wrap EF Core access behind interfaces.
- Services contain business rules where a domain has non-trivial behavior.
- ViewModels expose page state and commands for MAUI pages.

### Startup Flow

1. `App.CreateWindow` creates the Shell root.
2. `App.OnStart` creates a scoped service provider.
3. `AppDatabase.InitializeAsync()` creates and seeds the local database.
4. `IAuthService.IsLoggedInAsync()` checks secure storage for `current_user_id`.
5. Logged-in users navigate to `DashboardPage`; logged-out users navigate to `LoginPage`.

### Data Flow

```text
MAUI Page -> ViewModel -> Service or Repository -> AppDatabase -> SQLite
```

## Data Model

### User

- `User`
- `UserProfile`
- `UserProfileSnapshot`

### Quiz

- `QuizQuestion`
- `QuizAnswer`
- `UserQuizResponse`

### Cycle

- `CycleLog`
- `CyclePhaseLog`
- `SymptomLog`

### Nutrition

- `FoodItem`
- `FoodItemVersion`
- `FoodSyncLog`
- `MealTemplate`
- `MealTemplateEntry`
- `LoggedMeal`
- `LoggedMealEntry`

### Workout

- `Exercise`
- `WorkoutPlan`
- `WorkoutDay`
- `WorkoutDayExercise`
- `WorkoutLog`
- `WorkoutExerciseLog`

## Development Notes

- Keep business logic in `MuscleCuties.Core`.
- Keep platform-specific code in `MuscleCuties.App`.
- Prefer services for business workflows; use repositories for persistence details.
- Add focused tests when changing Core behavior.
- Keep README claims aligned with implemented code.
- Never commit FoodData Central API keys; use `FDC_API_KEY` locally.

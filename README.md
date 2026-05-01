# MuscleCuties

A cycle-aware fitness and nutrition companion for women. MuscleCuties syncs workout intensity, nutrition targets, and recovery guidance to the four phases of the menstrual cycle, delivering a personalized daily plan that respects how the body actually feels and performs throughout the month.

---

## Features

### Onboarding
- Email / password registration with secure token storage
- 12-question onboarding quiz capturing fitness goal, experience level, training frequency, dietary preference, and per-phase symptom baselines
- Profile setup: name, date of birth, height, weight, unit preference (metric/imperial)
- Quiz completion writes a `UserProfileSnapshot` with `SnapshotReason = Initial`; subsequent quiz retakes snapshot the previous profile before applying changes

### Today — Dashboard
- Time-aware greeting and date header
- Active cycle phase badge with short contextual advice
- Today's workout card: title, subtitle, duration, exercise count, intensity, and session progress within the week
- Readiness and Recovery rings with color-coded scores (green >= 70, amber 40–69, red < 40)
- Macro summary: calories with progress bar, protein / carbs / fat bars
- Hydration and sleep targets

### Cycle
- Month view calendar: 28-day grid color-coded by phase (menstrual / follicular / ovulatory / luteal)
- Current day highlighted with a primary-pink stroke
- Days-until-period counter
- Phase legend cards with name and day-range description

### Train
- Phase-aware workout plan header
- Filter chips: All / Strength / Cardio / Yoga / Recovery (horizontal scroll)
- Workout cards: phase icon box, tag, title, duration, chevron nav
- Workout completions logged via `WorkoutLog` with a 0–100 completion percentage — used as the recommendation feedback signal

### Nutrition
- Phase focus banner with cycle-phase color
- Today's balance card: calorie row with progress bar, macro grid (protein, carbs, fat)
- Meals list: time badge, meal type tag, name, calories
- Meals logged as `LoggedMeal` + `LoggedMealEntry` rows, optionally from a saved `MealTemplate`

### You — Profile
- Avatar circle with user initial
- Stats grid: sessions, cycle days, phases tracked
- Preferences list with chevron nav
- Profile edits write a `UserProfileSnapshot` with `SnapshotReason = UserEdit` before applying changes

---

## Current State (MVP)

Architecture, repositories, services, and data layer are wired through DI with a clean bounded-context model (6 domains, 22 entities). ViewModels receive data from Services via async calls; the service-to-ViewModel integration is ongoing. The database is rebuilt from scratch on every launch via `EnsureCreated()` — no production data exists yet.

A `FloAccessToken` placeholder exists on `User` for a future Flo API integration; it is not connected to anything. Recommendations are locally generated: `RecommendationService` produces a `RecommendationSet` per user per day, with typed children (`NutritionRecommendation`, `WorkoutRecommendation`, `WellnessRecommendation`) that carry an `ActedOnAt` feedback field.

Calorie targets support two modes: `Calculated` (BMR-derived via Mifflin-St Jeor, using date of birth and body metrics) and `Manual` (user-entered fixed target). Weight goal pace is configurable: `Steady` (~250–300 kcal delta) or `Aggressive` (~500 kcal delta).

FoodItem micronutrients (Iron, Calcium, Magnesium, Zinc, vitamins B6/B12/C/D/A/Folate) are tracked per 100g. FDC sync health is audited via `FoodSyncLog` (run-level status) and `FoodItemVersion` (per-item nutrient history written before every upsert that changes values).

---

## Getting Started

### Requirements
- .NET 10 SDK
- MAUI workload: `dotnet workload install maui`
- Android SDK (API 21+) or Xcode 15+ for iOS

### Run

```bash
# Android
dotnet build -t:Run -f net10.0-android

# iOS simulator
dotnet build -t:Run -f net10.0-ios

# Mac Catalyst
dotnet build -t:Run -f net10.0-maccatalyst
```

### Tests

```bash
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
```

### First launch
The database is created and seeded with quiz questions and system `MealTemplate` rows automatically on first run via `AppDatabase.InitializeAsync()`. Credentials are stored in platform `SecureStorage`.

---

## Project Layout

```
MuscleCuties/
├── src/
│   ├── MuscleCuties.Core/              # Platform-agnostic business logic
│   │   ├── Models/
│   │   │   ├── Enums/
│   │   │   │   ├── CyclePhase.cs
│   │   │   │   ├── UserGoal.cs
│   │   │   │   ├── DietaryTag.cs
│   │   │   │   ├── MealType.cs
│   │   │   │   ├── MuscleGroup.cs
│   │   │   │   ├── CalorieMode.cs
│   │   │   │   ├── WeightGoalPace.cs
│   │   │   │   ├── QuizQuestionType.cs
│   │   │   │   └── SymptomType.cs      # Cramps | Bloating | Fatigue | Headache | MoodSwings | Spotting | Other
│   │   │   └── Entities/
│   │   │       │   -- User domain --
│   │   │       ├── User.cs
│   │   │       ├── UserProfile.cs          # BMR fields; CalorieMode; WeightGoalPace; DietaryTags
│   │   │       ├── UserProfileSnapshot.cs  # JSON snapshot on Initial | QuizRetake | UserEdit
│   │   │       │   -- Cycle domain --
│   │   │       ├── CycleLog.cs
│   │   │       ├── SymptomLog.cs           # FK → CycleLog; typed via SymptomType enum; severity 1–5
│   │   │       │   -- Quiz domain --
│   │   │       ├── QuizQuestion.cs
│   │   │       ├── QuizAnswer.cs
│   │   │       ├── UserQuizResponse.cs     # FK → UserProfileSnapshot (set on quiz completion)
│   │   │       │   -- Nutrition domain --
│   │   │       ├── FoodItem.cs             # 12 micronutrient fields; IsCustom; FdcId; LastSyncedAt
│   │   │       ├── FoodItemVersion.cs      # Nutrient history before each FDC upsert
│   │   │       ├── FoodSyncLog.cs          # Per-sync-run audit: status, counts, errors
│   │   │       ├── MealTemplate.cs         # Saved recipe; IsSystem for app-seeded templates
│   │   │       ├── MealTemplateEntry.cs    # Ingredient row in a template
│   │   │       ├── LoggedMeal.cs           # What the user actually ate; optional FK → MealTemplate
│   │   │       ├── LoggedMealEntry.cs      # Individual food item + grams within a logged meal
│   │   │       │   -- Workout domain --
│   │   │       ├── Exercise.cs             # JointAreas for substitution logic
│   │   │       ├── WorkoutPlan.cs          # CyclePhaseTarget; IsActive
│   │   │       ├── WorkoutDay.cs
│   │   │       ├── WorkoutDayExercise.cs   # Sets, Reps, DurationSeconds per exercise
│   │   │       ├── WorkoutLog.cs           # Completion record: date, CompletionPercent, notes
│   │   │       │   -- Recommendation domain --
│   │   │       ├── RecommendationSet.cs            # One per user per day; CyclePhase; ExpiresAt
│   │   │       ├── NutritionRecommendation.cs      # FK → MealTemplate; ActedOnAt + FK → LoggedMeal
│   │   │       ├── WorkoutRecommendation.cs        # FK → WorkoutDay; ActedOnAt + FK → WorkoutLog
│   │   │       └── WellnessRecommendation.cs       # Category enum; advice text; ActedOnAt
│   │   │
│   │   ├── Data/
│   │   │   ├── AppDatabase.cs              # EF Core DbContext; all 22 DbSets; quiz + meal seed
│   │   │   └── IDbPathProvider.cs
│   │   │
│   │   ├── Repositories/
│   │   │   ├── IRepository.cs / BaseRepository.cs
│   │   │   │   -- User domain --
│   │   │   ├── IUserRepository.cs / UserRepository.cs
│   │   │   │   -- Cycle domain --
│   │   │   ├── ICycleRepository.cs / CycleRepository.cs
│   │   │   ├── ISymptomRepository.cs / SymptomRepository.cs
│   │   │   │   -- Quiz domain --
│   │   │   ├── IQuizRepository.cs / QuizRepository.cs
│   │   │   │   -- Nutrition domain --
│   │   │   ├── INutritionRepository.cs / NutritionRepository.cs    # FoodItem + LoggedMeal
│   │   │   ├── IMealTemplateRepository.cs / MealTemplateRepository.cs
│   │   │   ├── IFoodSyncRepository.cs / FoodSyncRepository.cs      # FoodSyncLog + FoodItemVersion
│   │   │   │   -- Workout domain --
│   │   │   ├── IWorkoutRepository.cs / WorkoutRepository.cs        # Plan + Day + Exercise + Log
│   │   │   │   -- Recommendation domain --
│   │   │   └── IRecommendationRepository.cs / RecommendationRepository.cs
│   │   │
│   │   ├── Services/
│   │   │   ├── IAuthService.cs
│   │   │   ├── ICycleService.cs / CycleService.cs
│   │   │   ├── ICyclePhaseCalculator.cs / CyclePhaseCalculator.cs  # Phase logic isolated here
│   │   │   ├── ICalorieCalculator.cs / CalorieCalculator.cs        # Mifflin-St Jeor BMR
│   │   │   ├── INutritionService.cs / NutritionService.cs
│   │   │   └── IQuizService.cs / QuizService.cs
│   │   │
│   │   └── ViewModels/
│   │       ├── LoginViewModel.cs, RegisterViewModel.cs
│   │       ├── QuizViewModel.cs, ProfileSetupViewModel.cs
│   │       ├── DashboardViewModel.cs
│   │       ├── CycleViewModel.cs       (+ CycleDayItem, PhaseItem)
│   │       ├── WorkoutViewModel.cs     (+ WorkoutItem, FilterChipItem)
│   │       ├── NutritionViewModel.cs   (+ MealItem)
│   │       ├── ProfileViewModel.cs     (+ PreferenceItem)
│   │       └── SelectableQuizAnswer.cs
│   │
│   └── MuscleCuties.App/               # MAUI host; platform code only
│       ├── App.xaml(.cs)               # Startup routing: auth check → LoginPage or AppShell
│       ├── AppShell.xaml(.cs)          # 5-tab TabBar + auth/onboarding route registrations
│       ├── MauiProgram.cs              # DI container setup, font registration
│       ├── Services/
│       │   ├── MauiDbPathProvider.cs
│       │   └── SecureStorageService.cs
│       ├── Pages/
│       │   ├── Auth/         LoginPage, RegisterPage
│       │   ├── Onboarding/   QuizPage, ProfileSetupPage
│       │   ├── Dashboard/    DashboardPage
│       │   ├── Cycle/        CyclePage
│       │   ├── Workout/      WorkoutPage
│       │   ├── Nutrition/    NutritionPage
│       │   └── Profile/      ProfilePage
│       └── Resources/
│           ├── Converters/   CyclePhaseToBrushConverter, ReadinessScoreToColorConverter,
│           │                 RecoveryScoreToColorConverter
│           ├── Fonts/        Nunito-Variable.ttf, Fraunces-Variable.ttf
│           ├── Images/       tab_today.svg, tab_cycle.svg, tab_train.svg,
│           │                 tab_nutrition.svg, tab_you.svg
│           └── Styles/       Colors.xaml, Styles.xaml
│
└── tests/
    └── MuscleCuties.Core.Tests/
        ├── DatabaseFixture.cs          # Shared SQLite in-memory context
        ├── Repositories/               # UserRepository, CycleRepository, NutritionRepository,
        │                               # SymptomRepository, QuizRepository, MealTemplateRepository,
        │                               # FoodSyncRepository, RecommendationRepository tests
        ├── Services/                   # CalorieCalculator, CyclePhaseCalculator, CycleService,
        │                               # NutritionService, QuizService tests
        └── ViewModels/                 # Login, Register, Dashboard, Quiz, ProfileSetup,
                                        # Nutrition, Cycle, Workout, Profile ViewModel tests
```

---

## Architecture

The app follows MVVM with constructor-injected dependencies throughout. Business logic lives entirely in `MuscleCuties.Core`; `MuscleCuties.App` contains only MAUI-specific wiring and platform code.

### Startup flow
1. `App.CreateWindow` fires `InitializeStartupAsync`
2. `IAuthService.IsLoggedInAsync` checks `SecureStorage` for a persisted session token
3. Logged-out → window root set to `LoginPage` (plain `ContentPage`, no Shell)
4. Logged-in → window root set to `AppShell` (tabbed Shell)
5. After successful login/registration, `App.ShowAuthenticatedRoot()` swaps the window page to `AppShell`

### Navigation
- Tab pages (Dashboard, Cycle, Workout, Nutrition, Profile) are declared in the Shell `<TabBar>` and auto-registered as routes
- Auth and onboarding pages are registered via `Routing.RegisterRoute` and pushed modally or as shell routes

### Bounded contexts
Six domains. Each owns its entity files. Cross-domain reads go through service interfaces only — no direct EF `Include()` across domain boundaries.

| Domain | Owns | Exposes |
|---|---|---|
| User | User, UserProfile, UserProfileSnapshot | IUserRepository |
| Cycle | CycleLog, SymptomLog | ICycleRepository, ISymptomRepository, ICycleService |
| Quiz | QuizQuestion, QuizAnswer, UserQuizResponse | IQuizRepository, IQuizService |
| Nutrition | FoodItem, FoodItemVersion, FoodSyncLog, MealTemplate, MealTemplateEntry, LoggedMeal, LoggedMealEntry | INutritionRepository, IMealTemplateRepository, IFoodSyncRepository, INutritionService |
| Workout | Exercise, WorkoutPlan, WorkoutDay, WorkoutDayExercise, WorkoutLog | IWorkoutRepository |
| Recommendation | RecommendationSet, NutritionRecommendation, WorkoutRecommendation, WellnessRecommendation | IRecommendationRepository |

### Recommendation feedback loop
`RecommendationService` generates one `RecommendationSet` per user per day (re-generated if stale). Each typed child (`NutritionRecommendation`, `WorkoutRecommendation`, `WellnessRecommendation`) carries an `ActedOnAt` timestamp and a FK back to the log row that satisfied it. This gives the future AI layer supervised labels: what was recommended, whether it was followed, and how quickly.

### Data flow
ViewModels → Services (async calls) → Repository interfaces → EF Core / SQLite
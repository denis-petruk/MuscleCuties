# MuscleCuties

A cycle-aware fitness and nutrition companion for women. MuscleCuties syncs workout intensity, nutrition targets, and recovery guidance to the four phases of the menstrual cycle, delivering a personalized daily plan that respects how the body actually feels and performs throughout the month.

---

## Features

### Onboarding
- Email / password registration with secure token storage
- 12-question onboarding quiz capturing fitness goal, experience level, training frequency, dietary preference, and per-phase symptom baselines (pain and energy for all four cycle phases)
- Profile setup: name, date of birth, height, weight, unit preference (metric/imperial)

### Today — Dashboard
- Time-aware greeting and date header
- Active cycle phase badge with short contextual advice
- Today's workout card: title, subtitle, duration, exercise count, intensity, and session progress within the week
- Readiness & Recovery rings with color-coded scores (green ≥ 70, amber 40–69, red < 40)
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

### Nutrition
- Phase focus banner with cycle-phase color
- Today's balance card: calorie row with progress bar, macro grid (protein, carbs, fat)
- Meals list: time badge, meal type tag, name, calories

### You — Profile
- Avatar circle with user initial
- Stats grid: sessions, cycle days, phases tracked
- Preferences list with chevron nav

---

## Current State (MVP)

The full architecture — repositories, services, and data layer — is in place and wired through DI. ViewModels currently bind to stub data while the service → ViewModel integration is being completed. The database is wiped and reseeded on every launch (intentional for this phase — no persistence requirement yet).

A `FloAccessToken` placeholder exists on the `User` model for a future Flo API integration; it is not connected to anything. Recommendations are locally generated (rule-based by cycle phase).

Calorie targets support two modes: `Calculated` (BMR-derived via Mifflin-St Jeor, using date of birth and body metrics) and `Manual` (user-entered fixed target). Weight goal pace is configurable: `Steady` (~250–300 kcal delta) or `Aggressive` (~500 kcal delta).

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

### First launch
The database is created and seeded with quiz questions automatically on first run via `AppDatabase.InitializeAsync()`. Credentials are stored in platform `SecureStorage`.

---

## Project Layout

```
MuscleCuties/
├── App.xaml(.cs)               # Startup routing: auth check → LoginPage or AppShell
├── AppShell.xaml(.cs)          # 5-tab TabBar + auth/onboarding route registrations
├── MauiProgram.cs              # DI container setup, font registration
│
├── Models/                     # EF Core entities + enums
│   ├── Enums/                  # CyclePhase, UserGoal, DietaryTag, MealType, MuscleGroup,
│   │                           # CalorieMode, WeightGoalPace, QuizQuestionType, …
│   ├── User.cs
│   ├── UserProfile.cs          # Age computed from DateOfBirth; ExperienceLevel, CalorieMode,
│   │                           # ManualTargetCalories, WeightGoalPace
│   ├── UserBaselineProfile.cs  # Quiz-derived per-phase baselines: pain + energy × 4 phases
│   ├── CycleLog.cs
│   ├── SymptomLog.cs           # Pain, Energy, Sleep, Bloating, Appetite, Stress, Notes
│   ├── Exercise.cs             # JointAreas for substitution logic (Shoulder, Knee, …)
│   ├── WorkoutExercise.cs      # RestSeconds per set
│   ├── WorkoutPlan / WorkoutDay
│   ├── FoodItem.cs             # Iron (mg/100g), VitaminB12 (µg/100g)
│   ├── FoodLog.cs
│   ├── DailyRecommendation.cs  # TargetIron, TargetVitaminB12
│   └── QuizQuestion / QuizAnswer / UserQuizResponse
│
├── Data/
│   └── AppDatabase.cs          # EF Core DbContext + 12-question quiz seed data
│
├── Repositories/               # Typed interfaces and implementations
│   ├── IUserRepository / UserRepository
│   ├── ICycleRepository / CycleRepository
│   ├── ISymptomRepository / SymptomRepository
│   ├── IWorkoutRepository / WorkoutRepository
│   ├── INutritionRepository / NutritionRepository
│   └── IQuizRepository / QuizRepository
│
├── Services/                   # Business logic layer
│   ├── IAuthService / AuthServices
│   ├── ICycleService / CycleService
│   ├── INutritionService / NutritionService
│   └── IQuizService / QuizService
│
├── ViewModels/
│   ├── Auth/         LoginViewModel, RegisterViewModel
│   ├── Onboarding/   QuizViewModel, ProfileSetupViewModel
│   ├── Dashboard/    DashboardViewModel
│   ├── Cycle/        CycleViewModel  (+ CycleDayItem, PhaseItem)
│   ├── Workout/      WorkoutViewModel (+ WorkoutItem, FilterChipItem)
│   ├── Nutrition/    NutritionViewModel (+ MealItem)
│   └── Profile/      ProfileViewModel (+ PreferenceItem)
│
├── Pages/
│   ├── Auth/         LoginPage, RegisterPage
│   ├── Onboarding/   QuizPage, ProfileSetupPage
│   ├── Dashboard/    DashboardPage
│   ├── Cycle/        CyclePage
│   ├── Workout/      WorkoutPage
│   ├── Nutrition/    NutritionPage
│   └── Profile/      ProfilePage
│
└── Resources/
    ├── Converters/   CyclePhaseToBrushConverter, ReadinessScoreToColorConverter,
    │                 RecoveryScoreToColorConverter
    ├── Fonts/        Nunito-Variable.ttf, Fraunces-Variable.ttf
    ├── Images/       tab_today.svg, tab_cycle.svg, tab_train.svg,
    │                 tab_nutrition.svg, tab_you.svg
    └── Styles/       Colors.xaml, Styles.xaml
```

---

## Architecture

The app follows MVVM with constructor-injected dependencies throughout.

**Startup flow:**
1. `App.CreateWindow` fires `InitializeStartupAsync`
2. `IAuthService.IsLoggedInAsync` checks `SecureStorage` for a persisted session token
3. Logged-out → window root set to `LoginPage` (plain `ContentPage`, no Shell)
4. Logged-in → window root set to `AppShell` (tabbed Shell)

**Navigation:**
- Tab pages (Dashboard, Cycle, Workout, Nutrition, Profile) are declared in the Shell `<TabBar>` and auto-registered as routes
- Auth and onboarding pages are registered via `Routing.RegisterRoute` and pushed modally or as shell routes
- After successful login/registration the app calls `App.ShowAuthenticatedRoot()` which swaps the window page to `AppShell`

**Data flow:**
- ViewModels receive data from Services via async calls
- Services use Repository abstractions backed by EF Core / SQLite
- Quiz responses are stored in `UserQuizResponse` and rolled up into `UserBaselineProfile` to drive phase-aware recommendations

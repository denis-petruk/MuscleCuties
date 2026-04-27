# MuscleCuties — Technical Stack

---

## Platform & Framework

| Item | Version |
|---|---|
| .NET | 10 |
| .NET MAUI | 10.0.10 |
| XAML Inflator | Source generator (`MauiXamlInflator=SourceGen`) |
| Target frameworks | `net10.0-android`, `net10.0-ios`, `net10.0-maccatalyst` |
| Min Android API | 21 (Android 5.0) |
| Min iOS / Mac Catalyst | 15.0 |
| Nullable | Enabled |
| Implicit usings | Enabled |

---

## Dependencies

| Package | Version | Role |
|---|---|---|
| `Microsoft.Maui.Controls` | 10.0.10 | UI framework |
| `CommunityToolkit.Maui` | 13.0.0 | `StringToBoolConverter`, `InvertedBoolConverter`, `UseMauiCommunityToolkit()` |
| `CommunityToolkit.Mvvm` | 8.4.0 | `[ObservableProperty]`, `[RelayCommand]`, `[NotifyPropertyChangedFor]` |
| `Microsoft.EntityFrameworkCore.Sqlite` | 10.0.5 | Local database ORM |
| `Microsoft.EntityFrameworkCore.Design` | 10.0.5 | EF Core tooling (build-time only) |
| `SQLite` | 3.13.0 | SQLite native driver |
| `Microsoft.Extensions.Logging.Debug` | 10.0.0 | Debug-mode logging |

---

## Architecture

### Pattern: MVVM + Repository + Service

```
View (XAML)
  └── ViewModel (CommunityToolkit.Mvvm)
        └── Service (business logic)
              └── Repository (data access)
                    └── AppDatabase (EF Core / SQLite)
```

- Views bind directly to ViewModel properties via `x:DataType` (compiled bindings)
- ViewModels are injected into pages via constructor DI; `BindingContext` is set in code-behind
- Services hold all business logic (phase calculation, nutrition targets, quiz evaluation)
- Repositories are thin wrappers over EF Core `DbSet<T>` with typed query methods
- All service and repository dependencies are registered as `Transient` in `MauiProgram.cs`

### Dependency Injection

Registration in `MauiProgram.CreateMauiApp()`:

```
Database:     AppDatabase (DbContext, Transient)

Repositories: IUserRepository       → UserRepository
              ICycleRepository      → CycleRepository
              ISymptomRepository    → SymptomRepository
              IWorkoutRepository    → WorkoutRepository
              INutritionRepository  → NutritionRepository
              IQuizRepository       → QuizRepository

Services:     IAuthService          → AuthService
              ICycleService         → CycleService
              INutritionService     → NutritionService
              IQuizService          → QuizService

Shell:        AppShell
Pages:        LoginPage, RegisterPage, ProfileSetupPage, QuizPage,
              DashboardPage, CyclePage, WorkoutPage, NutritionPage, ProfilePage
ViewModels:   LoginViewModel, RegisterViewModel, ProfileSetupViewModel, QuizViewModel,
              DashboardViewModel, CycleViewModel, WorkoutViewModel,
              NutritionViewModel, ProfileViewModel
```

### Startup Flow

```
App.CreateWindow()
  └── InitializeStartupAsync()
        ├── IAuthService.IsLoggedInAsync()  ← SecureStorage token check
        ├── [not logged in]  → window.Page = LoginPage
        └── [logged in]      → IUserRepository.GetByIdAsync()
                                  ├── [user missing] → logout → LoginPage
                                  └── [ok]           → window.Page = AppShell
```

After successful login/registration, `App.ShowAuthenticatedRoot()` swaps the window page to `AppShell` without a full app restart.

---

## Data Layer

### Database

`AppDatabase` extends `EF Core DbContext`. The SQLite file is stored in `FileSystem.AppDataDirectory` (`musclecuties.db`). Schema is created with `Database.EnsureCreatedAsync()` on first launch.

On every launch the existing database is deleted and recreated (`InitializeAsync` always deletes and reseeds). This is intentional for the current MVP phase — persistence between sessions is not yet a requirement.

### Entities

| Entity | Key relations | Purpose |
|---|---|---|
| `User` | has many `CycleLog`, `FoodLog`, `SymptomLog`, `UserQuizResponse` | Authentication identity |
| `UserProfile` | belongs to `User` | Name, age, height, weight, goal, cycle length, dietary preference |
| `UserBaselineProfile` | derived from quiz responses | Per-phase symptom baselines (pain/energy/mood scores 1–5) used for recommendation engine |
| `QuizQuestion` | has many `QuizAnswer` | Seeded at startup; drives onboarding quiz flow |
| `QuizAnswer` | `MappedValue: int` | Integer that maps to an enum or scale value |
| `UserQuizResponse` | belongs to `User` + `QuizQuestion` + `QuizAnswer` | Records which answer the user selected |
| `CycleLog` | belongs to `User` | Start/end date of a cycle; used for phase calculation |
| `SymptomLog` | belongs to `User` | Daily symptom entry (pain, energy, mood, notes) |
| `WorkoutPlan` | has many `WorkoutDay` | A named training plan |
| `WorkoutDay` | has many `WorkoutExercise` | One day within a plan |
| `Exercise` | referenced by `WorkoutExercise` | Exercise library entry (name, muscle group, instructions) |
| `WorkoutExercise` | join of `WorkoutDay` + `Exercise` | Sets / reps / rest prescribed for a given day |
| `FoodItem` | referenced by `FoodLog` | Nutrition database entry (kcal, protein, carbs, fat per 100 g) |
| `FoodLog` | belongs to `User` + `FoodItem` | What the user ate, when, and how much |
| `DailyRecommendation` | belongs to `User` | Phase-aware daily guidance generated by the recommendation engine |

### Key Enums

| Enum | Values |
|---|---|
| `CyclePhase` | `Menstrual`, `Follicular`, `Ovulatory`, `Luteal` |
| `UserGoal` | `FatLoss`, `MuscleTone`, `Strength`, `MaintainHealth` |
| `DietaryTag` | `None`, `Vegetarian`, `Vegan`, `GlutenFree`, `LactoseFree` |
| `MealType` | Breakfast, Lunch, Dinner, Snack |
| `MuscleGroup` | Upper, Lower, Core, FullBody, Cardio, … |
| `QuizQuestionType` | `Goal`, `ExperienceLevel`, `WorkoutDaysPerWeek`, `DietaryPreference`, `MenstrualPain`, `MenstrualEnergy`, `MenstrualMood`, `FollicularPain`, … (3 per phase × 4 phases + 4 general) |

### Repository Pattern

```csharp
// Base interface
interface IRepository<T>

// Base implementation handles common queries
class BaseRepository<T> : IRepository<T>

// Typed repositories extend with domain-specific queries
interface ICycleRepository : IRepository<CycleLog>
class CycleRepository : BaseRepository<CycleLog>, ICycleRepository
```

---

## Services

### `IAuthService` / `AuthService`
- Register and login with email + password
- Session token stored in `SecureStorage`
- `IsLoggedInAsync()` / `GetCurrentUserIdAsync()` / `LogoutAsync()`

### `ICycleService` / `CycleService`
- `GetCurrentPhaseAsync(userId)` — queries latest `CycleLog`, calculates day, returns `CyclePhase`
- `CalculatePhase(cycleDay, cycleLength)` — local phase mapping logic:
  - Days 1–5: Menstrual
  - Days 6–(length×0.46): Follicular
  - Days around ovulation: Ovulatory
  - Remainder: Luteal
- `StartNewCycleAsync` / `EndCurrentCycleAsync`

### `INutritionService` / `NutritionService`
- Phase-aware calorie and macro target calculation
- Food log queries for daily totals

### `IQuizService` / `QuizService`
- Load all quiz questions with answers (ordered)
- Save user quiz responses
- Build `UserBaselineProfile` from responses

---

## Authentication & Session

- Credentials are validated against the `Users` table in SQLite
- On success, a session identifier is written to `SecureStorage` (platform keychain on iOS/Mac, Android Keystore on Android)
- No remote auth — fully local in the current MVP

---

## XAML Patterns

### Compiled Bindings
All pages declare `x:DataType` for full compile-time binding verification:
```xml
<ContentPage x:DataType="vm:DashboardViewModel">
```

### ObservableProperty Source Generation
```csharp
[ObservableProperty]
private string _greetings = "Good evening, lovely";
// generates: public string Greetings { get; set; }
```

### RelayCommand Source Generation
```csharp
[RelayCommand]
private async Task RefreshAsync() { ... }
// generates: public AsyncRelayCommand RefreshCommand { get; }
```

### NotifyPropertyChangedFor
Used to invalidate computed properties when their dependencies change:
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(SessionProgressText))]
private int _completedWorkoutsThisWeek;

public string SessionProgressText =>
    $"Today · session {CompletedWorkoutsThisWeek} / {WeeklyWorkoutGoal}";
```

### AppThemeBinding
Light/dark theming applied inline throughout — no code-behind theme detection required:
```xml
    BackgroundColor="{AppThemeBinding
    Light={StaticResource PageBackground},
    Dark={StaticResource PageBackgroundDark}}"
```

---

## Resources

### Fonts
Both fonts are variable TTF files registered in `MauiProgram.cs`:

```csharp
fonts.AddFont("Nunito-Variable.ttf",   "NunitoRegular");
fonts.AddFont("Fraunces-Variable.ttf", "FrauncesDisplay");
```

Weight variation is handled by `FontAttributes="Bold"` — the variable font responds to the OS weight axis automatically.

### Images
SVG files in `Resources/Images/` are declared as `<MauiImage>` in the `.csproj`. At build time MAUI transcodes each SVG to platform-native raster formats (PNG at multiple densities for Android/iOS).

### Styles & Colors
- `Resources/Styles/Colors.xaml` — all color and brush tokens
- `Resources/Styles/Styles.xaml` — implicit control styles + named style keys
- Both are merged in `App.xaml` `ResourceDictionary.MergedDictionaries`

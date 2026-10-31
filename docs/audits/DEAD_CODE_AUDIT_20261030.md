# Dead code, unused surface and magic-number audit — 2026-10-30

Status: **READ-ONLY AUDIT — NO SOURCE CHANGED.** This document is the Phase 1 deliverable of the
approved plan. It records what this checkout actually contains, what is provably unreachable, what
only *looks* unreachable, and what must be deleted, wired, or kept. No production file, migration,
test, secret or environment was modified.

Checkout audited: `main` @ `97677ca4`, working tree dirty with five modified tracked files and several
untracked paths (see §1.2). Date: 2026-10-30.

Companion artifacts:

| File | What it is |
| --- | --- |
| [`dead-code-manifest.csv`](dead-code-manifest.csv) | One row per file or member: cluster, evidence, action, replacement, test impact, confidence, risk |
| [`ui-wiring-register.md`](ui-wiring-register.md) | For every server surface with no client caller: what wiring to the UI would require, and the alternative cost |
| [`magic-numbers-register.csv`](magic-numbers-register.csv) | File-level literal inventory with proposed named-constant families and sample lines |
| [`OBSOLETE_CLAIMS.md`](OBSOLETE_CLAIMS.md) | Claims in the two branch-specific documents that this checkout does not support |

---

## 1. Baseline

### 1.1 Shape

| Fact | Value | How measured |
| --- | --- | --- |
| Branch / HEAD | `main` @ `97677ca4` | `git rev-parse --abbrev-ref HEAD`; `git log -1` |
| Projects | 12 in `MuscleCuties.sln` | `grep -oE '"[^"]+\.csproj"' MuscleCuties.sln` |
| C# files | 989 (excluding `bin/`, `obj/`) | `find src services tests -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' \| wc -l` |
| Public type declarations | 1,468 across Core (287), UseCases (580), Infrastructure (345), Infrastructure.PostgreSql (213), Server (43) | `grep -rhoE "public (sealed \|abstract \|static \|partial )*(class\|record\|interface\|enum\|struct) ..."` |
| Client DI registrations | 200 | `grep -cE 'Add(Singleton\|Scoped\|Transient)' src/MuscleCuties.UI/MauiProgram.cs` |
| Server DI registrations | 110 | `grep -cE 'Add(Singleton\|Scoped\|Transient)' services/MuscleCuties.Server/ApiApplication.cs` |
| Mapped server endpoints | 142 `.Map*` calls; 44 absolute `/api/v1` literals; 16 group prefixes | `grep -rhoE '\.Map(Get\|Post\|Put\|Delete\|Patch)\('` |
| Client route literals | 58 unique `/api/...` strings | `grep -rhoE '"/?api[^"]*"' src/MuscleCuties.Infrastructure` |
| UI pages / view-model files | 26 `.xaml` pages, 44 view-model files, 26 page↔view-model pairs | `find src/MuscleCuties.UI/Pages -name '*.xaml'` |
| Tests | 5 projects, 56 files, 249 `[Fact]`/`[Theory]` methods (Core 180, Server 46, Registration 7, Registration.Integration 2, Mobile.Contracts 14 via `[Xunit.*]`) | `grep -rho '\[Fact\]\|\[Theory\]'` per project |

### 1.2 Working tree at audit time

```text
 M .graphifyignore
 M services/postgresql/README.md
 M services/postgresql/roles-and-grants.sql
 M src/MuscleCuties.Infrastructure.PostgreSql/Persistence/Migrations/20261018025413_AddPhotoCleanupDispatchCursor.cs
 M src/MuscleCuties.Infrastructure.PostgreSql/Persistence/Migrations/20261018030000_AddPhotoObjectReconcileTargets.cs
 M src/MuscleCuties.Infrastructure.PostgreSql/Persistence/Migrations/20261018030500_AddAccountPhotoPurgeStageBridge.cs
 M tools/graphify/figures.py
?? .codex/
?? docs/RELEASE_INVENTORY_20261030.md
?? docs/orchestration/
?? graphify-out/
?? services/postgresql/bootstrap.sql
?? tests/MuscleCuties.Registration.Integration.Tests/PostgreSqlBootstrapMigrationTests.cs
```

These paths are in-flight work and were not touched. The manifest excludes them. The tree kept moving
while this audit was written: `README.md` and `src/MuscleCuties.UI/MuscleCuties.UI.csproj` were also
modified by concurrent work later in the session. Every row in the manifest remains scoped to files
this audit owns.

### 1.3 Gates

There is **no pipeline file** in the repository root (no `.gitlab-ci.yml`, no `.github/workflows/`),
no analyzer package reference, no `TreatWarningsAsErrors`, and `.editorconfig` explicitly sets:

```ini
resharper_unused_auto_property_accessor_global_highlighting = none
resharper_unused_auto_property_accessor_local_highlighting = none
```

So unused members were invisible to both the IDE and the build. That is the root cause of everything
in §4.

---

## 2. Method

Candidate generation was static and read-only. The order matters because each step filters the previous
one.

1. **Type census.** Extract every public type declaration in the five backend projects, then count the
   distinct files that mention each type name across `src/`, `services/` and `tests/`.
2. **DI graph.** Extract every `Add*` registration, then search for constructor injection and
   `GetRequiredService`/`GetService` call sites, and resolve the concrete type's own constructor
   dependencies.
3. **UI reachability.** For each of the 26 pages, check (a) `ShellContent` roots, (b) `Routing.RegisterRoute`
   plus a `GoToAsync`/`NavigateToAsync` target, (c) command-parameter routes, (d) `MauiProgram` navigation
   delegates. Separately, for each `[ObservableProperty]` field, derive the generated property name and
   search every XAML binding and C# reference.
4. **Member scan.** Private members, with `[RelayCommand]` awareness: the attribute generates a command
   from the method name, so a method can be live without its name appearing anywhere.
5. **Route parity.** Client literals plus interpolated route construction (`$"{NutritionRoute}/recipes"`)
   against server route constants and group prefixes.
6. **Localization.** Every catalog key against XAML markup, `UiTextCatalog.Get("...")`, and string
   constants in C#.
7. **Literal density.** Numeric literals outside `const` declarations, per file.

### 2.1 False positives that must not drive deletion

| Class | Example | Why the name count lies |
| --- | --- | --- |
| EF configuration discovery | 86 `*Configuration` types in Infrastructure.PostgreSql | `modelBuilder.ApplyConfigurationsFromAssembly` (`PostgresDbContext.cs:81`) constructs them by reflection |
| Extension-method containers | 39 endpoint-map classes in Server | Called as `app.MapNutritionDiaryEndpoints()`, which never names the class |
| Source-generated commands | 11 `WorkoutSessionViewModel` methods | `[RelayCommand]` generates `StartActivityCommand`, `NextExerciseCommand`, ...; bound in `WorkoutSessionPage.xaml` |
| Target-typed `new` / `var` / structural types | `DashboardSupportResult`, `PortionResult`, `GeneratedWorkoutDay` | Returned and consumed structurally; zero name references |
| XAML markup extensions | `LocalizedText`, `StaticResource` | Types resolved from XAML namespace declarations |
| Runtime translation table | 1,397 hashed `T...` keys | Reached through `UiTextCatalog.Translate(english)` from `LocalizedStringConverter` |

**Consequence:** a zero-name-reference is a *candidate*, never a verdict. Every deletion in the manifest
is either already proven by a second signal (no registration, no XAML binding, no navigation edge, no
caller of any kind) or is marked `verify-at-delete` and requires the compiler proof described in §7.

---

## 3. Findings summary

| ID | Cluster | Confidence | Size | Recommendation |
| --- | --- | --- | --- | --- |
| F1 | Workout History page and archive slice | Confirmed | 1 page, 1 VM, 5 production types, 3 UI models | Delete page/client slice; keep server `.../count` |
| F2 | Fasting timer (P06) | Confirmed UI removal; product decision needed | 4 VM properties, 1 use-case file, 1 store, 1 domain file, 10 routes | Wire the existing UI hooks, or delete the slice |
| F3 | Water reminders and hydration presets | Confirmed | 1 dead use case, 3 VM properties, 13 routes | Wire a water screen, or delete the slice |
| F4 | Body circumference measurements | Confirmed | 4 VM properties, 1 use-case file, 1 store, 4 routes | Wire the existing VM properties into the profile page |
| F5 | Legacy account/auth layer | Confirmed | 10 files + 6 result contracts | Delete |
| F6 | Legacy repository layer | Confirmed dead concretes; consumers entangled | 17 files, 8 concrete classes | Delete with the services that host them |
| F7 | Readiness/progress leftovers | Confirmed | 9 files | Delete |
| F8 | V02 planning rules + their tests | Confirmed | 5 rules, 32 test cases | Delete rule + test together |
| F9 | Dead members | Confirmed | 3 private methods | Delete |
| F10 | Orphan contracts | Confirmed (5 structural exceptions) | 11 types | Delete with their clusters |
| F11 | UI content | Confirmed | 187 keys, 79 legacy entries, 1 image | Delete |
| F12 | Uncalled server surface | Confirmed | 14 route families | Wire or delete per §5 |
| F13 | Unconstructable DI graph | Statically proven; runtime proof pending | 8 missing registrations | Fix before deleting anything else |
| F14 | Magic numbers | Confirmed | 15 files above 20 inline literals | Whole-solution constant extraction |
| F15 | No gates | Confirmed | 0 analyzers, 0 pipelines | Add gates |

---

## 4. Findings

### F1. `WorkoutHistoryPage` is the only unreachable page

All 26 pages were checked for an inbound edge. Twenty-five have one of: a `ShellContent` root, a
`Routing.RegisterRoute` plus a navigation delegate in `MauiProgram`, a command-parameter route, or a
`GoToAsync` call. `WorkoutHistoryPage` has only the route registration and the DI registration:

```text
src/MuscleCuties.UI/AppShell.xaml.cs:65:  Routing.RegisterRoute(nameof(WorkoutHistoryPage), ...)
src/MuscleCuties.UI/MauiProgram.cs:687:  services.AddTransient<WorkoutHistoryPage>();
```

No `GoToAsync`, `NavigateToAsync`, `CommandParameter`, shell route or string literal anywhere in `src/`
reaches it. Every other page has such an edge, including `ProfileHealthSyncPage` (via
`ProfileDetailsContent.xaml`) and `DailyCheckInPage` (via the `DashboardViewModel` factory delegate).

The whole client slice is therefore dead:

| Component | File |
| --- | --- |
| Page + code-behind | `src/MuscleCuties.UI/Pages/Workout/WorkoutHistoryPage.xaml(.cs)` |
| View model | `src/MuscleCuties.UI/ViewModels/Workout/WorkoutHistoryViewModel.cs` |
| UI models | `src/MuscleCuties.UI/Models/UI/Workout/{MonthFilterItem,EditableExerciseSetItem,WorkoutArchiveUiModels}.cs` |
| Use case + store port | `src/MuscleCuties.UseCases/Features/Workout/Archive/{WorkoutArchiveUseCase,IWorkoutArchiveStore}.cs` |
| Client store | `CloudWorkoutArchiveStore` in `CloudNutritionAndWorkoutStores.cs:505` |
| Legacy service | `src/MuscleCuties.Infrastructure/Services/Workout/{IWorkoutArchiveService,WorkoutArchiveService}.cs` |

**Do not delete the server count route.** `GET /api/v1/workouts/history/count` and
`GET .../month/{year}/{month}/count` are live through
`LocalProgressSummaryStore.cs:14` and `CloudAccountAndProfileStores.cs:503`. Both are served by
`ServerWorkoutArchiveUseCase` on top of `PostgresWorkoutArchiveStore`, so those three stay. The list,
month, session-detail, notes, set-update and personal-record routes are dead with the page.

### F2–F4. Dead feature slices: the server and domain remain, the UI is gone

| Slice | UI evidence | Backend evidence |
| --- | --- | --- |
| Fasting (P06) | `ProfilePersonalInfoPage.xaml` has **zero** `Fasting` elements; no `Fasting_*` key is used in any XAML | 4 orphan VM properties (`ProfilePersonalInfoViewModel.cs:341-344`); `/api/v1/fasting` group with 10 routes; `PostgresFastingStore`; `FastingSessionUseCase`; `Core/Domain/Fasting/FastingSession.cs` |
| Water reminders | `FoodLogViewModel.cs:603-605` declares `WaterReminderOptedIn/QuietStart/QuietEnd`; no XAML binding | `/api/v1/hydration` group with 13 routes; `PostgresHydrationStore`; `PreviewWaterReminderUseCase` has no caller |
| Body circumference | `ProfilePersonalInfoViewModel.cs:334-337` declares 4 properties; no XAML binding | `/api/v1/progress/measurements` group with 4 routes; `PostgresBodyMeasurementStore`; four body-measurement use cases |

The view-model properties are not "just unused" in isolation: they are the only remaining client hooks
for features whose pages were removed. The product decision is binary, and both costs are in the
wiring register:

- **Wire**: add the XAML section, bind the properties, inject the use case, add client methods and
  tests.
- **Delete**: remove the properties, the use-case file, the store and the routes.

Nothing in this checkout proves the feature was intentionally abandoned; the fasting policy comment
("Policy P06-01-v0") and the server code suggest it was built and then hidden.

### F5. Legacy account/auth layer

Zero references outside its own files and no registrations:

```text
Infrastructure/Services/Auth/AuthService.cs                    (line 10)
Infrastructure/Services/Auth/OnboardingValidator.cs            (line 7)
Infrastructure/Services/Auth/IEmailChallengeService.cs         (line 22)
Infrastructure/Services/Auth/IPasswordRecoveryService.cs       (line 20)
Infrastructure/Services/Auth/IOnboardingCommitService.cs       (line 11)
Infrastructure/Services/Auth/EmailReceiptVerifier.cs           (lines 10, 17)
Infrastructure/Account/Recovery/LegacyAccountRecoveryStore.cs  (line 8)
Infrastructure/Account/Registration/LegacyAccountRegistrationGateway.cs (line 11)
Infrastructure/Account/SignIn/LegacyAccountSignInStore.cs      (line 9)
Infrastructure/Account/Otp/LegacyRegistrationOtpGateway.cs     (line 9)
```

`LegacyAccountRegistrationGateway`, `LegacyRegistrationOtpGateway`, `LegacyAccountRecoveryStore` and
`LegacyAccountSignInStore` are the only consumers of `IEmailChallengeService` and
`IPasswordRecoveryService`, so the four interfaces die together. Result contracts die with them:
`EmailChallengeVerificationResult`, `OnboardingCommitResult`, `PasswordRecoveryStartResult`,
`PasswordRecoveryVerifyResult`, `PasswordResetResult`, `VerifiedEmailReceipt`.

`LocalProfileSetupStore` (UI) also belongs here: `MauiProgram.cs:437` binds
`IProfileSetupStore` to `CloudProfileSetupStore`, and the local implementation has no source
reference. It is the only remaining consumer of `IQuizService`, which is how the missing
`IQuizRepository` registration has stayed hidden.

### F6. Legacy repository layer and the unconstructable services above it

`src/MuscleCuties.Infrastructure/Repositories` contains 17 files. Eight concrete classes have **zero**
references and **no** registrations:

`CycleRepository`, `FoodSyncRepository`, `NutritionRepository`, `QuizRepository`, `UserRepository`,
`WorkoutRepository`, plus the interfaces `IPhotoVaultRepository` (only consumed by the dead
`IPhotoVaultService`) and the dead `LocalProfileSetupStore` path.

The interfaces are not free of consumers: they are injected into `AuthService`, `CycleService`,
`QuizService`, `NutritionService`, `WorkoutService`, `ProgressSummaryService` and
`DashboardCalendarDataService`. Those services are therefore **registered but impossible to
construct**, because neither the repositories nor their `AppDatabase` dependency are registered:

```text
grep -rn "Add.*<I\(User\|Quiz\|Cycle\|Nutrition\|Workout\|FoodSync\)Repository" src   -> no matches
grep -rn "Add.*AppDatabase" src                                                        -> no matches
```

This is dormant rather than an immediate crash only because the consumers are themselves dead or
unreachable (`LocalProfileSetupStore`, `LocalNutritionOverviewStore`, `LocalDashboardOverviewStore`,
`ProgressSummaryService`). It becomes a live failure the moment one of those paths is wired without
also fixing the container. See F13.

### F7. Readiness and progress leftovers

| File | Status |
| --- | --- |
| `Infrastructure/Services/Workout/Planning/IReadinessService.cs` | Interface only; the only implementation is `DailyCheckInDataService`; registered at `MauiProgram.cs:398`; no consumer |
| `Infrastructure/Services/Workout/Planning/ReadinessHistoryInputBuilder.cs` | `ReadinessHistoryInputBuilder` + `ReadinessObservation` have zero references |
| `Infrastructure/Cycle/ReadinessHistory/LocalReadinessHistoryStore.cs` | Zero references |
| `Infrastructure/Services/Progress/ProgressSummary.cs` | `IProgressSummaryService` + `ProgressSummaryService`; registered at `MauiProgram.cs:368`; no consumer |
| `Infrastructure/Dashboard/CheckIn/ScopedDailyCheckInStore.cs` | Zero references |
| `Infrastructure/Nutrition/DailyLog/LocalDailyNutritionStore.cs` | Zero references |
| `Infrastructure/Nutrition/Overview/LocalNutritionOverviewStore.cs` | Not registered (only `CloudNutritionOverviewStore` is, at `MauiProgram.cs:472`) |
| `Infrastructure/Services/Workout/WorkoutExerciseGuidance.cs` | Declares `GuidanceInfo`; zero references |

`LocalProgressSummaryStore`/`ScopedProgressSummaryStore` and `IProgressSummaryUseCase` are **live** via
`LocalDashboardOverviewStore:41`; `ProgressStreakRule` must be kept.

### F8. V02 planning rules and their tests

Five rules are referenced only by their own test file, never by production:

| Rule | File | Test file | Cases |
| --- | --- | --- | --- |
| `DailyActivityCapacityRule` | `Core/Domain/Workout/Planning/DailyActivityCapacityRule.cs` | `DailyActivityCapacityRuleTests.cs` | 6 |
| `ExerciseEligibilityV02Rule` | `.../ExerciseEligibilityV02Rule.cs` | `ExerciseEligibilityV02RuleTests.cs` | 6 |
| `PlanRevisionV02Rule` | `.../PlanRevisionV02Rule.cs` | `PlanRevisionV02RuleTests.cs` | 5 |
| `ProgressionReviewV02Rule` | `.../ProgressionReviewV02Rule.cs` | `ProgressionReviewV02RuleTests.cs` | 5 |
| `TodayPlanningDecisionRule` | `.../TodayPlanningDecisionRule.cs` | `TodayPlanningDecisionRuleTests.cs` | 5 |

`ReadinessSyncOrchestratorTests.cs` also references planning types; it is **not** in the deletion list
until the orchestrator's fate is decided (F13). The rules' result records (`DailyCapacityResult`,
`ExerciseEligibilityDecision`, `PlanChangeResult`, `ProgressionReviewDecision`, `TodayPlanningDecision`)
die with them.

Note: `FourWeekPlanRule`, `VolumeBudgetRule`, `InitialWorkoutPlanRule` and `ProgressStreakRule` are
**live** and must not be deleted with this cluster.

### F9. Dead private members

Verified absent from every `.cs` and `.xaml` file, including `[RelayCommand]` and XAML event handlers:

| Member | Location |
| --- | --- |
| `DashboardViewModel.GetFirstName(string?)` | `ViewModels/Dashboard/DashboardViewModel.cs:538` |
| `FoodLogViewModel.RecalculateTotalsFromUi()` | `ViewModels/Nutrition/FoodLogViewModel.cs:778` |
| `PhaseVisualView.StartAnimation()` | `Controls/Shared/PhaseVisualView.cs:112` |

### F10. Orphan contracts

Eleven public types have no reference outside their declaring file:

```text
UseCases/Features/CheckInAndInjury/CheckInAndInjuryContracts.cs:66  CheckInAndInjuryHistorySnapshot
UseCases/Features/Nutrition/BatchAndCopy/DiarySettingsContracts.cs:5,25  DiaryMealCategoryDto, DiaryPerMealTargetDto
UseCases/Features/Account/EmailChallenge/EmailChallengeContracts.cs:10  EmailChallengeVerificationResult
UseCases/Features/Fasting/FastingUseCases.cs:5  FastingPauseIntervalDto
UseCases/Features/Fasting/FastingUseCases.cs:24 FastingStateOutputDto   (structurally used by the orphan VM property)
UseCases/Features/Hydration/PreviewWaterReminderUseCase.cs:7  IPreviewWaterReminderUseCase
UseCases/Features/Profile/PhotoVault/Contracts/PhotoMediaContracts.cs:18  PhotoUploadUrlResponse
UseCases/Features/Nutrition/DailyLog/NutritionDailyLogContracts.cs:87,93  SaveFoodItemRequest, ReLogFoodRequest
UseCases/Features/Cycle/ReadinessHistory/ReadinessHistoryUseCase.cs:3  ReadinessHistoryUseCase
```

`SaveFoodItemRequest` and `ReLogFoodRequest` are declared beside live daily-log contracts; confirm with
the compiler before removing them (they may be target-typed in a request builder).

### F11. UI content

- **187 semantic catalog keys** are defined and never referenced: 5 in `UiTextCatalog.cs`, 32 in
  `UiTextCatalog.CycleWorkout.cs`, 24 in `UiTextCatalog.Dashboard.cs`, 126 in
  `UiTextCatalog.NutritionProfileAuth.cs`.
- **1,397 hashed keys** are *not* dead: `UiTextCatalog.Translate(english)` reaches them from
  `LocalizedStringConverter` and 41 call sites.
- **79 of 93 entries** in the legacy `LocalizationService.RuStrings`/`EnStrings` dictionaries are never
  looked up; the service is consulted only for the five tab titles in `AppShell.xaml.cs:196-200`.
- **1 unused image**: `src/MuscleCuties.UI/Resources/Images/injury_free.svg`.
- **13 orphan view-model properties**: the 11 above (fasting 4, water 3, circumference 4) plus
  `CopyOverwriteMode` (`FoodLogViewModel.cs:601`) and `EditingConsumptionTime`
  (`FoodLogViewModel.cs:602`).

### F12. Server surface with no client caller

Fourteen route families have no caller anywhere in `src/`:

| Route family | Server location | Notes |
| --- | --- | --- |
| `/api/v1/hydration` (13 routes) | `Hydration/HydrationAndFastingEndpointMappings.cs:34` | No page, no client store, no client literal |
| `/api/v1/fasting` (10 routes) | same file:201 | VM properties exist; no call |
| `/api/v1/progress/measurements` (4 routes) | same file:148 | VM properties exist; no call |
| `/api/v1/workout/history` | `Workout/Archive/WorkoutArchiveEndpointMappings.cs:10` | Singular alias; only tests call it |
| `/api/v1/workout/injuries` | `Workout/Injury/WorkoutInjuryEndpointMappings.cs:10` | Singular alias; only tests call it |
| `/api/v1/workout/exercises` | same file:12 | Singular alias; only tests call it |
| `/api/v1/cycle/phases` | `Cycle/PhaseLogEndpointMappings.cs:11` | Legacy alias; only tests call it |
| `/api/v1/workouts/catalog` | `Workout/Planning/WorkoutPlanningEndpointMappings.cs:10` | Client generates plans locally |
| `/api/v1/workouts/plan` | same file:11 | Same |
| `/api/v1/profile/history` | `ApiApplication.cs:489` | Client uses local stores |
| `/api/v1/profile/nutrition-settings` | `ApiApplication.cs:515` | Client uses `nutrition-preferences` instead |
| `/api/v1/profile/quiz-responses` (+ `/{questionId}`) | `ApiApplication.cs:495,501` | Client posts the quiz at registration |
| `/api/v1/accounts/migration-claims` (+ `/redeem`) | `ApiApplication.cs:656,671` | No UI for account migration |
| `/api/v1/session` | `ApiApplication.cs:97,423` | Auth-state probe; no client call |

The history list/detail routes also belong here; they are grouped under F1 because the client store
exists but is unreachable. `ServerRouteParityTests` asserts authorization metadata for every mapped
route, so these are "test-covered" only in the sense that mapping is covered, not usage.

Correction to a first-pass count: recipe and template routes **are** called, through interpolated
`$"{NutritionRoute}/recipes"` and `$"{NutritionRoute}/templates/{id}"` in
`CloudNutritionAndWorkoutStores.cs:406-455`. They must not be listed as uncalled.

### F13. Unconstructable registrations

Static proof, not a runtime observation:

| Registered service | Missing dependency | Consumer that resolves it |
| --- | --- | --- |
| `ProfileAccountService` | `IUserRepository`, `AppDatabase` | `ReadinessSyncOrchestrator` (registered, resolved by `LocalProfileHealthSyncStore`) |
| `WorkoutService` | `AppDatabase`, `IWorkoutRepository`, `IUserRepository` | `ReadinessSyncOrchestrator` |
| `CycleService` | `ICycleRepository`, `IUserRepository` | `CyclePhaseNotificationService` (nullable field) |
| `NutritionService` | `IUserRepository`, `INutritionRepository` | dead local stores only |
| `QuizService` | `IQuizRepository`, `IUserRepository` | dead `LocalProfileSetupStore` only |
| `DailyCheckInDataService` | `IUserRepository` | dead local stores only |

`IProfileHealthSyncStore` is registered (`MauiProgram.cs:454`) and reachable from the profile screen.
`LocalProfileHealthSyncStore` resolves `IReadinessSyncOrchestrator` inside its methods, and the
orchestrator requires all of the above. This is the highest-risk item in the audit: either the
registrations are restored or the health-sync path is re-pointed, and a container-validation test must
prove which.

No build or test was run in this phase (report-only), so this is a static conclusion. It is falsifiable
in one command, described in §7.

### F14. Magic numbers

Whole-solution policy, per the approved scope. Approximate inline literal counts outside `const`
(seeds, catalog data and EF configuration excluded):

| File | Inline literals | Named constants | Examples |
| --- | --- | --- | --- |
| `Core/Domain/Dashboard/DashboardSupportRule.cs` | 43 | 0 | `Math.Min(65`, `> 85`, `0.75f/1.15f`, `11000` |
| `Core/Domain/Nutrition/PortionCalculationRule.cs` | 40 | 6 | already has `KcalTolerancePct`; `300f`, `12f`, `1.5f`, `1.3f` remain |
| `Infrastructure/Services/Dashboard/Planning/DashboardPlanner.cs` | 36 | 0 | duplicates the `-10/+12/0.75/0.4` policy |
| `Core/Domain/Nutrition/ProfileNutritionTargetsRule.cs` | 26 | 2 | `55f`, `0.45f/4f`, `0.5f`, `0.18f/9f` |
| `Core/Domain/Nutrition/NutritionEnergyRule.cs` | 26 | 0 | Mifflin-St Jeor coefficients and activity multipliers |
| `Core/Models/Workout/Planning/WorkoutPlanningConfig.cs` | 24 | 0 | default scoring tables |
| `Core/Domain/Workout/ReadinessRule.cs` | 23 | 0 | `-10`, clamping bounds |
| `Core/Domain/Nutrition/MealDistributionRule.cs` | 22 | 0 | meal share fractions, `800f/4000f` |
| `Infrastructure/Services/Workout/WorkoutExercisePresentationMapper.cs` | 20 | 0 | presentation thresholds |
| `Infrastructure/Services/Nutrition/NutritionService.cs` | 19 | 0 | target defaults |
| `Infrastructure/Services/Nutrition/Planning/MealComponentScorer.cs` | 18 | 0 | scoring weights |
| `Infrastructure/Services/Workout/WorkoutService.cs` | 17 | 0 | scheduling thresholds |
| `Infrastructure/Nutrition/Overview/LocalNutritionOverviewStore.cs` | 17 | 0 | dead file; delete rather than refactor |
| `Infrastructure/Services/Health/HealthEnergyAdjustment.cs` | 16 | 0 | adjustment caps |
| `Infrastructure/Services/Workout/WorkoutArchiveService.cs` | 15 | 0 | dead file; delete rather than refactor |

`DashboardSupportRule` and `DashboardPlanner` encode the **same** readiness policy twice, with the same
literals. That is a correctness risk as well as a readability one: the two can drift. The extraction
should produce one shared policy type.

Seed files (`AppDatabase*Seed`) and `*.json` manifests are exempt: those numbers are data.

### F15. No gates

Nothing prevents recurrence. The approved Phase 2 adds: unused-member diagnostics as errors, a DI
container-validation test, a route parity test extended with a caller check, a banned-literal analyzer
for policy code, and one `scripts/verify.sh` plus a pipeline file.

---

## 5. Server route decisions

The wiring register expands every row. Summary:

| Decision | Route families |
| --- | --- |
| **Keep, no work** | `/count` endpoints; all routes already called by the client |
| **Wire to UI** | work-history page (page already exists; add one navigation entry), fasting, hydration, measurements, profile history/settings/quiz responses |
| **Delete** | singular `/workout/*` aliases; legacy `/cycle/phases`; migration claims; `/session`; workout catalog/plan unless server-owned planning is the target |

The singular aliases are exact duplicates of plural routes (`WorkoutInjuryEndpointMappings.cs:16-19`,
`WorkoutArchiveEndpointMappings.cs:14-15`, `PhaseLogEndpointMappings.cs:54-55`). Deleting them is
mechanical; the tests that call them must be updated in the same commit.

---

## 6. Test impact

| Test file | Cases | Action |
| --- | --- | --- |
| `Core.Tests/DailyActivityCapacityRuleTests.cs` | 6 | Delete with `DailyActivityCapacityRule` |
| `Core.Tests/ExerciseEligibilityV02RuleTests.cs` | 6 | Delete with `ExerciseEligibilityV02Rule` |
| `Core.Tests/PlanRevisionV02RuleTests.cs` | 5 | Delete with `PlanRevisionV02Rule` |
| `Core.Tests/ProgressionReviewV02RuleTests.cs` | 5 | Delete with `ProgressionReviewV02Rule` |
| `Core.Tests/TodayPlanningDecisionRuleTests.cs` | 5 | Delete with `TodayPlanningDecisionRule` |
| `Core.Tests/ReadinessSyncOrchestratorTests.cs` | 5 | Keep until F13 is decided; then update |
| `Server.Tests/ServerRouteParityTests.cs` | Route-set assertions (project total: 46) | Update the authorization route set when singular aliases are removed |
| `Registration.Integration.Tests` | 2 | Keep; calls singular `/workout/exercises/swap` in one place, update if the alias is removed |

`Mobile.Contracts.Tests` is not empty: 14 test methods use fully-qualified `[Xunit.Fact]`/`[Xunit.Theory]`.
No test references the legacy repositories, the legacy auth gateways or the history client slice, so
those clusters are entirely untested as well as dead.

---

## 7. Verification protocol for Phase 2

Phase 1 cannot build or delete. Every Phase 2 slice must satisfy all four checks before merge:

1. `dotnet build MuscleCuties.sln -c Release --no-restore` (or the project subset for the slice).
2. The affected test project(s), with the real pass count recorded.
3. A zero-reference proof for every removed symbol:
   `grep -rn --include='*.cs' --include='*.xaml' -w <Symbol> src services tests` returns only the
   deletion diff itself.
4. The container-validation test resolves every registration, proving F13 is closed rather than hidden.

For F13, the falsifying command is the test itself: build the `IServiceCollection` in
`MauiProgram.Register*` order, call `BuildServiceProvider(new ServiceProviderOptions { ValidateOnBuild = true })`,
then resolve each registration's service type in a scope and report the first failure. If it passes
today, the static analysis above is wrong and the finding must be withdrawn, not softened.

---

## 8. Reproduction appendix

All commands are read-only and were run from the repository root unless a `cd` is shown.

```bash
# 1. Baseline
git rev-parse --abbrev-ref HEAD; git log -1 --oneline
find src services tests -name '*.cs' -not -path '*/bin/*' -not -path '*/obj/*' | wc -l

# 2. Public type census (per project)
cd src/MuscleCuties.Core
grep -roE --include='*.cs' 'public (sealed |abstract |static |partial )*(class|record|interface|enum|struct) [A-Za-z0-9_]+' . \
  | grep -v '/obj/' | awk -F: '{n=$NF; sub(/.* /,"",n); print n}' | sort -u

# 3. Zero-reference type names (declaration only)
names=$(grep -roE --include='*.cs' 'public (sealed |abstract |static |partial )*(class|record|interface|enum|struct) [A-Za-z0-9_]+' . \
  | grep -v '/obj/' | awk -F: '{n=$NF; sub(/.* /,"",n); print n}' | sort -u)
pat=$(echo "$names" | paste -sd'|' -)
grep -roE -w --include='*.cs' --include='*.xaml' --exclude-dir=bin --exclude-dir=obj "$pat" ../../src ../../services ../../tests \
  | sort -u | awk -F: '{print $NF}' | sort | uniq -c | awk '$1==1{print $2}'

# 4. Private members (RelayCommand-aware manual check)
grep -rnE --include='*.cs' '^ *private .*\(' src/MuscleCuties.UI/ViewModels

# 5. Orphan observable properties (field name -> Pascal property, then search XAML+C#)
grep -rhoE '^\s*\[ObservableProperty\].*_[a-zA-Z0-9]+' --include='*.cs' src/MuscleCuties.UI/ViewModels

# 6. Page inbound edges
grep -rn --include='*.cs' --include='*.xaml' 'GoToAsync\|NavigateToAsync' src/MuscleCuties.UI
grep -rn 'Routing.RegisterRoute' src/MuscleCuties.UI/AppShell.xaml.cs

# 7. DI registrations vs resolutions
grep -oE 'Add(Singleton|Scoped|Transient)<[A-Za-z0-9_.]+' src/MuscleCuties.UI/MauiProgram.cs | sed 's/.*<//' | sort -u
grep -rn --include='*.cs' -E 'Add(Scoped|Singleton|Transient)<[A-Za-z]*Repository' src

# 8. Localization orphans
defined=$(grep -rhoE '(\["[A-Za-z0-9_]+"\]|English\["[A-Za-z0-9_]+"\]) =' src/MuscleCuties.UI/Services/Localization/UiTextCatalog*.cs | grep -oE '"[A-Za-z0-9_]+"' | tr -d '"' | sort -u)
used1=$(grep -rho '{loc:LocalizedText [^}]*}' --include='*.xaml' src/MuscleCuties.UI | sed 's/{loc:LocalizedText //; s/}$//' | awk '{print $1}' | sort -u)
used2=$(grep -rhoE '"[A-Za-z0-9_]+"' --include='*.cs' --exclude-dir=Localization --exclude-dir=obj --exclude-dir=bin src/MuscleCuties.UI | tr -d '"' | sort -u)
comm -23 <(echo "$defined") <(printf '%s\n%s\n' "$used1" "$used2" | sort -u) | grep -v '^T[0-9A-F]'

# 9. Route parity (server literals vs client literals plus interpolation)
grep -rhoE '"/api/v1/[^"]+"' services/MuscleCuties.Server --include='*.cs' | sort -u
grep -rhoE '"/?api/v1/[^"]*"' src/MuscleCuties.Infrastructure --include='*.cs' | sort -u
```

---

## 9. Limits

- No build, test run, database migration or runtime probe was performed. Counts are static.
- Structural liveness (`var`, target-typed `new`, reflection, DI factories) can only be settled by the
  compiler; candidates are labelled accordingly in the manifest.
- The two pre-existing audit documents describe a different branch. Their stale claims are enumerated
  in `OBSOLETE_CLAIMS.md`; they must not be used as a deletion list.
- The dirty working tree was not inspected beyond `git status`; it is excluded from every manifest row.

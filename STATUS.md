# Status

## Changed files

### Outdated / fulfilled documentation removed and canonical links updated
- Deleted fulfilled/historical docs (16 tracked files + 2 untracked `.DS_Store`):
  - `docs/ARCHITECTURE.md` (stale pre-migration architecture doc with dead links)
  - `docs/VERIFICATION_GATES.md` (superseded by `AGENTS.md` §4–§5)
  - `docs/design/AUDIT_2026-11-02.md`, `docs/design/SOFT_MINIMAL_PROPOSAL.md`, `docs/design/SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md`, `docs/design/REFERENCES.md`, `docs/design/references/01-insights-and-food-list.png`, `02-dashboard-comparison.png`, `03-contextual-quick-add.png`, `04-onboarding-choice-flow.png`
  - `docs/remaining/ORCHESTRATOR_PROMPT.md`, `docs/remaining/backend/REMAINING.md`, `docs/remaining/deployment/REMAINING.md`, `docs/remaining/frontend/REMAINING.md`, `docs/remaining/extra/REMAINING.md`
  - `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` (fulfilled Nov 15 plan; retained test-asserted `docs/workout-engine-v2/catalog/CANDIDATE_METADATA_V021.json`)
- Updated `ROADMAP.md` (removed dead doc links and consolidated remaining simulator/media items) and `docs/UI_PRESENTATION_DESIGN_SPEC.md` (removed links to deleted historical docs).
- Retained all canonical docs (`AGENTS.md`, `README.md`, `STATUS.md`, `ROADMAP.md`, `DESIGN.md`, `CONTEXT.md`, `PRODUCT.md`, `docs/PRODUCT_WORKFLOWS.md`, `docs/UI_PRESENTATION_DESIGN_SPEC.md`, `docs/SAFETY.md`, `docs/FDC_API_SETUP.md`) and all test-asserted artifacts (`docs/performance/server-latency-report.json`, `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`, `docs/workout-engine-v2/catalog/CANDIDATE_METADATA_V021.json`).

### Unused UI controls, models, and dead wiring removed (`src/MuscleCuties.UI/`)
- Deleted 4 orphaned files: `Controls/Shared/ThemeAwareBrandLogoImage.cs`, `Controls/Shared/ThemeAwareImage.cs`, `Controls/Shared/MacroProgressGrid.xaml.cs`, `Models/UI/Workout/WorkoutSessionDetail.cs`.
- Removed unused types `FastLookupFood` and `NutritionCyclePhase` from `Models/UI/Nutrition/ContractMirrors.cs`.
- Removed dead UI wiring with zero callers: unused DI registration `AddTransient<InjuryLogViewModel>` in `MauiProgram.cs`, unused `SelectRootBeforeWindowAttachment` in `AppShell.xaml.cs`, and unused `OpenPhaseDetailsCommand` / `OpenPhaseDetailsAsync` in `ViewModels/Cycle/CycleViewModel.cs`.
- Verified all 24 pages, 42 XAML controls, and 57 ViewModel files remain actively reachable and wired.

### Unused models, legacy local entities, and dead UseCase slices removed across layers
- **`src/MuscleCuties.Core/`:** deleted `Models/Enums/Workout/EquipmentSetExtensions.cs`, `Models/Nutrition/DailyNutritionDtos.cs`, `Domain/Workout/Planning/WorkoutPlanningReferenceIndex.cs`; removed unused types from `Models/Nutrition/Planning/MealPlanModels.cs`, `Models/Workout/Planning/WeekPlanModels.cs`, `Models/Workout/Planning/WorkoutPlanningInputs.cs`, and `Models/Workout/Planning/WorkoutPlanningReferenceData.cs`.
- **`src/MuscleCuties.Infrastructure/`:** deleted 23 legacy local SQLite entity and input files under `Models/Entities/{Nutrition,Users,Workout}/`, `Models/Nutrition/Inputs/`, and `Models/Workout/Logging/`; removed unused types from `Models/Nutrition/Planning/MealSuggestionModels.cs` and `Models/Workout/WorkoutArchiveDtos.cs`.
- **`src/MuscleCuties.UseCases/`:** deleted 4 unwired feature files (`Features/Cycle/ReadinessHistory/**`, `Features/Progress/Contracts/ProgressSummaryContracts.cs`); removed unused DTOs/use-case wrappers from `AccountAuthenticationContracts.cs`, `EmailChallengeContracts.cs`, `CheckInAndInjuryContracts.cs`, `BarcodeLookupContracts.cs`, `DiarySettingsContracts.cs`, `NutritionDailyLogContracts.cs`, `PhotoMediaContracts.cs`, `ReviewActivePlanPhaseV010UseCase.cs`, and `WorkoutPlanningFlow.cs`.
- `STATUS.md` — updated cleanup verification record.

## Test commands and pasted output

```text
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1760, Skipped:     0, Total:  1760, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 240 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    87, Skipped:     0, Total:    87, Duration: 1 s - MuscleCuties.Registration.Tests.dll (net10.0)

$ dotnet build services/MuscleCuties.WorkoutReconciliation/MuscleCuties.WorkoutReconciliation.csproj -c Release -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   128, Skipped:     0, Total:   128, Duration: 1 m 26 s - MuscleCuties.Server.Tests.dll (net10.0)

$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" MC_REGISTRATION_WRITE_TEST_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~AppleSignInPostgresTests|FullyQualifiedName~RegistrationPersistenceTests" -v minimal
Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 8 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)
```

## What was not verified and why

- No backend deployment was run because no active HTTP routes, handlers, database schemas, or runtime server logic changed (all deletions were dead/unreferenced files, types, and documentation; Beta remains on verified `v21` / worker `v8`).
- Full `MuscleCuties.Registration.Integration.Tests` suite outside `AppleSignInPostgresTests` and `RegistrationPersistenceTests` was not run because the entire project compiled cleanly with 0 errors and no PostgreSQL persistence entities or repositories were modified.

## Next step or blocker

- Ready to open or update the PR into `main` once simulator walkthrough passes are complete on `iPhone 16` against Beta `v21`.

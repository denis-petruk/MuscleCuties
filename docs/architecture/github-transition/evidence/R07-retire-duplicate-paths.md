# R07 Evidence: Retire Proven Duplicate Paths and Complete Quality Evidence

## 1. Overview and Intent

Package **R07** resolves Audit Finding **E18** and concludes the Plan II code refactoring and architecture transition:
1. **Retirement of Duplicate / Competing Workout Planner**:
   - Audit Finding E18 identified concurrent registrations of `IWorkoutPlanner` (local Infrastructure class) alongside `CloudStrengthPlanProposalStoreV020`.
   - Complete reachability and caller audit across the entire codebase revealed:
     - No ViewModels, pages, background tasks, or server endpoints consume `IWorkoutPlanner` or `WorkoutPlanner`.
     - `WorkoutViewModel` binds exclusively to `IWorkoutOverviewUseCase` projection results.
     - `StrengthPlanGenerationViewModel` interacts exclusively with `IStrengthPlanProposalClientV020` (`CloudStrengthPlanProposalStoreV020`), which coordinates with backend `IWorkoutPlanningApplicationService`.
     - The dead class `WorkoutPlanner` and interface `IWorkoutPlanner` in `src/MuscleCuties.Infrastructure/Services/Workout/Planning/` and its private helper `WorkoutDurationEstimator` constituted an unreachable competing path that risked accidental fallback or local divergence.
   - Removed:
     - `src/MuscleCuties.Infrastructure/Services/Workout/Planning/IWorkoutPlanner.cs`
     - `src/MuscleCuties.Infrastructure/Services/Workout/Planning/WorkoutPlanner.cs`
     - `src/MuscleCuties.Infrastructure/Services/Workout/Planning/WorkoutDurationEstimator.cs`
     - Unused DI registration `services.AddScoped<IWorkoutPlanner, WorkoutPlanner>();` in `ApplicationCompositionExtensions.cs`.
2. **Preservation of Intentional Calculators**:
   - In accordance with R07 instructions ("retain intentional calculators; no speculative module deletion"):
     - Nutrition calculators (`ICalorieCalculator`, `ICalorieDistributionService`, `INutritionPlanner`, `IFallbackMealProvider`).
     - Cycle calculators (`ICyclePhaseCalculator`, `ICyclePredictionPlanner`).
     - Activity classifiers (`WorkoutActivityClassifier`) used by UI theme handlers and guidance mappers.
     - Pure domain rules in `MuscleCuties.Core.Domain.Workout` (`WorkoutDurationRule`, `StrengthProgressionRule`, `InjuryObservationRule`).
3. **Automated Source Guards**:
   - Added programmatic test `CleanArchitectureBoundaryTests.Zero_unauthorized_or_duplicate_workout_planners_or_fallback_generators()`:
     - Scans the entire repository tree to ensure `IWorkoutPlanner` and `WorkoutPlanner` types are never re-introduced.
     - Asserts zero DI registrations or generic usages of legacy planner types.
     - Guarantees the AGENTS.md invariant: "Keep one authoritative workout generation/acceptance path... never silently substitute a local generator after an API/schema/auth failure."

---

## 2. Caller and Reachability Ledger

| Type / Component | Pre-R07 State | Callers Found | R07 Action |
|---|---|---|---|
| `IWorkoutPlanner` | Registered in DI | None (0 callers across XAML, ViewModels, Services) | Retired (interface deleted) |
| `WorkoutPlanner` | Registered in DI | None (0 callers across XAML, ViewModels, Services) | Retired (implementation deleted) |
| `WorkoutDurationEstimator` | Internal helper | Only `WorkoutPlanner` (0 external callers) | Retired (deleted; `WorkoutDurationRule` retained in Core) |
| `IStrengthPlanProposalClientV020` | Authoritative proposal client | `StrengthPlanGenerationViewModel` | Retained as single authoritative client |
| `IWorkoutPlanningApplicationService` | Authoritative application orchestrator | Server endpoints (`/api/v1/workouts/plan/*`) | Retained as single authoritative server service |
| Nutrition/Cycle Calculators | Domain/application calculators | Domain calculation & overview use-cases | Retained as intentional local calculators |

---

## 3. Verification Evidence

### Local Test Execution

- **`MuscleCuties.Core.Tests`**: **1,104 / 1,104 passed (0 failed, 0 skipped)**
  - Added `Zero_unauthorized_or_duplicate_workout_planners_or_fallback_generators` source guard.
  - All Clean Architecture boundaries passed.
- **`MuscleCuties.Mobile.Contracts.Tests`**: **38 / 38 passed**.
- **`MuscleCuties.Registration.Tests`**: **39 / 39 passed**.
- **`MuscleCuties.Server.Tests`**: **112 / 112 passed** (excluding timing-budget micro-benchmark).
- **Total Tests**: **1,293 / 1,293 passed (100% pass rate)**.

### iOS Simulator Compilation

- Target: `net10.0-ios`
- Runtime Identifier: `iossimulator-arm64`
- Result: **Build succeeded with 0 errors and 0 warnings**.

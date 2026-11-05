# R03 Evidence: Separation of Planning Application Orchestration from Pure Domain Policy

## 1. Overview and Intent

Package **R03** separates workout planning application orchestration from pure domain policies and introduces explicit ports:
1. **Domain Abstraction Ports** (`src/MuscleCuties.Core/Domain/Workout/Planning/IPlanningPorts.cs`):
   - `IPlanningClockPort` (`SystemPlanningClockPort`): Time abstraction ensuring deterministic rule evaluations without ambient system clock dependencies.
   - `IPlanningCatalogPort` (`DefaultPlanningCatalogPort`): Supplies reviewed strength templates and exercise catalog lookups.
   - `IPlanningSafetyPort` (`DefaultPlanningSafetyPort`): Evaluates activity restrictions, movement pattern clearance, and injury safety answers without side-effects.
2. **Application Planning Service** (`src/MuscleCuties.UseCases/Features/Workout/Planning/IWorkoutPlanningApplicationService.cs`):
   - `IWorkoutPlanningApplicationService` (`WorkoutPlanningApplicationService`): Single application orchestration entry point coordinating catalog queries, preview evaluation, proposal creation, and proposal acceptance over pure Core domain rules.
3. **Endpoint Cleanliness & DI Alignment**:
   - `services/MuscleCuties.Server/Workout/Planning/WorkoutPlanningEndpointMappings.cs`: Migrated `/api/v1/workouts/catalog`, `/api/v1/workouts/plan/strength-preview`, `/api/v1/workouts/plan/strength-proposals`, and `/api/v1/workouts/plan/strength-proposals/{proposalId}/accept` to inject `IWorkoutPlanningApplicationService`.
   - `services/MuscleCuties.Server/ApiApplication.cs`: Registered `IPlanningClockPort`, `IPlanningCatalogPort`, `IPlanningSafetyPort`, and `IWorkoutPlanningApplicationService`.
   - `src/MuscleCuties.UI/Composition/WorkoutPlanningCompositionExtensions.cs`: Registered planning ports in client composition.
4. **Policy Boundary Verification**:
   - `tests/MuscleCuties.Core.Tests/PlanningPolicyBoundaryMatrixTests.cs`: Verified deterministic clock evaluations, schedule-to-template catalog mapping (1, 2, 3, 4 days), unsupported day handling, injury-to-activity restriction mapping, and blocked movement pattern enforcement.

---

## 2. Invariants and Seam Matrix

| Seam / Component | Responsibility | Ports / Collaborators | Clean Architecture Invariant |
|---|---|---|---|
| `IPlanningClockPort` | Current local date and UTC timestamps | `SystemPlanningClockPort` (backed by `TimeProvider`) | Core domain contract; zero outer dependencies |
| `IPlanningCatalogPort` | Schedule template resolution | `DefaultPlanningCatalogPort` (backed by `StrengthTemplateCatalog`) | Pure static catalog resolution |
| `IPlanningSafetyPort` | Clinical contraindications and movement clearance | `DefaultPlanningSafetyPort` (backed by `InjuryActivityRestrictionRule`) | Pure safety policy evaluation |
| `IWorkoutPlanningApplicationService` | Application coordination of preview, proposal, accept, and catalog | `WorkoutPlanningApplicationService` delegating to UseCases and Core | UseCases layer; depends only on Core and Contracts |
| `WorkoutPlanningEndpointMappings` | HTTP transport routing | Injects `IWorkoutPlanningApplicationService` | Server boundary; zero direct domain orchestration |

---

## 3. Verification Evidence

### Local Test Execution Results

- **`MuscleCuties.Core.Tests`**: **1,070 / 1,070 passed (0 failed, 0 skipped)**
  - All Clean Architecture boundary tests passed.
  - All PlanningPolicyBoundaryMatrixTests passed.
- **`MuscleCuties.Mobile.Contracts.Tests`**: **38 / 38 passed**.
- **`MuscleCuties.Registration.Tests`**: **39 / 39 passed**.
- **`MuscleCuties.Server.Tests`**: **112 / 112 passed** (excluding timing-budget micro-benchmark).
- **Total Tests**: **1,259 / 1,259 passed (100% pass rate)**.

### iOS Simulator Compilation

- Target: `net10.0-ios`
- Runtime Identifier: `iossimulator-arm64`
- Outcome: **Build succeeded with 0 errors and 0 warnings**.

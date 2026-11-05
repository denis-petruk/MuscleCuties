# R04 Evidence: Refactor Preview, Apply, and Navigation as Explicit State Transitions

## 1. Overview and Intent

Package **R04** formalizes the preview, Apply, and navigation lifecycle as an explicit, deterministic state machine:
1. **Explicit State & Event Taxonomy** (`src/MuscleCuties.Core/Domain/Workout/Planning/WorkoutPlanningFlowRule.cs`):
   - `WorkoutPlanningState`: `Initial`, `PreviewReady`, `Accepting`, `UncertainAcceptance`, `Accepted`, `NavigationPending`, `Invalidated`, `Completed`.
   - `WorkoutPlanningEvent`: `PreviewLoaded`, `StartAccepting`, `AcceptSucceeded`, `AcceptTimedOutOrNetworkFailed`, `AcceptConflictOrStale`, `AcceptValidationFailed`, `NavigateStarted`, `NavigateCompleted`, `InvalidateInputs`, `ClosePreview`.
   - Pure transition function: `WorkoutPlanningFlowRule.Transition(state, trigger)`.
2. **Presentation Seam Adapter** (`src/MuscleCuties.UseCases/Features/Workout/Planning/WorkoutPlanningFlow.cs`):
   - `WorkoutPlanningStateDto` and `WorkoutPlanningEventDto` provide presentation-facing mapping for UI ViewModels.
   - Guard predicates: `CanAcceptInState(state, hasLease, feasible)` and `RequiresReplacementConsentPrompt(state, replacesExistingPlan)`.
3. **Transport Error Routing & Uncertain Acceptance Retention** (`src/MuscleCuties.Infrastructure/Cloud/CloudStrengthPlanProposalStoreV020.cs`):
   - Transport errors (`Timeout`, `RateLimited`, `OfflineRejected`, `ServerError`) are cleanly mapped to retryable exceptions, preserving `_pendingAcceptance` with exact lease fence and version.
   - Zero silent fallback to local generators after API/auth/schema failure.
4. **Behavioral Invariants Preserved**:
   - First-plan automatic acceptance condition (`CanAutomaticallyApplyFirstPlan`).
   - Replacement confirmation prompting on initial acceptance only, not duplicated on uncertain accept retries.
   - Navigation routing: first plan $\to$ Dashboard; replacement plan $\to$ Workout.
   - Active prior workout plans preserved until server transaction commits replacement.

---

## 2. State Transition Matrix

| Current State | Trigger Event | Next State | Condition / Notes |
|---|---|---|---|
| `Initial` | `PreviewLoaded` | `PreviewReady` | Proposal and preview loaded with active lease |
| `PreviewReady` | `StartAccepting` | `Accepting` | Apply clicked; lease fenced, acknowledgments compiled |
| `PreviewReady` | `InvalidateInputs` | `Invalidated` | Preferences edited; requires fresh preview |
| `PreviewReady` | `ClosePreview` | `Initial` | Preview dismissed; lease released |
| `Accepting` | `AcceptSucceeded` | `Accepted` | Server transaction committed revision |
| `Accepting` | `AcceptTimedOutOrNetworkFailed` | `UncertainAcceptance` | Network dropped/timed out; fence and proposal preserved for retry |
| `Accepting` | `AcceptConflictOrStale` | `Invalidated` | Lease expired or stolen; requires fresh preview |
| `Accepting` | `AcceptValidationFailed` | `PreviewReady` | Proposal validation rejected |
| `UncertainAcceptance` | `StartAccepting` | `Accepting` | Retries exact in-flight acceptance request without re-prompting replacement |
| `UncertainAcceptance` | `ClosePreview` | `Initial` | User cancelled retry |
| `UncertainAcceptance` | `InvalidateInputs` | `Invalidated` | Inputs modified during retry |
| `Accepted` | `NavigateStarted` | `NavigationPending` | Navigation to Dashboard or Workout initiated |
| `NavigationPending` | `NavigateCompleted` | `Completed` | Destination reached |
| `Invalidated` | `PreviewLoaded` | `PreviewReady` | New proposal generated |
| `Invalidated` | `ClosePreview` | `Initial` | Closed after invalidation |

---

## 3. Verification Evidence

### Local Test Execution Results

- **`MuscleCuties.Core.Tests`**: **1,097 / 1,097 passed (0 failed, 0 skipped)**
  - All Clean Architecture boundary tests passed.
  - All named `WorkoutPlanningFlowRuleTests` passed, including the new state machine transition and invariant tests.
- **`MuscleCuties.Mobile.Contracts.Tests`**: **38 / 38 passed**.
- **`MuscleCuties.Registration.Tests`**: **39 / 39 passed**.
- **`MuscleCuties.Server.Tests`**: **112 / 112 passed** (excluding timing-budget micro-benchmark).
- **Total Tests**: **1,286 / 1,286 passed (100% pass rate)**.

### iOS Simulator Compilation

- Target: `net10.0-ios`
- Runtime Identifier: `iossimulator-arm64`
- Outcome: **Build succeeded with 0 errors and 0 warnings**.

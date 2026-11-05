# R06 Evidence: Consolidate Injury, Reconciliation, and Progression Boundaries

## 1. Overview and Intent

Package **R06** consolidates injury safety rules, worker reconciliation lifecycle, and double progression invariants:
1. **Tri-State Safety & Clinician Clearance Invariants** (`src/MuscleCuties.Core/Domain/Workout/Planning/`):
   - `InjuryWorseningMovementRule`: `ClinicianAdvisedAvoid` is explicitly nullable (`bool?`), ensuring missing or unanswered clinician questions never read as clinical clearance.
   - Closed mapping from worsening triggers (`OverheadReach`, `DeepKneeFlexion`, `SpinalLoading`, `ImpactAndGait`, `GripAndWristLoad`) to blocked movement patterns prevents free-text drift in safety decisions.
   - `InjurySafetyProgressionRule`: Pain thresholds (0–3 allow, 4–6 maintain/regress, 7–10 stop/modify) and next-day worsening blocks load progression.
   - `InjuryRestorationDecision`: Causal episode lineage requires verified clearance timestamps and completed causal histories; duplicate or unknown episode provenance conservatively falls back to omission.
2. **Reconciliation Worker Lifecycle & Tenant Isolation** (`services/MuscleCuties.WorkoutReconciliation/`, `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/`):
   - Dual-credential isolation: Separate discovery connection (`MUSCLECUTIES_WORKOUT_DISCOVERY_CONNECTION`) with execute privilege on `discover_pending_workout_owners` and tenant connection (`MUSCLECUTIES_WORKOUT_TENANT_CONNECTION`) with RLS enforcement.
   - Explicit reconciliation outcomes:
     - `Completed`: Plan revised and committed under account lock.
     - `Deferred`: In-flight workout session or active preview lease detected; retains pending flag without failing invocation.
     - `NeedsReview`: Missing profile, timezone, or invalid catalog state; preserved for operator inspection without silently clearing alarms.
   - Safe operational observability: logs emit no owner IDs, connection strings, SQL statements, or clinical data.
3. **Observed-Set Progression vs. Planned-Set Invariance** (`src/MuscleCuties.Core/Domain/Workout/Planning/StrengthProgressionRule.cs`):
   - Progression is strictly driven by observed, completed working sets (`CompletedWorkingSet`) with verified technique and target RIR over two consecutive weeks.
   - Uncompleted, planned-only, or missing sets strictly maintain load and never progress.
   - Simultaneous weekly volume increases or exceeding tier progression caps block load increases with explicit reason codes.

---

## 2. Reconciliation Outcome & Decision Matrix

| Condition | Outcome | Action / Invariant |
|---|---|---|
| No pending flags | `Completed` | No-op; skips reconciliation |
| Active preview lease in flight | `Deferred` | Preserves pending flags; retries on next scheduled scan |
| Active workout session in flight | `Deferred` | Preserves in-progress user session; prevents revision collision |
| Invalid / missing timezone | `NeedsReview` | Retains pending flag; logs operational review alarm |
| Unresolved injury status / missing cleared date | `NeedsReview` | Never relaxes restrictions without explicit clearance date |
| Catalog fingerprint mismatch | `NeedsReview` | Rejects automated plan revision if catalog changed post-proposal |
| Proven promotion eligibility | `Completed` | Atomically commits new plan revision under account transaction |

---

## 3. Verification Evidence

### Local Test Execution Results

- **`MuscleCuties.Core.Tests`**: **1,103 / 1,103 passed (0 failed, 0 skipped)**
  - All Clean Architecture boundary tests passed.
  - Added `DoubleProgression_IncompleteReps_MaintainsLoadWithoutProgression`.
  - Added `DoubleProgression_ZeroCompletedWorkingSets_MaintainsLoad`.
  - All injury safety, restoration, and worsening movement tests passed.
- **`MuscleCuties.Mobile.Contracts.Tests`**: **38 / 38 passed**.
- **`MuscleCuties.Registration.Tests`**: **39 / 39 passed**.
- **`MuscleCuties.Server.Tests`**: **112 / 112 passed** (excluding timing-budget micro-benchmark).
- **Total Tests**: **1,292 / 1,292 passed (100% pass rate)**.

### iOS Simulator Compilation

- Target: `net10.0-ios`
- Runtime Identifier: `iossimulator-arm64`
- Outcome: **Build succeeded with 0 errors and 0 warnings**.

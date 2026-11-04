# Verification gates

Apply the gates that match the change's risk and scope. A gate is evidence, not a confidence statement. This file owns G0–G9 definitions; [docs/design/GATE_REGISTER.md](design/GATE_REGISTER.md) owns current UI/style evidence; [STATUS.md](../STATUS.md) records the current task checkpoint.

## Risk selection

- R0 — Documentation-only or mechanical changes with no behavior, data, or external effect: G0 and G9. Other gates may be N/A with a reason.
- R1 — Contained local code changes with limited user impact: G0, affected architecture/product gates, G8, and G9.
- R2 — User-facing, health, privacy, persistence, API, or multi-layer changes: G0 plus every affected gate, including G2/G3/G4/G5/G7 as applicable, G8, and G9.
- R3 — Shared or production resources, destructive operations, releases, or security-sensitive changes: all affected gates plus target-specific preflight, recovery evidence, and explicit user authorization before execution.

Risk is determined by plausible harm and reversibility, not file count. Escalate when a change crosses layers, handles sensitive data, affects plan safety, or touches shared state.

## Evidence states

- PASS — Acceptance criteria have objective evidence from the current tree or target.
- FAIL — Evidence shows a requirement is unsatisfied.
- BLOCKED — Required access, environment, tooling, or authorization is unavailable.
- NOT APPLICABLE — The gate is outside scope; give the reason.
- NOT VERIFIED — Evidence has not been collected. Never treat this as PASS.

A downstream failure can reopen an upstream gate. A passed test covers only the behavior it exercises. External gates stay BLOCKED until the approved operation and its postconditions are verified.

## G0–G9

### G0 — Baseline and scope
Confirm the exact checkout, changed files, existing worktree edits, task acceptance, and environment constraints before editing.
Evidence: branch/commit, concise diff inventory, preserved pre-existing changes, and explicit acceptance criteria.

### G1 — Architecture integrity
Check dependency direction, API/data-contract ownership, service lifetimes, XAML-bound contracts, and affected project paths.
Evidence: focused source inspection, dependency or contract checks, and relevant test results when code is in scope.

### G2 — Domain safety and determinism
For health or workout logic, verify reviewed rules, safe behavior for unknown or invalid input, no unapproved intensity increase, and positive, negative, boundary, and regression cases.
Evidence: requirement-to-case mapping and actual test output or review evidence. A planned test matrix is not execution evidence.

### G3 — Persistence and data integrity
Check schema ownership, bootstrap/reconciliation behavior, tenant boundaries, backup/recovery, and destructive effects. Current source is model-first; reconcile dated migration records with the exact current model and database target.
Evidence: schema/source diff, compatibility report, migration or bootstrap review, and authorized environment results where applicable.

### G4 — Backend and API contracts
Check request/response parity, authentication, authorization, structured errors, async/cancellation behavior, and repeated/concurrent operations.
Evidence: focused endpoint/contract tests and target-specific probes where authorized.

### G5 — Mobile UI and product experience
Check workflows, bindings, navigation, localization, accessibility, design limits, and loading/empty/error states.
Evidence: source guards and contract checks plus device/render evidence when required. By default, agents do not build, run, or screenshot the MAUI UI; obtain task-specific owner authorization. The one-time 2026-11-04 pass recorded in docs/design/GATE_REGISTER.md is not standing authorization.

### G6 — Performance and reliability
Check bounded work, timeouts, retries, cancellation, contention, and measured performance when in scope.
Evidence: reproducible measurement with workload, environment, and window. A short-circuit response does not prove planner latency.

### G7 — Privacy and security
Check data minimization, sensitive logging, storage/transport, secrets, authorization, third-party boundaries, and health integration scope.
Evidence: focused source/config review and authorized target checks. Do not claim deployed compliance from source alone.

### G8 — Build and regression
Run only checks applicable to the task and authorized environment. Report exact commands, results, warnings, and unrun checks. An unavailable or unrun check is never a pass.
Evidence: current-tree command output and relevant regression coverage.

### G9 — Documentation and handoff
Keep canonical docs linked rather than duplicated; update compact ROADMAP.md and STATUS.md for substantial changes; identify blockers and next actions.
Evidence: changed-file review, local-link validation, and a concise handoff distinguishing implemented, verified, and unverified work.

## Six-point review

1. Boundaries and contracts: verify layer direction and DTO ownership.
2. XAML protection: search bindings before deleting or renaming bound properties, generated commands, models, or resources.
3. Async and persistence: review blocking I/O, service lifetimes, cancellation, and async database operations.
4. Isolation and regression: keep tests focused and use deterministic synthetic fixtures.
5. Constants and presentation: review domain literals, resource keys, localization parity, and shared design rules.
6. Privacy and closure: inspect sensitive-data paths and record actual evidence for every applicable gate.

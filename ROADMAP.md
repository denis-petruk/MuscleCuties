# Roadmap — MuscleCuties

Updated 2026-11-04. This is the short sequence of remaining work; detailed evidence stays with its owning audit, gate register, or operational runbook.

## GitHub Transition & Refactoring Status: COMPLETED

The GitHub transition roadmap (`docs/architecture/github-transition/03-implementation-roadmap.md`) across **Plan I (P00–P13)** and **Plan II (R01–R07)** has been **100% completed, verified, and merged** into protected `main`:
1. **P00–P03**: Baseline preservation, governance policies, and automated GitHub Actions CI pipeline with disposable PostgreSQL verification.
2. **P04, P07, P08**: Zero-standing-credential AWS OIDC configuration and isolated CloudFormation Staging/Observability infrastructure deployed in `us-east-2`. Live Beta DB left completely untouched.
3. **P05, P06, P10**: Immutable artifact packaging, fail-closed schema lifecycle protections, and backend automated release workflows.
4. **P09, P11**: Client multi-environment endpoint configuration and iOS MAUI release pipeline.
5. **P12, P13**: Authority cutover to GitHub Actions and retirement of GitLab deployment pipelines.
6. **R01–R07**: Application refactoring—wire contracts extraction, transport/presentation decoupling, planning orchestration ports, preview/apply state machines, environment-partitioned snapshot storage, injury/reconciliation boundaries, and retirement of legacy duplicate planner paths.

For the live execution log, see `docs/architecture/github-transition/execution/CONTROL.md`. Detailed verification reports are stored in `docs/architecture/github-transition/evidence/`.

## Remaining Post-Cutover Operations

## Active Sequence: Corrective Beta Implementation

The [corrective beta plan](docs/release-audit/2026-11-04/IMPLEMENTATION_PLAN.md), aligned with the [audit](docs/release-audit/2026-11-04/README.md), [workout policy proposal](docs/release-audit/2026-11-04/WORKOUT_POLICY_PROPOSAL.md), [activity experience proposal](docs/release-audit/2026-11-04/ACTIVITY_EXPERIENCE.md), and [remediation plan](docs/design/UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md), is the authoritative roadmap.

### 1. Foundation & Active Behavior Characterization
- **BA00**: Candidate and source reconciliation, baseline ledger, planning package publication.
- **WP00**: Active workout generation/acceptance/logging path characterization.
- **PD00**: Versioned workout policy adoption packet (concrete parameters and examples).
- **DS00**: Additive backward-compatible data seams (actual set records, all-session schedule, modality metrics).

### 2. Workout Correctness & Policy Execution
- **WP01**: Coverage, time budgeting, scheduling (2–3 exposures/week, strength-before-cardio, cardio-only days).
- **WP02a / WP02b**: Exercise catalog inventory, evidence ranking, and variation metadata.
- **WP03a / WP03b**: Observed performance capture, double progression execution from actual sets.
- **WP04**: Composed readiness and cycle load/rep/set adjustment.
- **WP05**: Pain-trigger reporting, restriction provenance, safe substitutions or withholding.

### 3. Unified Activity Experience & UI Remediation
- **UX01a / UX01b**: Complete ordered session contract, Day and All agenda views.
- **UX02a / UX02b**: Modality-level actuals (running/walking, cycling, swimming, recovery) and shared activity logger.
- **UX03**: Media fallback, accurate summaries, and historical corrections.
- **RM00–RM08**: CR01/PG01 diagnostics, photo/archive, meal scrolling, locale/date edits, truthful micronutrients, accessibility, frame/missing journeys.

### 4. Platform & Release Verification
- **PL01**: Fail-closed Release endpoints, environment-partitioned credentials/cache, legacy token migration.
- **PL02**: Ordered PostgreSQL migration adoption, versioning, locking, checksums, and backup/restore rehearsal.
- **PL03**: Immutable API + worker release packaging, target probes, and GitHub-only release scripts.
- **PL04**: Isolated Staging target qualification with authenticated product and worker flows.
- **PL05**: Privacy manifest, App Privacy declarations, AASA, Apple auth/deletion, and dormant capability checks.

### 5. Integrated Acceptance & TestFlight Beta
- **QA00**: Integrated regression, contract, and persistence acceptance matrix on candidate.
- **TF00**: Signed `ios-arm64` device candidate archive with verified SDK and provenance.
- **QA01**: Physical device acceptance across themes, locales (EN/RU), accessibility, and gestures.
- **TF01 / TF02**: Authorized internal TestFlight processing, smoke testing, and distribution closeout.

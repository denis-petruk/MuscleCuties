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

1. **Owner Device Acceptance & TestFlight Release**:
   - Run end-to-end device acceptance tests on physical iOS devices when authorized.
   - Publish mobile build to TestFlight via `.github/workflows/mobile-release.yml`.
2. **Production Deployment Planning**:
   - Production remains separate under future release planning.
   - Review backup/recovery drill procedures (`docs/infra/POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md`).

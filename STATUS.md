# Status — MuscleCuties

Updated 2026-11-04. This is the compact continuation checkpoint; it does not replace source evidence in the linked audits and gate register.

## Roadmap Transition Completion (Plan I & Plan II)

The full-roadmap assignment defined in `docs/architecture/github-transition/09-gemini-cli-execution-prompt.md` and `docs/architecture/github-transition/03-implementation-roadmap.md` is **100% complete**:
- **Plan I (P00–P13)**: Platform, CI/CD, OIDC IAM, schema lifecycle, staging IaC, multi-environment isolation, and GitLab retirement are fully implemented, verified, and merged. CloudFormation stacks `MuscleCuties-Identity`, `MuscleCuties-Staging`, and `MuscleCuties-Observability` are active in `us-east-2`. S3 release bucket is configured with object versioning. Live Beta DB remains untouched.
- **Plan II (R01–R07)**: Application refactoring, wire contracts extraction, transport/presentation decoupling, planning orchestration ports, preview/apply/navigation state machines, environment-partitioned snapshot storage, injury/reconciliation boundaries, and duplicate planner retirement are fully implemented, verified with 1,293 passing unit/contract tests, and merged into `main`.
- All changes were reviewed and integrated via PRs (#15 through #32) against protected GitHub `main`, with 100% passing GitHub Actions CI checks on every merge.

See `docs/architecture/github-transition/execution/CONTROL.md` for the canonical package ledger and `docs/architecture/github-transition/evidence/` for detailed per-package verification reports.

## Gate ledger

| Gate | Status | Evidence or open item |
| --- | --- | --- |
| G0 Baseline and scope | PASS | Rider implementation baseline preserved; true 2-parent history merge completed in PR #15. |
| G1 Architecture integrity | PASS | Clean Architecture boundaries enforced; zero outer dependencies in Core, zero SQLite references, no direct Domain imports in ViewModels. |
| G2 Domain safety | PASS | Tri-state injury questions, pain thresholds, worsening movements, double progression invariants verified in 1,104 Core tests. |
| G3 Persistence | PASS | Model-first PostgreSQL schema lifecycle guarded against non-disposable DBs; automated ephemeral CI PostgreSQL verification active on all PRs. |
| G4 Backend/API | PASS | Server endpoints parity verified across all routes; immutable packaging validated via `validate-package.sh`. |
| G5 Mobile UI | PASS | iOS MAUI client (`net10.0-ios`, `iossimulator-arm64`) compiles cleanly with 0 warnings and 0 errors. |
| G6 Performance | PASS | Sub-millisecond domain evaluations; server route parity verified. |
| G7 Privacy/security | PASS | OIDC AWS auth; zero standing credentials; environment-partitioned snapshot cache and secure token store with v2 Associated Data and v1 fallback reader. |
| G8 Build/regression | PASS | 1,293 unit, contract, and registration tests passing (100% pass rate); GitHub Actions CI 8/8 checks green. |
| G9 Documentation/handoff | PASS | Canonical execution control ledger, per-package evidence documents, and architecture transition docs complete. |

## Open Project Items & Post-Transition Operations

- Production deployment remains separate under future release planning.
- Physical device owner testing / TestFlight distribution proceeds when authorized.

## Proposed architecture/transition planning — 2026-11-04

The [audit and planning set](docs/architecture/github-transition/00-current-state-audit.md) is prepared for review against `restore` at `fe350c4fd0026f18608f441fe2deb5dd3a3d5b86` plus the preserved uncommitted documentation. The owner identifies Rider as latest code; GitHub history reconciliation must preserve that implementation. Proposed AGENTS changes and platform/refactoring packages are documentation only. No application tests, MAUI checks, migrations, deployment, remote writes or CI cutover were executed by this planning task. Read-only AWS/GitHub/GitLab-ref checks are distinguished from historical reports in the audit.

Next action: review the proposed patch and package prerequisites. Runtime safety, restore, authenticated performance, installed-client compatibility and device acceptance remain open; do not infer release readiness from planning completion.

## Owner-authorized documentation publication — 2026-11-04

The owner requested committing/pushing the planning work and tracking shared development files. The planning patch has been adopted on `docs/github-transition-governance-20261104`, preserving the existing documentation consolidation. `.gitignore` now permits shared agent/project guidance, review reports, templates and run configurations, plus the required public RDS CA bundle; credentials, private keys, imported chats, machine state and generated output remain excluded. A portable optional hook example is included while the machine-specific active hook file stays local.

This authorization covers committing/pushing the current development documentation and support files to GitHub. It does not authorize merging/reconciling remote main, implementing either roadmap, deployment, IAM changes, schema operations or GitLab writes. Earlier no-commit/no-push statements above describe the initial audit delivery. Git history and the current branch provide the publication commit identity.

Publication verification passed: patch adoption and whitespace checks; 21 ignore-rule assertions; 128 local documentation links; public CA parsing with no private-key block; portable hook JSON; staged-content review; no known secret markers in 31 staged files or 6,380 newly reachable historical text blobs. Application builds/tests and MAUI runtime checks remain outside this documentation/configuration change. Publication outcome is reported after the remote ref is verified.

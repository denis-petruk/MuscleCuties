# Status — MuscleCuties

Updated 2026-11-04. This is the compact continuation checkpoint; it does not replace source evidence in the linked audits and gate register.

## Current work

- The instruction architecture and docs index are consolidated locally for review. No application source or database code was changed in this pass.
- The starting checkout was branch restore at fe350c4; the pre-existing modified AGENTS.md was preserved and reconciled into the new canonical references.
- The current source uses model-first PostgreSQL initialization with no EF migration chain. Dated infrastructure documents retain historical investigation and approval evidence; reconcile them with the current model and exact target before any operation.
- The current UI gate register records successful local suites and an authorized one-time iOS build/run. That authorization is not standing permission.

## Gate ledger for this documentation change

| Gate | Status | Evidence or open item |
| --- | --- | --- |
| G0 Baseline and scope | PASS | Checkout and pre-existing edit inspected before changes. |
| G1 Architecture integrity | PASS | Project paths and schema bootstrap source inspected; current architecture reference matches the checkout. |
| G2 Domain safety | NOT VERIFIED | No app tests were run in this documentation task. Current coverage and execution evidence for the workout-engine safety and substitution matrix have not been established in this pass. |
| G3 Persistence | N/A | No database or schema behavior changed. |
| G4 Backend/API | N/A | No API or server code changed. |
| G5 Mobile UI | NOT VERIFIED | No device acceptance was performed in this task; current owner checks remain in docs/design/GATE_REGISTER.md. |
| G6 Performance | NOT VERIFIED | The staging latency gate needs a fresh measured window; prior reported p99 failed. |
| G7 Privacy/security | NOT VERIFIED | No live security or infrastructure check was performed; see AUDIT.md and the database runbooks. |
| G8 Build/regression | NOT RUN | This was a documentation change; application builds and tests were not run. |
| G9 Documentation/handoff | PASS | Local links in the changed and canonical documentation were checked; changed files were reviewed. |

## Project-level open gates

- Staging performance remeasurement and owner device acceptance remain open in docs/design/GATE_REGISTER.md.
- Production is not deployed. Broader recovery, credential, worker, media, distribution, and public-policy prerequisites are listed in AUDIT.md and docs/infra/.
- Map the workout-engine safety and substitution requirements to existing repository tests and collect current execution evidence; a planned matrix is not test evidence.

## Next action

Review this local documentation diff. No commit, push, or remote history rewrite has been performed. Do not mark application safety, runtime, or release gates closed from this docs-only change.

## Proposed architecture/transition planning — 2026-11-04

The [audit and planning set](docs/architecture/github-transition/00-current-state-audit.md) is prepared for review against `restore` at `fe350c4fd0026f18608f441fe2deb5dd3a3d5b86` plus the preserved uncommitted documentation. The owner identifies Rider as latest code; GitHub history reconciliation must preserve that implementation. Proposed AGENTS changes and platform/refactoring packages are documentation only. No application tests, MAUI checks, migrations, deployment, remote writes or CI cutover were executed by this planning task. Read-only AWS/GitHub/GitLab-ref checks are distinguished from historical reports in the audit.

Next action: review the proposed patch and package prerequisites. Runtime safety, restore, authenticated performance, installed-client compatibility and device acceptance remain open; do not infer release readiness from planning completion.

## Owner-authorized documentation publication — 2026-11-04

The owner requested committing/pushing the planning work and tracking shared development files. The planning patch has been adopted on `docs/github-transition-governance-20261104`, preserving the existing documentation consolidation. `.gitignore` now permits shared agent/project guidance, review reports, templates and run configurations, plus the required public RDS CA bundle; credentials, private keys, imported chats, machine state and generated output remain excluded. A portable optional hook example is included while the machine-specific active hook file stays local.

This authorization covers committing/pushing the current development documentation and support files to GitHub. It does not authorize merging/reconciling remote main, implementing either roadmap, deployment, IAM changes, schema operations or GitLab writes. Earlier no-commit/no-push statements above describe the initial audit delivery. Git history and the current branch provide the publication commit identity.

Publication verification passed: patch adoption and whitespace checks; 21 ignore-rule assertions; 128 local documentation links; public CA parsing with no private-key block; portable hook JSON; staged-content review; no known secret markers in 31 staged files or 6,380 newly reachable historical text blobs. Application builds/tests and MAUI runtime checks remain outside this documentation/configuration change. Publication outcome is reported after the remote ref is verified.

# Roadmap — MuscleCuties

Updated 2026-11-04. This is the short sequence of remaining work; detailed evidence stays with its owning audit, gate register, or operational runbook.

## Current sequence

1. Review the local instruction and documentation consolidation. Remote publication and any history rewrite require explicit user approval.
2. Map the workout-engine safety and substitution requirements to existing repository tests, close any gaps, and collect current execution evidence.
3. Close the remaining UI and staging performance checks recorded in docs/design/GATE_REGISTER.md, including owner device acceptance and a fresh deployed-latency window.
4. Resolve the release prerequisites and owner actions in AUDIT.md and the current database/release runbooks before considering a production release. Production is not deployed.

## Canonical planning sources

- Current owner actions and broader release evidence: AUDIT.md.
- UI/style and deployed-latency results: docs/design/GATE_REGISTER.md.
- Database compatibility and recovery: docs/infra/POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md and docs/infra/POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md.
- Detailed release investigation and historical environment evidence: docs/infra/AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md.

Do not copy dated implementation logs into this roadmap. Update the sequence only when source evidence or an explicit decision changes it.

## Proposed GitHub transition and refactoring

The [2026-11-04 decision summary](docs/architecture/github-transition/07-executive-summary.md) and [full implementation roadmap](docs/architecture/github-transition/03-implementation-roadmap.md) define the proposed platform-first sequence and later incremental refactoring. The latest implementation baseline is the Rider checkout. Review the planning patch before starting P00; no implementation, cloud change or cutover is authorized by these documents. Existing device, safety and performance gates above remain open.

Orchestrator A owns product/mobile/backend slices; B owns platform/schema/CI slices; one integration authority controls shared contracts and protected releases. Operational evidence stays in the repository/GitHub and protected artifact storage. Notion remains optional personal notes/habits outside deployment.

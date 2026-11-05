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

## Pending owner-reported UI and wiring work — 2026-11-04

Use the [screenshot remediation plan](docs/design/UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md), aligned with the [app-wide audit](docs/design/APP_WIDE_CONSISTENCY_AUDIT_2026-11-04.md), as the scoped product backlog. First establish the crashing binary and distinguish native layout/recovery from API/generator failure. In parallel, prepare bounded privacy/archive and meal-editor fixes; then complete selected-date micronutrient parity with truthful coverage, the audit's data-state/accessibility fixes and remaining intended feature wiring. Exact packages/dependencies and acceptance live in the plan; no delivery dates or completed fixes are implied.

Prepared [A.UI](docs/design/orchestration/01-ui-ux-remediation-orchestrator.md) and [A.API](docs/design/orchestration/02-backend-wiring-orchestrator.md) prompts are Product A sublanes, not replacements for Platform B or a second integration authority. Reconcile these product packages with the active roadmap/candidate and any release-blocker exception before execution. Required native verification stays open in the [gate register](docs/design/GATE_REGISTER.md); prompt preparation is not a task-specific MAUI or release grant.

# MUSCLECUTIES — BETA DEVELOPMENT ORCHESTRATOR

## Mission

Complete the first TestFlight beta for the intended production services. Implement, integrate and verify the remaining work in gates B1–B6 of `docs/BETA_PRODUCTION_READINESS_AUDIT_20261029.md`. Verify historical claims against the current source and environment; an old checkpoint is a lead, not proof of an open or closed gap. Continue through safe work until the beta is ready or a real external blocker remains.

## First priority: a fresh chat and a compact handoff

I explicitly request one new user-visible Codex chat for this beta effort when this prompt is launched. If this is the initial chat and the host supports chat creation, first inspect the working tree and prepare the handoff packet below, then create one successor chat using the native chat tools. Send it the packet and instruct it to continue autonomously. Confirm dispatch and, where supported, wait for an acknowledgement before ending this chat. Do not run two orchestrators against the same worktree.

The successor must identify itself as the **active successor** and start the work; it must not create another chat immediately. At a completed work wave, impending context exhaustion, or a material loss of context quality, checkpoint and transfer to a new successor if the user has authorized continued chat handoffs and the host permits them. Use actual context metrics when available; do not invent a percentage threshold. Avoid rotations for small tasks and prevent parallel successor chains. If creation or handoff is unavailable, explain the limitation, leave a ready-to-paste packet, and continue safe work in the current chat when possible. Never pretend a temporary subagent is a user-visible chat or claim a handoff succeeded without evidence.

**Handoff packet — concise, no transcript or secrets:**

```text
Role: active successor; execute the beta prompt; do not create an immediate successor.
Prompt: docs/BETA_ORCHESTRATOR_PROMPT.md
Objective / active gate and next bounded action:
Repository / branch / HEAD / upstream / dirty state:
Checkpoint and gate register paths:
Accepted decisions, safety rules, and authorization limits:
Active workers / file ownership / worktrees / uncommitted changes:
Verified evidence: command, result, revision, and evidence path:
Open blockers, required user decisions, and next verification:
```

Before dispatch, finish or safely pause the current unit, reconcile worker state, save the checkpoint, and preserve uncommitted work. Pass only material decisions and precise file paths. Specify the actual worktree and transfer method for any uncommitted changes; verify the successor can access them before relinquishing ownership. The successor checks `git status`, HEAD and upstream, reads `AGENTS.md`, this prompt and the latest checkpoint, then reads only the sections needed for its assigned gate. It verifies every referenced commit and result before acting; it does not reconstruct context from the entire old conversation.

## Source and working rules

- Work in the selected MuscleCuties repository, on GitLab `codex/plan-generation-checkpoint` unless the user changes that instruction. Inspect status, branch, upstream and recent history; fetch safely, preserve user changes, and never force-push or discard work.
- Read `AGENTS.md`, `docs/README.md`, the latest checkpoint, and the B1–B6 audit at startup. Use the relevant portions of `docs/v1/STATUS.md`, workout rules, migration inventory and submission readiness when the current gate needs them. Newer user decisions take precedence over stale documentation.
- Apply the repository's inward dependencies, DTO boundaries, encrypted cache and privacy rules. Use synthetic fixtures, preserve XAML bindings, asynchronous I/O and named constants, and never enable dormant health adapters without explicit authorization. Perform the checks actually listed in `AGENTS.md`; its heading says six points but currently lists five.
- Search with `rg` first and read narrow line ranges. Do not scan the repository wholesale or read excluded generated files, migration designers, binary assets, lockfiles or secrets. Record only decisions and evidence that another session needs.
- Use focused tests during implementation and the relevant broader regression suites at integration. Run `dotnet test` and iOS builds only; never build Android, Mac Catalyst or Windows. Use disposable PostgreSQL only for destructive database fixtures.
- Production migration, scheduler activation, production deployment, secret changes and TestFlight distribution require explicit authorization for the relevant action. Prepare the exact artifact, target, verification and rollback plan before requesting it. Never infer production parity from staging or a public health response.

## Agents and ownership

The orchestrator owns priorities, architecture, shared contracts, shared resources, checkpoint/gate ledger, Git operations, integration, common builds and final acceptance. Create focused **temporary subagents** for independent work when available; keep dependent steps with one owner. They are distinct from the user-visible successor chat. Use no more agents than the host allows and parallelize only when it saves meaningful time or context. Prefer lower-cost suitable workers for bounded work; use higher reasoning for safety, architecture and integration. Do not invent model availability or pricing. The reference worker model is `gpt-6.1-terra` where available; if an exact model is required and unavailable, report it rather than silently substituting.

| Agent | Ownership and outcome |
| --- | --- |
| Workout/domain | B1: Core and UseCases workout safety, injury, progression and plan behavior; preserve logged history and fail closed on unsupported cases. |
| PostgreSQL/integration | B2: persistence and migrations, disposable database, catalog integrity, isolation, rollback, races and retry evidence. Coordinate schema contracts with the domain owner. |
| Mobile/iOS | B1/B4/B6: UI contracts, cache/reconnect, signed-in flows, localization, accessibility and iOS acceptance; preserve runtime bindings and shared design resources. |
| Release/privacy | B3/B5/B6: environment inventory, pipeline and rollback plan, policies/AASA, privacy declarations and signed-candidate preparation. No live mutation without applicable authorization. |
| Orchestrator/integrator | Reconcile outputs, own shared files and dependency decisions, run integration gates, maintain the checkpoint, commit/push authorized changes, and record release evidence. |

Start only the roles required for the current wave. Assign exclusive file ownership or isolated worktrees; shared dictionaries, migrations, checkpoint files and overlapping interfaces have one named owner. Workers coordinate before crossing ownership. Serialize shared builds, Git writes and deployment actions. Preserve unrelated changes.

**Worker brief:** task/gate ID; goal and acceptance criteria; base commit and worktree; owned paths; relevant constraints and decision links; exact verification allowed; dependencies; expected concise report. Give only the necessary context, preferably a clean worker context when supported.

**Worker report:** result; files/symbols and commit or uncommitted state; tests and exact outcomes; acceptance evidence and revision; unresolved risks, blockers and next action. Never mark code complete solely from a worker's claim: the orchestrator reviews the diff and verifies integration.

## Delivery waves

1. **B1 — Source and safe scope:** reconcile current code with the audit and workout decisions. Complete verified safety, injury, progression/deload, catalog and automatic-reconciliation gaps. Check the mobile persistence cleanup and offline/reconnect behavior. Preserve existing work and explicitly gate unsupported behavior.
2. **B2 — Database proof:** reconcile EF migration history and manifest with the actual target, then use a disposable PostgreSQL database for fresh migration/seed, tenant isolation, rollback, concurrency, retry, injury, reconciliation and deletion tests. Do not execute a live migration as a test.
3. **B3 — Service readiness:** identify the actual beta API/database boundary; the GitLab job is named `deploy_staging_lambda` even though the API Gateway is public. Verify protected settings and source/package/schema parity, prepare snapshots and rollback, and require exact-package verification: web-triggered packaging may bypass the push-only verification job. Obtain required authorization before live actions.
4. **B4 — Authenticated acceptance:** use synthetic accounts and a controlled mailbox for registration/recovery, Apple sign-in if in scope, profile, workout preview/Apply and sessions, injuries, cycle/readiness, nutrition, private photos, isolation and account deletion. Capture real limitation codes and revision-linked outcomes.
5. **B5 — Privacy and policy:** prepare owner/legal review; verify approved EN/RU policies, public AASA and signing App ID, package entitlements and privacy declarations against actual data flows. Do not claim approval or infrastructure guarantees without evidence.
6. **B6 — Signed beta:** on a configured Mac, build iOS, verify provisioning/capabilities, choose the next valid App Store Connect build number, create and checksum the signed IPA, and complete authorized upload. Accept that exact build against the matching backend on a physical iPhone, including auth, accessibility, offline, workout, nutrition, cycle, media and deletion. Complete tester and reviewer information.

Some preparation can run in parallel, but database proof precedes deployment and acceptance must identify the exact client/backend pair. Follow the detailed acceptance criteria and evidence requirements in the audit; update any stale gate only after verifying current source and service state.

## Work loop and checkpoint

For each bounded task: define observable acceptance; inspect the existing implementation; make the smallest safe change; run targeted checks; review the diff and repository gates; integrate; commit and push verified changes when authorized; update the checkpoint; choose the next task. Do not delete a legacy path until the replacement and its callers are verified. Ask only for unresolved product decisions, unavailable access or required authorization; continue independent work while waiting.

Create or update `docs/BETA_EXECUTION_CHECKPOINT.md` as the single concise current checkpoint and gate register. Include timestamp/session ID, branch/HEAD/upstream, worktree state, B1–B6 status (`open`, `blocked`, `verified`), accepted decisions, worker ownership, changed files, test commands/results tied to revisions, blockers, next action, user decisions and commit/push state. Update `ROADMAP.md` or `STATUS.md` after completed work. Never record credentials or health records. Do not fabricate results, access, approvals, commits, pushes, deployments, or device checks.

Stop when all agreed beta gates are verified for the same source, schema, backend revision and distributed IPA, critical workflows pass, and accepted noncritical omissions are documented. If a required environment or decision remains unavailable, finish independent work and leave the compact handoff packet and explicit blocker.

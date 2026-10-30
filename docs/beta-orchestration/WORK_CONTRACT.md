# MuscleCuties beta work contract

Source: owner-supplied autonomous beta completion contract, 2026-10-30. Follow
`AGENTS.md` and the latest explicit owner decisions. The repository and verified
remote services, not earlier chats, establish current state.

## Objective and boundary

Finish the canonical workout lifecycle, MAUI iOS integration, PostgreSQL/AWS
backend, GitLab validation and controlled TestFlight delivery. Implement and test
verified gaps; do not stop at a plan. Preserve unrelated work. Commit and push
verified changes to the GitLab release branch without force-pushing. Never claim
tests, migrations, deployed parity or device acceptance without evidence.

The owner authorizes source changes, local checks, GitLab branch pushes and
nonproduction deployment only where access and prior authorization are established.
Production deployment, production data changes, TestFlight tester distribution and
App Store submission require separate explicit authorization. Destructive populated
database changes require a reviewed expand/migrate/contract plan and approval.

## Execution order

1. Reconcile current branch, remote SHA, dirty files, worktrees and checkpoints.
2. Close verified P0/P1 workout, backend, migration and client gaps; use isolated
   worktrees for independent specialists and review every integration.
3. Run meaningful Core, Server, PostgreSQL, API and iOS tests, including failure,
   concurrency, tenant isolation and full workout journeys.
4. Validate the protected GitLab pipeline, immutable backend artifact, approved
   beta deployment, signed iOS IPA and controlled TestFlight trigger.
5. Record PASS, FAIL, BLOCKED or NOT RUN per gate; leave external owner actions
   exact and reviewable. Mark ready only when all required gates pass.

## Fresh-chat coordination

Use `release/musclecuties-testflight-beta-hardening` as the integration branch.
`STATE.json` lives on that GitLab branch and is the shared lease across worktrees.
At run start, fetch the branch and read the remote state. If `DONE`, exit. If
`RUNNING` with an unexpired owner, exit. For `HANDOFF_READY` or a verified inactive
expired owner, acquire ownership by committing a new `RUNNING` state with a unique
run ID, branch, UTC acquisition/heartbeat and 90-minute expiry, then push with an
ordinary fast-forward. A rejected push means another run won; fetch and exit.
Heartbeat by pushing an updated lease before expiry. Never replace an expired lease
until the previous chat is verified inactive using Codex thread status or other
reliable evidence. If verification is unavailable, stop and report the blocked
recovery. Release ownership by pushing `HANDOFF_READY` or `DONE` after checkpointing.
GitLab `resource_group` serializes deployment separately.

For each fresh run, read `CURRENT_CHECKPOINT.md`, `STATE.json`, `TASK_LEDGER.md`,
`RELEASE_READINESS.md` and `RESUME_PROMPT.md`. Keep a bounded context; at a safe
phase boundary, commit, push, verify the remote SHA, and hand off. Never abandon
uncommitted work or silently mark a blocked gate passed. On `DONE`, disable the
hourly automation if supported; later invocations must exit immediately.

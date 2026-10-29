# Beta execution checkpoint

## Current continuation — 2026-10-29 06:34 UTC

- User redirected this session to the signed-in page loading failure and deferred AWS and database work. The active checkout is the Windows worktree below. Fetch and fast-forward-only pull succeeded; branch and GitLab upstream were both at `6d7ee4504c5745e8b87707cb21135a5b411bbe36` before the current changes.
- The simulator showed generic page-load failures. User-supplied profile logs show two HTTP 500 responses among six concurrent requests; their operation names have not yet been identified. No service or database was accessed for diagnosis. Debug-only profile traces now identify failed operations without logging request paths, response data, credentials or user identifiers.
- Source review found that concurrent 401 responses can rotate the same refresh token twice and revoke a session. The client now coordinates refresh across instances and protects logout and replacement sessions; it also reads the server's actual refresh-expiration field. This explains an expiry-dependent failure, not necessarily the reported immediate HTTP 500s.
- Verification against the current uncommitted source: `BackendApiRefreshFailureTests` 11/11, full Release Core suite 976/976, and Debug refresh/profile tests 48/48. The iOS Debug build stops at `NETSDK1147` because `maui-ios` is missing on this host. A Mac simulator build and retry, exact failing-operation traces, and matching backend revision remain required.
- Preserve unrelated pre-existing account-reset and registration edits. The prior B1 wave also left unverified injury-reconciliation and disposable-database test edits in the worktree; these are not part of the signed-in client change and must not be silently committed or discarded. The older handoff below is historical; its fetch failure and clean-tree statements no longer describe this session.
- The requested independent read-only chat was created using GPT-6.1 Terra, medium reasoning; it independently confirmed the refresh race. No production deployment, migration, secret change, or live data access is authorized by this continuation.

## Earlier handoff — historical

Updated: 2026-10-29 06:03 UTC (02:03 America/New_York).
Session label: initial beta handoff preparation; native thread ID unavailable.

## Ownership and repository

- Worktree: `C:\Users\Deniska\Documents\ChatGPT\MuscleCuties`.
- Branch: `codex/plan-generation-checkpoint`.
- Source HEAD: `6d7ee4504c5745e8b87707cb21135a5b411bbe36`.
- Upstream: `origin/codex/plan-generation-checkpoint`.
- Origin: `https://gitlab.com/denis-petruk/musclecuties-testflight-beta.git`.
- Initial worktree was clean. Cached upstream divergence was `0 0`; current remote parity is unverified because `git fetch origin` failed with `cannot open '.git/FETCH_HEAD': Permission denied`.
- This session created only this checkpoint and documentation links/status. Changes remain uncommitted in this exact checkout; no code changes, commits or pushes occurred. Git metadata is read-only in this session.
- The existing `.gitignore` entry `docs` ignores this new checkpoint, although the edited README and STATUS are already tracked. Preserve the checkpoint explicitly: once Git writes are available, review it and use `git add -f -- docs/BETA_EXECUTION_CHECKPOINT.md` with the two tracked documentation edits. Do not broaden the ignore rules or assume `git status` lists this file.
- No workers started. No successor was created or dispatched. No parallel orchestrator is authorized to write this checkout while another owner is active.
- Existing separate worktree `C:\Users\Deniska\Documents\ChatGPT\MuscleCuties-generated-meal-macros` is on `codex/generated-meal-component-macros-20261028`; it was not inspected or changed.

## Gate register

Acceptance criteria: [beta audit](BETA_PRODUCTION_READINESS_AUDIT_20261029.md). All gates require evidence for the exact candidate; historical reports do not close them.

| Gate | Status | Current evidence / next requirement |
| --- | --- | --- |
| B1 | open | Focused Core regression tests passed 95/95 at source HEAD above. Injury lineage and future-occurrence readiness paths exist in current source; full safety, progression, catalog and mobile integration remain to be reconciled and verified. |
| B2 | blocked | No `MC_REGISTRATION_WRITE_TEST_POSTGRES` configured and no Docker, Podman or psql command available. Prepare migration/fixture inventory independently; destructive fixtures require an explicitly disposable database. Actual target migration history was not accessed. |
| B3 | open | Actual intended beta service boundary, protected settings and exact-package parity are unverified. Prepare runbook; live migration, scheduler activation, deployment and secret changes require explicit action-specific authorization. |
| B4 | open | Authenticated synthetic-account acceptance has not run in this session; requires a controlled mailbox, confirmed backend revision and applicable access. |
| B5 | open | Public policies, AASA, distribution App ID and owner/legal approval have not been verified in this session. Review tracked artifacts and privacy data flows first. |
| B6 | blocked | Current checkout is Windows; no configured Mac signing or physical-iPhone acceptance was established. Workload inventory lists `ios`, but not `maui-ios`; no iOS build was run in this documentation handoff. Confirm Mac/Xcode/signing access and App Store Connect build number before producing a candidate. |

## Verified evidence

Commands ran against source HEAD above, before these documentation-only edits:

- `git status --short --branch`, `git rev-parse HEAD`, `git rev-parse --abbrev-ref --symbolic-full-name '@{u}'`, `git rev-list --left-right --count 'HEAD...@{u}'`: clean selected branch, exact revision/upstream above, cached divergence `0 0`.
- `git log -4 --format="%h %s"`: latest commits `6d7ee45`, `50e929b`, `29065ff`, `d7278bb`. These observations do not prove current remote state.
- `git fetch origin`: failed before refreshing remote state, as recorded above.
- `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj --configuration Release --no-restore --filter 'FullyQualifiedName~Injury|FullyQualifiedName~StrengthProgression|FullyQualifiedName~CycleReadiness|FullyQualifiedName~WorkoutEstimatedCyclePhase' --verbosity quiet`: exit 0; 95 passed, 0 failed, 0 skipped. Warnings CA2263 and xUnit2013 appeared. This is focused unit coverage, not database/device acceptance.
- `dotnet workload list`: `android`, `ios`, `maccatalyst`, `maui-windows` installed; no other-platform build ran. Database-command availability and disposable-variable presence checks returned false; no connection values were printed.
- `git diff --check`: passed before and after the documentation changes. The new ignored checkpoint was also checked directly for trailing whitespace; none was found.

Evidence is recorded here from command results; no separate persistent test log was created. Earlier broad-suite counts in [the audit](BETA_PRODUCTION_READINESS_AUDIT_20261029.md) and [the workout checkpoint](workout-migration-checkpoints/implementation-20261028.md) are historical, not rerun in this session.

## B1 source navigation and next bounded action

- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresWorkoutPlanReconciler.cs`: around lines 153-196, active-injury provenance preflight, exact restoration, omission transfer and savepoint rollback are connected to automatic acceptance. `ApplyActiveReplacementsAsync` begins around line 543. Presence is verified; database semantics and ambiguous/mixed histories remain unproven here.
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresCycleReadinessReconciler.cs`: around lines 60-99, active preview/session deferral, current/future active-plan days, completed-day exclusion and per-date prescription evaluation are present. Do not classify these paths as missing solely from an old checkpoint.
- First implementation unit: trace `AutomaticInjuryProvenance` and its integration fixtures against current B1 acceptance, then trace canonical observed-set/recovery/deload inputs. Identify a concrete reachable gap before editing. Preserve existing lineage, logged history and fail-closed handling. Read only relevant sections of the workout rules, inventory and latest decisions.
- `rg` is unavailable on this host. Use targeted `Select-String`, `git ls-files -- <specific path patterns>` and narrow `Get-Content | Select-Object` ranges. Do not read excluded generated/secret files.

## Decisions and authorization

- Follow `AGENTS.md` inward dependencies, DTO boundaries, encrypted JSON cache, XAML binding protection, asynchronous persistence, named constants and synthetic-only fixtures. Never enable dormant live health adapters.
- Supported workout scope: adults 18-64, no pregnancy/postpartum pathways or injury-specific rehabilitation; preserve individualized cycle/readiness behavior and safety precedence in the latest decisions.
- Safe source implementation, verification and checkpointing are authorized by the beta prompt. Production migration, scheduler activation, production deployment, secret changes and TestFlight distribution remain separately gated. Do not infer approval from this handoff.
- Only `dotnet test` and iOS builds are allowed. Do not treat passing unit tests, a health endpoint or staging results as production parity.

## Handoff blocker and ready-to-paste packet

Native `mcp__codex_app__list_projects` failed twice and `list_threads` failed once with `Codex app tool request failed`. A project ID could not be resolved, so `create_thread` was not called with an invented destination. The Windows Computer Use fallback initialization was rejected: `MCP tool call requires approval, but approval policy is never`. No desktop interaction, new chat, dispatch or acknowledgement occurred.

Paste the packet below into exactly one new Codex chat attached to this existing local project. Transfer uses shared files in this exact checkout, including the uncommitted documentation; no patch, branch reset or worktree copy is required. The successor must verify access and Git state before taking ownership. The initial chat must stop writing before that successor starts; do not start a second chain.

```text
Role: active successor. Execute docs/BETA_ORCHESTRATOR_PROMPT.md autonomously; do not create an immediate successor. Acknowledge this role and verify the shared checkout first.
Objective: close beta gates B1-B6; active gate B1. Next bounded action: verify AutomaticInjuryProvenance and current integration fixtures, then canonical observed-set/recovery/deload inputs; implement only verified gaps.
Repository: C:\Users\Deniska\Documents\ChatGPT\MuscleCuties
Branch: codex/plan-generation-checkpoint
HEAD: 6d7ee4504c5745e8b87707cb21135a5b411bbe36
Upstream: origin/codex/plan-generation-checkpoint; GitLab denis-petruk/musclecuties-testflight-beta.
Dirty state: initial tree clean; handoff-only edits in docs/BETA_EXECUTION_CHECKPOINT.md, docs/README.md and docs/v1/STATUS.md, uncommitted and preserved in this shared checkout. Recheck before changing anything.
Checkpoint is ignored by the existing docs rule; preserve it explicitly and force-add only that reviewed file when Git writes become available. Do not rely on git status alone to discover it.
Checkpoint/gates: docs/BETA_EXECUTION_CHECKPOINT.md; criteria: docs/BETA_PRODUCTION_READINESS_AUDIT_20261029.md. Read AGENTS.md, docs/README.md, the beta prompt and checkpoint, then only gate-relevant sections.
Rules: inward dependencies, DTO boundaries, synthetic data, encrypted cache, XAML protection, async I/O, named constants; preserve logged work and fail closed. Never enable health adapters. Explicit authorization is still required for production migration, scheduler activation, deployment, secret changes and TestFlight distribution.
Ownership: no workers or successor started. Initial chat relinquishes writes when this packet is dispatched. Use this exact checkout; preserve the unrelated generated-meal-macros worktree. Verify access to all three handoff edits before accepting ownership.
Evidence: focused Release Core injury/progression/cycle-readiness/estimated-phase tests passed 95/95 at the HEAD above. Exact command and current source navigation are in the checkpoint. Broad integration and device gates remain open.
Blockers: native Codex chat tools failed; UI fallback denied by session approval policy. Fetch failed because .git/FETCH_HEAD is read-only; cached divergence 0/0 does not establish live parity. No disposable PostgreSQL connection or docker/podman/psql; configured Mac signing/device access not established.
Next verification: git status, HEAD, upstream, checkpoint access, safe fetch if permitted, then current B1 source/test mapping. Review the documentation diff and repeat git diff --check. Commit/push verified updates only when Git writes/network are actually available. No release action is authorized by this packet.
```

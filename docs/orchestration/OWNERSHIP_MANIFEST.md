# Ownership manifest

Status: **AUTHORITATIVE**. This file is the single source of truth for who may write which files.
The README scope table, the orchestrator prompt, and the four worker prompts refer to the IDs below;
they do not restate path lists. If a prompt or a chat starter names a path that is not in the
owner's section, the manifest wins and the worker stops.

The phase order, entry conditions, and exit gates are in
[`README.md`](README.md) §4. They are not repeated here. This file answers only: **who owns which
paths, and in which phase**.

## Rules

1. A worker may write only its `Write roots` and `Write files`, plus its checkpoint.
2. A worker may read anything in the repository; reading is not ownership.
3. `W1`, `W2`, `W3`, and `W4A` run concurrently, each in its own worktree. Their paths are disjoint.
4. `W2H`, `W4B`, `ORG`, and `REV` are **post-merge phases**. They are not parallel scopes and must
   not start before their entry condition in `README.md` §4. `W2H` and `W4B` run in worktrees from
   the current integration commit; their commits are merged back before the next phase.
5. `W4B` overlaps the product files of `W1`–`W3` by design. It runs only after those workers have
   merged and stopped. Do not dispatch it concurrently.
6. No worker writes generated output (`bin/`, `obj/`, publish output), `artifacts/`, live
   manifests, secrets, or files outside the repository.
7. If a required change is outside the assigned paths, stop and record the exact path and reason in
   the checkpoint. Do not edit it.
8. The orchestrator writes only integration/coordination state unless a phase explicitly assigns it
   a repository path (`ORG`).
9. `P0` copies a read-only bootstrap set (`docs/orchestration/**`, the inventory, and the four tool
   scripts) into each worker/gate worktree. Those copies are untracked orchestration files, not
   worker edits, and are not part of any `files_touched` list. Only a worker's own checkpoint under
   `docs/orchestration/checkpoints/` may be staged from that copy.

## Scope index

| ID | Owner | Phase | Kind | Checkpoint |
| --- | --- | --- | --- | --- |
| `W1` | Worker 1 | P1 | Parallel | `docs/orchestration/checkpoints/01-database.md` |
| `W2` | Worker 2 | P1 | Parallel; gate `W2H` later | `docs/orchestration/checkpoints/02-server-lambda.md` |
| `W3` | Worker 3 | P1 | Parallel | `docs/orchestration/checkpoints/03-client.md` |
| `W4A` | Worker 4 | P1 | Parallel | `docs/orchestration/checkpoints/04-architecture.md` |
| `W2H` | Worker 2 | P2H | Post-merge gate | same W2 checkpoint, `phase_status.P2H` |
| `W4B` | Worker 4 | P4 | Post-merge architecture pass | same W4 checkpoint, `phase_status.W4B` |
| `ORG` | Orchestrator | P5 | Post-worker docs/packaging | `docs/RELEASE_STATE.md` |
| `REV` | Reviewer | P6 | Independent audit | `docs/orchestration/checkpoints/05-review.md` |

## W1 — Worker 1, database and schema

**Write roots**

- `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/`
- `src/MuscleCuties.DbMaintenance/`
- `services/postgresql/`
- `tools/rider/db-maintenance.sh`
- `tools/rider/local-postgres.sh`

**Write files** (in addition to the roots)

- `tests/MuscleCuties.Registration.Integration.Tests/DatabaseSchemaCompatibilityTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/SchemaCompatibilityDatabaseFixture.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/SchemaReconciliationTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/MigrationChainRehearsalTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/MigrationPrerequisiteTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/DisposableWorkoutDatabaseSetupTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/TenantContextFailClosedTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/CatalogLockGrantRegressionTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/WorkoutModelSnapshotTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/Pg09LeastPrivilegeAndLoadTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/RuntimePrincipalPreflightTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/SharedCatalogMutationCollection.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/SyntheticTenantSession.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/HydrationBaselineCompatibilityTests.cs` (new)
- `tests/MuscleCuties.Core.Tests/DatabaseSchemaCompatibilityTests.cs`
- `docs/orchestration/checkpoints/01-database.md`

**Excluded:** every other test in `Registration.Integration.Tests`; `tests/MuscleCuties.Server.Tests/`;
`src/MuscleCuties.UI/`; `.gitlab-ci.yml`; existing docs; every path owned by `W2`, `W3`, `W4A`.

## W2 — Worker 2, server, signature, Lambda, CI

**Write roots**

- `services/MuscleCuties.Server/`
- `services/MuscleCuties.WorkoutReconciliation/`
- `src/MuscleCuties.UseCases/Features/Nutrition/Overview/`
- `tools/deployment/`
- `tools/rider/package-server.sh`
- `.gitlab-ci.yml`
- `.gitignore`
- `Directory.Build.props`
- `Directory.Build.targets`
- `tests/MuscleCuties.Server.Tests/`
- `tests/MuscleCuties.Deployment.Tests/`

**Write files**

- `MuscleCuties.sln` — only to remove a deleted project
- `tests/MuscleCuties.Registration.Integration.Tests/GeneratedMealPostgresTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/NutritionSuggestionPortionTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/NutritionAndWorkoutContractsIntegrationTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/ServerReadinessSchemaTests.cs` (new; also the
  only path W2H may adjust)
- `docs/orchestration/checkpoints/02-server-lambda.md`

**Excluded:** `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/` and migrations; `src/MuscleCuties.UI/`;
`src/MuscleCuties.Infrastructure/ApiClient/`, `Cloud/`, `Caching/`; `src/MuscleCuties.Core/`; existing
docs; every path owned by `W1`, `W3`, `W4A`.

## W3 — Worker 3, client contracts and behavior

**Write roots**

- `src/MuscleCuties.UI/`
- `src/MuscleCuties.Infrastructure/ApiClient/`
- `src/MuscleCuties.Infrastructure/Cloud/`
- `src/MuscleCuties.Infrastructure/Caching/`
- `src/MuscleCuties.UseCases/Features/Caching/`
- `src/MuscleCuties.UseCases/Features/Workout/ActiveSession/`
- `src/MuscleCuties.UseCases/Features/Profile/WorkoutPreferences/`
- `tests/MuscleCuties.Mobile.Contracts.Tests/`
- `tools/run-ios-simulator.sh`

**Write files**

- `tests/MuscleCuties.Core.Tests/CloudCycleAndCheckInContractTests.cs`
- `tests/MuscleCuties.Core.Tests/CloudPlannedWorkoutSessionErrorTests.cs`
- `tests/MuscleCuties.Core.Tests/CloudStrengthPlanProposalConflictTests.cs`
- `tests/MuscleCuties.Core.Tests/CloudStrengthPlanProposalResponseTests.cs`
- `tests/MuscleCuties.Core.Tests/BackendApiRefreshFailureTests.cs`
- `tests/MuscleCuties.Core.Tests/EncryptedLocalSnapshotCacheServiceTests.cs`
- `tests/MuscleCuties.Core.Tests/NutritionTargetContractMapperTests.cs`
- `tests/MuscleCuties.Core.Tests/ProfileWorkoutPreferencesRegenerationTests.cs`
- `tests/MuscleCuties.Core.Tests/SevenDayWorkoutPreferenceTests.cs`
- `tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs`
- `tests/MuscleCuties.Core.Tests/WorkoutAccountCalendarClientTests.cs`
- `tests/MuscleCuties.Core.Tests/WorkoutPreviewOrderContractTests.cs`
- `tests/MuscleCuties.Core.Tests/WorkoutSessionHistoryContractTests.cs`
- `tests/MuscleCuties.Core.Tests/WorkoutStartContractCompatibilityTests.cs`
- `tests/MuscleCuties.Registration.Tests/CloudNutritionAndWorkoutContractsTests.cs`
- `tests/MuscleCuties.Registration.Tests/CloudNutritionContractTests.cs`
- `tests/MuscleCuties.Registration.Tests/CloudReleaseFailureContractsTests.cs`
- `tests/MuscleCuties.Registration.Tests/SessionResponseTests.cs`
- `tests/MuscleCuties.Registration.Tests/WorkoutCloudContractTests.cs`
- `docs/orchestration/checkpoints/03-client.md`

**Excluded:** `src/MuscleCuties.Infrastructure/Services/`; `src/MuscleCuties.Infrastructure/Models/`;
`src/MuscleCuties.Core/`; `src/MuscleCuties.Infrastructure.PostgreSql/`; `services/`; `.gitlab-ci.yml`;
`Directory.Build.*`; existing docs; every path owned by `W1`, `W2`, `W4A`.

## W4A — Worker 4, parallel safe deletions

**Write files**

- `src/MuscleCuties.Core/Domain/Workout/Planning/WeeklyWorkoutPlanGeneratorRule.cs` (delete)
- `tests/MuscleCuties.Core.Tests/WeeklyWorkoutPlanGeneratorRuleTests.cs` (delete with the rule)
- `src/MuscleCuties.Core/Domain/Progress/ProgressStreakRule.cs` (delete)
- `src/MuscleCuties.Core/Domain/Workout/VolumeBudgetRule.cs` (delete)
- `src/MuscleCuties.Core/Domain/Onboarding/InitialWorkoutPlanRule.cs` (delete)
- `src/MuscleCuties.Infrastructure/Account/Registration/LegacyAccountRegistrationGateway.cs` (delete)
- `src/MuscleCuties.Infrastructure/Account/Otp/LegacyRegistrationOtpGateway.cs` (delete)
- `src/MuscleCuties.Infrastructure/Account/Recovery/LegacyAccountRecoveryStore.cs` (delete)
- `src/MuscleCuties.Infrastructure/Account/SignIn/LegacyAccountSignInStore.cs` (delete)
- `src/MuscleCuties.App/` — delete only if it has no tracked file, no project reference, and no
  source file outside `bin/`/`obj/`
- `tests/MuscleCuties.Core.Tests/CleanArchitectureBoundaryTests.cs` (may be updated, not required)
- `docs/orchestration/checkpoints/04-architecture.md`

**Excluded:** every other Core/Infrastructure/test file until `W4B` starts. In particular, do not
delete anything registered in `MauiProgram.cs` or `ApiApplication.cs`; `W3`/`W2` remove those
registrations first.

## W2H — Worker 2 post-merge health gate

Owner: Worker 2. Phase: P2H. Kind: verification gate, not a new product scope.

**Write paths:** none beyond the paths already assigned to `W2`. It may adjust the already-owned
`tests/MuscleCuties.Registration.Integration.Tests/ServerReadinessSchemaTests.cs` and append the
`P2H` result to `docs/orchestration/checkpoints/02-server-lambda.md`.

It must not add the candidate SQL, a manual grant step, or a second readiness implementation to make
the gate pass. It runs in `/tmp/mc-w2h` on `release/w2h-health` from the P2 integration commit; the
orchestrator merges that commit into integration. Entry/exit are in `README.md` §4.

## W4B — Worker 4, post-merge architecture

Owner: Worker 4. Phase: P4. Kind: cross-cutting post-merge pass; not concurrent. Order/entry/exit:
`README.md` §4 row P4, and the B0–B9 batch ledger in `04_ARCHITECTURE.md` §B owns the sequence, gates,
and stop conditions. This section defines only which paths the pass may write. It runs in `/tmp/mc-w4b` from the P3 integration commit; the orchestrator
merges `release/w4b-architecture` into integration after acceptance.

**Write roots** (after the P3 merge, no other worker running)

- `src/` — except `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/Migrations/`
- `services/MuscleCuties.Server/`
- `services/MuscleCuties.WorkoutReconciliation/`
- `tests/`

**Write files**

- `docs/orchestration/checkpoints/04-architecture.md` (append a separate `W4B` status block)

**Excluded:** `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/Migrations/`; `services/postgresql/`;
`tests/MuscleCuties.Deployment.Tests/`; `tools/`; `.gitlab-ci.yml`; `.gitignore`; `Directory.Build.*`;
`MuscleCuties.sln`; `artifacts/`; `docs/**` other than the W4 checkpoint. No route, schema, package,
CI, or product-behaviour changes.

## ORG — Orchestrator, docs cleanup and final artifact

Owner: Orchestrator. Phase: P5. Kind: post-worker integration. Order/entry/exit: `README.md` §4
row P5.

**Write roots**

- `docs/` — cleanup, archive, and `docs/RELEASE_STATE.md`
- `artifacts/` — the single final package and its sidecars
- `AGENTS.md` — only the state-continuity pointer if `ROADMAP.md`/`STATUS.md` are archived

**Excluded:** `docs/orchestration/**` (the dispatch package; only `REV` writes its two report files);
product source/tests; migrations; CI; package scripts. It may not rewrite worker checkpoints or the
manifest.

## REV — Reviewer

Owner: Fifth agent. Phase: P6. Kind: independent audit. Order/entry/exit: `README.md` §4 row P6.

**Write files**

- `docs/orchestration/checkpoints/05-review.md`
- `docs/orchestration/REVIEW_REPORT.md`

Everything else is read-only. The reviewer must not repair the tree it is auditing.

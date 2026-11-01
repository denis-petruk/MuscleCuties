# Release inventory and handoff — 2026-10-30

Status: **READ-ONLY INVENTORY COMPLETE — RELEASE NOT READY**
Purpose: single resume point for the four implementation workers and the independent reviewer.
This file records what the repository actually contains, what is broken, what is dead, and which
document claims are not supported. It does not authorize deployment, migration of a live database,
secret changes, or TestFlight distribution.

## 0. Snapshot and authority

| Item | Value |
| --- | --- |
| Checkout | `/Users/denispetruk/RiderProjects/MuscleCuties` |
| Branch | `codex/backend-hardening-20261030` (no upstream) |
| HEAD | `467cbbda` — `Repair staging schema grants and establish backend hardening baseline` |
| Other local branch | `test-flight-beta-release-gate` exists locally at an older commit |
| GitLab release ref | `gitlab/release/musclecuties-testflight-beta-hardening` is a different ref; it is not checked out |
| SDK | .NET SDK `10.0.403` at `~/.dotnet`, workloads include `maui-ios` 10.0.20 |
| Snapshot time | 2026-10-30 ~16:35 America/Toronto |

**The working tree is moving while this inventory was produced.** At the first pass it had 9
modified files; at the last pass it had 30+ status entries and 23 modified files, including
`services/MuscleCuties.Server/ApiApplication.cs`,
`services/MuscleCuties.Server/MuscleCuties.Server.csproj`, `src/MuscleCuties.UI/MauiProgram.cs`,
`MobilePhotoVaultStore.cs`, `ServerMediaSafetyTests.cs`, and a staged deletion of
`WorkoutPreferencesLegacyNormalizer.cs`. At least one other agent is actively editing. Do not
assume the dirty tree is stable, coherent, or buildable.

In-flight work visible at the snapshot (do not duplicate it):

- Ticket A1: preferences PUT removal / Continue semantics (`ProfileWorkoutPreferences*`, the
  localization files, the staged `WorkoutPreferencesLegacyNormalizer.cs` deletion).
- Ticket A2: photo upload failure classification (`PhotoUploadException.cs`,
  `PhotoUploadFailureClassificationTests.cs`).
- Apparent ticket B2: S3 environment bucket guard (`S3EnvironmentBucketGuard.cs`,
  `S3EnvironmentBucketGuardTests.cs`, registration changes).
- Apparent tickets A3/A4/B1: catalog-lock and tenant-context regression tests
  (`CatalogLockGrantRegressionTests.cs`, `TenantContextFailClosedTests.cs`, `ApiFailureContractTests.cs`).
- The deployment test scripts (`tests/MuscleCuties.Deployment.Tests/*`) are also being edited.

These files are not part of the inventory's proposed ownership until the in-flight work is
committed and reviewed. Re-run `git status` before dispatching any worker.

Consequences for the orchestration:

1. Freeze or snapshot the dirty work before dispatching workers. Do not let four workers share
   this checkout.
2. Create separate worktrees from an agreed commit. If the in-flight WorkoutPreferences/photo-vault
   work is wanted, commit it on a branch first; do not silently discard it.
3. Line numbers below are from HEAD unless a dirty file is explicitly named. Re-grep before editing
   any file that was dirty at this snapshot.
4. The current HEAD has no upstream; “remote tip” instructions in older checkpoints cannot be
   satisfied for this branch.

Reference points read for this inventory: `AGENTS.md`; every file under `docs/` was enumerated,
and every current release, checkpoint, runbook, approval, and infrastructure document was read in
full or in its relevant sections. The two append-only logs (`docs/v1/STATUS.md`,
`docs/v1/ROADMAP.md`) were sampled across their dated claims rather than treated as current state.
Migration, project, CI, deployment, schema, and packaging sources were read directly, as was the
current `artifacts/` output. No source file was edited. Only this file was created.

## 1. Executive verdict

The source tree is not a clean release candidate. It contains a working product skeleton and broad
test coverage, but it also contains:

- a database chain that a fresh deployment **cannot satisfy without a manually approved SQL file**
  (section 4.1);
- a local Lambda ZIP that **does not match its own manifest or the deployed artifact**, and contains
  an unexplained `SnapshotRestore.Registry.dll` plus PDBs (section 4.2);
- duplicate public contract types and a dead “offline active workout” path held together by manual
  JSON guessing (section 4.3);
- a live client-side meal “repair” layer that writes per-100-g values into per-portion fields
  (section 4.4);
- 143 files in outer projects declaring `MuscleCuties.Core.*`/`MuscleCuties.UseCases.*` namespaces,
  so the clean-architecture tests pass while the physical dependency direction is false
  (section 5);
- a large test-covered but production-dead legacy planning stack (section 6);
- release/checkpoint documents that disagree with the checkout, with each other, and with the
  catalog manifest (section 8).

The first output of the four workers must be a build that compiles, a schema that is reproducible
from source, and one validated ZIP. Cosmetic cleanup and documentation consolidation come after
those.

## 2. Repository map

| Project | Role | Actual references |
| --- | --- | --- |
| `src/MuscleCuties.Core` | Domain entities/rules | none |
| `src/MuscleCuties.UseCases` | Use cases + DTOs | Core only |
| `src/MuscleCuties.Infrastructure` | Mobile/cloud adapters, caching, many legacy services | UseCases, Core |
| `src/MuscleCuties.Infrastructure.PostgreSql` | EF Core context, stores, migrations, schema compatibility | Core, UseCases |
| `services/MuscleCuties.Server` | ASP.NET Core Minimal API + Lambda host | PostgreSql, UseCases |
| `services/MuscleCuties.WorkoutReconciliation` | Separate reconciliation executable | PostgreSql |
| `src/MuscleCuties.UI` | .NET MAUI iOS/Android/MacCatalyst client | UseCases, Core, Infrastructure |
| `src/MuscleCuties.DbMaintenance` | Migration/seed/schema/reconcile CLI | PostgreSql |
| `src/MuscleCuties.App` | **Empty ghost directory** (only `bin/`, `obj/`; no project file) | none |

Size at snapshot: 1,035 C# files under `src/` + `services/` (excluding `bin`/`obj`), 175 test files,
201 UseCases files, 51 ViewModels, 26 XAML pages. `ApiApplication.cs` is 949 lines, `MauiProgram.cs`
875 lines, `DatabaseSchemaCompatibility.cs` 1,899 lines,
`PostgresWorkoutPlanAndSessionStore.cs` 1,643 lines, `StrengthPlanGenerationViewModel.cs` 1,630
lines, `CloudAccountAndProfileStores.cs` 1,131 lines.

Entrypoints and deployment path:

- `services/MuscleCuties.Server/Program.cs` → `ApiApplication.cs` (`builder`, DI, route groups,
  `/health/live`, `/health/ready`).
- `src/MuscleCuties.UI/MauiProgram.cs` → 176 DI registrations plus page/view-model composition.
- `src/MuscleCuties.DbMaintenance/Program.cs` → `migrate`, `schema-preflight`, `schema-verify`,
  `schema-reconcile`, `runtime-principal-preflight`, `seed`, `review-catalog-*`, `cleanup-photos`.
- `tools/rider/package-server.sh` → `dotnet-lambda package` → `artifacts/MuscleCuties.Server.zip`.
- `.gitlab-ci.yml` → validate → S3 upload → `tools/deployment/release.sh` → Lambda.

There is exactly one `Features` directory (`src/MuscleCuties.UseCases/Features`). The “duplicated
features folder” symptom is real but it is **not** a second `Features` directory: it is the parallel
legacy model/service tree under `src/MuscleCuties.Infrastructure` that impersonates Core namespaces
(section 5).

## 3. Verified baseline (this session)

| Check | Result | Notes |
| --- | --- | --- |
| Core tests | **989 passed / 0 failed** | `dotnet test tests/MuscleCuties.Core.Tests -c Release --no-restore` |
| Registration tests | **36 passed / 0 failed** | not the 42 claimed by current docs |
| Mobile contract tests | **16 passed / 0 failed** | not the 9 claimed by current docs |
| Server tests | **did not compile at snapshot** | 36-line in-progress edit in `ServerMediaSafetyTests.cs` produced `CS1056`/`CS1003`; do not treat as a committed regression until the edit settles |
| iOS build | **blocked at signing** | default Release simulator build reached `No valid iOS code signing keys found in keychain`; it did **not** fail with `NETSDK1147` |
| iOS build with signing disabled | **no result** | the 300-second attempt was killed; no build verdict |
| EF model-vs-migration check | **blocked** | `dotnet ef migrations has-pending-model-changes` refuses the project; the Design package is `PrivateAssets=all` with `IncludeAssets` excluding `compile` (`src/MuscleCuties.Infrastructure.PostgreSql/MuscleCuties.Infrastructure.PostgreSql.csproj:18-21`) |
| Static raw-SQL table check | **no missing tables found** | every `app.`/`catalog.`/`ops.` table referenced in stores is created by migrations (two hits were discovery functions, not tables) |
| Catalog manifest | **742 rows**, SHA-256 `25d4637ab020f96712bcaf4f8c87fc4195dabb4ee6f4d48b143c23fb1ee7575e` | docs claim 746 rows with the same hash (section 8) |

Do not report Server 112, Registration 42, or Mobile 9 for this checkout. Those counts appear in
documents written on other branches.

## 4. Release blockers (P0)

### 4.1 A fresh database cannot satisfy the schema contract from source alone

Evidence:

- `20261026200000_AddWorkoutCalendarAndCompletionCredits.cs` creates
  `app.workout_account_planning_states` (line 13) and `app.workout_completion_day_credits` (line 26)
  with RLS/policies but **no GRANT** to `musclecuties_app`.
- `src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json` requires
  `musclecuties_app:SELECT,INSERT,UPDATE,DELETE` on both tables.
- `services/postgresql/roles-and-grants.sql:59-88` grants app access from a **static table list**
  that omits both tables. Its own comment at line 91 says to review grants when migrations add
  tables. Even running the script after migrations leaves these two tables ungranted.
- Later tables (`workout_injury_*`, `hiit_evidence_capture_challenges`, `strength_style_offers`,
  `workout_injury_omitted_slots`, `workout_cycle_readiness_occurrences`) **do** self-grant in their
  migrations. The two calendar tables are the inconsistent pair.
- The only source of the missing grants is
  `src/MuscleCuties.DbMaintenance/Schema/Reconciliation/20261029_grant_workout_calendar_app_access.sql`,
  a reviewed **candidate** that is not a migration and is not applied by `migrate`.
- `SchemaCompatibilityDatabaseFixture.cs:58-59` and `MigrationChainRehearsalTests.cs:100-101` apply
  that candidate SQL themselves. Therefore the documented “fresh PostgreSQL 17, 59 migrations PASS”
  includes a manual fix and does not prove a pure chain.
- `SchemaReconciliation` fails closed without an approved manifest. No approved manifest is
  committed. The live manifest in `artifacts/` is environment-specific and untracked.
- `SchemaRequirements.generatedFrom.sourceMigrationCount` and
  `migrationHistory.expectedAppliedCount` are both 59; the compiled source also has exactly 59
  `[Migration]` classes and final ID `20261028190000_AddWorkoutCycleReadinessOccurrences`. Do not
  “fix” the count to 68; 69 `.cs` files in `Migrations/` include 9 `.Designer.cs` files and the
  model snapshot.

Required outcome: a fresh database migrated **only** by `DbMaintenance migrate` must produce
`status=compatible` with zero manual SQL. The clean fix is a new idempotent grant migration (making
the source 60 migrations), plus updating `SchemaRequirements` (`sourceMigrationCount`,
`expectedAppliedCount`, final ID), `roles-and-grants.sql`, and removing the candidate-SQL step from
tests. Applying the candidate to staging is not a source fix.

Secondary database findings:

- `app.lock_workout_planning_catalog()` is `SECURITY DEFINER` owned by `musclecuties_migrator`.
  `20261030_grant_workout_catalog_lock_owner_access.sql` grants the owner `USAGE` on `catalog` and
  `UPDATE` on five locked tables. Those grants already exist in
  `roles-and-grants.sql:35-37,95`, so this file corrects an **old live bootstrap**, not a current
  source gap. Keep it as a live-repair record, not as the fresh-install mechanism.
- `/health/ready` does **not** run the schema contract. `PostgreSqlSchemaCompatibilityCheck`
  compares only the EF migration ledger (`services/MuscleCuties.Server/ApiApplication.cs:899-910`).
  A database with all 59 migrations recorded but missing grants can report Healthy. The docs imply
  readiness catches missing privileges; it does not. The release gate must run
  `schema-preflight` and require `compatible`.
- The EF tool cannot validate that the model snapshot matches the 59 hand-written migrations
  (section 3). That is itself a release risk: no automated model drift check exists.

### 4.2 The Lambda ZIP is not reproducible, not the deployed artifact, and contains extras

At snapshot:

| Artifact | Bytes | SHA-256 |
| --- | --- | --- |
| `artifacts/MuscleCuties.Server.zip` | 9,137,263 | `0ba3f418718f3afba2d773c3f7e5788d6d70b19e8aff98ba2a69bdca437540df` |
| `artifacts/MuscleCuties.Server.zip.sha256` | — | records the same `0ba3f418…` |
| `artifacts/build-manifest.json` | — | records `9,136,376` and `0b0358e89d726d107b52b3440dc221d5804db39680beacd147b2915ab6d03e98` |
| Deployed record (`STAGING_SCHEMA_CHANGE_APPROVAL_20261030.md` §7) | 9,136,376 | base64 `CwNY6J1ybRB7UrNEDcIh1YBNs5aAvqzRR7KRWrbQPpg=` = `0b0358…` |

The manifest was written at 00:56; the ZIP was rebuilt at 14:23. The local ZIP is therefore **not**
the artifact recorded as deployed, and the manifest is stale. `tools/rider/package-server.sh` only
runs `dotnet-lambda package` and writes a `.sha256`; it never regenerates `build-manifest.json`.
Only CI’s `validate-package.sh` generates the manifest.

Unexpected package contents (39 entries total):

- `SnapshotRestore.Registry.dll` (19,624 bytes). It is not referenced by any project, NuGet file, or
  source file. It exists in `services/MuscleCuties.Server/bin/{Debug,Release}` and was copied into
  the publish output. Its strings show snapshot/restore callback code, unrelated to MuscleCuties.
  `validate-package.sh` checks required files but does not reject extra assemblies, so this passes
  validation.
- `MuscleCuties.Server.pdb`, `MuscleCuties.Core.pdb`, `MuscleCuties.UseCases.pdb`,
  `MuscleCuties.Infrastructure.PostgreSql.pdb` — source paths/debug symbols are unnecessary in a
  Lambda package.
- `appsettings.Development.json`, `MuscleCuties.Server.staticwebassets.endpoints.json` — development
  artifacts not needed by the deployed function.
- `libSkiaSharp.so` (8.4 MB) **is** intentional: `SkiaPhotoByteSanitizer` uses it and
  `validate-package.sh` requires the ARM64 ELF.
- `artifacts/.DS_Store` and an untracked live reconciliation manifest are present in the same
  directory. The latter contains an owner approval reference and a live database fingerprint; it
  must never be committed or shipped.

Required outcome: clean `bin`/`obj`/publish, rebuild once, regenerate the manifest from the ZIP, and
keep exactly one `artifacts/MuscleCuties.Server.zip` plus its checksum/manifest. The package must
contain no unrelated DLLs, PDBs, development config, or macOS metadata.

### 4.3 Duplicate active-workout contracts and a dead guessing fallback

- Cache contract: `MuscleCuties.UseCases.Features.Caching.ActiveWorkoutSessionSnapshot`
  (`src/MuscleCuties.UseCases/Features/Caching/ILocalCacheService.cs:15-21`) with
  `SessionId, LocalDate, ArchetypeCode, ElapsedSeconds, Exercises, ConcurrencyVersion`.
- HTTP contract with the same name:
  `MuscleCuties.UseCases.Features.Workout.ActiveSession.ActiveWorkoutSessionSnapshot`
  (`src/MuscleCuties.UseCases/Features/Workout/ActiveSession/WorkoutActiveSessionContracts.cs:46-58`)
  with `WorkoutName, StartedAtUtc, Sets, ...`.
- `MuscleCutiesBackendApiClient` imports the **Caching** namespace and calls
  `/api/v1/workouts/sessions/{id}` expecting the cache shape. Because the server returns the HTTP
  shape, it uses `MapActiveWorkoutSnapshot` (`MuscleCutiesBackendApiClient.cs:545-595`) to guess
  camelCase/PascalCase fields. It:
  - invents `DateOnly.FromDateTime(DateTime.UtcNow)` instead of the account’s local date (line 579);
  - invents `ConcurrencyVersion: 1` (line 583);
  - marks every set as completed (`true`, line 569);
  - substitutes `"Workout"`, `"Exercise"`, `1`, `0` for missing data;
  - catches a deserialization failure and returns `null` with a bare `// Fallback` (lines 586-593).
- The only caller of `GetActiveWorkoutWithOfflineCacheAsync` is its own definition
  (`MuscleCutiesBackendApiClient.cs:498`). **Nothing in `src/` or `services/` calls it.** The whole
  method, mapper, and active-workout cache read/write path are dead.
- The live workout client uses the HTTP contract through `CloudNutritionAndWorkoutStores.cs`
  (`Workout.ActiveSession` using at lines 20-22), so the dead path is not needed for today’s UI.

Required outcome: one contract type. Delete the cache-shaped duplicate and the dead method, or move
the cache contract to a distinct name and serialize the HTTP contract, never a hand-written field
guesser. If offline active-workout restore is a product requirement, implement it against the real
contract and test it; do not leave a fallback that fabricates completion state.

### 4.4 Generated meal repair writes per-100-g values into portion fields

`src/MuscleCuties.Infrastructure/Cloud/GeneratedMealResponseRepair.cs:46-55`:

```csharp
Grams = portion.Grams,
Calories = portion.Profile.CaloriesPer100Grams,
Protein  = portion.Profile.ProteinPer100Grams,
Carbs    = portion.Profile.CarbsPer100Grams,
Fats     = portion.Profile.FatsPer100Grams
```

`MealSuggestionComponentDto.Calories/Protein/Carbs/Fats` are consumed as the component’s portion
totals (`NutritionViewModel.Suggestions.cs:274,467,486`). `GeneratedMealPortionRule` already computes
the correct totals in `portion.Nutrients` (`GeneratedMealPortionRule.cs:77-83`). The repair layer is
therefore wrong for every component whose grams differ from 100 g.

Why it exists: it detects a legacy equal-split server response
(`HasLegacyEqualSplit`, lines 57-77) and silently recomputes portions client-side. It is a band-aid
for a server generation bug, has no tests, and is called from three live paths
(`CloudNutritionAndWorkoutStores.cs:73,85,99`).

Required outcome: fix the server generation/serialization so the client receives correct totals, and
delete the repair layer. If a transitional repair is unavoidable, use `portion.Nutrients` and add a
test with grams ≠ 100.

### 4.5 CI cannot upload or deploy the current release branch

`.gitlab-ci.yml`:

- `upload_lambda_artifact` is manual only for `codex/plan-generation-checkpoint` or
  `test-flight-beta` (lines 212-215).
- `deploy_staging_lambda` is manual only for `codex/plan-generation-checkpoint` (lines 256-259).
- `deploy_production_lambda` is `when: never` (lines 261-267).

The current branch (`codex/backend-hardening-20261030`) and the local
`test-flight-beta-release-gate` branch match none of those rules. A green pipeline can build and
validate a package but cannot upload or deploy it. The docs say “GitLab pipeline on the new branch
not run”; that is accurate, but the gate is structural, not just an unrun job.

Also: `package_staging_lambda` does not validate the ZIP’s **absence** of unexpected files; it only
checks required files, hashes, runtime, and secrets. `SnapshotRestore.Registry.dll` would pass.

### 4.6 The checkout does not currently build its Server test project

At snapshot, `tests/MuscleCuties.Server.Tests/ServerMediaSafetyTests.cs:320-323` had an in-progress
edit with malformed interpolated strings (`$"{{{\"bucket\"…`). `dotnet test` failed with `CS1056`
and `CS1003`. `git blame` marked the lines “Not Committed Yet”. Another agent was editing the file.
This is a **transient** failure, not proof that HEAD is broken, but it means no worker may start from
the dirty tree and assume a green baseline. Re-run the Server suite after the edit settles and record
the real count.

### 4.7 The preferences route removal is only half-landed

At HEAD, `CloudProfileWorkoutPreferencesStore.SaveAsync` still sends
`PUT /api/v1/profile/workout-preferences`, which the server rejects with `409`
(`WorkoutPreferencesRequireProposal`). The dirty tree removes that write path and renames the screen
action to Continue. The docs describe the removal as current, but a clean HEAD checkout still has the
retired call. Treat the dirty work as in-flight ticket A1: verify it, do not re-implement it in
parallel, and do not ship a build from a tree where half the rename is applied.

## 5. Architecture violations and duplication

### 5.1 Namespace squatting (the central structural defect)

143 files under `src/MuscleCuties.Infrastructure` and
`src/MuscleCuties.Infrastructure.PostgreSql` declare `MuscleCuties.Core.*` or
`MuscleCuties.UseCases.*` namespaces (excluding `bin`/`obj`). Examples:

- `src/MuscleCuties.Infrastructure/Services/Workout/Planning/ReadinessEngine.cs:7` declares
  `MuscleCuties.Core.Services.Workout.Planning`.
- `src/MuscleCuties.Infrastructure/Models/Entities/Users/UserProfile.cs:1` declares
  `MuscleCuties.Core.Models.Entities.Users`.
- `src/MuscleCuties.Infrastructure/Services/Auth/AuthInputValidator.cs:1` declares
  `MuscleCuties.Core.Services.Auth`.

Namespace counts: 20 `Core.Services.Auth`, 11 `Core.Services.Workout.Planning`, 11
`Core.Services.Nutrition`, 11 `Core.Services.Nutrition.Planning`, 9 `Core.Services.Health`, 9
`Core.Models.Entities.Workout`, 9 `Core.Models.Entities.Nutrition`, 7 `Core.Services.ExerciseMedia`,
7 `Core.Services.Cycle.Planning`, 7 `Core.Models.UI.Workout`, 5 `Core.Models.Entities.Users`, etc.

Why it matters:

- `MuscleCuties.UI` compiles against `MuscleCuties.Core.Models.*` and
  `MuscleCuties.Core.Services.*`; some of those types physically live in the Infrastructure
  assembly. Deleting or moving Infrastructure can therefore break “Core” consumers in ways the
  project graph does not show.
- `CleanArchitectureBoundaryTests` only inspects the Core assembly/csproj and forbids
  `using MuscleCuties.Core.Domain` in ViewModels. It does **not** assert that a `Core.*` namespace is
  declared in the Core project. The 143 files pass.
- Dead-code analysis is ambiguous: a file can look like Core domain code and still be in an outer
  project.

This is not a rename-only cleanup. The worker must move the live types into
`src/MuscleCuties.Core` (or rename them to a truthful outer namespace and update every consumer),
compiling after each bounded batch. Do not move `MuscleCuties.Infrastructure.Models` wholesale:
`UserProfile` is still used by the live `CloudProfileNutritionSettingsStore` for a planning profile
(`CloudAccountAndProfileStores.cs:926`), and the nutrition planning models are used by the live
`NutritionPlanner`.

### 5.2 Duplicate and confusing types

| Duplicate | Locations | Assessment |
| --- | --- | --- |
| `ActiveWorkoutSessionSnapshot` | `UseCases/Features/Caching/ILocalCacheService.cs:15` and `UseCases/Features/Workout/ActiveSession/WorkoutActiveSessionContracts.cs:46` | Two public contracts, different shapes; root cause of section 4.3 |
| `IReadinessEngine` | `Core/Domain/Workout/ReadinessEngine.cs:9`; `Infrastructure/Services/Workout/Planning/ReadinessEngine.cs:10` | Second interface extends the first; UI registers `DailyReadinessEngine`, server registers the Core engine; no production endpoint consumes either |
| `InjuryObservationRule` | `Core/Domain/Workout/` and `Infrastructure/Services/Workout/Planning/` | Parallel legacy/new rule |
| `AuthInputValidator` | `Core/Domain/Auth/`; `Infrastructure/Services/Auth/` | Second is a pure delegating facade; redundant |
| `INutritionChangeNotifier` + `NutritionChangedEventArgs` | `UseCases/Features/Nutrition/DailyLog/`; `Core.Services.Nutrition` (physically Infrastructure) | Two interfaces with the same name, bridged by `NutritionChangeNotifierAdapter`; DI registers both |
| UI models | `Infrastructure/Models/UI/**` (8 files) vs `UI/Models/UI/**` | Infrastructure copies are `Core.Models.UI.*`, used only by legacy planners/tests |
| Catalog entities | `Infrastructure/Models/Entities/**` vs `Infrastructure.PostgreSql/Models/Catalog/**` | Legacy domain entities vs persistence repo models; only some legacy entities still live |
| `ChangePasswordBody` | `UseCases/Features/Account/Authentication/AccountAuthenticationContracts.cs`; `services/MuscleCuties.Server/ApiApplication.cs` | Same DTO defined twice |
| `CycleObservation` | Core domain, Infrastructure cycle planning, UseCases cycle history | Three representations |
| `DashboardTrendPoint`, `ExerciseSetInputItem`, `ExerciseSwapOption`, `ScheduleDayItem`, `WorkoutPresentationModels`, `WorkoutSessionDetail`, `WorkoutExerciseItem` | Infrastructure/Models/UI + UI/Models/UI | Short-name duplicates; only UI copies are live |

### 5.3 Oversized, multi-responsibility files

- `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/DatabaseSchemaCompatibility.cs` (1,899
  lines): schema introspection, contract model, fingerprinting, evaluation, and failure formatting
  in one static class.
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Session/PostgresWorkoutPlanAndSessionStore.cs`
  (1,643 lines): plan, session, prescription, and reconciliation persistence in one store.
- `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs` (1,630 lines) and
  `WorkoutSessionViewModel.cs` (1,308 lines): iterative UI fixes layered into god objects.
- `src/MuscleCuties.Infrastructure/Cloud/CloudAccountAndProfileStores.cs` (1,131 lines): at least
  six store classes and a legacy quiz-question upgrader in one file.
- `services/MuscleCuties.Server/ApiApplication.cs` (949 lines): composition root, all route groups,
  health checks, and failure mapping.
- `src/MuscleCuties.UI/MauiProgram.cs` (875 lines): 176 registrations plus view-model factories.

These are not automatically “delete” targets, but they are where the bad fixes accumulate. Refactor
only after the P0 behavior is fixed and tested.

### 5.4 Repo-level build workarounds

`Directory.Build.targets` contains:

- `RemoveTraceFromReleaseConstants` — mutates `DefineConstants` before compile.
- `RemoveMissingAnalyzers` — removes analyzers whose files do not exist.
- `CopyTestDependencies` — copies `Microsoft.VisualStudio.CodeCoverage.Shim.dll` and
  `Amazon.Lambda.APIGatewayEvents.dll` into test output from hard-coded NuGet paths/versions.

These are environment patches, not product code. They hide restore/workload problems and should be
replaced by correct package references or removed once the build is reproducible. The hard-coded
versions are a maintenance trap.

## 6. Dead, unreachable, and test-covered-but-unused code

### 6.1 Confirmed production-dead

| Item | Evidence | Action |
| --- | --- | --- |
| `LegacyAccountRegistrationGateway`, `LegacyRegistrationOtpGateway`, `LegacyAccountRecoveryStore`, `LegacyAccountSignInStore` | Zero references outside their own files (grep over `src`, `services`, `tests`) | Delete |
| `GetActiveWorkoutWithOfflineCacheAsync` + `MapActiveWorkoutSnapshot` + active-workout cache load/save | Only definition/call inside itself; no UI/server caller | Delete with the duplicate contract (section 4.3) |
| `IWorkoutPlanner` / `WorkoutPlanner` | Registered at `MauiProgram.cs:311`; zero consumers outside registration/implementation | Delete the registration and the legacy workout planner stack |
| `IGatingEngine` / `GatingEngine` | `MauiProgram.cs:369`; zero consumers | Delete |
| `ICyclePhaseCalculator` / `CyclePhaseCalculator` | `MauiProgram.cs:308`; zero consumers | Delete |
| `ICyclePredictionPlanner` / `CyclePredictionPlanner` | `MauiProgram.cs:309`; zero consumers | Delete |
| `IDashboardPlanner` / `DashboardPlanner` | `MauiProgram.cs:310`; zero consumers | Delete |
| `IExerciseMediaStore` / `LocalExerciseMediaStore` | `MauiProgram.cs:302`; zero consumers | Delete unless the UI gains a media caller |
| `IFdcApiClient` / `FdcApiClient` / `FdcAirGapHandler` | `MauiProgram.cs:283-290`; zero consumers | Delete with its HTTP registration |
| `IReadinessService`, `ReadinessHistoryInputBuilder`, `ReadinessObservation` | Zero references outside their own files/registrations | Delete after the readiness path is settled |
| `EvaluateReadinessUseCase` + `IEvaluateReadinessUseCase` | Registered at `ApiApplication.cs:308-309`; no endpoint injects it | Delete or wire a real route; do not leave a registered dead use case |
| `WeeklyWorkoutPlanGeneratorRule` | Only its own file; `WeeklyWorkoutPlanGeneratorRuleTests` covers it | Delete rule + test |
| `ProgressStreakRule` | Only its own file (grep outside tests); test-covered | Delete if no dashboard caller |
| `VolumeBudgetRule` | Only its own file (grep outside tests) | Delete with tests if unused |
| `InitialWorkoutPlanRule` | Only its own file; no production caller | Delete |
| `src/MuscleCuties.App` | No project file; only `bin/`/`obj/` | Delete directory |
| `services/MuscleCuties.WorkoutReconciliation` | Separate host; absent from `.gitlab-ci.yml` and the Lambda package; README requires a scheduler that is not configured | Decide product scope. If retained, add it to release gates/tests; if not, delete it. It cannot be part of “one server.zip”. |

### 6.2 Legacy subtrees that become dead once the planner registrations are removed

Counts (files, excluding generated output):

- `src/MuscleCuties.Infrastructure/Services/Workout/Planning` — 11
- `src/MuscleCuties.Infrastructure/Services/Cycle/Planning` — 7
- `src/MuscleCuties.Infrastructure/Services/Dashboard/Planning` — 4
- `src/MuscleCuties.Infrastructure/Models/UI` — 8
- `src/MuscleCuties.Infrastructure/Models/Workout` — 2
- `src/MuscleCuties.Infrastructure/Models/Entities/Workout` — 13

Do not delete blindly:

- `NutritionPlanner` and its planning models are live via
  `CloudProfileNutritionSettingsStore(INutritionPlanner? planner = null)` (used at
  `CloudAccountAndProfileStores.cs:923-940`). Keep the nutrition planning chain.
- `UserProfile` is live for the same planning profile. Move it, do not delete it.
- `Core.Domain.Workout.ReadinessEngine` is registered by the server but not consumed; decide whether
  it is a domain foundation to keep or dead code to remove. Do not remove the Core cycle/readiness
  rules that the live preview/reconciler uses.

### 6.3 Tests that should be deleted or rewritten with the dead code

- `tests/MuscleCuties.Core.Tests/WeeklyWorkoutPlanGeneratorRuleTests.cs` — tests a production-dead
  generator.
- `tests/MuscleCuties.Core.Tests/WorkoutExerciseEffortSliderTests.cs` — uses the dead
  `Core.Models.UI.Workout` presentation types.
- Any tests for `ProgressStreakRule`, `VolumeBudgetRule`, `InitialWorkoutPlanRule`, `GatingEngine`,
  `WorkoutPlanner`, `CyclePredictionPlanner`, `DashboardPlanner` — verify by type reference first;
  delete with the production code, not before.
- `tests/MuscleCuties.Core.Tests/DatabaseSchemaCompatibilityTests.cs` and
  `tests/MuscleCuties.Registration.Integration.Tests/DatabaseSchemaCompatibilityTests.cs` are not
  duplicates: the first tests the evaluator, the second mutates a real disposable database. Keep
  both, but remove the candidate-SQL application from the fixture once the grant migration exists.

### 6.4 Duplicate/redundant code that is not dead

- `AuthInputValidator` facade in `Core.Services.Auth` delegates every member to
  `Core.Domain.Auth.AuthInputValidator`; collapse to one.
- `NutritionChangeNotifier` bridge exists only to reconcile two same-named interfaces; collapse to
  the UseCases contract.
- `ChangePasswordBody` is declared in both the UseCases contract file and the server; use the
  contract.
- `ActiveWorkoutSessionSnapshot` duplicate is a correctness issue, not just cleanup (section 4.3).

## 7. Fallbacks, stubs, and “fix in a separate file” patterns

| Pattern | Location | Why it counts | Gate |
| --- | --- | --- | --- |
| Manual JSON field guessing with fabricated defaults | `MuscleCutiesBackendApiClient.cs:545-595` | Hides contract mismatch; invents local date, version, and completed sets | Delete with dead method |
| Silent legacy meal response rewrite | `GeneratedMealResponseRepair.cs` | Client-side repair of a server bug; wrong per-portion macros | Fix server, delete repair |
| Legacy quiz-question rewrite | `CloudAccountAndProfileStores.cs:491-567` | Upgrades an old server question shape client-side; masks stale server catalog | Move to server migration or delete after catalog contract is fixed |
| Legacy hydration baseline fallback | `PostgresHydrationStore.cs:150-193` | Reads old `diary_water_ml` when no intake days exist; intentional compatibility, but untested against fresh data | Keep only with a documented migration/deprecation date |
| Refresh token rotation ignores caller cancellation | `MuscleCutiesBackendApiClient.cs:193` (`CancellationToken.None`) and `228-250` (`WriteTokensAsync(..., None)`) | Prevents half-rotation, but can outlive a cancelled request; document as deliberate | Keep with a comment/test |
| Two nutrition change notifiers | `UseCases/.../INutritionChangeNotifier.cs`; `Core.Services.Nutrition`; adapter | Duplicate abstraction created to bridge layers | Collapse |
| `WorkoutPreferencesLegacyNormalizer` | Deleted in the dirty tree | Correct removal; verify all callers/tests are gone | Confirm before merge |
| Disabled health adapter | `services/.../DisabledHealthSyncService.cs` | Intentional V1 boundary; must stay dormant | Keep; do not wire |
| `// Fallback` in `MapActiveWorkoutSnapshot` | line 590-593 | Returns `null` after a swallowed `JsonException`; caller then silently fails | Delete |

Also present but not a fallback: `PostgresDbContext` uses `EnableRetryOnFailure`; that is a transient
retry, not a data fallback. Keep it.

## 8. Documentation claims that the code does not support

The docs are append-only and contradictory. Treat every undated/unsuffixed claim as suspect. The
following are concrete discrepancies at this snapshot.

| Claim | Where | Reality | Verdict |
| --- | --- | --- | --- |
| Catalog manifest has **746** rows | `docs/SUBMISSION_READINESS.md:14`; `docs/v1/ROADMAP.md:43` | `CanonicalCatalogManifest.json.summary.rowCount` is **742**; its SHA-256 is `25d463…`, the same hash the doc prints | **False count**; fix docs to 742 |
| Manifest hash is `927597…` | `docs/v1/STATUS.md:308` | Actual SHA-256 is `25d4637a…` | **Stale hash**; historical entry |
| Checkout is `release/musclecuties-testflight-beta-hardening` through `652c1f31` | `docs/beta-orchestration/CURRENT_CHECKPOINT.md:3-5` | Branch is `codex/backend-hardening-20261030`, HEAD `467cbbda` | **Wrong checkout**; cannot resume from it |
| State is `RUNNING` with owner/lease | `docs/beta-orchestration/STATE.json:3-8` | Checkpoint says `HANDOFF_READY`; branch in state is not local; lease timestamp is historical | **Stale lease**; replace before dispatch |
| iOS is blocked by `NETSDK1147` / missing workload | `CURRENT_CHECKPOINT.md:20`; `RELEASE_READINESS.md:18,25,31` | `maui-ios` 10.0.20 is installed. The build reaches code signing and fails with `No valid iOS code signing keys found in keychain` | **Wrong blocker for this host**; actual gate is signing |
| Staging is `DEPLOYED_AND_VERIFIED` **and** still at 31/59 with no deployment authorized | `docs/infra/checkpoints/aws-lambda-postgres-final-integration.md:5` vs `:8, :94, :142-144` | The same document contains both states. The later approval document says 59/59 + deployed | **Internal contradiction**; rewrite as history + current state |
| Fresh PostgreSQL “59 migrations and reviewed catalog setup PASS” | `CURRENT_CHECKPOINT.md:18` | The fixture applies `20261029_grant_workout_calendar_app_access.sql`; a pure `migrate` is incompatible | **Misleading**; manual SQL is part of the pass |
| Full integration is `132/132` | `TASK_LEDGER.md:16`; `RELEASE_READINESS.md:21-23` | The same readiness file first says “130 post-fix … plus a separately passed load test” | **Contradictory counts**; re-run and record one number |
| Server 82/90/112, Registration 41/42, Mobile 8/9 | `docs/v1/STATUS.md`, `RELEASE_READINESS.md`, `AWS_BACKEND_REPAIR_20261030.md` | At snapshot: Core 989, Registration 36, Mobile 16; Server not runnable due active edit | **Stale counts**; do not reuse |
| Current UI already removes the retired preferences PUT | `docs/infra/AWS_STAGING_REPAIR_AND_FALLBACKS_20261030.md` §1.3 | Committed HEAD still has `CloudProfileWorkoutPreferencesStore.SaveAsync` calling it; only the dirty tree removes it | **Describes uncommitted state** |
| All `PG01–PG10` phases accepted as code foundations | `docs/v1/ROADMAP.md:47` | Dead/duplicated modules and an incompatible fresh schema remain | **Overstated**; replace with gate register |
| `/health/ready` proves schema compatibility | `AWS_STAGING_REPAIR_AND_FALLBACKS_20261030.md` §3.1 and failure matrix | Readiness checks only the migration ledger, not grants/tables/columns | **Overstated**; preflight is the gate |
| Production signature protection is implemented | `docs/infra/checkpoints/lambda-release-orchestrator.md` (historical) | Deployment checkpoint says it is not implemented and the production job is disabled | **Stale/contradictory** |
| TestFlight `1.0 (2)` verifies the candidate | `SUBMISSION_READINESS.md` first paragraphs | The document itself says it does not; build 2 predates current source | **Already corrected**, keep the correction |

Accurate claims worth preserving:

- Core 989 is accurate for this checkout.
- 59 migration classes and final ID `20261028190000_AddWorkoutCycleReadinessOccurrences` are accurate.
- Catalog SHA-256 `25d463…` is accurate; only the row count is wrong.
- The server deliberately has no fallback database and `RunMigrationsOnColdStart=false`; the code
  supports that.
- `DisabledHealthSyncService` is intentionally dormant.

## 9. Database and schema inventory

| Item | State |
| --- | --- |
| Migration classes | 59 (`Migrations/*.cs` with `[Migration]`; excludes 9 `.Designer.cs` and the snapshot) |
| Final migration | `20261028190000_AddWorkoutCycleReadinessOccurrences` |
| Tables created by chain | 73 distinct `schema.table` names |
| Schema contract | `SchemaRequirements.json`, version 1.0.0, 11 required tables, 46 RLS entries, 48 policies, 10 roles, 2 functions, 27 privileges |
| Contract source count | 59; matches migration count |
| Raw SQL table references | All referenced tables exist in migrations (static check) |
| Contract compatibility on pure fresh chain | **Fails** on two missing app grants (section 4.1) |
| Approved reconciliation manifest committed | **No**; candidate SQL only, `schema-reconcile` fails closed |
| Live staging state | Per docs: 59/59 applied, `compatible`, catalog grants applied, Lambda deployed. Not reverified here (no live credentials used) |
| EF model drift check | **Not possible** with current Design package settings |
| Catalog seed manifest | 742 rows, hash `25d463…`; reviewed catalog JSON hash `399a32…` |

Deployment order trap: `services/postgresql/roles-and-grants.sql` is documented “run after applying
the application migrations” (line 1), but its static app-table list omits the two calendar tables.
Running it after migrations does **not** fix them. The only correct source fix is self-granting
migrations (or generated grants), not another manual reconciliation.

## 10. Lambda, packaging, and CI inventory

Artifact facts are in section 4.2. Additional CI facts:

- Validation chain: `verify_source` → `verify_plan_generation` (Core/Server/Registration/catalog
  tests + release-engine Python tests) → `verify_postgres_compatibility` (fresh PG17, 59
  migrations, schema tests) → `verify_mobile_contracts` → `package_staging_lambda` →
  `validate_lambda_artifact` → `verify_lambda_arm64` → `upload_lambda_artifact` (manual) →
  `deploy_staging_lambda` (manual) / `deploy_production_lambda` (never).
- `verify_source` builds only the Server; it does not build the UI or the MAUI client.
- `verify_mobile_contracts` runs only the contract tests, not an iOS build.
- `verify_postgres_compatibility` installs PostgreSQL 17 and runs the schema tests; it does not
  apply a live schema. It is the closest thing to a reproducible DB gate, but it currently relies on
  the candidate grant SQL in the fixture.
- `package_staging_lambda` runs `dotnet-lambda package` and `validate-package.sh`; it does not run
  `git clean`, so a dirty build output can contaminate the ZIP.
- `validate-package.sh` rejects tracked modifications, wrong SDK/tool versions, duplicate/unsafe
  paths, symlinks, likely secrets, non-ARM64 ELF, missing required files, wrong runtime, missing
  embedded catalog. It does **not** reject unexpected DLLs, PDBs, development settings, or macOS
  metadata.
- Upload/deploy branch rules are stale (section 4.5).
- `tools/deployment/release.sh` and `release-engine.py` implement the AWS release state machine and
  fail-closed tests, but they were exercised with fake AWS/provider executables, not a real release.
  Do not treat them as live evidence.

## 11. Checkpoint and handoff file register

Legend: **KEEP** = authoritative/current reference; **UPDATE** = keep but rewrite state; **ARCHIVE**
= historical evidence, move out of the active set; **DELETE** = no longer useful once superseded.

| File | Role | Status | Action |
| --- | --- | --- | --- |
| `AGENTS.md` | Architecture/privacy/build rules | Authoritative | KEEP |
| `README.md` (root) | Project overview | Current-ish | UPDATE after cleanup |
| `docs/README.md` | Docs index | Stale links/status | UPDATE |
| `docs/SUBMISSION_READINESS.md` | Release gates | Current, one false row count | UPDATE (742) |
| `docs/BETA_PRODUCTION_READINESS_AUDIT_20261029.md` | B1–B6 criteria | Baseline | KEEP; mark dates/history |
| `docs/BETA_EXECUTION_CHECKPOINT.md` | Windows handoff, `6d7ee45` | Superseded | ARCHIVE |
| `docs/beta-orchestration/CURRENT_CHECKPOINT.md` | 2026-10-30 handoff | Wrong branch/commit | ARCHIVE after this inventory |
| `docs/beta-orchestration/STATE.json` | Lease | Stale/mismatched | DELETE/rewrite for new orchestration |
| `docs/beta-orchestration/TASK_LEDGER.md` | B1–B6 gate register | Current but stale counts | UPDATE |
| `docs/beta-orchestration/WORK_CONTRACT.md` | Standing rules | GitLab lease model not applicable locally | KEEP rules, UPDATE coordination |
| `docs/beta-orchestration/RESUME_PROMPT.md` | GitLab fetch/lease resume | Obsolete | REPLACE |
| `docs/beta-orchestration/OWNER_RELEASE_GUIDE.md` | Owner approval draft | Current | KEEP |
| `docs/beta-orchestration/RELEASE_READINESS.md` | Gate summary | Contradictory counts | UPDATE |
| `docs/beta-orchestration/BACKEND_HARDENING_TICKETS_20261030.md` | Wave 1 tickets | Current; A1 in flight | KEEP; mark A1 in progress |
| `docs/beta-orchestration/evidence/*.md` | Run evidence | Historical/immutable | KEEP as evidence |
| `docs/infra/checkpoints/lambda-release-orchestrator.md` | CI/release engineering | Historical/blocked | ARCHIVE |
| `docs/infra/checkpoints/postgres-schema-orchestrator.md` | DB compatibility work | Historical/blocked | ARCHIVE |
| `docs/infra/checkpoints/aws-lambda-postgres-final-integration.md` | Integration record | Internally contradictory | ARCHIVE; keep corrected execution facts |
| `docs/infra/STAGING_SCHEMA_CHANGE_APPROVAL_20261030.md` | Owner approval + execution | Authoritative live record | KEEP immutable |
| `docs/infra/AWS_STAGING_REPAIR_AND_FALLBACKS_20261030.md` | Incident/fallback matrix | Current live record; one uncommitted-state claim | KEEP; annotate |
| `docs/infra/AWS_BACKEND_REPAIR_20261030.md` | Local repair evidence | Historical | ARCHIVE |
| `docs/infra/AWS_BACKEND_REPAIR_20261029.md` | Earlier repair | Historical | ARCHIVE |
| `docs/infra/AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md` | 730-line plan | Historical plan | KEEP reference, mark historical |
| `docs/infra/AWS_RELEASE_INFRASTRUCTURE_SPEC.md` | Release spec | Reference | KEEP |
| `docs/infra/ORCHESTRATOR_BACKEND_HARDENING_20261030.md` | Ticket source | Reference | KEEP |
| `docs/infra/POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md` | Schema runbook | Current procedure | KEEP |
| `docs/infra/POSTGRES_PRIVILEGE_SEPARATION_PLAN.md` | Runtime principal cutover | Open/blocked | KEEP |
| `docs/infra/POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md` | PITR drill | Procedure, no evidence | KEEP; do not claim drill |
| `docs/v1/ROADMAP.md` | Append-only roadmap log | Contradictory history | ARCHIVE |
| `docs/v1/STATUS.md` | Append-only status log | Hundreds of stale counts | ARCHIVE; keep as history |
| `docs/workout-migration-checkpoints/*` | Dated implementation history | Historical | ARCHIVE |
| `docs/workout-migration-checkpoints/implementation-20261028.md` | Latest workout history | Historical, some open gates | ARCHIVE after extracting open items |
| `docs/ci-injury-clock-20261028.md`, `docs/nutrition-breakfast-oil-cap-20261028.md`, `docs/validation-20261028.md` | Dated checkpoints | Historical | ARCHIVE |
| `docs/BETA_ORCHESTRATOR_PROMPT.md` | Old orchestrator prompt | Superseded by the 5 new prompts | ARCHIVE |
| `docs/WORKOUT_GENERATION_RULES_AUDIT.md`, `docs/WORKOUT_PLANNING_MIGRATION_INVENTORY.md`, `docs/WORKOUT_TEMPLATE_FLOOR_REVIEW_GATE.md` | Product rules/inventory | Mixed current/history | KEEP rules; reconcile with code |
| `docs/UI_PRESENTATION_DESIGN_SPEC.md`, `docs/UI_PRESENTATION_MIGRATION_MATRIX.md` | UI spec | Reference | KEEP |
| `docs/OPERATOR_ACTION_MANUAL.md`, `docs/EMAIL_DELIVERABILITY.md` | Operations | Reference; historical AWS observations | KEEP; verify before use |

Rules for the cleanup:

- Do not delete evidence documents that record a live change or an owner approval
  (`STAGING_SCHEMA_CHANGE_APPROVAL_20261030.md`, `AWS_STAGING_REPAIR_AND_FALLBACKS_20261030.md`,
  `docs/beta-orchestration/evidence/*`). Move them to `docs/archive/` if the active set must shrink.
- `docs/v1/STATUS.md` and `docs/v1/ROADMAP.md` are not current state. Do not “fix” every historical
  count; replace them with a short current-state file and archive the logs.
- Keep exactly one active checkpoint for the new orchestration. `CURRENT_CHECKPOINT.md` and
  `STATE.json` must not remain as competing sources of truth.

## 12. Proposed work-package split (for the next step, not yet written)

The next step writes five prompt starters. The boundaries below are the ones that keep the four
implementation workers from editing the same files.

### Worker 1 — Database and schema contract (P0)

Owns: `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/**` (including migrations and
`DatabaseSchemaCompatibility`), `src/MuscleCuties.DbMaintenance/**`,
`services/postgresql/**`, `tests/MuscleCuties.Registration.Integration.Tests/**` database fixtures.

Deliverables:

1. A fresh `migrate` chain produces `compatible` with **zero** manually applied SQL. Add the grant
   migration, update the contract count/final ID, update `roles-and-grants.sql`, and remove the
   candidate-SQL dependency from tests.
2. A disposable PostgreSQL 17 run proving fresh install, 59→60 upgrade, repeated-migrate no-op,
   RLS/tenant isolation, and schema-preflight exit codes.
3. A model-vs-migration check that is actually runnable (fix the Design package/startup project, or
   add an equivalent test).
4. A documented decision on the two live reconciliation SQL files: keep as live repair records, not
   as install prerequisites.

Must not touch: server routes, UI, packaging scripts, docs outside the DB runbook.

### Worker 2 — Server, Lambda package, and CI gate (P0)

Owns: `services/MuscleCuties.Server/**`, `tools/deployment/**`, `.gitlab-ci.yml`,
`tests/MuscleCuties.Server.Tests/**`, `tests/MuscleCuties.Deployment.Tests/**`.

Deliverables:

1. Server builds and its tests pass from a clean checkout after the in-flight edits settle.
2. `/health/ready` semantics are truthful (ledger **and** the schema contract, or an explicit
   documented split); the release gate cannot pass on a grant-missing DB.
3. Dead DI registrations and unused endpoints/use cases removed or wired (section 6.1), with route
   parity tests updated.
4. Exactly one reproducible `artifacts/MuscleCuties.Server.zip`: clean build, no `SnapshotRestore`,
   no PDBs, no development config, manifest generated from the same bytes.
5. CI rules allow the intended release branch to upload/deploy, with production still disabled;
   `validate-package.sh` rejects unexpected assemblies/files.

Must not touch: migrations, schema requirements, UI, Core domain moves.

### Worker 3 — Client contracts and client behavior (P0/P1)

Owns: `src/MuscleCuties.UI/**`, `src/MuscleCuties.Infrastructure/ApiClient/**`,
`src/MuscleCuties.Infrastructure/Cloud/**`, `src/MuscleCuties.Infrastructure/Caching/**`,
`src/MuscleCuties.UseCases/Features/Workout/**` contract files, `src/MuscleCuties.UseCases/Features/Profile/WorkoutPreferences/**`,
`tests/MuscleCuties.Mobile.Contracts.Tests/**`, `tests/MuscleCuties.Core.Tests/Cloud*` and client-facing
tests.

Deliverables:

1. One `ActiveWorkoutSessionSnapshot`; delete the dead offline-cache path and manual JSON guesser.
2. Fix or remove `GeneratedMealResponseRepair` after the server fix; add a grams≠100 test.
3. Finish and verify ticket A1 (retired preferences PUT removal, Continue semantics, no false
   “saved” copy). Do not re-implement the in-flight work; reconcile it.
4. Remove dead DI registrations and dead UI-model copies only after Worker 4 proves no callers.
5. Client builds for iOS (or records the exact signing blocker) and the mobile contract tests pass.

Must not touch: Server, migrations, schema contract, docs outside its checkpoint.

### Worker 4 — Dead code, architecture, and docs consolidation (P1)

Owns: `src/MuscleCuties.Core/Domain/**` dead rules, `src/MuscleCuties.Infrastructure/Models/**`
legacy subtrees, `src/MuscleCuties.Infrastructure/Services/{Workout,Cycle,Dashboard}/Planning/**`,
`src/MuscleCuties.Infrastructure/Account/**/Legacy*.cs`, `src/MuscleCuties.App`, `docs/**`
(consolidation only), and tests tied to the removed types.

Deliverables:

1. Delete confirmed dead subtrees and their tests (section 6), with `rg` proof and a clean build after
   each batch.
2. Move or rename namespace-squatted files in bounded batches, starting with the dead ones. Do not
   attempt a 143-file big-bang rename.
3. Consolidate docs: one current-state file, one gate register, one handoff checkpoint; archive
   historical logs; fix the 746/742 and iOS claims.
4. Produce a deletion manifest (file → reason → replacement) for the reviewer.

Must not touch: live Cloud/UI/server behavior, migrations, or files owned by Workers 1–3. Worker 4
runs after the other three have merged, or in a worktree that only deletes files already proven
unreferenced.

### Worker 5 — Independent reviewer (read-only)

Owns: no product files. Produces a verdict report and a list of blocking findings. Must run its own
commands, not trust worker checkpoints. Repeats at minimum:

- clean build of Server, Core, UseCases, UI (iOS target as far as signing allows);
- Core/Server/Registration/Mobile tests with real counts;
- fresh disposable PostgreSQL `migrate` + `schema-preflight` with no manual SQL;
- ZIP content/hash/manifest consistency and absence of extras;
- route parity for the client/server pairs;
- dead-code deletion spot checks;
- docs claim spot checks.

The reviewer must not “fix while reviewing”. Any repair goes back to the owning worker.

## 13. Execution order and handoff protocol

1. **Freeze the dirty tree.** Identify the in-flight WorkoutPreferences/photo-vault/server-test work.
   Commit it on a branch or preserve it in a worktree. Do not start four workers on this checkout.
2. **Create four worktrees** from one frozen commit. Each worker gets one checkpoint file and writes
   only inside its ownership boundary.
3. **Run Workers 1–3 in parallel.** They have disjoint files. Worker 4 waits for their merge (or
   works only on files no other worker touches) because its deletions require the new DI/route
   state.
4. **Merge in order:** database contract → server/package/CI → client → dead code/docs. Re-run the
   affected tests after each merge; do not carry a green result across a changed file.
5. **Hand off to Worker 5** with: frozen commit, artifact hash, DB fingerprint/migration head, test
   counts, and the worker checkpoint paths. The reviewer writes the final gate report; no code edits.
6. **Only after the reviewer passes** may the owner consider staging deployment or TestFlight. Live
   migration, production deployment, secret rotation, and distribution remain separately
   authorization-gated.

Checkpoint rules for the workers:

- One file per worker under `docs/orchestration/` (to be created with the prompts) recording:
  starting commit, files changed, commands run, real results, remaining blockers, and the exact next
  action.
- Never mark a gate passed from a historical document or another worker’s report.
- Never delete evidence of a live change or an owner approval.
- If a worker must touch a file outside its boundary, stop and record the conflict instead of
  editing it.

## 14. Limits of this inventory

- No live AWS, RDS, S3, Resend, Apple, or App Store Connect access was used. Live staging statements
  are quoted from the approval/repair documents, not reverified.
- No disposable PostgreSQL instance was available, so the schema contract was inspected statically
  and through the existing tests/docs. Do not treat the “59/59 compatible” claim as independently
  reproduced here.
- The EF model drift check could not run; the repository has no working design-time check as
  configured.
- The iOS build did not complete. The workload is present; signing is the observed blocker. A
  signing-disabled compile was attempted and timed out.
- The working tree changed during inspection. Counts and line numbers for dirty files may already
  be stale; re-grep before editing.
- `artifacts/` is git-ignored and was not committed. The live reconciliation manifest there contains
  an owner approval reference and must remain out of source control.

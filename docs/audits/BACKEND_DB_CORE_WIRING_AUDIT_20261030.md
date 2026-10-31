# Backend DB, Core and wiring audit — 2026-10-30

Status: read-only structural audit. No application code, migration, grant, secret or environment was
changed. Every number below was measured in this checkout on 2026-10-30; the evidence command or file
line is given next to it.

This audit complements, and does not repeat, [`RELEASE_INVENTORY_20261030.md`](../RELEASE_INVENTORY_20261030.md)
(P0 release blockers) and `docs/infra/*` (cutover runbooks). It answers a different question: **what
the persistence layer, the Core rules and the server wiring actually are, how they connect, and where
the structure itself creates risk.**

## 0. How to read this

| Artifact | What it answers | Size |
| --- | --- | --- |
| `graphify-out/graph.html` | Interactive symbol graph; click a community, search a class, follow an edge | 5,004 nodes · 11,507 edges · 364 communities · 8.0 MB |
| `graphify-out/wiki/index.md` | Agent-crawlable wiki: `index.md` plus one article per community and per god node | 374 articles |
| [`BACKEND_GRAPH_REPORT_20261030.md`](BACKEND_GRAPH_REPORT_20261030.md) | graphify's own report, committed: god nodes, cohesion, hyperedges, gaps, suggested questions | 1,395 lines |
| `graphify-out/graph.json` | Raw graph for GraphRAG or Neo4j import | 5,004 nodes |
| This document | The prose layer: anatomy, wiring, findings, handoff | — |

`graphify-out/` is git-ignored (`.gitignore:12`), so `graph.html`, `wiki/` and `graph.json` exist only
in the working copy that produced this audit. Only part of that is reproducible from this commit: §8
separates the stages that are scripted (`tools/graphify/`) from the stages that were hand-performed and
cannot be replayed. The graph's own report is committed next to this file so the findings survive a
fresh clone without the ignored directory.

### Corpus and method

- Detected: **486 files** — 437 code (405 C#, 10 shell, 8 JSON, 5 csproj, 3 SQL, 2 YAML, 2 Python, 1 solution, 1 JS) and 49 documents (45 Markdown, 4 static HTML), ~397k words.
- Code extraction: tree-sitter AST (deterministic, no model calls).
- Document extraction: 49 docs read and mapped to concepts; because this environment exposes no
  general-purpose subagent, that pass ran inline and wrote the same chunk schema
  (`graphify-out/.graphify_chunk_0*.json`). Edges from documents are tagged `EXTRACTED` (the doc
  states the link) or `INFERRED` (this audit's reading).
- Scope boundary: `src/MuscleCuties.UI`, `src/MuscleCuties.Infrastructure`, `src/MuscleCuties.UseCases`,
  `tests/`, `artifacts/` and process-only doc trees are excluded via a labelled block in
  [`.graphifyignore`](../../.graphifyignore). **UseCases is outside the graph**, so §3.3 covers the
  `endpoint → use case → store` hop as a table instead of graph edges.
- The graph is symbol-level: C# file nodes and namespace nodes were removed before clustering (418
  file + 74 namespace nodes), because they dominate community detection and hide the real type
  structure. Consequence: file-to-file dependency questions are answered by this document, not by the
  graph.

## 1. Verified baseline

| Project | C# files* | References | Role |
| --- | --- | --- | --- |
| `src/MuscleCuties.Core` | 191 | **none** | Domain rules and models |
| `src/MuscleCuties.UseCases` | 201 | Core | Interactors and DTOs |
| `src/MuscleCuties.Infrastructure.PostgreSql` | 176 | Core, UseCases | EF Core context, stores, migrations, schema contract |
| `services/MuscleCuties.Server` | 24 | PostgreSql, UseCases | ASP.NET Core minimal API + Lambda host |
| `src/MuscleCuties.DbMaintenance` | 10 | PostgreSql | Migration / schema / seed CLI |
| `services/MuscleCuties.WorkoutReconciliation` | 1 | PostgreSql | Separate reconciliation host |

\*excluding `bin/`, `obj/` and EF `*.Designer.cs`. Measured: 592 files and 68,936 lines across the four
backend projects including UseCases; 391 files in the graph-eligible backend slice.

Boundary checks, both clean:

- `rg 'using (Microsoft.EntityFrameworkCore|Npgsql|System.Text.Json.Serialization|Microsoft.AspNetCore|SkiaSharp|Microsoft.Maui)' src/MuscleCuties.Core` → **0 matches**.
- `rg 'using (MuscleCuties.Infrastructure|Microsoft.EntityFrameworkCore|Npgsql|Microsoft.AspNetCore)' src/MuscleCuties.UseCases` → **0 matches**.
- `[PostgresDbContext.cs](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresDbContext.cs)` holds every EF model; Core never sees an ORM type.

The graph reports **no import cycles**.

One correction to earlier assumption: within the PostgreSQL project **no** file declares a
`MuscleCuties.Core.*` or `MuscleCuties.UseCases.*` namespace (0 matches). The namespace-squatting defect
described in the release inventory §5.1 lives in `src/MuscleCuties.Infrastructure`, which is outside
this graph.

## 2. Database anatomy

### 2.1 Shape

| Fact | Value | Evidence |
| --- | --- | --- |
| Schemas | `app`, `catalog`, `ops` | `PostgresDbContext.PostgresSchemas` |
| `DbSet`s | 69 | `PostgresDbContext.cs:10-79` |
| EF configurations | 69 `IEntityTypeConfiguration<>` in 10 files | `Configurations/**`; **100 % of DbSets configured** |
| Migrations | 61 (`[Migration]`), 71 files with designers + snapshot | `Persistence/Migrations` |
| Migration range | `20261017003443_InitialPostgreSqlApplication` → `20261030220000_SplitAuthRuntimeContext` | same |
| Tenant marker | `interface ITenantRecord { Guid Id; Guid UserId; }` | `Models/App/AppRepoModels.cs:6` |

`ITenantRecord` is a real architectural seam, not a formality: it is the graph's #7 god node (46 edges)
because it links every tenant repo model, and it is what the RLS policies key on (`user_id`).

### 2.2 Schema contract as a specification

`src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json` (contractVersion `1.0.0`) pins, measured:

| Requirement | Count |
| --- | --- |
| Required schemas | 3 |
| Required tables | 11 |
| Pinned columns | 26 |
| Pinned indexes | 19 |
| Pinned constraints | 12 |
| Row-level-security entries | 46 |
| Policies | 49 |
| Security-definer functions | 6 |
| Roles | 10 |
| Privileges | 42 |

The evaluator is [`DatabaseSchemaCompatibility.cs`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/DatabaseSchemaCompatibility.cs)
(1,899 lines, static, 51 edges in the graph). It resolves and SHA-256s the manifest before opening a
connection, runs in a read-only transaction with `statement_timeout`, `lock_timeout` and
`idle_in_transaction_session_timeout`, and returns stable failure codes (`SCHEMA_*`,
`MIGRATION_HISTORY_*`, `TARGET_*`) with exit codes 0/1/2. See
[`POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md`](../infra/POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md).

**Doc drift (verified):** the release inventory §9 and the privilege-separation plan still cite *48
policies, 2 functions, 27 privileges*; the manifest now pins **49 / 6 / 42**. The runbook already says
"six pinned security-definer functions". Treat the manifest, not the prose, as authoritative.

### 2.3 Two runtime principals, two DbContext instances

[`PostgreSqlServiceCollectionExtensions.cs`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgreSqlServiceCollectionExtensions.cs):

| Registration | Line | Principal | Purpose |
| --- | --- | --- | --- |
| `AddDbContextPool<PostgresDbContext>` | 13 | `musclecuties_app` via `ConnectionStrings:PostgreSql` | All tenant routes, registration bridge, purge bridge |
| `AddKeyedScoped<PostgresDbContext>(AuthServiceKey)` | 30 | `musclecuties_auth` via `ConnectionStrings:AuthPostgreSql` | `app.users` and the seven global auth tables |

Both pools enable `EnableRetryOnFailure(3, 5s)`. Lambda rewrites both connection strings through
[`LambdaHostingConfiguration`](../../services/MuscleCuties.Server/LambdaHostingConfiguration.cs)
(`ResolveOptions`, line 41; `BuildBoundedRdsProxyConnectionString`, line 82): pool 0–25, idle lifetime
60 s, TLS `VerifyFull`, and **cold-start migrations are rejected** with `InvalidOperationException`.

Why the split exists is a wiring decision, not a preference: with a single app-principal context,
login/password-reset read `app.users` through a tenant policy that returns nothing without
`app.user_id`, and granting auth tables to the app group would let any tenant request read all users.
The reasoning and the rejected alternatives are in
[`C1_AUTH_CONTEXT_SPLIT_PLAN_20261030.md`](../infra/C1_AUTH_CONTEXT_SPLIT_PLAN_20261030.md) §2.

### 2.4 Tenant identity lifecycle

[`PostgresTenantContext.SetForCurrentTransactionAsync`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresTenantContext.cs#L12):

1. rejects an empty `Guid` **before** any database work;
2. requires an already-open transaction (throws otherwise — no silent session-level fallback);
3. executes `SELECT set_config('app.user_id', @user_id, true)` — `true` means transaction-local, so
   commit/rollback clears it and pooled connections cannot leak a tenant;
4. reads the value back and throws unless it matches, so a missing GUC fails closed instead of
   returning empty result sets.

This is the strongest single invariant in the persistence layer. §5.3 records where it is *not* applied
and why.

### 2.5 Idempotent commands

[`PostgresIdempotencyExecutor`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresIdempotencyExecutor.cs)
(registered as a singleton, line 211 of the composition root — safe, because it takes the scoped
`PostgresDbContext` as a method parameter):

- validates `commandKey` (≤ 128 chars) and a 64-hex SHA-256 request hash;
- runs the whole delegate inside `context.Database.CreateExecutionStrategy()` so EF's transient retry
  covers the explicit transaction;
- writes the journal row and the command response in the **same** transaction;
- on `23505` for constraint `uq_command_idempotency_user_key`, rolls back, clears the change tracker and
  reads the winner in a fresh transaction;
- refuses replay when the request hash differs (`PostgresIdempotencyConflictException`).

### 2.6 Readiness and the second access path

`/health/ready` maps two checks (`ApiApplication.cs:372-374`): `PostgreSqlReadinessCheck` (can we reach
the database) and `PostgreSqlSchemaCompatibilityCheck` (does the catalog satisfy the contract). The
second is what makes "deployed but schema lags" visible instead of a 200.

Four classes bypass EF and open raw `NpgsqlDataSource` connections, and the schema evaluator opens its
own `NpgsqlConnection`:

| Class | Connection | Why |
| --- | --- | --- |
| `PostgresPhotoCleanupDispatchCursor` | dispatch source | advisory-lock lease across connections |
| `PostgresDuePhotoCleanupOwnerDiscovery` | dispatch source | set-based discovery, no entity shape |
| `PostgresPhotoObjectReconcileTargetStore` | maintenance source | `ops` table, no tenant grant |
| `PostgresWorkoutReconciliationDispatcher` | discovery source + `IDbContextFactory` | cross-tenant discovery, then per-tenant work |
| `DatabaseSchemaCompatibility` | inspection connection | read-only catalog metadata |

None of these data sources is registered in the API host: they are composed in
`src/MuscleCuties.DbMaintenance/Program.cs:254` and
`services/MuscleCuties.WorkoutReconciliation/Program.cs:25-26`. That is deliberate isolation — the API
cannot accidentally run maintenance SQL — but it does mean **the same tables have two access paths**
with different principals, and only the EF path is covered by the tenant-GUC invariant.

## 3. Wiring

### 3.1 Boot chain

```
Program.cs (9 lines)
  AddAWSLambdaHosting(LambdaEventSource.HttpApi)
  → ApiApplication.Build(builder, configureServices?)        ApiApplication.cs:108
      config gate: ConnectionStrings:PostgreSql, AuthPostgreSql, Authentication:{SigningKey,Issuer,Audience}
                   S3EnvironmentBucketGuard, trusted proxies
      → AddPostgreSqlPersistence(app) + AddPostgreSqlAuthPersistence(auth)
      → 113 AddScoped + 8 AddSingleton + 3 named HttpClients + 1 typed client
      → JWT bearer: 32-byte key floor, zero clock skew, OnTokenValidated → revocation check
      → exception handler (problem+json + requestId), X-Request-Id, forwarded headers
      → 48 app.Map* calls
```

### 3.2 Registration census

| Kind | Count | Notes |
| --- | --- | --- |
| `AddScoped<T>` | 113 | stores, use cases, per-request ports |
| `AddSingleton<T>` | 8 | see below |
| `AddHttpClient("…")` | 3 | `AppleAccountTokenRevoker`, `ResendEmailDelivery`, `FirstPartyEmailChallengeRelay` — all 15 s timeout, `AllowAutoRedirect = false` |
| `AddHttpClient<TInterface,TImpl>` | 1 | `IS3PhotoObjectInspector` |
| `AddDbContextPool` / `AddKeyedScoped<PostgresDbContext>` | 1 / 1 | app and auth |
| `app.Map*` | 48 | 17 route groups + 30 direct endpoints + 1 health |

The eight singletons: `IRegistrationPasswordHasher`, `IRegistrationEmailReceiptVerifier`,
`PostgresIdempotencyExecutor`, `IS3PresignedUrlService`, `IPhotoByteSanitizer`,
`IAccountDeletionAuditStore`, `IReadinessEngine`, `IEmailReceiptTokenIssuer`. Each is stateless or
holds only immutable configuration; none captures a scoped `PostgresDbContext`. The classic
"scoped-in-singleton" trap is **not** present here.

### 3.3 Vertical slices

| Slice | Endpoint | Use case | Store | Tenant GUC |
| --- | --- | --- | --- | --- |
| Profile | `GET/PUT /api/v1/profile` (`ApiApplication.cs:502`, `:509`) | `ServerProfileUseCase` | `PostgresProfileDataStore` (`:334`) | yes |
| Strength plan | `MapWorkoutPlanningEndpoints` (`ApiApplication.cs:773`) | `CreateStrengthPlanProposalV020UseCase`, `AcceptStrengthPlanProposalV020UseCase` | `PostgresStrengthPlanProposalStoreV020` (`:36`) | yes |
| Nutrition diary | `MapNutritionDiaryEndpoints` (`ApiApplication.cs:770`) | `NutritionDiaryUseCase` | `PostgresNutritionDiaryStore` (`:445`) | yes |
| Progress photos | `MapPhotoVaultEndpoints` (`ApiApplication.cs:784`) | `ServerPhotoVaultUseCase` | `PostgresPhotoVaultStore` (6 sites, `:55`…) | yes |
| Account deletion | `DELETE /api/v1/accounts/me` (`ApiApplication.cs:716`) | `ServerAccountDeletionUseCase` | `PostgresAccountPurgeStore` (`:69`) + `SECURITY DEFINER` purge bridges | yes |

Every authenticated tenant slice follows the same five steps: `TryGetAuthenticatedUserId` → use case →
store → `SetForCurrentTransactionAsync` → `SaveChanges`/commit. There is exactly one way to do it, and
the stores do it consistently (§5.3).

Auth-family slices deliberately diverge: the types that inject
`[FromKeyedServices(PostgreSqlServiceCollectionExtensions.AuthServiceKey)] PostgresDbContext`
(`PostgresAccountAuthenticationUseCase`, `PostgresEmailChallengeUseCase`,
`PostgresAccountPasswordResetStore`, `PostgresAccountMigrationClaimStore`,
`PostgresCompleteAccountRegistrationStore`, `AppleAccountTokenRevoker`) use the **keyed auth context**
and never set a tenant GUC, because they operate on global tables under the auth principal.
`PostgresPasswordLoginLimiter` is internal and is constructed with that same auth context
(`PostgresAccountAuthenticationUseCase.cs:53`), so it inherits the behaviour.

## 4. What the graph shows

### 4.1 God nodes

| # | Node | Edges | Community |
| --- | --- | --- | --- |
| 1 | `PostgresDbContext` | 242 | DbContext and Model Factory |
| 2 | `WorkoutActivityType` | 128 | Activity Preference Rules |
| 3 | `CyclePhase` | 70 | Cycle Phase Rule |
| 4 | `SchemaRequirements` | 53 | Database Schema Compatibility · Schema |
| 5 | `DatabaseSchemaCompatibility` | 51 | Database Schema Compatibility · DatabaseSchemaCompatibility |
| 6 | `CatalogSnapshot` | 49 | Database Schema Compatibility · CatalogSnapshot |
| 7 | `UserProfileRecord` | 46 | App Repo Models · UserProfileRecord |
| 8 | `ITenantRecord` | 46 | App Repo Models · Id (2) |
| 9 | `WorkoutDayExerciseRecord` | 45 | App Repo Models · PlanningSlotId |
| 10 | `UserFoodItemRecord` | 44 | App Repo Models · UserFoodItemRecord |

Two readings matter:

- `PostgresDbContext` at 242 edges is the single hub of the backend; nothing in the persistence layer is
  reachable without it. That is expected for EF, but it also means one type's lifetime decision (pooled
  vs keyed) governs every tenant and auth path.
- The schema contract (`SchemaRequirements` + `DatabaseSchemaCompatibility` + `CatalogSnapshot`, 153
  edges combined) is a **second structural model** of the database that exists in parallel to the EF
  model. The two are kept honest by `SCHEMA_MODEL_CONTRACT_MISMATCH`, but they are maintained
  separately. Note that the three sit in *separate* communities (§4.2), so the graph itself reports the
  contract as three loosely joined pieces rather than one subsystem.

### 4.2 Community structure

364 communities; cohesion is low everywhere (0.023 for the largest community, 0.084 for the densest
of the top six), which is normal for C# symbol graphs where DTO clusters connect loosely. The useful
signal is the *shape*:

| Community | Nodes | Interpretation |
| --- | --- | --- |
| Use Case Interactors | 88 | cohesion 0.023 — all use-case classes collapse into one hub; they share DTOs, not behaviour |
| Cross-Module DTO Contracts | 87 | request/response records shared across features |
| DbContext and Model Factory | 78 | `PostgresDbContext`, 69 `DbSet`s, factory, tenant records |
| Body Measurement Contracts | 69 | a single vertical slice with its own DTO family |
| Activity Preference Rules | 67 | Core rule family |
| EF Model Configuration | 49 | the 10 configuration files |
| Schema Introspection Model | 45 | `CatalogSnapshot`, `PhysicalColumn`, `PhysicalConstraint` |

The schema-contract family carries a `Database Schema Compatibility · …` label in **19** communities
holding 205 nodes in total, and 34 + 33 + 24 are only the three largest of them. The long tail is one
record type per community (`ModelColumn`, `SchemaPolicyRequirement`, `PhysicalFunction`,
`SchemaColumnRequirement`, `SchemaFailureCodes`, …), each of 1–20 nodes — the honest signature of one
static class doing two jobs (catalog introspection and contract evaluation) while its record types
fragment into clusters too small for the report to print.

### 4.3 Surprising connections

The doc→code `rationale_for` edges are the only place where intent is stored next to implementation:

- `docs/orchestration/01_DATABASE.md` → `DatabaseSchemaCompatibility` (the database worker's brief),
- `docs/orchestration/02_SERVER_LAMBDA.md` → `PostgreSqlSchemaCompatibilityCheck` (the "make
  `/health/ready` tell the truth" requirement),
- `docs/infra/AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md` → `LambdaHostingConfiguration`,
- `docs/infra/POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md` → `DatabaseSchemaCompatibility`,
- `docs/infra/C1_AUTH_CONTEXT_SPLIT_PLAN_20261030.md` → `PostgreSqlServiceCollectionExtensions`.

All five are `INFERRED` (the docs describe the decision; no code comment links them). They are the
reading order for anyone changing those files.

### 4.4 Knowledge gaps

1,963 symbols have ≤ 1 connection (2,238 nodes once file, concept and rationale nodes are included).
They are properties, enum members and constructor parameters that
the AST extractor records but that no other symbol references directly. They do not indicate dead code;
they cap community precision. For dead-code questions use the release inventory, not the graph.

## 5. Findings register

Severity is about **structural risk**, not release blocking. No new P0 was found in the DB/Core/wiring
path; the standing P0s are the migration-chain and artifact issues in `RELEASE_INVENTORY_20261030.md` §4.

### 5.1 P1 — `DatabaseSchemaCompatibility` is a module, not a class

1,899 lines, 51 edges, static, and spread across 19 communities in the graph (§4.2). It parses and validates
the manifest, introspects `pg_catalog`, builds a `CatalogSnapshot`, evaluates 11 tables / 26 columns /
19 indexes / 12 constraints / 46 RLS entries / 49 policies / 6 functions / 10 roles / 42 privileges,
computes a deterministic fingerprint, formats issues, and maps exit codes. The graph shows no seam
inside it because there is none.

Risk: every contract change (a new pinned policy, a new role) edits the same file as the evaluator, so
review cannot distinguish "requirement changed" from "evaluation changed". The runbook already treats
the manifest as the specification; the evaluator should be split along that line
(`CatalogInspector` / `ContractEvaluator` / `ResultFormatter`) without behaviour change.

### 5.2 P1 — the composition root is the second-largest module

`ApiApplication.cs` is 956 lines with 113 scoped registrations, 48 route mappings, 13 inline request
records, JWT wiring, health checks and the error pipeline in one static method. Graph evidence: the
"Use Case Interactors" community (88 nodes) has cohesion 0.02 — the use cases are only connected through
this file.

Concrete duplication found: `ChangePasswordBody` is declared **twice** — public in
`src/MuscleCuties.UseCases/Features/Account/Authentication/AccountAuthenticationContracts.cs:7` and
private in `ApiApplication.cs:804`. The private copy shadows the contract and will drift.

### 5.3 P1 — the tenant-GUC invariant is convention, not enforcement

Measured: 47 files in the persistence project open a transaction; 37 also set the tenant GUC, and the
10 exceptions are all justified:

| File | Tx | Why no GUC |
| --- | --- | --- |
| `Account/Authentication/PostgresAccountAuthenticationUseCase.cs` | 4 | auth principal, global auth tables |
| `Account/EmailChallenge/PostgresEmailChallengeUseCase.cs` | 3 | auth principal |
| `Account/Authentication/PostgresPasswordLoginLimiter.cs` | 3 | inherits the auth context from `PostgresAccountAuthenticationUseCase.cs:53` |
| `Account/Authentication/PostgresAccountPasswordResetStore.cs` | 1 | auth principal (keyed context, `[FromKeyedServices]` at line 10) |
| `Account/MigrationClaims/PostgresAccountMigrationClaimStore.cs` | 1 | auth principal (keyed context, `[FromKeyedServices]` at line 11) |
| `Persistence/PostgresCanonicalCatalogSeeder.cs` | 1 | `catalog` schema, migrator |
| `Nutrition/CatalogVersioning/PostgresCanonicalFoodUpsertStore.cs` | 1 | `catalog` schema |
| `Workout/Planning/PostgresReviewedExerciseCatalogImporterV020.cs` | 1 | `catalog` schema |
| `Profile/PhotoVault/PostgresPhotoObjectReconcileTargetStore.cs` | 1 | `ops` table, maintenance source |
| `Persistence/DatabaseSchemaCompatibility.cs` | 1 | read-only inspection |

The exceptions are correct today. The problem is that nothing prevents the next store from forgetting
the call: RLS would fail closed (good) but only at runtime, and only on the paths a test exercises.
`TenantContextFailClosedTests` covers the setter's own preconditions, not store coverage. A cheap
architecture test — reflect over `Infrastructure.PostgreSql` types whose methods call
`BeginTransactionAsync` and assert a `SetForCurrentTransactionAsync` call in the same method, with an
explicit allow-list for the ten rows above — would make the invariant enforceable.

### 5.4 P2 — two access paths to the same tables

EF for tenant/auth traffic; raw `NpgsqlDataSource` for photo cleanup, reconciliation and schema
inspection (§2.6). Isolation is good (the API host registers none of them), but the `ops` tables and
`app.workout_account_planning_states` are reachable from both paths with different principals, so a
grant change must be reasoned about twice. Document the second path wherever `roles-and-grants.sql` is
edited.

### 5.5 P2 — documentation drift on contract counts

See §2.2: prose says 48 policies / 2 functions / 27 privileges; the manifest has 49 / 6 / 42. Any
reviewer comparing the runbook to the manifest will see three mismatches that are not defects.

### 5.6 P2 — oversized store

`PostgresWorkoutPlanAndSessionStore.cs` is 1,643 lines with 10 tenant transactions. It is the largest
store by an order of magnitude and mixes plan reads, session lifecycle, timing checkpoints and
completion credits. Not urgent, but it is where a future "one transaction, two aggregates" bug will
come from.

### 5.7 Informational — what is healthy

- 100 % of the 69 `DbSet`s have an explicit `IEntityTypeConfiguration<>`.
- Zero import cycles; Core has zero ORM/HTTP/serialization imports.
- Every tenant store that should set the GUC does so, and the setter fails closed twice (no transaction,
  wrong value). The ten exceptions are all auth-principal or catalog/maintenance paths (§5.3).
- Cold-start migration is not merely avoided by convention — it throws.
- The Lambda pool bounds are validated at startup (0–25, TLS required, positive timeouts).

## 6. Verification log

| Check | Command (abridged) | Result |
| --- | --- | --- |
| Corpus detection | `python -c "detect(Path('.'))"` | 486 files, 397,150 words |
| AST extraction | `extract(437 code files)` | 5,504 symbol nodes, 15,986 edges |
| Semantic extraction | inline over 49 docs | 62 nodes, 79 edges, 4 hyperedges |
| Graph build | symbol projection | 5,004 nodes, 11,507 edges, 364 communities |
| Import cycles | graph analysis | none |
| DbSet configuration coverage | regex over `Configurations/**` | 69/69 |
| Tenant GUC coverage | per-file `BeginTransactionAsync` vs `SetForCurrentTransactionAsync` | 37 balanced, 10 justified exceptions |
| Boundary imports | `rg` over Core and UseCases | 0 / 0 |
| Migration count | `rg -l '\[Migration\('` | 61 |
| Contract counts | parse `SchemaRequirements.json` | 11 tables / 26 cols / 19 idx / 12 constraints / 46 RLS / 49 policies / 6 functions / 10 roles / 42 privileges |
| Graph regeneration | `PYTHONHASHSEED=0 … relabel.py --files 486 --words 397150` | committed graph report reproduced byte-for-byte; 5,004 nodes / 11,507 edges / 364 communities / 374 articles |
| Corpus scope probe | `tools/graphify/scope.py` | 439 code + 51 documents today; zero files from an excluded tree |

### Limits

- **No database was contacted.** Nothing here proves a live catalog matches the contract; use
  `DbMaintenance schema-verify` with a read-only connection for that.
- **No build or test was run.** No C# file changed in this task, so the iOS/`dotnet test` gate is not
  triggered. The baseline in `RELEASE_INVENTORY_20261030.md` §3 still stands.
- **EF model-vs-migration drift** could not be evaluated: the Design package is `PrivateAssets=all`
  with `IncludeAssets` excluding `compile`, so `dotnet ef migrations has-pending-model-changes`
  refuses the project (inventory §3).
- **`graph.html` is 8.0 MB / 5,004 nodes**, above graphify's default 5,000-node guard; it was generated
  with `GRAPHIFY_VIZ_NODE_LIMIT=6000`. It opens, but the wiki is the faster surface on a laptop.
- **UseCases is outside the graph.** §3.3 compensates with a table, not with edges.

## 7. Handoff

Read in this order to rebuild the mental model:

1. [`PostgresDbContext.cs`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresDbContext.cs) — the 69-row model and the three schemas.
2. [`PostgreSqlServiceCollectionExtensions.cs`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgreSqlServiceCollectionExtensions.cs) — app vs auth, retry policy, TLS bootstrap.
3. [`PostgresTenantContext.cs`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresTenantContext.cs) — the one invariant everything else assumes.
4. [`ApiApplication.cs`](../../services/MuscleCuties.Server/ApiApplication.cs) — registrations, JWT, routes.
5. [`DatabaseSchemaCompatibility.cs`](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/DatabaseSchemaCompatibility.cs) — what "compatible" means, and the file §5.1 wants split.
6. [`services/postgresql/README.md`](../../services/postgresql/README.md) — principals, grants, bridges, cutover state.

Three graph queries worth running first (`graphify query "…"`):

- "How does a request reach a tenant table?" — `ApiApplication → *UseCase → *Store → PostgresTenantContext`.
- "What else changes if I touch `SchemaRequirements`?" — the schema-introspection community plus the
  DbMaintenance host.
- "Which types bridge the auth and tenant worlds?" — `ITenantRecord`, the keyed `PostgresDbContext`, and
  the registration bridge.

## 8. Regenerating the graph

Only part of this graph is reproducible from this commit. The toolchain, the corpus boundary and the
finalisation stage are scripted and were run to verify the commands below; the extraction stages that
needed judgement were hand-performed and are listed as such instead of being implied away.

**Toolchain** (verified: Python 3.11.15, graphifyy 0.9.99). A global `pipx` install of graphify also
exists on this machine, so every command below calls the venv interpreter directly:

```bash
uv venv .tools/graphify-venv --python /opt/homebrew/bin/python3.11
uv pip install --python .tools/graphify-venv/bin/python graphifyy
.tools/graphify-venv/bin/python -c "import importlib.metadata as m, sys; print(sys.executable, m.version('graphifyy'))"
# → .../.tools/graphify-venv/bin/python 0.9.99
```

**Scope** (verified). `tools/graphify/scope.py` runs the same detection `graphify` uses and fails if an
excluded tree leaks in, which is how the `.graphifyignore` block is checked rather than trusted:

```bash
.tools/graphify-venv/bin/python tools/graphify/scope.py
```

At audit time it reported **437 code + 49 documents, 397,150 words** — the run's corpus. On today's
checkout the same command reports 439 code + 51 documents: the two audit Markdown files and the two
scripts in `tools/graphify/` are new. The scope decision itself is the labelled, reversible block in
[`.graphifyignore`](../../.graphifyignore); delete the block to graph the whole repository.

**Finalisation** (verified). Given `graphify-out/graph.json` and `.graphify_analysis.json`,
`tools/graphify/relabel.py` re-applies the 20 inspection-named communities, names the remaining 344
from their dominant source, disambiguates duplicates by dominant type, writes the report, `graph.json`,
`graph.html` at `GRAPHIFY_VIZ_NODE_LIMIT=6000` (the default guard would replace the graph with an
aggregated community meta-graph above 5,000 nodes) and the 374-article wiki:

```bash
PYTHONHASHSEED=0 .tools/graphify-venv/bin/python tools/graphify/relabel.py --files 486 --words 397150
```

This reproduces the committed [`BACKEND_GRAPH_REPORT_20261030.md`](BACKEND_GRAPH_REPORT_20261030.md)
byte-for-byte. After the first run has rewritten `graphify-out/graph.json`, consecutive runs are
byte-identical including that file; the first run only changes its key order (verified by comparing the
node, edge and community sets, which were unchanged). `PYTHONHASHSEED` pins graphify's equal-weight tie
order; `--files/--words` pin the corpus header to this audit's snapshot instead of today's checkout.

**Hand-performed, not reproducible.** Three stages were run as ad-hoc Python against graphify's API and
left no tracked script, so a fresh clone cannot replay them:

- the AST extraction over the 437 code files (5,504 symbol nodes, 15,986 edges) and its merge with the
document layer;
- the inline 49-document semantic pass (62 nodes, 79 edges, 4 hyperedges). Its intermediates
  (`graphify-out/.graphify_chunk_*.json`) were removed with the rest of the scratch output, so
  reproducing it means re-reading the docs with a model, not re-running a command;
- the symbol projection (418 file + 74 namespace nodes removed before clustering) and the first
  clustering pass.

The nearest single command for those stages is `graphify . --mode deep`, which runs the full
detect → extract → cluster pipeline over the corpus and yields a *different* graph: 5,504 nodes with file
and namespace nodes kept and no inline document pass. It is a usable graph — and
`tools/graphify/relabel.py` will label it — but it is not the one this audit read.

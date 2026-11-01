# Worker 1 — Database and schema contract

You are Worker 1 in the MuscleCuties release cleanup. You own the database chain, the schema
contract, the maintenance CLI, and the database fixtures. You do not own the server, UI, packaging,
CI, or docs.

Read first:

- `docs/RELEASE_INVENTORY_20261030.md` §0, §3, §4.1, §5.3 (only `DatabaseSchemaCompatibility`), §6.3
  (database rows), §7 (database rows), §9, §11 (database runbook delta only), §12 (Worker 1), §13,
  §14.
- `AGENTS.md` — architecture, privacy, deterministic-math, and build rules.
- `docs/orchestration/README.md` §4 (phase plan) and §5 (checkpoint format).
- `docs/orchestration/OWNERSHIP_MANIFEST.md` — your `W1` scope.
- This file. No chat context is needed beyond these files.

## Isolation and authority

The shared checkout is dirty and another agent may be editing it. Inventory §0 records the files that
were in flux; treat every one as potentially changed and re-read it in your worktree. Do not edit the
shared checkout.

The orchestrator creates `/tmp/mc-w1` from the freeze commit. Enter it:

```bash
cd /tmp/mc-w1
git status --short
```

If you are starting without an orchestrator and the worktree does not exist, create it once from the
`freeze_sha` in `/tmp/mc-freeze/freeze.env`; do not infer the commit from an older document:

```bash
git worktree add /tmp/mc-w1 -b release/w1-db "$(grep '^freeze_sha=' /tmp/mc-freeze/freeze.env | cut -d= -f2)"
```

Use branch `release/w1-db`. Local commits on this branch are allowed for handoff. **Do not push.**
Do not deploy, migrate a live database, change a secret, or use a live connection string. Every
database you create is disposable and local.

### Bootstrap and toolchain

The coordinator copied `docs/orchestration/`, the inventory, and the four tool scripts into this
worktree. They are untracked, read-only bootstrap files. Only your checkpoint under
`docs/orchestration/checkpoints/` may be staged from that copied package; add your own code/test
changes normally. Do not stage the rest of the package.

Read `/tmp/mc-freeze/freeze.env`. `diff_outcome=no-tracked-diff` is a normal outcome: do not apply a
patch. If `diff_outcome=tracked-diff`, check it in this clean worktree and apply only hunks in your
scope; never apply UI/server/Cloud hunks:

```bash
git apply --check /tmp/mc-freeze/tracked.patch
```

Use the pinned SDK explicitly; bare `dotnet` is the wrong install:

```bash
export DOTNET="$HOME/.dotnet/dotnet"
"$DOTNET" --version          # must print 10.0.403
"$DOTNET" workload list      # must include maui-ios
```

## Mission non-negotiables (same for every worker)

1. **No pushes.** Local commits on your worker branch are allowed; pushing, deploying, live
   migrations, secret changes, and TestFlight are forbidden.
2. **Database reproducible from migrations alone.** A fresh PG17 database migrated only by
   `DbMaintenance migrate` must be `compatible` with zero manual SQL. Your change must not create a
   dependency on a reconciliation manifest.
3. **Exactly one Lambda artifact.** `artifacts/MuscleCuties.Server.zip` must contain no stray DLLs,
   PDBs, development config, or metadata, and its routed functions must work. Do not add a second
   package or a packaging exception.
4. **Remove dead/duplicated code; do not bypass it.** Deleting a caller while leaving the dead
   implementation, or adding a second adapter/repair/fallback, is not a fix.
5. **Fix the cause in place and in the right layer.** No sidecar normalizer, client-side repair of a
   server bug, manual JSON guesser, test-only SQL fix, or fabricated default.
6. **Tests:** delete a test only when the behaviour it covers is genuinely gone; never skip, weaken,
   or comment out a test to get green.
7. **Clean architecture and only necessary files.** Core has no outer dependencies; UseCases only
   Core; no `Core.*`/`UseCases.*` namespace outside its project; no ghost/duplicate trees.
8. **Isolation:** use your own worktree; the shared checkout is read-only. Stop if a required file
   is outside your scope.
9. **Sequencing:** docs checkpoint cleanup and the final single-artifact decision happen after all
   workers report, not in your pass. Do not edit existing docs or `artifacts/`.

## Write scope (manifest `W1`)

Your only write list is the `W1` section of `docs/orchestration/OWNERSHIP_MANIFEST.md`. It contains
the database roots, the DB tools, the named database tests, and your checkpoint. Do not write a path
that is not listed there.

The work items below name files inside `W1`. If a named path is not in `W1`, stop and record it; the
manifest wins. Other database tests, server/UI/Cloud files, CI, and existing docs are outside `W1`.

## Inventory items you own

- **4.1** — fresh database cannot satisfy the contract from source alone.
- **9** — database/schema inventory.
- **6.3** — the database test rows (candidate-SQL application, model drift).
- **7** — the schema-reconciliation fallback and hydration baseline row.
- **5.3** — `DatabaseSchemaCompatibility.cs` only, if a split is necessary for a clear contract; no
  behaviour change.
- **12 (Worker 1)** and **13** — deliverables and handoff.

## Required work

### 1. Make grants part of the migration chain

`20261026200000_AddWorkoutCalendarAndCompletionCredits` creates
`app.workout_account_planning_states` and `app.workout_completion_day_credits` with RLS/policies but
no `GRANT` to `musclecuties_app`. `SchemaRequirements.json` requires
`SELECT,INSERT,UPDATE,DELETE` on both. `services/postgresql/roles-and-grants.sql` uses a static table
list that omits both.

Add a new idempotent grant migration (the chain becomes **60** migrations unless the repository has
already advanced; do not invent a count—derive it). It must:

- grant the runtime role exactly the contract privileges on both tables;
- be safe on a database where a live repair already granted them (idempotent, no duplicate-object
  failure);
- preserve RLS/policy ownership and not grant `BYPASSRLS`, schema ownership, or superuser;
- run inside the normal `DbMaintenance migrate` path, not a reviewed reconciliation manifest.

Update every source-of-truth field that names the chain: `SchemaRequirements.json` counts/final ID
(including `generatedFrom.sourceMigrationCount`, `migrationHistory.expectedAppliedCount`, final
migration ID, and any contract fingerprint that changes), `services/postgresql/roles-and-grants.sql`
so the two tables are in the app grant list, and any generated/checked-in contract file that repeats
the count. Do not change a count to a number you did not observe.

### 2. Remove the manual SQL from the pass

`SchemaCompatibilityDatabaseFixture.cs` and `MigrationChainRehearsalTests.cs` currently apply
`src/MuscleCuties.DbMaintenance/Schema/Reconciliation/20261029_grant_workout_calendar_app_access.sql`
themselves. Remove that application. The tests must prove the migration chain alone. Keep the
candidate file only as a live-repair record with a comment that it is not an install prerequisite and
is not applied by `migrate`; do not delete the evidence of the live repair. After your change,
`grep -rn "20261029_grant_workout_calendar_app_access" tests` must show no test applying it.

`SchemaReconciliation` must continue to fail closed without an approved manifest. Do not commit a
live manifest, fingerprint, owner approval, or database credential. Do not "solve" the problem by
making `schema-reconcile` auto-approve.

### 3. Prove the database is reproducible and deployment-ready

Use a disposable PostgreSQL 17 database (Docker or `tools/rider/local-postgres.sh`). Never use a
shared/live database. Record exact commands and results. Prove all of:

1. **Fresh install:** empty database → `DbMaintenance migrate` only → `schema-preflight` returns
   `compatible`; zero manually applied SQL. Capture the migration count and final ID.
2. **Upgrade:** a database at the previous chain head (59 migrations / the ID the inventory records)
   → apply your branch's migrations → `compatible`; the new grant migration applies once and is
   idempotent on a second `migrate` (no-op, no duplicate grant error).
3. **Repeated migrate:** a second `migrate` makes no schema change and still reports compatible.
4. **Least privilege:** `musclecuties_app` can perform the contracted CRUD on both calendar tables
   and cannot bypass RLS or read another tenant's rows. Include a negative tenant/RLS test.
5. **Role script:** run `services/postgresql/roles-and-grants.sql` after migrations on a fresh
   database and confirm the contract is still compatible (the script must not be required to repair
   the two tables, and it must not remove grants).
6. **Runtime principal:** `runtime-principal-preflight` passes for the runtime role and fails for a
   role without the contracted privileges.
7. **Reconcile negative:** `schema-reconcile` without an approved manifest still fails closed; it is
   not the install path.
8. **EF model drift:** `dotnet ef` is not installed in this repository (no tool manifest), so use the
   in-repo snapshot test that already calls `HasPendingModelChanges()`. It needs no database and no
   tool install:

   ```bash
   "$DOTNET" build tests/MuscleCuties.Registration.Integration.Tests -c Release -v q --nologo
   "$DOTNET" test tests/MuscleCuties.Registration.Integration.Tests -c Release --no-build \
     --filter 'FullyQualifiedName~WorkoutModelSnapshot'
   ```

   Pass is `Failed: 0, Passed: 1` for `ModelSnapshotMatchesCurrentPersistenceContracts` (observed on
   2026-10-30 at `c8ffbccb`). Record the exact command and counts. If the test cannot run, the
   checkpoint says `not-run`, not `pass`.

### 4. Keep the intentional compatibility path, but test it

`PostgresHydrationStore` reads the old `diary_water_ml` baseline when no intake days exist. This is
intentional compatibility, not a fabrication fallback. Keep it, add or update a test that exercises
fresh data and legacy data, and document a deprecation/migration condition in the code comment. Do
not remove it to make the diff smaller, and do not add another fallback.

### 5. Keep the contract usable by Worker 2

Worker 2 will make `/health/ready` run the schema contract. Keep the public
`DatabaseSchemaCompatibility.EvaluateAsync(string connectionString, SchemaRequirements, CancellationToken)`
signature stable. If you must change it, record the exact replacement in the `Interface changes`
section of your checkpoint before you finish; Worker 2 will adapt to it. Do not edit the server
yourself.

### 6. Size, not cosmetics

`DatabaseSchemaCompatibility.cs` is 1,899 lines. You may split it into cohesive files (introspection,
contract model, fingerprint, evaluation) only if the split is mechanical and every existing test
still passes. Do not rename failure codes, change fingerprints, or rewrite evaluation semantics as
part of a file split. If the split is not necessary to close a gate, leave it and record it as
follow-up. Clean architecture means the behaviour is in the right layer; it does not mean a large
file must be churned during a release gate.

## What you must prove before declaring done

- `"$DOTNET" build` of `MuscleCuties.Infrastructure.PostgreSql`, `MuscleCuties.DbMaintenance`, and the
  affected test projects succeeds in Release.
- The database tests named in your scope pass; record exact counts.
- A fresh disposable PG17 database migrated only by `migrate` is `compatible` with zero manual SQL.
- The upgrade/59→branch path and repeated-migrate no-op are recorded.
- The two calendar tables have the contracted grants from source, and the candidate SQL is no longer
  applied by tests.
- RLS/tenant isolation and runtime-principal preflight pass, including one negative case.
- The EF drift check is runnable or explicitly `not-run` with the exact blocker.
- `git diff --name-only` contains only your scope plus the checkpoint.
- No live manifest, secret, fingerprint from a real database, `.env`, `artifacts/`, or generated
  output is in the diff.

Do not report "59/59 compatible" from a document. Report the count you observed.

## Checkpoint

Write `docs/orchestration/checkpoints/01-database.md` using the format in
`docs/orchestration/README.md` §5. Required specifics:

- `worker: 01-database`, `scope_ref: W1`, base commit, branch, worktree;
- `phase_status: { P1: ready-for-merge | blocked }`;
- inventory items 4.1, 9, 6.3, 7;
- old/new migration count and final ID;
- fresh/upgrade/repeat/RLS/preflight/reconcile/drift results with commands;
- the exact `roles-and-grants.sql` and `SchemaRequirements.json` changes;
- any signature change Worker 2 must consume;
- any doc delta the orchestrator must apply in P5 (runbook, migration count, etc.).

## Stop conditions

Stop and write a `blocked` checkpoint if:

- you cannot create a disposable PostgreSQL 17 database;
- the new migration cannot be made idempotent without changing product semantics;
- a required file is outside your scope;
- the EF drift check cannot be made runnable without a design decision;
- another worker's change is needed to make the chain pass.

Do not edit the server, UI, CI, packaging, or existing docs to unblock yourself. Record the exact
file and the reason; the orchestrator will route it.

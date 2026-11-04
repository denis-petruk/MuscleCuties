# PostgreSQL privilege separation plan

**Current-checkout note (2026-11-04):** This dated document records a migration-era source tree and target investigation. The current checkout is model-first and has no EF migration chain; its database commands are dbinit, dbcheck, and seed. Treat migration, schema-preflight, schema-verify, schema-reconcile, and migration-prerequisite instructions here as historical evidence, not executable instructions for this checkout. Revalidate the current source, target, and authorization before any database operation. See [the current schema runbook](POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md).


Status: staging migration-runner membership was applied on 2026-10-30 under
`OWNER-APPROVAL-STAGING-SCHEMA-20261030`. The serving Lambda still authenticates as
`mc_admin`; the runtime-principal cutover is not executed. No production role, grant,
principal or connection setting was changed.

## Verified problem

The serving Lambda connects with PostgreSQL master login `mc_admin`. That principal is not a
restricted application role. This is a real privilege-separation defect, but it is **not** the cause
of the physically missing columns: the incident is code/schema deployment drift across the 28 pending
migrations. Fixing the principal must not be presented as repairing the schema.

## Proposed principals

| Principal | Purpose | Attributes |
| --- | --- | --- |
| `musclecuties_api_login` | Dedicated API login, granted only the required group roles | `LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| `musclecuties_app` | Effective tenant application group role | `NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| `musclecuties_migrator` | DDL, ownership transfer, catalog locking | `NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| `musclecuties_workout_discovery_owner` | Owner of the discovery function and column-level reads | `NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| `musclecuties_workout_dispatch` | Cursor read/update and function execute | `NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| schema inspector | Read-only release preflight | Membership in `pg_read_all_data`-style inspection only; no write, no DDL |

The inspector is deliberately described as a capability, not a created role: the current contract
uses existing principals and the release runner's connection. Creating a new inspector login is an
owner-approved cutover step.

## Required separation

1. **Runtime application:** the dedicated login must assume `musclecuties_app` as its
   effective role (`Options=-c role=musclecuties_app`). Neither role may own tables, hold
   DDL, or inherit `musclecuties_migrator`, `musclecuties_workout_discovery_owner` or
   `musclecuties_workout_dispatch`. The existing schema contract keeps the app group
   `NOLOGIN`; changing it to `LOGIN` would contradict that contract.
2. **Migration/reconciliation:** `musclecuties_migrator` or a dedicated deployment principal that can
   assume it. The `OwnReviewedTemplateVolumeFloors` migration explicitly requires the executor to be
   able to assume the floor owner and `musclecuties_migrator`, and the migrator needs `catalog CREATE`
   for the ownership transfer. The staging upgrade revealed that `mc_admin` was not a member:
   18 migrations applied before the ownership migration stopped. The approved staging remedy was
   `GRANT musclecuties_migrator TO mc_admin;`, followed by a successful rerun. Production must
   verify its own runner role before starting the migration chain; staging approval does not
   authorize a production grant.
3. **Discovery/dispatch:** no login, no inheritance from the app role. The discovery function is
   `SECURITY DEFINER` with `search_path = pg_catalog, app, pg_temp`, owned by the discovery owner, and
   executable only by dispatch. The catalog lock function is owned by the migrator and executable by
   the app.
4. **Read-only preflight:** may read catalog metadata and evaluate `has_*_privilege`; it must not
   write, create temp objects in `app`, or change role membership. `schema-preflight`/`schema-verify`
   run in a read-only transaction.

## Tenant safety

- Every tenant table keeps `ENABLE` and `FORCE ROW LEVEL SECURITY`; the app role must not be the table
  owner and must not have `BYPASSRLS`.
- Tenant policies compare `user_id` with `current_setting('app.user_id')`; `app.users` compares `id`.
  The contract pins these policies by name, command and permissiveness.
- The discovery role receives only the columns it needs on
  `app.workout_account_planning_states`; the cycle-readiness flag is granted separately.
- Never disable RLS or remove tenant predicates to make a test pass. The compatibility contract
  fails closed on either.

## Approval-gated cutover (not executed)

1. Inventory every API operation on global authentication tables and user lookup before
   approving grants. Current `PostgresDbContext` serves tenant routes **and** email
   challenges, registration, password reset, login and migration-ledger readiness. The
   existing `musclecuties_app` grants alone do not prove those paths work. Split the
   auth context or approve a narrowly scoped auth role and prove it cannot bypass
   tenant isolation. Do not grant the app group broad auth access as a shortcut.
2. Owner approves the exact login, role memberships, auth design, grants and staged
   secret rotation. Provision `musclecuties_api_login` with no elevated attributes;
   retain `musclecuties_app` as `NOLOGIN` and prohibit migrator/dispatch memberships.
3. In a protected runner, set `MC_RUNTIME_CANDIDATE_POSTGRES` to the candidate TLS
   connection with `Username=musclecuties_api_login` and
   `Options=-c role=musclecuties_app`, then run
   `dotnet run --project src/MuscleCuties.DbMaintenance -- runtime-principal-preflight`.
   This read-only check must report `catalog-compatible` with no failure codes. It
   deliberately reports only catalog access; it does not certify runtime behavior.
4. Run `schema-preflight` with the separate inspection connection, then exercise
   synthetic registration, password login/reset, authenticated tenant read/write,
   cross-tenant denial, and `/health/ready` against a candidate environment using the
   new principal. Record the observed effective role and test evidence without secrets.
5. Rotate the serving Lambda connection only after those checks and separate owner
   approval. Verify the same routes after cutover. Restore the previous secret if
   compatibility or isolation fails; keep the schema unchanged.

## Migration-runner prerequisite

Run `migration-prerequisites` with the exact connection that will execute `migrate --confirm`.
It reads the migration history and PostgreSQL role catalog without changing either. Exit 0 means
the pending ownership migration can assume `musclecuties_migrator` and, when the floor table
already exists, its current owner. Exit 1 returns a stable code such as
`runner_lacks_migrator_membership` or `runner_lacks_floor_owner_membership`. The `migrate`
command repeats this check before applying pending EF migrations.

If the migrator membership is missing, review the target database and migration login, then
have an authorized database administrator grant `musclecuties_migrator` to that **deployment
login only**. Do not grant it to `musclecuties_app` or the serving Lambda login. Re-run the
read-only command and record its output before approving the migration. The staging `mc_admin`
grant is an interim deployment credential arrangement, not the target runtime design.

## Evidence in this branch

- The contract pins role attributes and forbidden memberships.
- Disposable tests revoke a required grant and prove `SCHEMA_GRANT_MISMATCH`.
- A disposable probe membership proves `SCHEMA_ROLE_MEMBERSHIP_MISMATCH`.
- Function owner and `search_path` drift proves `SCHEMA_FUNCTION_SECURITY_MISMATCH`.
- No live role, grant, principal or secret was modified.

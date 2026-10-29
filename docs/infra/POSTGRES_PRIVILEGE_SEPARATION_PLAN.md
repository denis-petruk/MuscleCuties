# PostgreSQL privilege separation plan

Status: **proposal only**. No live role, grant, principal or connection setting was changed.

## Verified problem

The serving Lambda connects with PostgreSQL master login `mc_admin`. That principal is not a
restricted application role. This is a real privilege-separation defect, but it is **not** the cause
of the physically missing columns: the incident is code/schema deployment drift across the 28 pending
migrations. Fixing the principal must not be presented as repairing the schema.

## Proposed principals

| Principal | Purpose | Attributes |
| --- | --- | --- |
| `musclecuties_app` | Runtime application login | `LOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| `musclecuties_migrator` | DDL, ownership transfer, catalog locking | `NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| `musclecuties_workout_discovery_owner` | Owner of the discovery function and column-level reads | `NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| `musclecuties_workout_dispatch` | Cursor read/update and function execute | `NOLOGIN NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE` |
| schema inspector | Read-only release preflight | Membership in `pg_read_all_data`-style inspection only; no write, no DDL |

The inspector is deliberately described as a capability, not a created role: the current contract
uses existing principals and the release runner's connection. Creating a new inspector login is an
owner-approved cutover step.

## Required separation

1. **Runtime application:** `musclecuties_app` only. It must own no tables, hold no DDL, and never be
   a member of `musclecuties_migrator`, `musclecuties_workout_discovery_owner` or
   `musclecuties_workout_dispatch`. The compatibility contract enforces these forbidden memberships.
2. **Migration/reconciliation:** `musclecuties_migrator` or a dedicated deployment principal that can
   assume it. The `OwnReviewedTemplateVolumeFloors` migration explicitly requires the executor to be
   able to assume the floor owner and `musclecuties_migrator`, and the migrator needs `catalog CREATE`
   for the ownership transfer.
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

1. Owner approves the runtime principal change and the exact role/grant set.
2. Create the application login without superuser, bypass-RLS, createdb or createrole.
3. Grant only the privileges the contract requires; verify no table ownership and no forbidden
   memberships.
4. Rotate the Lambda connection secret through the approved secret store; never write credentials to
   a file, log, ticket or chat.
5. Run `schema-preflight` with the inspector connection and record the fingerprint.
6. Deploy the Lambda with the new secret; verify one authenticated request path and one tenant-scoped
   read/write.
7. Roll back the secret if compatibility or tenant isolation fails; do not roll back schema changes.

## Evidence in this branch

- The contract pins role attributes and forbidden memberships.
- Disposable tests revoke a required grant and prove `SCHEMA_GRANT_MISMATCH`.
- A disposable probe membership proves `SCHEMA_ROLE_MEMBERSHIP_MISMATCH`.
- Function owner and `search_path` drift proves `SCHEMA_FUNCTION_SECURITY_MISMATCH`.
- No live role, grant, principal or secret was modified.

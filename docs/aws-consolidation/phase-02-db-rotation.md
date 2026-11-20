# Phase 0.2 — application database role rotation

Executed 2026-11-19 against `musclecuties-staging-db.cxi4kwwq853m.us-east-2.rds.amazonaws.com` /
`musclecuties_staging`, connecting as `mc_admin` over SSL.

## Roles discovered (actual `pg_roles` rows, not assumed)

```
       rolname        | rolcanlogin | rolsuper | rolcreatedb | rolconnlimit | valid_until
----------------------+-------------+----------+-------------+--------------+-------------
 mc_admin             | t           | f        | t           |           -1 | infinity
 mc_workout_discovery | t           | f        | f           |           -1 | never
 mc_workout_tenant    | t           | f        | f           |           -1 | never
```

The brief's premise holds: both application roles exist. There are exactly three `mc_*` roles.

`mc_admin` is **not** a superuser but does hold CREATEDB, and it is the account the running
Lambda authenticates with.

## Rotated and verified

Each role was altered with a fresh 256-bit password (`openssl rand -hex 32`), then
re-authenticated on a **separate connection** as that role:

```
verified mc_workout_discovery -> musclecuties_workout_dispatch|mc_workout_discovery
verified mc_workout_tenant    -> musclecuties_app|mc_workout_tenant
```

Note the asymmetry: `session_user` is the login role while `current_user` is a different
working role. Both logins carry an `ALTER ROLE ... SET ROLE` default, so each acts as a
least-privilege identity (`musclecuties_app`, `musclecuties_workout_dispatch`) rather than
touching tables directly. That is a deliberate design and it survived the rotation.

Passwords went to Secrets Manager `musclecuties/staging/db-role-credentials` **before** being
applied, so no password can be orphaned by a partial run. None were printed, logged or
committed.

## Baseline

`docs/aws-consolidation/baseline-pre-rename-20261119.txt`

- `server_version` = **17.5** (matches the expected engine version for the restore)
- schemas: `app` 55 tables, `catalog` 17, `ops` 3, `public` 1
- migration history: `ops.schema_migrations` and `public.__EFMigrationsHistory` (exact counts)
- planning states: **`app.workout_account_planning_states`**, not `public.*` — the brief's
  schema assumption was wrong and the first baseline run errored on it
- `pg_stat_user_tables` cardinalities for every table

Diff the Phase 1c restore against this file.

## Deliberately untouched

- `mc_admin` — not rotated. Rotating it requires rewriting the secret *and* republishing the
  Lambda alias in one step. A plaintext copy was also pasted into a chat transcript on
  2026-11-19, so it should be treated as compromised and rotated at the next opportunity.
- No snapshot, restore, rename, alias, secret or Lambda repoint was performed.
- The `MuscleCuties` budget was not deleted.

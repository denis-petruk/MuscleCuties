# AWS backend repair — clean migration chain and readiness

**Current-checkout note (2026-11-04):** This dated document records a migration-era source tree and target investigation. The current checkout is model-first and has no EF migration chain; its database commands are dbinit, dbcheck, and seed. Treat migration, schema-preflight, schema-verify, schema-reconcile, and migration-prerequisite instructions here as historical evidence, not executable instructions for this checkout. Revalidate the current source, target, and authorization before any database operation. See [the current schema runbook](POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md).


Date: 2026-10-29. Branch: `infra/aws-lambda-postgres`. Scope: local repair and verification only.

This record follows the read-only diagnosis in
[AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md](AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md), which established
that the deployed Lambda served code ahead of 28 unapplied database migrations while `/health/ready`
reported Healthy. No AWS resource, database, deployment package or pipeline was changed in this phase.

## 1. Defects confirmed and repaired

### 1.1 The migration chain could not be applied to a clean database

**Root cause.** `20261027234500_OwnReviewedTemplateVolumeFloors` transfers ownership of
`catalog.workout_template_volume_floors` to the `musclecuties_migrator` group. PostgreSQL requires the
incoming owner to hold `CREATE` on the schema. The only grant of `CREATE ON SCHEMA catalog` was in
`services/postgresql/roles-and-grants.sql`, which the repository documents as running **after** the
migrations. On a clean database the chain therefore aborted at migration 51 of 59.

Reproduction, before the repair, using the documented maintenance command against an empty
PostgreSQL 17 database and no manual grants:

```text
$ dotnet run --project src/MuscleCuties.DbMaintenance -c Release -- migrate --confirm
Database maintenance failed (PostgresException: P0001: Migration executor must be able to assume both
table owner and migrator roles; migrator requires catalog CREATE for ownership transfer).
```

`migration-status` afterwards showed 49 applied, 10 pending, starting at `20261027234500`.

**Fix.** The migration now establishes the same catalog `CREATE` grant that `roles-and-grants.sql` makes
for the migration group when the incoming owner lacks it, then performs the ownership transfer. The
role-assumption preconditions still fail closed, and a runner without grant authority still receives the
provider error instead of a silently skipped transfer. The change is confined to an unapplied migration
body; applied migration history is untouched.

### 1.2 Readiness passed while the schema lagged the deployed code

**Root cause.** `PostgreSqlReadinessCheck` called `CanConnectAsync` only. A deployment that replaced
Lambda code without applying pending migrations returned `Healthy`, which is how the 2026-10-29
dashboard and injury failures reached clients on a "healthy" deployment.

**Fix.** A second health check, `postgresql-schema`, compares the database migration ledger with the
migrations compiled into the running assembly. `DatabaseSchemaCompatibility` evaluates the two sets and
reports one of: matching, schema behind the assembly, ledger ahead of the assembly, or divergent. The
check is read-only, applies no DDL, and its description names no table, column or SQL text. `/health/ready`
now returns 503 while a code/schema mismatch exists.

## 2. Verification evidence

All commands ran on the local machine against disposable PostgreSQL 17 containers.

| Scenario | Command | Result |
|---|---|---|
| Empty database, clean chain | `DbMaintenance migrate --confirm` | 59 applied, 0 pending, exit 0 |
| Repeated run on current database | `DbMaintenance migrate --confirm` | no-op, exit 0 |
| Model snapshot vs migrations | `WorkoutModelSnapshotTests` | passes, no pending model changes |
| Older schema upgrade (31 → 59) | `DbMaintenance migrate --confirm` on a database updated to `20261024200000` | 59 applied, 0 pending |
| Data preservation across upgrade | synthetic user, injury, plan, day, exercise and session rows | all rows present; `cleared_date` NULL, `confirmed_active_seconds` 0, `is_removed_by_safety` false |
| New schema objects after upgrade | ledger and catalog inspection | `workout_account_planning_states`, `workout_completion_day_credits`, `workout_injury_episodes`, `workout_cycle_readiness_occurrences` present |
| Incident columns | catalog inspection | `workout_injury_logs.cleared_date`, `workout_session_logs.confirmed_active_seconds`, `workout_session_logs.timing_interval_id` present |
| Runtime on migrated schema | server on the migrated database | `/health/live` 200, `/health/ready` 200, `/api/v1/accounts/registration/quiz` 200 |
| Runtime on schema 28 migrations behind | server on the partially migrated database | `/health/live` 200, `/health/ready` **503**, log: "Database schema is behind the deployed assembly; apply the pending migrations before serving traffic." |
| Invalid database credentials | server with a wrong password | `/health/ready` 503; route returns HTTP 500 with `code: databaseError`; PostgreSQL `28P01` in the log; no fallback |
| Lambda package | `dotnet-lambda package --function-architecture arm64` | archive built, 27 assemblies including EF Core, Npgsql, SkiaSharp |
| Packaging completeness | `deps.json` runtime targets compared with archive contents | 28 of 28 present, 0 missing |
| Packaged artifact boot | archive contents launched under `mcr.microsoft.com/dotnet/aspnet:10.0` (linux/arm64) | binds and serves `/health/live` 200, `/health/ready` 200, quiz 200 |
| Test suites | Core, Server, Registration, Mobile contracts | Core 989, Server 88, Registration 42, Mobile contracts 9 — all pass |

The container boot logged an optional `libgssapi_krb5.so.2` load error from the base image. Password
authentication succeeded and every probed route returned 200, so the dependency closure is complete for
this function; the message is a base-image difference, not a packaging defect.

## 3. Not performed

- No migration was executed against the AWS staging database, and no snapshot or backup was taken. The
  live database still ends at `20261024200000_AddWorkoutExerciseExclusionsV010`.
- No Lambda deployment, alias change or pipeline edit. `deploy_staging_lambda` still has no schema gate;
  adding one requires database credentials this pipeline does not hold.
- The Release device iOS build fails in this environment: "No valid iOS code signing keys found in
  keychain". Signing identities remain an external prerequisite.
- The readiness check validates the migration ledger, not the physical catalog. The requirement-manifest
  evaluator described in the implementation plan (§`DatabaseSchemaCompatibility`) remains future work.

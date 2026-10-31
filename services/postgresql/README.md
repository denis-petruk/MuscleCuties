# PostgreSQL installation, roles, and tenant context

## Fresh database without migrations

The fresh-install path is static SQL. It does not run EF migrations and does not create
`public."__EFMigrationsHistory"`. The Lambda server also never migrates on cold start
(`Lambda__DatabasePool__RunMigrationsOnColdStart=false`).

Run the installer once against an empty database, as an administrator that can create roles and
`SET ROLE musclecuties_migrator` (the RDS master user or equivalent):

```sh
psql "$DATABASE_URL" -f services/postgresql/install.sql
```

`install.sql` applies these files in order:

1. `bootstrap.sql` — group roles and the `app`, `catalog`, and `ops` schemas.
2. `schema.sql` — every table, index, function, policy, and RLS setting.
3. `ownership.sql` — restricted owners for the `SECURITY DEFINER` bridge objects.
4. `roles-and-grants.sql` — runtime grants and removal of the temporary `public` CREATE.
5. `catalog-seed.sql` — the reviewed 742-row canonical catalog.

The installer fails if `catalog.week_templates`, `catalog.exercises`, or
`catalog.workout_exercise_definitions` is empty, because workout generation cannot run without
that reference data. It refuses to take ownership of pre-existing application schemas; resolve any
ownership mismatch through a separately reviewed procedure.

`schema.sql` and `catalog-seed.sql` are generated from a disposable PostgreSQL 17 database after
applying the reviewed EF migration chain and canonical seed. Do not hand-edit them. When the EF
model or canonical manifest changes, regenerate both files, then run `install.sql` against a fresh
database and repeat the integration test in this README.

The EF migration chain and `DbMaintenance migrate` remain only as a legacy upgrade path for
databases created before the static installer. Do not run them on a fresh database.

## Runtime logins and tenant context

Provision secret-bearing login roles through the deployment secret manager. The runtime data
connection uses a login assigned to `musclecuties_app` with `Options=-c role=musclecuties_app`;
authentication uses a separate login assigned to `musclecuties_auth` with
`Options=-c role=musclecuties_auth`. These group-role settings ensure `current_user` inside RLS and
the auth lookup policy names the least-privilege role.

The runtime data group can read `catalog`, read and modify only tenant tables protected by RLS, and
cannot access global email challenges, operations logs, migration history, or schema ownership.
Authentication can call `app.lookup_user_by_email(email)` or `app.lookup_user_by_apple_id(subject)`
to retrieve the minimal login fields for one account and can use the email challenge table. Each
function runs as the migration owner and is constrained by the migration-owner-only RLS policy; the
auth role has no direct access to `app.users`. It cannot access tenant tables, catalog data, or
operations logs.

Resolve the tenant UUID from authenticated server-side identity. Start a database transaction, call
`PostgresTenantContext.SetForCurrentTransactionAsync(context, authenticatedUserId, cancellationToken)`,
perform the operation and save changes, then commit. This uses PostgreSQL `set_config(..., true)`,
which is transaction-local; committing or rolling back clears the setting before the connection
returns to the pool. Never accept the tenant UUID from an untrusted request body or use session-level
`SET`.

Because the provider enables transient retries, callers that open explicit transactions must
execute the complete transaction delegate under EF Core's configured execution strategy. Operations
with externally visible effects also need the PG02-05 idempotency key before retrying an ambiguous
commit.

## Verification

To rehearse the static installer in an isolated PostgreSQL 17 container, have Docker running with
the `postgres:17-alpine` image already available, then run:

```sh
MC_BOOTSTRAP_DOCKER_TEST=1 dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj --filter FullyQualifiedName~PostgreSqlStaticInstallTests
```

The test creates its own container and removes it when finished. It is skipped unless explicitly
enabled.

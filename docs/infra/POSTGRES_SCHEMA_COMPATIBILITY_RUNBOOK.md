# PostgreSQL schema initialization and compatibility runbook

Updated 2026-11-04 to match the current model-first checkout.

## Current schema authority

PostgresDbContext is the model authority for tables, columns, indexes, and constraints. The current checkout contains no EF migration chain. PostgresSchemaBootstrap uses EnsureCreated for model objects, then applies the embedded schema supplement, security SQL, and schema fingerprint metadata. DbMaintenance exposes dbinit, dbcheck, and seed for these operations; do not use commands from older migration-era reports as if they exist in the current checkout.

## Read-only compatibility check

Run dbcheck against a target only after verifying the endpoint, account, database name, and environment through the approved access path. The connection string is read from ConnectionStrings__PostgreSql. For non-loopback hosts, the tool requires SSL Mode=VerifyFull and a valid root certificate.

    dotnet run --project src/MuscleCuties.DbMaintenance -- dbcheck

The current inspector reads catalog metadata, embedded security declarations, and ops.schema_metadata. It compares model tables and columns, required roles/functions/RLS/policies, and the recorded model fingerprint. It does not change the database and does not prove application behavior, all grants, or release readiness. A compatible result is evidence only for these checks and this exact target at that time.

Do not place connection strings or credentials in documentation, logs, shell history, or command output. Record the target identity, timestamp, status, model fingerprint, and failure codes without secrets.

## Initialization and seeding

dbinit uses EnsureCreated, applies supplemental/security SQL, updates schema metadata, and seeds the canonical catalog. It is a database write. Run it only against a disposable local database or an explicitly approved target after the current backup, compatibility, recovery, and release preconditions are satisfied. It is not an incremental migration command and must not be treated as a repair for an unknown existing schema.

The seed command writes catalog data. Treat it as a target-sensitive write and use only the approved environment. Lambda startup must not run database maintenance.

## Evidence and limits

- Current command definitions: [Program.cs](../../src/MuscleCuties.DbMaintenance/Program.cs).
- Bootstrap behavior: [PostgresSchemaBootstrap.cs](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresSchemaBootstrap.cs).
- Drift checks: [PostgresSchemaDriftInspector.cs](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresSchemaDriftInspector.cs).
- Supplemental objects and security policy: [Persistence/Schema](../../src/MuscleCuties.Infrastructure.PostgreSql/Persistence/Schema/).
- Isolated recovery procedure: [POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md](POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md).

Older release plans and approval records preserve dated evidence from a migration-based source tree. Reconcile them against this checkout and the exact target before relying on them.

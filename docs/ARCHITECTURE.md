# Architecture

Read this reference for cross-layer changes, new services, API contracts, persistence, or hosting changes. Paths below were checked against the current checkout.

## Dependency direction

Dependencies point toward the domain. Presentation, transport, persistence, and platform details do not determine Core behavior.

- src/MuscleCuties.Core — domain entities, value objects, deterministic rules, and invariants. No ORM, ASP.NET, MAUI, or transport dependencies.
- src/MuscleCuties.UseCases — application orchestration and use-case contracts. It coordinates domain behavior and boundary abstractions.
- src/MuscleCuties.Infrastructure.PostgreSql — PostgreSQL persistence and the EF Core PostgresDbContext.
- src/MuscleCuties.Infrastructure — mobile-side API, encrypted cache, notification, and localization adapters.
- services/MuscleCuties.Server — ASP.NET Core API composition, authentication, authorization, serialization, and AWS Lambda host.
- services/MuscleCuties.WorkoutReconciliation — scheduled worker for pending workout revisions.
- src/MuscleCuties.UI — .NET MAUI iOS presentation. ViewModels use client-facing contracts and services, never DbContext or repositories.
- src/MuscleCuties.DbMaintenance — operator CLI for database initialization, compatibility checks, and catalog seeding.
- tests — Core, Server, Registration, Mobile Contracts, and database-backed Registration integration suites.

Core has no outer-layer dependencies. UseCases may depend on Core and application abstractions. Infrastructure implements inward-facing ports. Server and UI are outer layers and must not leak persistence models across API or presentation boundaries.

## PostgreSQL authority

The current checkout is model-first and contains no EF migration chain. PostgresDbContext is the model authority for tables, columns, indexes, and constraints. PostgresSchemaBootstrap initializes from that model using EnsureCreated; Persistence/Schema/schema_supplement.sql and security.sql own supplemental objects and security policy that the model does not express. DbMaintenance dbinit initializes and seeds; dbcheck reports schema compatibility and the model fingerprint.

Treat dbinit as a target-sensitive database write. Do not run it against shared, staging, or production infrastructure without the authorization and preconditions in the current database runbooks. Do not infer that a dated migration or deployment plan matches this checkout. Reconcile such records against the current model and exact target before any operation.

## Client and API boundaries

The Server owns HTTP and authentication boundaries. Keep request and response contracts synchronized with the client. Return structured errors and enforce user ownership on the server.

The UI is iOS-only; do not add an Android target. The UI uses API/client contracts, not Core persistence types. Models under Models/UI may be XAML runtime contracts, so search all XAML bindings before renaming or deleting a bound member. Offline snapshots use ILocalCacheService and encrypted JSON in FileSystem.AppDataDirectory, with keys held in SecureStorage. Do not use SQLite or Preferences for sensitive records.

The production runtime is C#/.NET. Python tooling may be used only where a task-specific tool or script requires it; it is not an application runtime dependency.

## Canonical references

- [DESIGN.md](../DESIGN.md) owns the design entry point; [docs/UI_PRESENTATION_DESIGN_SPEC.md](UI_PRESENTATION_DESIGN_SPEC.md) and [docs/design/](design/) hold detailed UI contracts and evidence.
- [PostgreSQL schema runbook](infra/POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md) owns the current schema-check procedure.
- [Backup and restore runbook](infra/POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md) owns the isolated recovery procedure.
- [AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md](infra/AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md) and its dated companion records preserve release investigations and historical environment evidence. Revalidate them against the current code and target before treating any instruction as current.

## Proposed environment and release evolution — 2026-11-04

The current project/schema description above remains an observation of this checkout. The [GitHub transition target](architecture/github-transition/01-target-architecture.md) proposes four change boundaries, isolated Staging/Beta, a versioned migration lifecycle and protected independent release tracks. The [audit](architecture/github-transition/00-current-state-audit.md) records conflicts between current model-first initialization and dated migration runbooks. This target is not implemented: do not run dbinit to repair drift or assume a migration chain exists. Adopt changes incrementally through the [ordered packages](architecture/github-transition/03-implementation-roadmap.md), with tester data preserved and owner-approved external actions.

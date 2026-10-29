# MuscleCuties

iOS-first fitness, nutrition and cycle-tracking app. The mobile client is .NET MAUI; the backend is an ASP.NET Core API packaged for AWS Lambda and backed by PostgreSQL. Domain logic is pure C# — no SQLite, no Python, and no health data leaves first-party services.

## Architecture

Dependencies point inward only: `UI / Server → Infrastructure → UseCases → Core`.

| Project | Layer | Responsibility |
| --- | --- | --- |
| `src/MuscleCuties.Core` | 1 | Domain entities, rules and deterministic math. No ORM, JSON, ASP.NET or MAUI references. |
| `src/MuscleCuties.UseCases` | 2 | Single-purpose interactors (`ExecuteAsync(input) → output`). Orchestrates stores and delegates every rule to Core. |
| `src/MuscleCuties.Infrastructure` | 3 | Client adapters: backend API client, encrypted snapshot cache, notifications, localization. |
| `src/MuscleCuties.Infrastructure.PostgreSql` | 3 | Server persistence. `PostgresDbContext` is the sole schema and migration authority. |
| `services/MuscleCuties.Server` | 4 | ASP.NET Core composition root and HTTP surface, deployed to AWS Lambda. |
| `src/MuscleCuties.UI` | 4 | .NET MAUI client: iOS primary, with Mac Catalyst, Android and Windows targets. |
| `src/MuscleCuties.DbMaintenance` | — | Operator CLI for migrations, seeding and catalog maintenance. |
| `services/MuscleCuties.WorkoutReconciliation` | — | Recurring reconciliation host, invoked one page at a time. Read its README before deploying. |

## Layout

```
src/                 Production libraries, the maintenance CLI and the MAUI client
services/            Deployable hosts (Lambda API, reconciliation worker)
tests/               xUnit suites
docs/                Delivery, migration and release documentation
artifacts/           Local packaging output (ignored)
MuscleCuties.sln     Solution over the production and test projects
Directory.Build.*    Shared nullable/implicit-using settings, analyzers and test-dependency copy
```

## Build and test

The SDK is pinned by `global.json`, which rolls forward to the latest installed 10.x.

```bash
dotnet restore MuscleCuties.sln
dotnet build MuscleCuties.sln -c Release
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
```

| Suite | Covers |
| --- | --- |
| `MuscleCuties.Core.Tests` | Domain rules, use cases and architecture constraints; in memory, no database. |
| `MuscleCuties.Server.Tests` | Server route parity and HTTP contracts. |
| `MuscleCuties.Registration.Tests` | Registration and onboarding contracts. |
| `MuscleCuties.Registration.Integration.Tests` | Database-backed flows; needs a disposable PostgreSQL instance. |
| `MuscleCuties.Mobile.Contracts.Tests` | Client and backend contract shapes. |

The backend needs a PostgreSQL connection string and JWT signing settings from configuration or environment variables. No credential, signing file or database connection belongs in the repository.

```bash
dotnet run --project services/MuscleCuties.Server
```

The MAUI client builds for iOS only on macOS with Xcode and the .NET iOS workload installed; on Linux the project targets Android.

```bash
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios
```

GitLab CI runs the suites, packages the Lambda archive, and exposes deployment as a manual job.

## Documentation

- [STATUS.md](STATUS.md): dated delivery log, verification evidence and open items.
- [ROADMAP.md](ROADMAP.md): delivered foundations and active scope.
- [docs/README.md](docs/README.md): index of delivery, migration and release documents.
- [Submission readiness](docs/SUBMISSION_READINESS.md): release gates before TestFlight and the App Store.
- [Operator guide](docs/OPERATOR_ACTION_MANUAL.md): environment, email delivery, database maintenance and device acceptance.
- [AGENTS.md](AGENTS.md): engineering directive — layer boundaries, privacy invariants and the pre-commit gate.

## Conventions

- Every threshold, offset and dimension is a named constant; bare numeric literals are rejected in review.
- Schema changes ship as migrations under `Infrastructure.PostgreSql`; applied migrations are never edited.
- User and health data stays in first-party PostgreSQL and private object storage. External APIs receive generic queries only.
- Failed network or server operations surface a specific outcome to the user rather than a generic message.

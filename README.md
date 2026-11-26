# MuscleCuties

iOS fitness, nutrition and cycle-tracking app. The client is .NET MAUI (iOS only); the backend is an ASP.NET Core API on AWS Lambda backed by PostgreSQL. All application logic is C#.

## Layout

- `src/MuscleCuties.Core` — domain entities, deterministic rules, invariants. No outer dependencies.
- `src/MuscleCuties.UseCases` — application orchestration and contracts; depends only on Core.
- `src/MuscleCuties.Infrastructure.PostgreSql` — PostgreSQL persistence, EF Core `PostgresDbContext`, schema scripts.
- `src/MuscleCuties.Infrastructure` — mobile-side API client, encrypted cache, notifications, localization.
- `src/MuscleCuties.UI` — MAUI iOS presentation.
- `src/MuscleCuties.DbMaintenance` — operator CLI: `migrate`, `seed`, `dbcheck`.
- `services/MuscleCuties.Server` — API composition, auth, Lambda host.
- `services/MuscleCuties.WorkoutReconciliation` — scheduled worker for pending workout revisions.
- `tests/` — xUnit suites. `artifacts/` — packaging output, ignored by Git.

## Build and test

The SDK is pinned by `global.json` (.NET 10).

    dotnet restore MuscleCuties.sln
    dotnet build MuscleCuties.sln -c Release
    dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release

| Suite | Covers | Needs PostgreSQL |
| --- | --- | --- |
| `MuscleCuties.Core.Tests` | Domain rules, use cases, architecture and UI guard tests | No |
| `MuscleCuties.Server.Tests` | HTTP contract and authentication route tests | No |
| `MuscleCuties.Registration.Tests` | Registration and onboarding contracts | No |
| `MuscleCuties.Mobile.Contracts.Tests` | Client and backend contract shapes | No |
| `MuscleCuties.Registration.Integration.Tests` | Database-backed flows | Yes |

Run the suite that covers the files you changed, not the whole solution.

### PostgreSQL-backed suites

    brew services start postgresql@16
    source .env

Configure loopback PostgreSQL connection strings in your ignored local `.env` file.

### Local API

    dotnet run --project services/MuscleCuties.Server

The backend reads PostgreSQL connection and JWT signing settings from configuration or environment variables. Credentials, signing files and connection strings stay out of the repository.

## Database

`migrate` + `seed` + `dbcheck` is the provisioning path. `EnsureCreated` and `dbinit` are for disposable local databases only.

## CI/CD

GitHub Actions, in `.github/workflows/`: `ci.yml` (build, unit tests), `db-verify.yml` (migration rehearsal on disposable databases), `backend-build.yml` (immutable Lambda packages), `backend-release.yml` (deploy), `db-release.yml` (schema migrations), `mobile-release.yml` (signed TestFlight build). A push to `main` runs `ci.yml` and, once every required check passes, releases the backend to Lambda and the app to TestFlight through the same reusable workflows; until the six Apple secrets exist the iOS job is skipped with a notice naming them.

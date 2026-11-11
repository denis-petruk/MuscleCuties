# Reproducible PostgreSQL fixtures

The `MuscleCuties.Registration.Integration.Tests` suite and the all-route latency gate in
`MuscleCuties.Server.Tests` are deliberately database-backed. They must run against a real
PostgreSQL server: an in-memory store cannot exercise the schema, row-level security, SQL
locking, or the route latency that those tests assert.

This repository therefore includes a loopback-only bootstrap script:

```bash
brew services start postgresql@16
./scripts/prepare-integration-postgres.sh
source .env.integration
```

`prepare-integration-postgres.sh` refuses any host other than `127.0.0.1`/`localhost`, drops
and recreates only the `mc_*` disposable databases, seeds the canonical schema through the
repository's own `InitializeAndSeedExplicitlySelectedDisposableDatabase` harness, and creates
the restricted Pg09 login role. It writes `.env.integration`, which is ignored by Git and
contains no credentials or secrets (local PostgreSQL uses the workstation login and loopback
authentication).

## Fixtures and environment variables

| Variable | Fixture | Used by |
| --- | --- | --- |
| `MC_REGISTRATION_WRITE_TEST_POSTGRES` | `mc_test_nutrition_seeded` | Registration, cycle, injury, nutrition, and planning persistence tests |
| `MC_REGISTRATION_TEST_POSTGRES` | `mc_test_nutrition_seeded` | Canonical registration catalog tests |
| `MC_SCHEMA_COMPATIBILITY_POSTGRES` | `mc_test_nutrition_seeded` | Schema drift and disposable-database collection fixture |
| `MC_RETRY_TEST_POSTGRES` | `mc_retry_disposable` | Transient-failure retry integration test |
| `MC_PG09_LOAD_POSTGRES` | `mc_pg09_disposable`, role `mc_pg09_migrator` | Pg09 least-privilege and bounded-pool load test |
| `MC_LATENCY_POSTGRES` | `mc_test_nutrition_seeded` | All-route cold/warm p95 latency gate |
| `MC_INTEGRATION_POSTGRES` | marker (`1`) | Discovery-time guard for the database-backed test attributes |
| `MC_PREPARE_DISPOSABLE_WORKOUT_DATABASE` | setup-only marker (`True`) | Seeds the disposable database; do not set it for normal test runs |

The Pg09 role is created as `LOGIN CREATEROLE NOSUPERUSER NOBYPASSRLS`. The script transfers
ownership of the fixture's `app`/`public` tables and grants schema access to `app`, `public`,
and `catalog`; the test only reads `catalog.quiz_questions` as that role.

## Running the suites

Source the generated environment first, then run the suites normally:

```bash
source .env.integration

dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Debug -m:1
dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Debug -m:1
dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Debug -m:1
dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Debug -m:1
dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Debug -m:1 --blame-hang-timeout 10m
python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests
```

The integration tests use `[PostgresFact]`/`[PostgresTheory]`. When the bootstrap marker or
connection variables are absent, each database-backed test is reported as **skipped** with the
bootstrap instruction instead of failing with an opaque connection error. The all-route latency
test likewise fails once, with an explicit `MC_LATENCY_POSTGRES` message, when its fixture is
missing.

## Latency budget

`EveryMappedRouteHasMeasuredColdAndWarmP95WithinBudget` keeps its **1000 ms** budget unchanged.
The earlier timeout was caused by pointing the test at a synthetic unreachable loopback database,
which made every measured request short-circuit at the cancellation timeout. The budget is an
acceptance threshold for real route behavior, not a value to raise when the database is absent.
`MC_LATENCY_POSTGRES` points the test at the seeded fixture so cold and warm p95 are measured
against real PostgreSQL. The checked-in `docs/performance/server-latency-report.json` is the
route inventory/shape gate; the test measures the current tree and does not rewrite that report
unless `MC_WRITE_LATENCY_REPORT=1` is set.


## Verified fresh-clone run — 2026-11-11

A throwaway clone of `origin/product/qa-fixes-20261109` was created with no `.env.integration`
and no build artifacts. All disposable fixture databases and the `mc_pg09_migrator` role were
removed first. The committed bootstrap then exited 0, recreated the three databases and the
role, and the suites were run from that clone in a non-interactive shell after sourcing
`.env.integration`:

```bash
./scripts/prepare-integration-postgres.sh
source .env.integration
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Debug -m:1 --blame-hang-timeout 10m
dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Debug -m:1 --blame-hang-timeout 10m
dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Debug -m:1 --blame-hang-timeout 10m
dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Debug -m:1 --blame-hang-timeout 10m
dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Debug -m:1 --blame-hang-timeout 10m
python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests
```

Observed result lines:

```text
Core:         Failed: 0, Passed: 1378, Skipped: 0, Total: 1378
Mobile:       Failed: 0, Passed: 183,  Skipped: 0, Total: 183
Registration: Failed: 0, Passed: 54,   Skipped: 0, Total: 54
Server:       Failed: 0, Passed: 120,  Skipped: 0, Total: 120
Integration:  Failed: 0, Passed: 105,  Skipped: 0, Total: 105
Deployment:   Ran 17 tests, OK
```

With `MC_INTEGRATION_POSTGRES` unset, the same integration assembly reported
`Failed: 0, Passed: 1, Skipped: 82, Total: 83`; every skipped test carries the bootstrap
instruction. The one passing test is the explicit seed harness, which returns immediately unless
`MC_PREPARE_DISPOSABLE_WORKOUT_DATABASE=True`.

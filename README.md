# MuscleCuties

iOS-first fitness, nutrition, and cycle-tracking app. The mobile client is .NET MAUI; the backend is an ASP.NET Core API hosted on AWS Lambda and backed by PostgreSQL. Application logic is C# and health data stays within approved first-party services.

## Architecture

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for the verified project map, dependency direction, API/client boundaries, and current model-first database setup.

## Layout

- src/ — production libraries, database maintenance CLI, and MAUI client.
- services/ — Lambda API, workout reconciliation worker, static site, and local PostgreSQL tooling.
- tests/ — xUnit suites.
- docs/ — canonical architecture, product, safety, verification, design, and operational references.
- artifacts/ — local packaging output, ignored by Git.
- MuscleCuties.sln — solution over production and test projects.

## Build and test

The SDK is pinned by global.json. Install a compatible .NET 10 SDK before building.

    dotnet restore MuscleCuties.sln
    dotnet build MuscleCuties.sln -c Release
    dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release

| Suite | Covers |
| --- | --- |
| MuscleCuties.Core.Tests | Domain rules, use cases, and architecture constraints; in memory, no database. |
| MuscleCuties.Server.Tests | Server route parity and HTTP contracts. |
| MuscleCuties.Registration.Tests | Registration and onboarding contracts. |
| MuscleCuties.Registration.Integration.Tests | Database-backed flows; requires disposable PostgreSQL. |
| MuscleCuties.Mobile.Contracts.Tests | Client and backend contract shapes. |

### PostgreSQL-backed suites

`MuscleCuties.Registration.Integration.Tests` and the all-route latency gate in
`MuscleCuties.Server.Tests` require real PostgreSQL fixtures. From a fresh checkout:

```bash
brew services start postgresql@16
./scripts/prepare-integration-postgres.sh
source .env.integration
```

The bootstrap creates and seeds the disposable databases and the restricted Pg09 role, then
writes an ignored `.env.integration` file containing only loopback connection strings. See
[docs/INTEGRATION_POSTGRES.md](docs/INTEGRATION_POSTGRES.md) for the fixture/env-var table,
the exact suite commands, the clear skip behavior when PostgreSQL is absent, and why the
latency budget remains 1000 ms.

The backend needs PostgreSQL connection and JWT signing settings from configuration or environment variables. Keep credentials, signing files, and database connections out of the repository. Start the local API with:

    dotnet run --project services/MuscleCuties.Server

The MAUI client is iOS-only. Agents do not build, run, or screenshot it unless the user authorizes that check for the task; see [docs/VERIFICATION_GATES.md](docs/VERIFICATION_GATES.md). TestFlight archive, signing, and export steps are in [docs/beta-readiness/TESTFLIGHT_BUILD_2026-11-11.md](docs/beta-readiness/TESTFLIGHT_BUILD_2026-11-11.md).

## CI/CD and Automation

All automated verification, testing, and deployment pipelines are executed via GitHub Actions:
- `.github/workflows/ci.yml`: Full solution build, unit tests, and code coverage checks.
- `.github/workflows/db-verify.yml`: PostgreSQL integration and schema migration verification against isolated disposable databases.
- `.github/workflows/backend-build.yml`: Immutable packaging and validation for ASP.NET Core Lambda API and background workers.
- `.github/workflows/backend-release.yml`: OIDC-governed promotion to Staging and Beta environments.
- `.github/workflows/db-release.yml`: Advisory session-locked database schema migrations.
- `.github/workflows/mobile-release.yml`: Environment-specific iOS builds and TestFlight packaging.

GitLab CI is retired; GitHub is the sole source of truth and deployment authority.

## Documentation

### Canonical instructions

- [AGENTS.md](AGENTS.md) — compact operating directive and links to on-demand references.
- [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) — project and dependency boundaries, API and persistence authority.
- [docs/PRODUCT_WORKFLOWS.md](docs/PRODUCT_WORKFLOWS.md) — the six approved workout-planning rules.
- [docs/SAFETY.md](docs/SAFETY.md) — health, privacy, security, and fail-safe constraints.
- [docs/VERIFICATION_GATES.md](docs/VERIFICATION_GATES.md) — risk-based G0–G9 gates and evidence states.
- [DESIGN.md](DESIGN.md) and [docs/UI_PRESENTATION_DESIGN_SPEC.md](docs/UI_PRESENTATION_DESIGN_SPEC.md) — design entry point and app-wide presentation contract.

### Current state and detailed references

- [ROADMAP.md](ROADMAP.md) — short delivery sequence.
- [GitHub transition and incremental refactoring proposal](docs/architecture/github-transition/07-executive-summary.md) — 2026-11-04 audit, two-orchestrator ownership, governance changes and gated implementation packages; planning only.
- [STATUS.md](STATUS.md) — current checkpoint and gate status.
- [AUDIT.md](AUDIT.md) — owner actions and broader release evidence.
- [docs/design/](docs/design/) — references, design decisions, implementation evidence, and current UI gate register.
- [docs/infra/](docs/infra/) — database/release investigations, approval records, compatibility and backup/restore runbooks.
- [docs/performance/README.md](docs/performance/README.md) — reproducible server-latency measurement procedure and reports.

Canonical rule owners are linked above. Dated reports preserve evidence; verify their source date and current checkout before treating them as live instructions.

## Shared development files

Development instructions, design/product documents, repository skills, review reports and shared run configurations are versioned by default. `.gitignore` excludes credentials/signing keys, imported conversations, per-machine IDE/agent state and generated build/test output. Keep environment examples free of real credentials.

The worker requires the public RDS CA bundle at `certs/global-bundle.pem`; it is versioned while other local certificate material remains excluded. `.codex/hooks.example.json` is a portable example of the optional installed Impeccable hooks; copy it to the ignored `.codex/hooks.json` only for local use. The hook example contains no required build or release dependency.

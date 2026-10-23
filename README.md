# MuscleCuties

MuscleCuties is a .NET MAUI fitness and wellness app. The current development focus is the iOS client, with a first-party ASP.NET Core API and PostgreSQL persistence.

## Repository layout

| Path | Purpose |
| --- | --- |
| `src/MuscleCuties.UI` | MAUI mobile app |
| `src/MuscleCuties.Core` | Domain rules and state |
| `src/MuscleCuties.UseCases` | Application workflows |
| `src/MuscleCuties.Infrastructure*` | Storage and service adapters |
| `services/MuscleCuties.Server` | API and AWS Lambda host |
| `tests` | Unit, contract, and PostgreSQL integration tests |

## Build and test

The iOS app requires the .NET 10 SDK, the MAUI iOS workload, and Xcode. From the repository root:

```sh
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
```

The PostgreSQL integration suite requires a disposable local database configured as described in its test fixtures. Keep credentials and personal data out of Git.

## Current status

This is an active beta codebase. The expanded iOS candidate still needs signed-device and live-backend acceptance before TestFlight or App Store submission. See the [architecture overview](docs/ARCHITECTURE.md) for how the app and backend are organized.

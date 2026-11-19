# MuscleCuties — Project Index

## Project overview
MuscleCuties is a women's fitness, nutrition, and cycle-tracking application written entirely in C#/.NET 10 (`global.json`). The client is an iOS-only .NET MAUI app, and the backend is an ASP.NET Core Minimal API deployed to AWS Lambda backed by PostgreSQL, paired with a scheduled workout reconciliation worker.

## Repository structure

| Component | Path | Responsibility |
| --- | --- | --- |
| Mobile UI | `src/MuscleCuties.UI` | .NET MAUI iOS pages, XAML controls, ViewModels, EN/RU localization, Apple Sign-In UI |
| Mobile infrastructure | `src/MuscleCuties.Infrastructure` | API HTTP client, encrypted offline cache, notifications, auth services |
| Core domain | `src/MuscleCuties.Core` | Domain entities, deterministic rules, invariants (no outer dependencies) |
| Use cases | `src/MuscleCuties.UseCases` | Application orchestration, feature handlers, contracts (depends only on Core) |
| Contracts | `src/MuscleCuties.Contracts` | Shared client/server DTOs |
| PostgreSQL persistence | `src/MuscleCuties.Infrastructure.PostgreSql` | EF Core `PostgresDbContext`, repositories, schema migrations, catalog seeding |
| DB maintenance CLI | `src/MuscleCuties.DbMaintenance` | `migrate`, `seed`, and `dbcheck` commands |
| Backend API | `services/MuscleCuties.Server` | ASP.NET Core Lambda host, auth, HTTP endpoint mappings |
| Background worker | `services/MuscleCuties.WorkoutReconciliation` | Scheduled worker for pending workout revisions |
| Infrastructure & deploy | `infra/`, `tools/deployment/` | CloudFormation templates, Lambda packaging/validation/deploy scripts |
| Tests | `tests/` | xUnit suites (`Core`, `Server`, `Registration`, `Mobile.Contracts`, `Deployment`, `Registration.Integration`) and `maestro/` |
| CI/CD | `.github/workflows/` | `ci.yml`, `db-verify.yml`, `backend-build.yml`, `backend-release.yml`, `db-release.yml`, `mobile-release.yml` |
| Developer scripts | `scripts/` | Local API/Postgres setup, iOS simulator, TestFlight IPA verification, `ai-status.sh` |

## Key code entry points
- **App composition:** `src/MuscleCuties.UI/MauiProgram.cs`, `src/MuscleCuties.UI/AppShell.xaml.cs`, `services/MuscleCuties.Server/Program.cs`, `services/MuscleCuties.WorkoutReconciliation/Program.cs`.
- **Workout generation (Engine 2.0):** `src/MuscleCuties.UseCases/Features/Workout/Planning/WorkoutGenerationV2Service.cs`, `services/MuscleCuties.Server/Workout/Planning/WorkoutPlanningEndpointMappings.cs`, `src/MuscleCuties.UI/Pages/Workout/StrengthPlanGenerationPage.xaml`, `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs`.
- **Workout logging & active sessions:** `src/MuscleCuties.UseCases/Features/Workout/ActiveSession/WorkoutActiveSessionUseCase.cs`, `services/MuscleCuties.Server/Workout/ActiveSession/WorkoutActiveSessionEndpointMappings.cs`, `services/MuscleCuties.Server/Workout/Session/WorkoutPlanAndSessionEndpointMappings.cs`, `src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml`, `src/MuscleCuties.UI/Pages/Workout/WorkoutHistoryPage.xaml`.
- **Nutrition:** `services/MuscleCuties.Server/Nutrition/NutritionDiaryEndpointMappings.cs`, `FoodCatalogEndpointMappings.cs`, `NutritionBatchAndCopyEndpointMappings.cs`, `src/MuscleCuties.UseCases/Features/Nutrition/`.
- **Authentication & onboarding:** `src/MuscleCuties.Infrastructure/Services/Auth/CloudAuthService.cs`, `src/MuscleCuties.UI/Services/Auth/AppleSignInService.cs`, `src/MuscleCuties.UseCases/Features/Account/`.
- **Persistence & migrations:** `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresDbContext.cs`, `PostgresSchemaMigrations.cs`, `PostgresCanonicalCatalogSeeder.cs`, `src/MuscleCuties.DbMaintenance/Program.cs`.

## Navigation guidance
- **UI/XAML changes:** Inspect `DESIGN.md`, target `src/MuscleCuties.UI/Pages/` and `ViewModels/`, and check `CONTEXT.md` for EN/RU copy rules.
- **API or contract changes:** Start at `services/MuscleCuties.Server/<Domain>/*EndpointMappings.cs`, trace into `src/MuscleCuties.UseCases/Features/`, and run `MuscleCuties.Server.Tests` and `MuscleCuties.Mobile.Contracts.Tests`.
- **Domain/workout rules:** Start in `src/MuscleCuties.Core` and `src/MuscleCuties.UseCases/Features/Workout/`, and read `docs/PRODUCT_WORKFLOWS.md` / `docs/SAFETY.md` only when rules are affected.
- **Database/schema changes:** Start in `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/` and `src/MuscleCuties.DbMaintenance/`.

## Source-of-truth note
This index is a navigation aid only, not proof of runtime behavior. Verify exact implementations directly in the current working tree.

# MuscleCuties — Current State

## Verified repository
- **GitHub:** `denis-petruk/MuscleCuties` (`git@github.com:denis-petruk/MuscleCuties.git`)
- **Local path:** `/Users/denispetruk/MuscleCuties`
- **Default branch:** `main` (local `origin/main` ref at `f9aa2ee9`; not live-fetched during offline cleanup)
- **Active branch:** `chore/lean-ai-workspace` (branched from `fix/qa-audit-followups` @ `a8109cbd`)
- **Verified at:** 2026-11-19
- **Environment & worktree state:** Single checkout (no linked worktrees); local environment variables consolidated in root `.env` (gitignored) with clean `.env.example` template.

## Current solution
- **Mobile:** .NET 10 MAUI iOS client (`src/MuscleCuties.UI`, `src/MuscleCuties.Infrastructure`), iOS-only.
- **Backend:** ASP.NET Core Minimal API on AWS Lambda (`services/MuscleCuties.Server`) and scheduled worker (`services/MuscleCuties.WorkoutReconciliation`).
- **Database:** PostgreSQL via EF Core (`src/MuscleCuties.Infrastructure.PostgreSql`) and `src/MuscleCuties.DbMaintenance` (`migrate`/`seed`/`dbcheck`).
- **Infrastructure:** CloudFormation templates in `infra/cloudformation/` and immutable digest-verified Lambda deploy scripts in `tools/deployment/`.
- **CI/CD:** GitHub Actions in `.github/workflows/` (`ci.yml`, `db-verify.yml`, `backend-build.yml`, `backend-release.yml`, `db-release.yml`, `mobile-release.yml`).

## Current development status
- **Verified completed work:** Workout Engine 2.0 (Packages A–K), Sign in with Apple onboarding enforcement (Beta v21), USDA FoodData Central search + server-side meal generation, HealthKit multi-metric sync, dead code/doc cleanup (`a8109cbd`), lean AI workspace setup (`6b20118f`), retired obsolete doc tests + merged `russian-session-input` localization updates (`2f3bf70a`).
- **Known current blockers / notes:**
  - Remaining product verification items are tracked in `ROADMAP.md` (iOS simulator passes and Gate 95 controlled environment reset).
  - Run `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release` locally outside a restricted sandbox to confirm the merged localization and retired-test commit.

## Verification evidence
- **Latest confirmed relevant build & tests (per `STATUS.md` at `a8109cbd`):** `Core.Tests` (1760 passed), `Mobile.Contracts.Tests` (248 passed), `Registration.Tests` (87 passed), `Server.Tests` (128 passed), `Registration.Integration.Tests` filtered Apple/Registration subset (10 passed), `WorkoutReconciliation` Release build (0 errors), `MuscleCuties.UI` iOS simulator Debug build (0 errors).
- **Areas not independently verified during this workspace cleanup:** `dotnet test` after `2f3bf70a` (blocked by MSBuild socket permissions in sandbox), live GitHub network fetch/push (offline), and iOS simulator walkthroughs.

## References
- **Architecture index:** `docs/ai/PROJECT_INDEX.md`
- **Relevant current documentation:** `AGENTS.md`, `README.md`, `STATUS.md`, `ROADMAP.md`, `DESIGN.md`, `CONTEXT.md`, `docs/PRODUCT_WORKFLOWS.md`, `docs/SAFETY.md`

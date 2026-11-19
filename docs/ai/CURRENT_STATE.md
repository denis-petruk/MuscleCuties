# MuscleCuties — Current State

## Verified repository
- **GitHub:** `denis-petruk/MuscleCuties` (`git@github.com:denis-petruk/MuscleCuties.git`)
- **Local path:** `/Users/denispetruk/RiderProjects/MuscleCuties`
- **Default branch:** `main` (local `origin/main` ref at `f9aa2ee9`; not live-fetched during offline cleanup)
- **Active branch at baseline:** `fix/qa-audit-followups` @ `a8109cbdbf7a40ef13dc9b6ffb4036b6fa035d02` (branched to `chore/lean-ai-workspace` for workspace optimization)
- **Verified at:** 2026-11-19
- **Working-tree state at baseline:** 2 modified tracked files (`.aiignore`, `.env.example`), 2 uncommitted tracked deletions (`docs/release-audit/2026-11-05/WORKOUT_POLICY.md`, `docs/workout-engine-v2/catalog/CANDIDATE_METADATA_V021.json`), and 1 untracked project Codex config (`.codex/config.toml`).

## Current solution
- **Mobile:** .NET 10 MAUI iOS client (`src/MuscleCuties.UI`, `src/MuscleCuties.Infrastructure`), iOS-only.
- **Backend:** ASP.NET Core Minimal API on AWS Lambda (`services/MuscleCuties.Server`) and scheduled worker (`services/MuscleCuties.WorkoutReconciliation`).
- **Database:** PostgreSQL via EF Core (`src/MuscleCuties.Infrastructure.PostgreSql`) and `src/MuscleCuties.DbMaintenance` (`migrate`/`seed`/`dbcheck`).
- **Infrastructure:** CloudFormation templates in `infra/cloudformation/` and immutable digest-verified Lambda deploy scripts in `tools/deployment/`.
- **CI/CD:** GitHub Actions in `.github/workflows/` (`ci.yml`, `db-verify.yml`, `backend-build.yml`, `backend-release.yml`, `db-release.yml`, `mobile-release.yml`).

## Current development status
- **Verified completed work:** Workout Engine 2.0 (Packages A–K), Sign in with Apple onboarding enforcement (Beta v21), USDA FoodData Central search + server-side meal generation, HealthKit multi-metric sync, and dead code/doc cleanup (`a8109cbd`).
- **Current active changes:** Workspace AI-context reset and Codex lean configuration on `chore/lean-ai-workspace`.
- **Known current blockers / notes:**
  - Uncommitted local `.env.example` contains a populated `FDC_API_KEY` value and must not be committed.
  - Two test-asserted files (`WORKOUT_POLICY.md`, `CANDIDATE_METADATA_V021.json`) are currently deleted in the uncommitted working tree and should be restored before running full test suites unless intentionally retired with updated tests.
  - Remaining product verification items are tracked in `ROADMAP.md` (iOS simulator passes and Gate 95 controlled environment reset).

## Verification evidence
- **Latest confirmed relevant build & tests (per `STATUS.md` at `a8109cbd`, not re-run during workspace cleanup):** `Core.Tests` (1760 passed), `Mobile.Contracts.Tests` (248 passed), `Registration.Tests` (87 passed), `Server.Tests` (128 passed), `Registration.Integration.Tests` filtered Apple/Registration subset (10 passed), `WorkoutReconciliation` Release build (0 errors), `MuscleCuties.UI` iOS simulator Debug build (0 errors).
- **Areas not independently verified during this workspace cleanup:** Application builds/tests (no app source changed), live GitHub network fetch/push (declined during session), and iOS simulator walkthroughs.

## References
- **Architecture index:** `docs/ai/PROJECT_INDEX.md`
- **Relevant current documentation:** `AGENTS.md`, `README.md`, `STATUS.md`, `ROADMAP.md`, `DESIGN.md`, `CONTEXT.md`, `docs/PRODUCT_WORKFLOWS.md`, `docs/SAFETY.md`

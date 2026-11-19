# MuscleCuties — Agent Instructions

## Source of truth
Use the current working tree for implementation facts and GitHub `denis-petruk/MuscleCuties` for published state. Do not treat historical chats, old checkpoints, or superseded plans as current requirements. Work on a branch and integrate via PR into protected `main`; never push directly to `main`, force-push, or rewrite remote history. Keep commits and PRs free of AI tool or co-author mentions.

## Default execution & scope
Use one agent. Do not spawn subagents or run full-repository audits unless explicitly requested. Complete only the requested change and direct dependencies; do not refactor unrelated code or reopen completed product work. Before editing, run `./scripts/ai-status.sh` (or `git status --short --branch`) and preserve unrelated local changes without resetting, stashing, or committing them. Keep git worktrees outside the repo (e.g., `../MuscleCuties-worktrees/`).

## Context & efficiency
Use `docs/ai/PROJECT_INDEX.md` to locate components and `docs/ai/CURRENT_STATE.md` / `STATUS.md` for current state. Open `DESIGN.md`, `docs/PRODUCT_WORKFLOWS.md`, `CONTEXT.md`, or `README.md` only when relevant. Scope searches to `src/`, `tests/`, and `services/`. Skip `bin/`, `obj/`, `artifacts/`, `UiTextCatalog.RuntimeText.cs`, logs, and images unless explicitly required. Prefer targeted searches, bounded output, and the least expensive capable model; avoid unnecessary web research or specialist MCP tools.

## Architecture & product safety
- Dependencies point inward: `Core` has no outer dependencies; `UseCases` depends only on `Core`; `Infrastructure`/`Server` implement ports; UI ViewModels never touch `DbContext` or persistence models.
- iOS-only .NET MAUI client; C#/.NET is the sole production runtime. Preserve HTTP/JSON DTOs, routes, XAML bindings (search all XAML before renaming/deleting), EN/RU localization, shared UI styles (`ButtonPrimary`/`CardPrimary`), and encrypted SecureStorage offline storage (no SQLite/Preferences for sensitive data).
- Platform files (`.github/workflows/**`, `infra/**`, `tools/deployment/**`, `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/**`, `src/MuscleCuties.DbMaintenance/**`) are edited only when the task explicitly owns them.
- Workout safety: require all workout-influencing inputs before generation (report all gaps together with an editable setup action); missing/stale values never relax restrictions or increase load; keep the single plan-acceptance path (`Apply plan` for active plan replacement); withhold unsafe strength sessions with a review reason; keep safety thresholds in named constants.
- Privacy & secrets: keep Apple Health sync within approved scope (steps, sleep stages, active kcal, resting HR, HRV); never send health/cycle/injury/personal data or secrets to external services, logs, fixtures, or docs; never read, print, or rotate secrets; never run `dbinit` or `EnsureCreated` against shared databases.

## Verification, deploy & completion
- Automated: builds, targeted tests, analyzers, iOS simulator runs, branch pushes, PRs, and green PR merges. Local AWS Lambda deploy requires green tests, a pre-migration DB snapshot, and an immutable digest behind an alias (never `$LATEST`). TestFlight and App Store Connect remain owner-only.
- Write/run failing tests first for new behavior, run affected suites without weakening or skipping tests, and cite pasted command output as evidence.
- Stop once verified. Report changed files, checks performed, unverified areas, and blockers concisely (updating `STATUS.md` when completing a development task; no dated handoff files).

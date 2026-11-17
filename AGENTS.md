# MuscleCuties — agent operating contract

Read this file and the task card. Open other docs only when the card names them: `DESIGN.md` for UI work, `docs/PRODUCT_WORKFLOWS.md` for workout or nutrition behaviour, `CONTEXT.md` for localization terms, `README.md` for build and test commands. Scope `rg`/`find` searches to `src/`, `tests/`, and `services/` by default. Do not read `bin/`, `obj/`, `artifacts/`, `UiTextCatalog.RuntimeText.cs`, logs, or images unless the task explicitly requires it. Keep git worktrees outside the repository (e.g., `../MuscleCuties-worktrees/`).

## 1. Source of truth

- GitHub `denis-petruk/MuscleCuties` is the only repository. Work on a branch and integrate through a PR into protected `main`; never push directly to `main`, force-push or rewrite remote history.
- Before editing: run `git status --short --branch`, record HEAD and the files the task owns. Preserve unrelated changes; never reset, stash or commit them.
- Keep the repository, commits and PRs free of any mention of AI tools or AI co-authors.

## 2. Boundaries

- Dependencies point inward: `MuscleCuties.Core` has no ORM, ASP.NET, MAUI or transport dependencies; `UseCases` depends only on Core; Infrastructure and Server implement ports; UI ViewModels never touch DbContext, repositories or persistence models.
- iOS only, no Android target. C#/.NET is the only production runtime.
- Preserve HTTP/JSON DTOs, routes, XAML bindings, navigation, EN/RU localization and persistence semantics. Search all XAML bindings before renaming or deleting a bound member.
- Sensitive offline data lives in encrypted JSON with keys in SecureStorage; no SQLite, no Preferences for sensitive records.
- UI uses the shared page system, `ButtonPrimary`/`ButtonSecondary`, `CardPrimary`/`CardSecondary` and role-based colours. No competing style systems.
- Platform files (`.github/workflows/**`, `infra/**`, `tools/deployment/**`, `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/**`, `src/MuscleCuties.DbMaintenance/**`) are edited only by a task that explicitly owns them.

## 3. Product safety

- Every field that influences a workout must be filled before generation runs. If anything is missing, the page shows what is missing together with an action that opens the editable workout setup; a status message alone is not acceptable. All gaps are reported together, not one per retry.
- A missing or stale value never relaxes a restriction and never increases load or intensity.
- One plan-acceptance path: a feasible first plan generated in the foreground applies automatically; an active plan is replaced only through **Apply plan** after the Generate/Regenerate control has stated the replacement consequence. No local generator, cached preview or failed API call substitutes for this path.
- A session with no permitted strength block is withheld with a review reason, never replaced by unsafe work.
- Apple Health sync stays within the owner-approved scope (steps, sleep stages, active kcal, resting HR, HRV); never widen it. No health records, cycle data, injuries, measurements, photos, identifiers or secrets go to external AI or third-party services, logs, fixtures or docs. Tests use synthetic fixtures.
- Safety thresholds and multipliers live in named constants, not scattered literals.

## 4. Automation and owner actions

- Automated, no approval needed: builds, tests, analyzers, MAUI simulator build/run/screenshots, branch pushes, PRs, merging a PR whose checks are green.
- Deploy to AWS Lambda runs from the owner's local credentials without a separate request, on two conditions enforced by the script: all tests are green, and a database snapshot is taken before any migration. Deploy the exact built package digest to a published version behind an alias; never serve `$LATEST`.
- Owner only: TestFlight upload and App Store Connect actions.
- Agents never read, print, log or rotate secret values. Never run `dbinit` or `EnsureCreated` against a shared database.

## 5. Verification: test first

1. Write the test before the feature. Run it: it must fail, and fail for the reason the feature is missing, not because of a typo or a broken build.
2. Implement until the test is green. Run the affected suite, not only the new test.
3. Never weaken, delete, skip or `[Ignore]` an existing test, and never change an assertion to make a candidate pass. A test fixed to accept new behaviour must be justified in the PR.
4. Evidence is pasted command output. A dated report, a previous run or a confident description is not evidence. A check that could not run is reported as not run, never as passed.

## 6. Report

At the end of every task, overwrite `STATUS.md` with four parts: changed files; test commands with their pasted output; what was not verified and why; next step or blocker. No new dated handoff files. Never restate an old result as current.

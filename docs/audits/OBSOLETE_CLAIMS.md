# Obsolete claims — branch-specific audit statements that do not hold here

Status: read-only reconciliation. This file exists because two audit documents in `docs/` describe a
**different checkout** and remain in the tree as if current. Using their deletion lists against `main`
would delete live code.

Sources:

- [`docs/RELEASE_INVENTORY_20261030.md`](../RELEASE_INVENTORY_20261030.md) — snapshot
  `codex/backend-hardening-20261030` @ `467cbbda`, 2026-10-30 ~16:35.
- [`docs/audits/BACKEND_DB_CORE_WIRING_AUDIT_20261030.md`](BACKEND_DB_CORE_WIRING_AUDIT_20261030.md) —
  same branch, graph snapshot of 437 code files.

Current checkout: `main` @ `97677ca4`. The branch names, commit ids and file counts do not match, so
neither document is authoritative for this tree.

---

## 1. Claims that are false in this checkout

| # | Claim | Source | This checkout | Evidence |
| --- | --- | --- | --- | --- |
| 1 | The repository contains `services/MuscleCuties.WorkoutReconciliation`, a separate reconciliation executable | Inventory §2, §6.1 | **Absent.** `services/` contains only `MuscleCuties.Server`, `postgresql`, `static` | `ls services` |
| 2 | `WeeklyWorkoutPlanGeneratorRule` exists and is only covered by its test | Inventory §6.1, §6.3 | **Absent.** Zero occurrences of the name | `grep -rn WeeklyWorkoutPlanGeneratorRule src services tests` |
| 3 | `DatabaseSchemaCompatibility.cs` is a 1,899-line god node; `SchemaRequirements.json` pins 49 policies | Backend audit §2.2, §4 | **Both files absent.** There is no `*SchemaCompatibility*` file and no `SchemaRequirements.json` anywhere in the tree | `find . -name '*SchemaCompatibility*'`; `find . -name SchemaRequirements.json` |
| 4 | 61 migrations, 69 `DbSet`s, 69 EF configurations in 10 files | Backend audit §2.1 | **19 migration classes** (`[Migration(`), 29 files in `Persistence/Migrations`, **58 `DbSet`s**, 5 files declaring `IEntityTypeConfiguration<>` | `grep -rl '\[Migration(' Persistence/Migrations \| wc -l`; `ls Persistence/Migrations \| wc -l`; `grep -c 'DbSet<' Persistence/PostgresDbContext.cs` |
| 5 | 143 files in outer projects declare `MuscleCuties.Core.*` / `MuscleCuties.UseCases.*` namespaces | Inventory §5.1 | **215 source files** (excluding `bin`/`obj`) | `grep -rlE --include='*.cs' '^namespace MuscleCuties\.(Core\|UseCases)\.'` over Infrastructure, PostgreSql, UI, DbMaintenance, Server |
| 6 | `ApiApplication.cs` is 949 lines; `MauiProgram.cs` is 875; `CloudAccountAndProfileStores.cs` is 1,131 | Inventory §2 | **863 / 799 / 994** | `wc -l` on each |
| 7 | `PostgresWorkoutPlanAndSessionStore.cs` (1,643 lines) and `StrengthPlanGenerationViewModel.cs` (1,630 lines) are the largest files | Inventory §2 | **Both absent** | `test -f` on each path |
| 8 | `IWorkoutPlanner` / `WorkoutPlanner` have zero consumers | Inventory §6.1 | **Consumer in production code:** `WorkoutService` injects `IWorkoutPlanner` (`WorkoutService.cs:25,36`); registered at `MauiProgram.cs:315` | `grep -rn -w IWorkoutPlanner src` |
| 9 | `IGatingEngine` / `GatingEngine` have zero consumers | Inventory §6.1 | **Consumer in production code:** `WorkoutPlanGenerator` injects it at lines 51, 63, 76 | `grep -rn -w IGatingEngine src` |
| 10 | `ICyclePhaseCalculator` / `CyclePhaseCalculator` have zero consumers | Inventory §6.1 | **Consumers in production code:** `CyclePredictionPlanner` and `CyclePredictionRule` take it | `grep -rn -w ICyclePhaseCalculator src` |
| 11 | `IDashboardPlanner` / `DashboardPlanner` have zero consumers | Inventory §6.1 | **Two consumers:** `LocalDashboardOverviewStore.cs:199` and `ReadinessSyncOrchestrator.cs:16,29` | `grep -rn -w IDashboardPlanner src` |
| 12 | `IExerciseMediaStore` / `LocalExerciseMediaStore` have zero consumers | Inventory §6.1 | **Consumer exists:** `LocalExerciseMediaSyncService` injects it; the sync service is the dead part, not the store | `grep -rn -w IExerciseMediaStore src` |
| 13 | The working tree had 23 modified files and a staged deletion of `WorkoutPreferencesLegacyNormalizer.cs` | Inventory §0 | **7 modified tracked files, no staged deletion**; different set entirely | `git status --porcelain` |
| 14 | Test counts are Core 989, Registration 36/42, Mobile 16/9 | Inventory §3 | **Not reproducible here and not re-run.** Core has 180 `[Fact]`/`[Theory]` declarations; Mobile has 14 `[Xunit.Fact]`/`[Xunit.Theory]`; Registration.Tests has 7 and Registration.Integration.Tests has 2 | `grep -rho '\[Fact\]\|\[Theory\]' tests/...` |
| 15 | The Lambda ZIP is the deployed artifact / CI cannot upload the release branch | Inventory §4.2, §4.5 | **No pipeline file exists in this repository at all** (no `.gitlab-ci.yml`, no `.github/workflows/`), so the claim is not testable here | `ls -a` at repo root |

## 2. Claims that remain true here

These were re-checked and are valid in this checkout, so they are safe to act on:

| Claim | Evidence |
| --- | --- |
| Two distinct `ActiveWorkoutSessionSnapshot` records exist | `UseCases/Features/Caching/ILocalCacheService.cs:15` and `UseCases/Features/Workout/ActiveSession/WorkoutActiveSessionContracts.cs:26` |
| `GetActiveWorkoutWithOfflineCacheAsync` has only its own definition | `MuscleCutiesBackendApiClient.cs:434`; no other occurrence in `src/`, `services/`, `tests/` |
| `src/MuscleCuties.App` is a ghost directory | `git ls-files src/MuscleCuties.App` returns 0 paths; only untracked `bin/` and `obj/` exist; absent from the solution |
| The SQL grant gap may still exist | Not re-verified in this audit; it is a database question, outside the dead-code scope. Do not assume either way |

## 3. Not re-verified in this audit

The following need a build, a database or a deployment and were deliberately not repeated in a
report-only pass. They must not be treated as confirmed or refuted:

- test pass counts and whether the Server test project compiles;
- iOS build and signing status;
- EF `migrations has-pending-model-changes`;
- catalog row count and SHA-256;
- fresh-PostgreSQL migration chain and schema preflight;
- Lambda ZIP contents and manifest consistency;
- CI/CD behaviour on any branch.

## 4. Consequence

1. Treat both documents as **historical branch reports**, not as a work queue.
2. The only deletion list valid for this checkout is
   [`dead-code-manifest.csv`](dead-code-manifest.csv), and every row there is tied to a command in
   [`DEAD_CODE_AUDIT_20261030.md`](DEAD_CODE_AUDIT_20261030.md) §8.
3. If the branch documents are kept, add a one-line banner at the top of each stating the branch and
   commit they describe. That change is documentation-only and belongs to Phase 2, not this report.

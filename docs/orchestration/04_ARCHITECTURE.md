# Worker 4 — Dead code and architecture

You are Worker 4 in the MuscleCuties release cleanup. You own dead-code removal and the physical
namespace/dependency cleanup. You work under two manifest entries because the cross-cutting cleanup
would collide with the other workers if run in parallel.

Read first:

- `docs/RELEASE_INVENTORY_20261030.md` §0, §2, §5.1, §5.2, §5.3, §6.1, §6.2, §6.3, §6.4, §12
  (Worker 4), §13, §14.
- `AGENTS.md` — the inward dependency rule, namespace ownership, zero outer imports in Core/UseCases.
- `docs/orchestration/README.md` §4 (phase plan) and §5 (checkpoint format).
- `docs/orchestration/OWNERSHIP_MANIFEST.md` — your `W4A` and `W4B` scopes.
- This file. No chat context is needed beyond these files.

## Isolation and authority

The shared checkout is dirty and another agent may be editing it. Inventory §0 records the files that
were in flux; treat every one as potentially changed and re-read it in your worktree. Do not edit the
shared checkout.

The orchestrator creates `/tmp/mc-w4` from the freeze commit for `W4A`. Enter it:

```bash
cd /tmp/mc-w4
git status --short
```

If you are starting without an orchestrator and the worktree does not exist, create it once:

```bash
git worktree add /tmp/mc-w4 -b release/w4a-architecture <freeze-sha>
```

Use branch `release/w4a-architecture` for `W4A`. Local commits are allowed for handoff. **Do not
push.** Do not deploy, migrate a live database, change a secret, or touch TestFlight.

### Bootstrap and toolchain

The coordinator copied `docs/orchestration/`, the inventory, and the four tool scripts into each of
`/tmp/mc-w4` and `/tmp/mc-w4b`. They are untracked, read-only bootstrap files. Only your checkpoint
under `docs/orchestration/checkpoints/` may be staged from that copied package; add your own
code/test changes normally. Do not stage the rest of the package.

For `W4A`, read `/tmp/mc-freeze/freeze.env`. `diff_outcome=no-tracked-diff` is a normal outcome: do
not apply a patch. If `diff_outcome=tracked-diff`, check it in this clean worktree and apply only
W4A hunks:

```bash
git apply --check /tmp/mc-freeze/tracked.patch
```

Use the pinned SDK explicitly; bare `dotnet` is the wrong install:

```bash
export DOTNET="$HOME/.dotnet/dotnet"
"$DOTNET" --version          # must print 10.0.403
"$DOTNET" workload list      # must include maui-ios
```

For `W4B`, wait for the orchestrator's exact message `W4B — MERGE COMPLETE` with an integration
commit. The orchestrator creates `/tmp/mc-w4b` from that commit; enter it. If you are starting
standalone and it does not exist, create it once:

```bash
git worktree add /tmp/mc-w4b -b release/w4b-architecture <integration-sha>
```

Do not start `W4B` while Workers 1–3 are still running. Do not edit their unmerged branches.

## Mission non-negotiables (same for every worker)

1. **No pushes.** Local commits on your worker branch are allowed; pushing, deploying, live
   migrations, secret changes, and TestFlight are forbidden.
2. **Database reproducible from migrations alone.** A fresh PG17 database migrated only by
   `DbMaintenance migrate` must be `compatible` with zero manual SQL. Your change must not create a
   dependency on a reconciliation manifest.
3. **Exactly one Lambda artifact.** `artifacts/MuscleCuties.Server.zip` must contain no stray DLLs,
   PDBs, development config, or metadata, and its routed functions must work. Do not add a second
   package or a packaging exception.
4. **Remove dead/duplicated code; do not bypass it.** Deleting a caller while leaving the dead
   implementation, or adding a second adapter/repair/fallback, is not a fix.
5. **Fix the cause in place and in the right layer.** No sidecar normalizer, client-side repair of a
   server bug, manual JSON guesser, test-only SQL fix, or fabricated default.
6. **Tests:** delete a test only when the behaviour it covers is genuinely gone; never skip, weaken,
   or comment out a test to get green.
7. **Clean architecture and only necessary files.** Core has no outer dependencies; UseCases only
   Core; no `Core.*`/`UseCases.*` namespace outside its project; no ghost/duplicate trees.
8. **Isolation:** use your own worktree; the shared checkout is read-only. Stop if a required file
   is outside your scope.
9. **Sequencing:** docs checkpoint cleanup and the final single-artifact decision happen after all
   workers report, not in your pass. Do not edit existing docs or `artifacts/`.

## Write scope (manifest `W4A` and `W4B`)

Your only write lists are the `W4A` and `W4B` sections of
`docs/orchestration/OWNERSHIP_MANIFEST.md`. `W4A` is parallel and limited to the listed dead files.
`W4B` is post-merge and covers the product tree after P3; it is not concurrent with any worker.

Do not write a path outside the active manifest section. If a work item below names a path outside
that section, stop and record it; the manifest wins. Migrations, `services/postgresql/`, tools, CI,
build files, packages, artifacts, and docs are outside both sections (except your checkpoint).

## Inventory items you own

- **5.1** — 143 files outside Core/UseCases declaring `MuscleCuties.Core.*`/`MuscleCuties.UseCases.*`.
- **5.2** — remaining duplicate types after Workers 2–3.
- **5.3** — oversized/multi-responsibility files only where a mechanical split is needed for the
  namespace/dead-code cleanup; no behaviour rewrite.
- **6.1** — dead rules, legacy account gateways, ghost `src/MuscleCuties.App`, remaining dead services.
- **6.2** — legacy planning subtrees that become dead after Worker 3 removes registrations.
- **6.3** — tests tied to the removed types.
- **6.4** — duplicate auth validator, nutrition notifier/adapter, `ChangePasswordBody` if still present.
- **12 (Worker 4)** and **13** — deliverables and handoff.

## W4A — safe parallel deletions

`W4A` must compile and test in the freeze/parallel tree. Delete only types that have **zero
references outside the files you are deleting**. Do not delete anything registered in
`MauiProgram.cs` or `ApiApplication.cs`; Worker 3/2 remove those registrations, and `W4B` deletes the
implementations after merge.

### A1. Core rules with no production caller

The `W4A` manifest lists the exact files. Verified at the inventory snapshot:

- `WeeklyWorkoutPlanGeneratorRule` and its test — the rule is referenced only by its own test.
- `ProgressStreakRule` — no references.
- `VolumeBudgetRule` — no references.
- `InitialWorkoutPlanRule` — no references.

Before deleting each, re-run:

```bash
grep -rn --exclude-dir=bin --exclude-dir=obj "\b<TypeName>\b" src services tests
```

If a production reference exists outside the type's own file/test, do not delete it. Record it for
`W4B`. Delete the test only with the rule; do not delete a test for a rule that still ships.

### A2. Legacy account gateways

The `W4A` manifest lists the exact files. Verified zero references outside their own files:

- `LegacyAccountRegistrationGateway`
- `LegacyRegistrationOtpGateway`
- `LegacyAccountRecoveryStore`
- `LegacyAccountSignInStore`

Re-run the reference check for each class name. If a DI registration or test exists, record it and do
not delete until `W4B` (or until you can remove the registration in a file you own; you do not own
`MauiProgram.cs` or `ApiApplication.cs`).

### A3. Ghost project directory

The ghost App path in `W4A` has no project file; it contains only `bin/`/`obj/`. Confirm:

```bash
git ls-files src/MuscleCuties.App
grep -rn "MuscleCuties.App" --exclude-dir=bin --exclude-dir=obj . | head
find src/MuscleCuties.App -type f -not -path '*/bin/*' -not -path '*/obj/*'
```

If the tracked-file list, project-reference search, and non-build file search are all empty, remove
the directory. If any tracked/source file or project reference exists, do not delete it; record it.

### A4. Build and checkpoint

After each batch, build the affected project and then the solution:

```bash
"$DOTNET" build MuscleCuties.sln -c Release
"$DOTNET" test tests/MuscleCuties.Core.Tests -c Release --no-restore
```

Record exact counts. `git diff --name-only` must contain only the W4A deletion set plus the
checkpoint. Do not start namespace normalization, do not edit `MauiProgram.cs`, and do not touch docs.

Write `docs/orchestration/checkpoints/04-architecture.md` with a `W4A` status block, the deletion
manifest (file → reason → zero-reference proof), and build/test counts. Keep `W4B: not-started`. Then
stop and report to the orchestrator.

## W4B — merge-gated architecture cleanup

Start only after `W4B — MERGE COMPLETE`. Workers 1–3 are merged and no one else is editing. You may
edit the merged tree, but only for:

- namespace ownership/moves and the consumer updates they require;
- deletion of code that is now unreferenced;
- collapse of the duplicates listed below;
- tests for the removed behaviour and the new architecture guard.

Do not change routes, database schema/migrations, package contents, CI rules, product behaviour, or
docs.

### The batch ledger: ten batches, fixed order

This pass is **ten batches, B0–B9, in exactly this order**. The order is not interchangeable: dead
code is deleted before namespaces are touched so a corpse is never renamed; model leaves are
classified before the services that consume them; the guard is added last so it asserts the finished
tree.

One batch at a time. A batch begins only when the previous batch has an `end` row whose gate passed.
Never split one file group across two batches, never skip a batch, never run two batches in
parallel.

Set the gate commands once, before B0, and reference them by name in every row:

```bash
export DOTNET="$HOME/.dotnet/dotnet"

namespace_count() {
  grep -rl --include='*.cs' --exclude-dir=bin --exclude-dir=obj \
    -E '^[[:space:]]*namespace MuscleCuties\.(Core|UseCases)' \
    src/MuscleCuties.Infrastructure src/MuscleCuties.Infrastructure.PostgreSql \
    src/MuscleCuties.UI services src/MuscleCuties.DbMaintenance | wc -l
}
build_core()         { "$DOTNET" build src/MuscleCuties.Core/MuscleCuties.Core.csproj -c Release; }
build_solution()     { "$DOTNET" build MuscleCuties.sln -c Release; }
test_core()          { "$DOTNET" test tests/MuscleCuties.Core.Tests -c Release --no-build; }
test_server()        { "$DOTNET" test tests/MuscleCuties.Server.Tests -c Release --no-build; }
test_registration()  { "$DOTNET" test tests/MuscleCuties.Registration.Tests -c Release --no-build; }
test_reg_int()       { "$DOTNET" test tests/MuscleCuties.Registration.Integration.Tests -c Release --no-build; }
test_contracts()     { "$DOTNET" test tests/MuscleCuties.Mobile.Contracts.Tests -c Release --no-build; }
```

`build_solution` includes the iOS target. If iOS signing blocks the link step, run the UI with the
`-t:Compile` equivalent for that project, label the row **compile-only**, and record the exact
signing blocker. A failing compile is never "compile-only".

The groups are **disjoint**: every one of the 143 files belongs to exactly one batch, the `resolved`
column sums to 143, and the `namespace_count after` column is a fixed number the coordinator compares
against without recomputing anything.

| # | Batch | File group (count at freeze) | Gate that must pass before the next batch | resolved | namespace_count after |
| --- | --- | --- | --- | --- | --- |
| **B0** | Baseline, no writes | none | `build_solution` 0; `test_core`, `test_server`, `test_registration` counts recorded | 0 | **143** |
| **B1** | Delete the post-W3 dead stack | three `Planning` trees 22 + `Models/UI/**` 8 + `Models/Workout/**` 2 = 32 | `build_solution`, `test_core`, `test_server` all 0 | 32 | **111** |
| **B2** | Collapse duplicates outside the namespace groups | server `ChangePasswordBody`, UI `ActiveWorkoutSessionSnapshot` — 0 of the 143 | `build_solution`, `test_core`, `test_server`, `test_registration` all 0 | 0 | **111** |
| **B3** | `Models/Entities/**` | 13 + 9 + 5 + 4 + 3 + 3 = 33 | `build_core` 0, then `build_solution` 0, then `test_core` 0 | 33 | **78** |
| **B4** | `Models/Nutrition/**` | 3 + 2 = 5 | `build_solution`, `test_core`, `test_contracts` all 0 | 5 | **73** |
| **B5** | `Services/Auth/**`, `Services/Profile/**`, `Services/Quiz/**` | 20 + 5 + 2 = 27 | `build_solution`, `test_core`, `test_server`, `test_registration` all 0 | 27 | **46** |
| **B6** | `Services/Nutrition/**`, `Services/Health/**`, `Services/ExerciseMedia/**` | 22 + 9 + 7 = 38 | `build_solution`, `test_core`, `test_server`, `test_registration` all 0 | 38 | **8** |
| **B7** | Remaining services and data: `Services/Workout/*.cs` 2, `Services/Cycle/*.cs` 1, `Services/Notifications/**` 1, `Services/*.cs` 2, `Data/**` 2 | 2 + 1 + 1 + 2 + 2 = 8 | `build_solution`, `test_core`, `test_server`, `test_registration`, `test_reg_int`, `test_contracts` all 0 | 8 | **0** |
| **B8** | Architecture guard test | 1 test file, 0 of the 143 | `build_solution` 0; guard test passes | 0 | **0** |
| **B9** | Full verification and checkpoint | checkpoint only | All six suites 0 with recorded counts | 0 | **0** |

32 + 33 + 5 + 27 + 38 + 8 = **143**, and 143 − 32 = 111, −33 = 78, −5 = 73, −27 = 46, −38 = 8,
−8 = 0. The groups are disjoint because B1 takes the three `Planning` trees and B7 takes only what is
left in `Services/Workout`, `Services/Cycle`, and `Services/Dashboard` after them; entity files belong
to B3, never to B1. B2 resolves nothing for this count on purpose: both duplicates it collapses sit
inside B5's and B6's groups and are removed when those groups are resolved, so counting them in B2
would double-subtract them.

**The coordinator's rule is one subtraction:** after each batch, `namespace_count` must equal
`143 − Σ resolved`, where `resolved` is the sum of the completed rows' `resolved` column. If the
worker defers a file, it subtracts that file from the row's `resolved` and names it in the row; the
target for that row becomes the lower number and every later target drops by one until a later batch
resolves it. A count above the target, or a count that does not move at a batch that resolves files,
is a stall — not drift.

With the numbers fixed, the coordinator never has to recompute: 111, 111, 78, 73, 46, 8, 0, 0, 0.

### Batch rules (every row)

1. **Row before work.** Append `B<n> | started | group=<n> | target_remaining=<n>` to the ledger in
   the checkpoint before the first edit of the batch.
2. **Restore point.** Before B1 and before every later batch, commit the last green state. Stage only
   the files this pass owns; never stage the bootstrap package (`docs/orchestration/**` copies, the
   inventory, the tool scripts) and never stage `artifacts/`. The commit is local; pushing is
   forbidden.
3. **Classify, do not guess.** For each file in a namespace group: a true Core type with no outer
   dependency moves into `src/MuscleCuties.Core` under the matching folder and keeps its namespace; an
   outer type with a false Core namespace is renamed to the truthful `MuscleCuties.Infrastructure.*`
   namespace and every consumer is updated in the same batch; a dead type is deleted with its tests;
   a live type whose move needs a behaviour change is a stop condition, not a rename.
4. **Recount after the gate.** Run `namespace_count`, record it, and compare it with the row's fixed
   `namespace_count after` target plus any deferral the row names.
5. **Gate or restore.** A row is `pass` only when every gate command for that batch exits 0 and the
   count matches. Three failed attempts at one batch: restore to the restore point with
   `git reset --hard <last-green>` **in this worktree only**, then apply the pass-level stop condition.
6. **No half-applied batch.** Either the whole group is classified and the gate passes, or the tree
   returns to the restore point and the row says `blocked`. Never leave a batch's files renamed but
   uncompilable for the next batch to discover.

### B0 — baseline

No writes. Run `build_solution`, `test_core`, `test_server`, `test_registration`, `namespace_count`,
and `git status --short; git rev-parse HEAD`. Record the counts and the 143 for later arithmetic.

If `build_solution` fails here, stop before touching anything: `W4B: blocked`, `blocked_at_batch: B0`,
with the compiler output. Do not "fix the build on the way to the cleanup".

### B1 — delete the post-W3 dead stack

Group (32 files, all outside B3 and B7):

- `src/MuscleCuties.Infrastructure/Services/Workout/Planning/**` — 11 files
- `src/MuscleCuties.Infrastructure/Services/Cycle/Planning/**` — 7
- `src/MuscleCuties.Infrastructure/Services/Dashboard/Planning/**` — 4
- `src/MuscleCuties.Infrastructure/Models/UI/**` — 8, only after `grep` over every `.xaml` and `.cs`
  proves no binding or reference to the Infrastructure copy (`src/MuscleCuties.UI/Models/UI/**` is the
  XAML contract and stays)
- `src/MuscleCuties.Infrastructure/Models/Workout/**` — 2
- tests whose only subject is a deleted type

Entity files are **not** in this batch: `Models/Entities/**` belongs to B3, so a dead entity is
deleted there and B1's 32 stays exact.

Before deleting each type:

```bash
grep -rn --exclude-dir=bin --exclude-dir=obj "\b<TypeName>\b" src services tests
```

A production reference outside the deleted set means keep the file and add
`deferred: <file> — <consumer>` to the row. Do not delete anything registered in `MauiProgram.cs` or
`ApiApplication.cs`; Workers 2–3 removed those registrations, so a surviving registration is a
finding, not a reason to keep the type. Keep the live nutrition chain (`NutritionPlanner` and its
models) and `UserProfile` if the Cloud planning profile still uses it.

### B2 — collapse the duplicates outside the namespace groups

This batch resolves 0 of the 143 by design; the namespace count must be **111** after it, not lower.

- **`ChangePasswordBody`**: if a second definition still exists in the server, keep the UseCases
  contract and delete the server copy. Do not create a third DTO.
- **`ActiveWorkoutSessionSnapshot`**: Worker 3 should have left one. If two incompatible shapes remain,
  stop the batch, restore, and report `blocked`; do not choose a shape silently.

The two duplicates that live inside the namespace groups are collapsed by the batch that owns those
groups: `AuthInputValidator` in **B5**, `INutritionChangeNotifier`/`NutritionChangeNotifierAdapter` in
**B6**. Do not delete them here — that would break the 111 target.

### B3 — `Models/Entities/**` (33)

`Core.Models.Entities.Workout` (9), `.Nutrition` (9), `.Users` (5), `.Workout.Planning` (4), `.Quiz`
(3), `.Cycle` (3). Most are pure models: move the file into `src/MuscleCuties.Core/Models/Entities/`
with the namespace unchanged. `Users/UserProfile.cs` moves as a file only; the live
`CloudProfileNutritionSettingsStore` consumer keeps its behaviour. A model that needs an EF, MAUI, or
serialization attribute to move is renamed in place instead and recorded as such.

Entity files with no live consumer are deleted here, with their tests; that deletion is part of this
batch's 33, never B1's 32.

`build_core` runs first and alone because it proves Core gained no outer dependency.

### B4 — `Models/Nutrition/**` (5)

`Core.Models.Nutrition.Planning` (3), `.Inputs` (2). Same classification. `test_contracts` is in the
gate because these shapes reach the client contracts.

### B5 — `Services/Auth/**` (20), `Services/Profile/**` (5), `Services/Quiz/**` (2)

These are mostly Infrastructure implementations behind HTTP/MAUI stores: rename the namespace to
`MuscleCuties.Infrastructure.*`, update consumers, and leave the public member names alone. Pure rules
inside the group (`OnboardingValidator`, `QuizSelectionEvaluator`) may move to Core instead if they
add no outer dependency; `build_core` is not the gate for this batch, so prove it by the file's own
`using` list and the solution build.

This batch also deletes the Infrastructure `Core.Services.Auth.AuthInputValidator` facade and points
consumers at `Core.Domain.Auth.AuthInputValidator`: one of the 20 files is a deletion, so the group
still resolves 20 and the target stays 46.

### B6 — `Services/Nutrition/**` (22), `Services/Health/**` (9), `Services/ExerciseMedia/**` (7)

Includes `Services/Nutrition/Planning/**` (11) and the 11 files beside it. `NutritionPlanner` and its
models are live and stay; `DisabledHealthSyncService` stays dormant and must not gain a live adapter.
Rename the FDC/HTTP/store types to Infrastructure namespaces; move genuinely pure calculators only if
they carry no outer import.

This batch also collapses `INutritionChangeNotifier` + `NutritionChangedEventArgs` to the single
UseCases contract, deletes `NutritionChangeNotifierAdapter`, and updates DI: the deleted duplicate is
one of the 22, so the group still resolves 38 and the target stays 8.

### B7 — the remaining service and data groups (8)

Exactly what is left after B1: `Services/Workout/*.cs` (2), `Services/Cycle/*.cs` (1),
`Services/Notifications/**` (1), `Services/*.cs` (2), `Data/**` (2). `Services/Dashboard/**` is empty
here because all four of its files were in `Services/Dashboard/Planning/**` and B1 deleted them. The
target after this batch is **0**; this batch's gate is the full suite set because it touches the last
consumers.

### B8 — architecture guard

Add one test (`tests/MuscleCuties.Core.Tests/NamespaceOwnershipTests.cs`, or extend
`CleanArchitectureBoundaryTests.cs` in the established style) that scans the source tree at test time
and fails when:

- a file under `src/MuscleCuties.Infrastructure*`, `src/MuscleCuties.UI`, `services/`, or
  `src/MuscleCuties.DbMaintenance` declares `namespace MuscleCuties.Core` or
  `namespace MuscleCuties.UseCases`;
- `MuscleCuties.Core` declares `namespace MuscleCuties.Infrastructure`/`UseCases`/`UI`;
- a ViewModel injects `DbContext` or `IRepository`;
- a second `src/MuscleCuties.UseCases/Features` root exists.

Use the repository's existing path helper; no absolute machine path, no baseline allowlist, no
`Skip`. The guard must pass on the cleaned tree.

### B9 — full verification and checkpoint

```bash
build_solution
test_core
test_server
test_registration
test_reg_int
test_contracts
namespace_count          # must print 0
```

Append the separate `W4B` status block after the `W4A` block (never overwrite it) with: the completed
ledger table, the final zero count, the guard test path and result, the deletion/rename manifest, and
exact counts. Then report `phase_status.W4B = ready-for-merge`.

### Progress contract — what the coordinator reads

The ledger table at the end of `checkpoints/04-architecture.md` is the only status channel. Each row
carries: `batch`, `state` (`started`, `pass`, `deferred`, `blocked`), `group_count`, `deleted`,
`moved`, `renamed`, `resolved` (files this batch took out of the namespace count, deferrals
subtracted), `target_remaining` (the fixed number from the table above), `namespace_count` (measured),
and the gate result.

The coordinator does not ask for a narrative. It checks:

- **On schedule:** every finished batch has a `pass` (or `deferred` with named files) row and the
  measured `namespace_count` equals the row's fixed target (111, 111, 78, 73, 46, 8, 0, 0, 0) plus
  the number of files that row deferred;
- **Stalled:** a batch row is `started` with no verdict after the coordinator's next poll, or a
  `namespace_count` that did not decrease after a completed batch. The coordinator then stops P4,
  records `W4B: blocked` with the last green commit, and asks the worker for the failing gate output.
  It does not start P5 while any row is `started` or `blocked`;
- **Accepted:** B0–B9 all present, B9 `pass`, `namespace_count` = 0, gate commands quoted with exit
  statuses, and `phase_status.W4B = ready-for-merge`.

### Pass-level stop conditions

Stop the pass, restore to the last green commit, and report `W4B: blocked` with `blocked_at_batch` and
the exact failing command when:

- a batch gate still fails after three attempts, or the tree cannot be returned green;
- a required edit falls outside the `W4B` write roots in the manifest;
- a move or rename would change product behaviour, a route, a contract shape, or a migration;
- a duplicate cannot be collapsed because the surviving shape is contested;
- a type with a live consumer is reachable only by deleting the consumer;
- a batch's expected `namespace_count` cannot be met without renaming a live file into a namespace its
  project does not own.

A blocked pass is a valid handoff. It is not a partial success, and the tree is left compilable at
`last_green`.

## What you must prove before declaring done

**W4A**

- Each deleted type has zero references outside the deleted files; evidence is in the checkpoint.
- `src/MuscleCuties.App` has no tracked file/project reference before deletion.
- Solution builds; Core tests pass with exact counts.
- No file outside the W4A deletion set plus the checkpoint is in the diff.

**W4B**

- The ledger has a row for every batch B0–B9, each with a gate command, exit status, and count; no row
  is `started` or `blocked`.
- Zero `Core.*`/`UseCases.*` namespace declarations outside their owning projects, proved by `grep`
  and a passing architecture test.
- The legacy planner/model stack is gone; no dead DI registration remains; no test for removed
  behaviour remains.
- The duplicate auth validator, nutrition notifier/adapter, and any remaining `ChangePasswordBody`
  are collapsed to one type each.
- Core still has zero outer dependencies; UseCases only Core; UI/ViewModels have no
  `DbContext`/`IRepository`.
- Full solution build and all test suites pass with exact counts; no skipped/weakened tests.
- No route, schema, package, CI, or docs changes are in the diff.
- `git diff --name-only` contains only namespace/deletion/duplicate/DI/test files plus the checkpoint.

## Checkpoint

Write `docs/orchestration/checkpoints/04-architecture.md` using
`docs/orchestration/README.md` §5. Include:

- `worker: 04-architecture`, base commit, branch, worktree;
- `scope_ref: [W4A, W4B]`; one frontmatter with
  `phase_status: { W4A: ready-for-merge | blocked, W4B: not-started | ready-for-merge | blocked }`;
- W4A body: deletion manifest with zero-reference proofs, build/test counts.
- W4B body (append, do not overwrite W4A): integration commit, the B0–B9 ledger table
  (`batch`, `state`, `group_count`, `deleted`, `moved`, `renamed`, `deferred`, `namespace_count`, gate
  result), the deletion/rename manifest, the guard test path/result, and the full test counts.

Never copy the W4A result into W4B. A blocked W4A is `W4A: blocked, W4B: not-started`; a blocked W4B
is `W4A: ready-for-merge, W4B: blocked`.

## Stop conditions

W4A stops and records `blocked` if:

- a W4A type has a live reference outside its own file/test;
- `src/MuscleCuties.App` has a tracked file or project reference;
- you are asked to edit docs, migrations, packages, CI, or a worker's unmerged branch.

W4B uses the pass-level stop list above; do not restate it here.

Do not hide a violation behind an allowlist, a `Skip`, or a second namespace. Do not declare W4B
done while the architecture guard is red.

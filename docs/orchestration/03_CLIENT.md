# Worker 3 — Client contracts and client behavior

You are Worker 3 in the MuscleCuties release cleanup. You own the MAUI client, the backend API
client, the Cloud stores, the client caching layer, the active-workout and preferences contracts, and
the client-facing tests. You do not own the server, migrations, packaging, CI, or docs.

Read first:

- `docs/RELEASE_INVENTORY_20261030.md` §0, §3, §4.3, §4.4, §4.7, §5.1 (client impact only), §5.2
  (`ActiveWorkoutSessionSnapshot`), §6.1 (client rows), §6.3 (client tests), §7 (client rows), §12
  (Worker 3), §13, §14.
- `AGENTS.md` — UI/DTO boundary, zero SQLite, privacy, build gate.
- `docs/orchestration/README.md` §4 (phase plan) and §5 (checkpoint format).
- `docs/orchestration/OWNERSHIP_MANIFEST.md` — your `W3` scope.
- This file. No chat context is needed beyond these files.

## Isolation and authority

The shared checkout is dirty and another agent may be editing it. Inventory §0 records the files that
were in flux; treat every one as potentially changed and re-read it in your worktree. Do not edit the
shared checkout.

The orchestrator creates `/tmp/mc-w3` from the freeze commit. Enter it:

```bash
cd /tmp/mc-w3
git status --short
```

If you are starting without an orchestrator and the worktree does not exist, create it once:

```bash
git worktree add /tmp/mc-w3 -b release/w3-client <freeze-sha>
```

Use branch `release/w3-client`. Local commits are allowed for handoff. **Do not push.** Do not deploy,
use a live backend, rotate a token, or run TestFlight. Use synthetic fixtures only. Do not log health
records, tokens, passwords, photo bytes, or real user data.

### Bootstrap and toolchain

The coordinator copied `docs/orchestration/`, the inventory, and the four tool scripts into this
worktree. They are untracked, read-only bootstrap files. Only your checkpoint under
`docs/orchestration/checkpoints/` may be staged from that copied package; add your own code/test
changes normally. Do not stage the rest of the package.

Read `/tmp/mc-freeze/freeze.env`. `diff_outcome=no-tracked-diff` is a normal outcome: do not apply a
patch. If `diff_outcome=tracked-diff`, check it in this clean worktree and apply only client hunks in
your scope; never apply server/migration hunks:

```bash
git apply --check /tmp/mc-freeze/tracked.patch
```

Use the pinned SDK explicitly; bare `dotnet` is the wrong install:

```bash
export DOTNET="$HOME/.dotnet/dotnet"
"$DOTNET" --version          # must print 10.0.403
"$DOTNET" workload list      # must include maui-ios
```

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

## Write scope (manifest `W3`)

Your only write list is the `W3` section of `docs/orchestration/OWNERSHIP_MANIFEST.md`. It contains
the UI/ApiClient/Cloud/Caching roots, the client contracts, the client-facing tests, the iOS tool,
and your checkpoint. Do not write a path that is not listed there.

Use the manifest's test paths exactly. Similarly named contract tests are split across the Core
and Registration test projects; the manifest is the authoritative assignment.

The work items below name files inside `W3`. If a named path is not in `W3`, stop and record it; the
manifest wins. Infrastructure Services/Models, Core, Persistence, server generation, CI, build
files, and existing docs are outside `W3`.

## Inventory items you own

- **4.3** — duplicate active-workout contract and dead manual JSON guesser.
- **4.4** — client meal repair writing per-100-g values into portion fields.
- **4.7** — half-landed preferences route removal.
- **5.2** — `ActiveWorkoutSessionSnapshot` duplicate.
- **6.1** — dead offline active-workout path and client DI registrations for legacy planners.
- **6.3** — client tests tied to those dead paths.
- **7** — manual JSON fallback, meal repair, legacy quiz rewrite, refresh-token cancellation,
  `WorkoutPreferencesLegacyNormalizer`, dormant health adapter.
- **12 (Worker 3)** and **13** — deliverables and handoff.

## Required work

### 1. One active-workout contract; delete the guessing path

There are two public `ActiveWorkoutSessionSnapshot` types:

- cache shape in `UseCases/Features/Caching/ILocalCacheService.cs`;
- HTTP shape in `UseCases/Features/Workout/ActiveSession/WorkoutActiveSessionContracts.cs`.

`MuscleCutiesBackendApiClient` imports the cache namespace, calls the HTTP endpoint, and
`MapActiveWorkoutSnapshot` guesses camelCase/PascalCase fields. It invents
`DateOnly.FromDateTime(DateTime.UtcNow)`, `ConcurrencyVersion: 1`, marks every set completed, and
substitutes placeholder names/ids. The only caller of `GetActiveWorkoutWithOfflineCacheAsync` is its
own definition.

Default resolution: keep exactly one contract — the HTTP shape used by the live stores — and delete
the cache-shaped duplicate, the dead method, the mapper, and the active-workout cache load/save path
that only serves it. If offline active-workout restore is a real product requirement, implement it
against the HTTP contract with a real round-trip test and no fabricated defaults; a placeholder
fallback is not acceptable. In either case, `grep` for `MapActiveWorkoutSnapshot`,
`GetActiveWorkoutWithOfflineCacheAsync`, and the cache-shaped type must return zero production
references after your change.

Update the cache interface only if the type is truly unused; do not delete a cache method that
another live snapshot type uses. Record the one remaining `ActiveWorkoutSessionSnapshot` file and
shape in the checkpoint.

### 2. Delete the client meal repair

`GeneratedMealResponseRepair` detects a legacy equal-split server response and rewrites components
client-side. It writes `CaloriesPer100Grams`/`ProteinPer100Grams`/etc. into fields consumed as
per-portion totals, so it is wrong for every component whose grams differ from 100. It has no tests
and is called from three live Cloud paths.

Worker 2 fixes the server generation to return portion totals. Delete `GeneratedMealResponseRepair`
and every call site in `CloudNutritionAndWorkoutStores.cs`. Do not replace it with another normalizer,
a different fallback, or a per-100-g conversion in the client. Add a client contract test with grams
**≠ 100** (for example 37 g and 143 g) that uses a synthetic server payload with correct portion
totals and asserts the UI/contract sees those totals. If the payload is still legacy, the test must
fail, not repair it.

### 3. Finish ticket A1 in place

The committed HEAD still sends `PUT /api/v1/profile/workout-preferences`, which the server rejects
with 409. The dirty tree removes the write and renames the action to Continue. Reconcile that work:

- `CloudProfileWorkoutPreferencesStore.SaveAsync` must not send the retired PUT;
- the page/view-model action and copy must say Continue (or the final agreed wording), not "saved";
- `WorkoutPreferencesLegacyNormalizer` must be gone with no references;
- localization keys must be descriptive and present in both language catalogs;
- update `ProfileWorkoutPreferencesRegenerationTests` and any UI text tests.

Do not re-add a PUT behind a flag. Do not leave a half-renamed UI. If the server still exposes a
retired route, record it for Worker 2; do not edit the server.

### 4. Remove dead client DI registrations, not the implementations

`MauiProgram.cs` registers several legacy services with zero consumers: `IWorkoutPlanner`,
`IGatingEngine`, `ICyclePhaseCalculator`, `ICyclePredictionPlanner`, `IDashboardPlanner`,
`IExerciseMediaStore`, and `IFdcApiClient`/`FdcApiClient`/`FdcAirGapHandler`. Remove the registrations
and any client-only setup (for example the FDC HTTP client) when no client code resolves them. Do not
delete the underlying `Infrastructure/Services/**` files: W4B deletes them after the merge. This split is deliberate: your branch must compile even while the dead types still exist.

Remove dead UI model copies only if they are in `src/MuscleCuties.UI/Models/UI/**` and `grep` over
all `.xaml` and `.cs` proves zero bindings. `Infrastructure/Models/UI/**` is Worker 4's.

### 5. Legacy quiz-question rewrite

`CloudAccountAndProfileStores.cs` upgrades an old server quiz-question shape client-side. This masks a
stale server catalog. Determine whether the current server contract already returns the current
shape. If it does, delete the rewrite and add a contract test against the current shape. If it does
not, stop and record the exact server payload mismatch for the orchestrator; do not add a third
shape, and do not silently keep the rewrite as "compatibility" without a deprecation condition.

### 6. Refresh-token cancellation and dormant health adapter

The refresh-token rotation deliberately ignores the caller's cancellation token so a half-rotation
cannot be persisted (`CancellationToken.None` at the rotate and token-write calls). Keep that
behaviour, but add a test or a precise comment that states the trade-off and the timeout/ownership
expectation. Do not "fix" it by passing a cancelled token and leaving a half-written token.

`DisabledHealthSyncService` is an intentional dormant boundary. Do not wire it to Apple Health or
Google Health Connect. If you touch its registration, keep it dormant and test that no live adapter
is selected.

### 7. Prove the client builds and the contracts are singular

- Build the client for the iOS target. A full Release simulator build may reach code signing and fail
  with `No valid iOS code signing keys found in keychain`; that is the observed blocker, not
  `NETSDK1147`. If signing blocks, run a compile-only build (`-t:Compile` or the project's equivalent)
  and label it **compile-only** in the checkpoint. Do not claim a full iOS build that did not run.
- Run `tests/MuscleCuties.Mobile.Contracts.Tests`; record the exact count. Run the client-facing
  Core tests you changed; record the count. Do not run the whole Core suite as a substitute for the
  specific client test.
- Inventory the client route literals (`grep` for `"/api/..."`) and compare with the server route
  literals. Record every mismatch. If a mismatch is on the server side, it is Worker 2's; do not edit
  it. The reviewer will run the parity gate on the merged tree.
- Verify no ViewModel injects `DbContext`/`IRepository` and no client layer leaks a domain entity
  where a DTO is required. Fix your own new code; do not start the 143-file namespace migration
  (W4B).

## What you must prove before declaring done

- Client compiles for the iOS target; if only compile-only, the checkpoint says so.
- `Mobile.Contracts.Tests` pass with an exact count, and every changed client test passes.
- Exactly one `ActiveWorkoutSessionSnapshot` remains; the dead cache path and manual mapper have zero
  references.
- `GeneratedMealResponseRepair` has zero references and is deleted; the grams ≠ 100 contract test
  passes.
- The retired preferences PUT has zero client references; the Continue flow and copy are consistent;
  `WorkoutPreferencesLegacyNormalizer` has zero references.
- Dead DI registrations are gone; their implementations remain for W4B.
- The legacy quiz rewrite is either deleted with a current-shape test or recorded as a specific
  blocker with the server payload mismatch.
- Refresh-token trade-off is documented/tested; health adapter remains dormant.
- `git diff --name-only` contains only your scope plus the checkpoint.
- No live backend, token, health record, password, photo bytes, or real user data is in the diff or
  logs.

## Checkpoint

Write `docs/orchestration/checkpoints/03-client.md` using
`docs/orchestration/README.md` §5. Include:

- `worker: 03-client`, `scope_ref: W3`, base commit, branch, worktree;
- `phase_status: { P1: ready-for-merge | blocked }`;
- inventory items 4.3, 4.4, 4.7, 5.2, 6.1, 6.3, 7;
- the one surviving snapshot contract and shape;
- deleted repair/mapper/normalizer and their zero-reference proof;
- grams ≠ 100 test evidence;
- preferences PUT/Continue evidence and localization keys;
- dead DI lines removed;
- iOS build command and exact result; label compile-only if applicable;
- route parity mismatches (if any) and owner.

## Stop conditions

Stop and record `blocked` if:

- the server's current meal payload cannot be represented with portion totals without a shape change
  Worker 2 has not made;
- deleting the legacy quiz rewrite would break a current server contract you cannot prove;
- the iOS target cannot compile for a reason other than signing;
- a required file is outside your scope;
- the in-flight patch conflicts with the committed code in a way you cannot verify safely.

Do not edit server, migrations, packaging, CI, or docs to unblock yourself.

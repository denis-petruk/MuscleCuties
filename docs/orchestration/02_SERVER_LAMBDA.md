# Worker 2 — Server, Lambda payload, packaging, and CI

You are Worker 2 in the MuscleCuties release cleanup. You own the ASP.NET Core/Lambda host, the
server-side nutrition generation path, the packaging and deployment scripts, the CI gate, and the
server/deployment test projects. You do not own migrations, UI, client stores, or docs.

Read first:

- `docs/RELEASE_INVENTORY_20261030.md` §0, §3, §4.2, §4.5, §4.6, §5.2 (`ChangePasswordBody`), §5.4,
  §6.1 (server rows), §7 (health/packaging/CI/build rows), §10, §12 (Worker 2), §13, §14.
- `AGENTS.md` — architecture, privacy, build gate, deterministic rules.
- `docs/orchestration/README.md` §4 (phase plan) and §5 (checkpoint format).
- `docs/orchestration/OWNERSHIP_MANIFEST.md` — your `W2` and `W2H` scopes.
- This file. No chat context is needed beyond these files.

## Isolation and authority

The shared checkout is dirty and another agent may be editing it. Inventory §0 records the files that
were in flux; treat every one as potentially changed and re-read it in your worktree. Do not edit the
shared checkout.

The orchestrator creates `/tmp/mc-w2` from the freeze commit. Enter it:

```bash
cd /tmp/mc-w2
git status --short
```

If you are starting without an orchestrator and the worktree does not exist, create it once:

```bash
git worktree add /tmp/mc-w2 -b release/w2-server <freeze-sha>
```

Use branch `release/w2-server`. Local commits are allowed for handoff. **Do not push.** Do not
deploy, invoke a real Lambda, read/write S3, use live AWS credentials, or run a live migration. All
tests use local/disposable resources.

### Bootstrap and toolchain

The coordinator copied `docs/orchestration/`, the inventory, and the four tool scripts into this
worktree. They are untracked, read-only bootstrap files. Only your checkpoint under
`docs/orchestration/checkpoints/` may be staged from that copied package; add your own code/test
changes normally. Do not stage the rest of the package.

Read `/tmp/mc-freeze/freeze.env`. `diff_outcome=no-tracked-diff` is a normal outcome: do not apply a
patch. If `diff_outcome=tracked-diff`, check it in this clean worktree and apply only server/package/
test hunks in your scope; never apply UI or Cloud hunks:

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

## Write scope (manifest `W2`)

Your only write list is the `W2` section of `docs/orchestration/OWNERSHIP_MANIFEST.md`. It contains
the server, server-side nutrition generation, deployment/CI/build files, the server/deployment
tests, the named integration tests, and your checkpoint. Do not write a path that is not listed
there.

The work items below name files inside `W2`. If a named path is not in `W2`, stop and record it; the
manifest wins. Migrations, UI, Cloud/ApiClient/Caching, Core, and existing docs are outside `W2`.

## Inventory items you own

- **4.2** — non-reproducible, contaminated Lambda ZIP and stale manifest.
- **4.5** — CI cannot upload/deploy the release branch.
- **4.6** — server test project does not build from the dirty snapshot (verify after the in-flight
  edit settles; do not carry a malformed test forward).
- **5.2** — duplicate `ChangePasswordBody`.
- **5.4** — `Directory.Build.targets` hard-coded test dependency copies.
- **6.1** — server rows: `EvaluateReadinessUseCase` dead registration and the separate reconciliation
  host decision.
- **7** — health-readiness overstatement, stale manifest, package contamination, CI branch gate,
  build workarounds.
- **10** — Lambda/CI inventory.
- **12 (Worker 2)** and **13** — deliverables and handoff.

## Required work

### 1. Make `/health/ready` tell the truth

`PostgreSqlSchemaCompatibilityCheck` currently compares only the EF migration ledger. A database with
all migrations recorded but missing grants can report Healthy. Change readiness so it cannot pass a
database that fails the schema contract:

- use `DatabaseSchemaCompatibility.EvaluateAsync` (Worker 1 keeps the signature stable; if it changes,
  adapt to the `Interface changes` note in Worker 1's checkpoint);
- require both the ledger and the contract to be compatible;
- return a stable, non-sensitive unhealthy description; never include SQL text, table names, user
  data, or connection strings;
- keep `/health/live` as a liveness check only.

Add `tests/MuscleCuties.Registration.Integration.Tests/ServerReadinessSchemaTests.cs` using the
disposable database fixture. Prove the negative: migrate, revoke a contracted grant (or drop a
required grant) in the disposable database, call the readiness check, and assert `Unhealthy`. Restore
the grant and assert `Healthy`. This is the gate that stops the current false green.

**Cross-worker dependency / P2H:** on the freeze branch the fresh contract is already incompatible
because the two calendar tables lack app grants (Worker 1 closes that). Do **not** apply the candidate
SQL manually to make this test green. Write the test and run the negative path if your fixture can
reach a compatible baseline; record the positive path as the **P2H** gate that the orchestrator runs
after W1 and W2 are merged. A local `not-run` for that half is honest; a manual SQL fixture is a
rejection. P2H is the only W2 proof that runs post-merge; append its result to your checkpoint rather
than creating a second status file. Run P2H in `/tmp/mc-w2h` on `release/w2h-health` from the P2
integration commit; commit the checkpoint update and, only if the gate requires it, the already-owned
`ServerReadinessSchemaTests.cs`. The orchestrator merges that commit before P3.

### 2. Fix the server-side meal totals, not the client repair

The client `GeneratedMealResponseRepair` writes per-100-g values into per-portion fields because the
server response is wrong or legacy. Fix the server path in
`src/MuscleCuties.UseCases/Features/Nutrition/Overview/**` (and the server mapping if needed) so
`MealSuggestionComponentDto.Calories/Protein/Carbs/Fats` are the component's **portion totals**, with
`portion.Nutrients` as the calculation source. Do not add a new response-repair layer, and do not
change the DTO shape without recording the contract change.

Add or update an integration test with grams **≠ 100** (for example 37 g and 143 g) that asserts the
serialized totals equal the portion calculation, not the per-100-g profile. Run it against the
disposable database. Worker 3 deletes the client repair in parallel; the integration reviewer proves
the two halves together.

### 3. Remove dead server code and duplicate DTOs

- Remove the `EvaluateReadinessUseCase` registrations in `ApiApplication.cs` if no endpoint injects
it. Do not leave a registered dead use case. The use-case file itself stays for W4B to delete after
merge; record that dependency.
- Remove the duplicate `ChangePasswordBody` in the server and consume the UseCases contract in
  `src/MuscleCuties.UseCases/Features/Account/Authentication/AccountAuthenticationContracts.cs`.
- Remove the retired `PUT /api/v1/profile/workout-preferences` endpoint if no live client calls it
  after Worker 3's change. Keep a route parity test that proves the removal. If a product decision
  requires an explicit 409 guard, keep exactly one guard and document it; do not leave a half-removed
  route.
- Enumerate every route group in `ApiApplication.cs` and the `*.cs` mapping files. Remove an endpoint
  only when no client/store/test still calls it. Record the route inventory (method, path, auth
  requirement, client caller) in the checkpoint. This is the server half of "all functions work".

### 4. Separate reconciliation host (resolved)

The standalone `services/MuscleCuties.WorkoutReconciliation` host was removed from the tree and
solution. The authenticated `/api/v1/workouts/plan/reconcile` route and the
`WorkoutPendingRevisionLambda` handler share `WorkoutPendingRevisionJobHandler`; external scheduling
remains an operator gate. Do not restore the retired host.

### 5. Make the Lambda package reproducible and clean

The local ZIP is `9,137,263` bytes / `0ba3f418…`; `build-manifest.json` and the deployed record say
`9,136,376` / `0b0358…`. The ZIP contains `SnapshotRestore.Registry.dll`, four PDBs,
`appsettings.Development.json`, a static-web-assets endpoint file, and macOS metadata. Fix the
**inputs**, not the validation:

- trace `SnapshotRestore.Registry.dll` to its source. It is not referenced by any project, NuGet
  file, or source file; it exists in `services/MuscleCuties.Server/bin/{Debug,Release}`. Clean the
  publish output and ensure a clean build does not produce or copy it. Do not add it to an allowlist
  or a "known extra" exception.
- exclude PDBs, `appsettings.Development.json`, `*.staticwebassets.endpoints.json`, `.DS_Store`, and
  `__MACOSX` from the publish/package. Keep `libSkiaSharp.so` (ARM64, required by
  `SkiaPhotoByteSanitizer`) and the embedded catalog.
- make `tools/rider/package-server.sh` produce a reproducible package from a clean `bin`/`obj`/
  publish state and regenerate `.sha256` plus `build-manifest.json` from the **same bytes** every
  time. CI's `validate-package.sh` must not be the only place the manifest is written.
- extend `tools/deployment/validate-package.sh` to reject unexpected DLLs, any PDB, development
  config, `.DS_Store`, `__MACOSX`, a live reconciliation manifest, and a static-web-assets endpoint
  file. Keep the required-file, runtime, ARM64, secret, and embedded-catalog checks.
- prove one candidate at `/tmp/mc-w2-candidate.zip` (not `artifacts/`): `unzip -l`, required and
  forbidden file checks, size/hash, manifest agreement, `validate-package.sh`,
  `verify-arm64-package.sh`, `verify-runtime.sh`. Run an in-process Lambda handler/API Gateway smoke
  test; do not call AWS.
- Do not delete or replace `artifacts/MuscleCuties.Server.zip` or its sidecars. The final single
  artifact is the orchestrator's P5 decision, not yours.

### 6. Fix the CI gate

`.gitlab-ci.yml` only allows upload for `codex/plan-generation-checkpoint` or `test-flight-beta` and
deploy for `codex/plan-generation-checkpoint`; production is `when: never`. The checked-out release
branch matches none.

- Make the intended release ref(s) eligible for **manual** staging upload/deploy. Use a variable or
  protected ref pattern, not a stale hard-coded branch. The orchestrator will name the release ref;
  default to `test-flight-beta-release-gate` and the checked-out release ref.
- Keep production `when: never` in this release. Do not add an automatic deploy or enable production.
- Clean `bin`/`obj`/publish before `dotnet-lambda package`; do not rely on a dirty workspace.
- Add the package content checks to CI so a contaminated ZIP cannot pass.
- Do not claim an iOS build in CI unless a macOS runner actually exists. If it does not, record the
  gap; Worker 3 proves the local iOS compile.
- Remove the hard-coded `Directory.Build.targets` copies of `Microsoft.VisualStudio.CodeCoverage.Shim.dll`
  and `Amazon.Lambda.APIGatewayEvents.dll` from absolute NuGet paths. Replace them with correct
  package references or delete the workaround. If a test project outside your scope needs a package
  reference, record it for the integration phase; do not edit that project's csproj.

### 7. Server test baseline

The snapshot's `ServerMediaSafetyTests.cs` had an in-progress malformed edit; the shared tree is not a
baseline. Start from the freeze commit. If the frozen `ServerMediaSafetyTests.cs` is valid, leave it;
if the in-flight patch is the intended fix, apply and verify it. Do not weaken the presigned-POST
assertions to make it compile. Record the real Server test count; the old documents' 82/90/112 counts
are not evidence.

## What you must prove before declaring done

- `"$DOTNET" build services/MuscleCuties.Server -c Release` succeeds.
- `"$DOTNET" test tests/MuscleCuties.Server.Tests -c Release` passes; record the exact count.
- Deployment tests (`release-fail-closed.sh`, `test_package_upload.py`, `test_release_engine.py`) pass
  on the frozen test fixtures; record the command.
- Every route in the server route inventory has a smoke test or an explicit "no caller, removed"
  entry; 401/403 behavior is tested for at least one protected route.
- `/health/ready` is `Unhealthy` when a contracted grant is revoked in a disposable database, and
  `Healthy` when restored. If the freeze branch cannot reach a compatible baseline before Worker 1
  merges, record the negative path and mark `P2H: not-run`; the orchestrator runs the full positive
  and negative gate on the merged W1+W2 tree before accepting this branch.
- The meal integration test with grams ≠ 100 passes and proves portion totals.
- The candidate ZIP has no forbidden content, has the required ARM64 Skia library and embedded
  catalog, and its size/hash/manifest agree. The exact candidate hash is in the checkpoint.
- `artifacts/` was not modified by you.
- `git diff --name-only` contains only your scope plus the checkpoint.
- No live AWS/S3/secret/live database was used.

## Checkpoint

Write `docs/orchestration/checkpoints/02-server-lambda.md` using
`docs/orchestration/README.md` §5. Include:

- `worker: 02-server-lambda`, `scope_ref: W2`, base commit, branch, worktree;
- `phase_status: { P1: ready-for-merge | blocked, P2H: not-run | pass | fail }`;
- inventory items 4.2, 4.5, 4.6, 5.2, 5.4, 6.1, 7, 10;
- route inventory and removed-route list;
- readiness negative-test evidence, then the P2H positive/negative evidence after merge;
- meal grams ≠ 100 evidence;
- candidate path/size/SHA-256/manifest result/content allowlist;
- CI rule diff (staging manual, production never);
- reconciliation-host decision;
- any cross-worker interface dependency (Worker 1 signature, Worker 3 route/contract).

## Stop conditions

Stop and record `blocked` if:

- you cannot trace `SnapshotRestore.Registry.dll` to a removable build input;
- a clean publish still includes a forbidden file you cannot exclude without breaking a required
  runtime dependency;
- the health check needs a schema-contract API Worker 1 has not provided;
- removing a route would break a live client caller you cannot prove is gone;
- a required fix is in a file outside your scope.

Do not edit migrations, UI, Cloud stores, or docs to unblock yourself. Do not add a validation
exception for a stray file.

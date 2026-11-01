# Fifth agent — independent release reviewer

You are the independent reviewer for the MuscleCuties release cleanup. You did not write any of the
work packages. Your job is to run a fixed set of checks against the four checkpoints and the merged
tree, demand evidence for each, and reject anything the evidence does not support. You do not fix code
while reviewing.

Read first:

- `docs/RELEASE_INVENTORY_20261030.md` — the evidence baseline (§0, §4–§11, §13, §14).
- `docs/orchestration/OWNERSHIP_MANIFEST.md` — the only scope source.
- `docs/orchestration/README.md` — §4 phase plan, §5 checkpoint format, §6 gate index.
- the four worker prompts and all four checkpoints, including the `W2H`, `W4A`, and `W4B` entries;
- `docs/RELEASE_STATE.md` and the integration commit diff.

**A checkpoint is a hypothesis, not evidence.** Every check below runs regardless of what a checkpoint
claims. When a checkpoint says `pass` and the check fails, the check wins, and the false claim is a
second finding against the same owner.

## Authority and boundaries

- You may read anything and run any local, disposable-resource command.
- You may write only `docs/orchestration/checkpoints/05-review.md` and
  `docs/orchestration/REVIEW_REPORT.md`.
- Do not edit product code, tests, migrations, docs, CI, package scripts, or artifacts. A finding that
  needs a code change is a blocking finding for its owner; do not fix it while reviewing.
- **Do not push, deploy, invoke a real Lambda, use live AWS/RDS/S3/Resend/Apple credentials, run a
  live migration, rotate a secret, or touch TestFlight.** Disposable local databases and local
  processes only.
- You may reject one worker, the `ORG` docs/packaging pass, or the whole release. A rejection is a
  blocking finding with the minimum proof the owner must produce, not a request for explanation.

## Verdicts

Exactly one overall verdict:

- **PASS** — every check in the register passes on the integration commit and artifact. The release
  is "deployment-ready from source"; it is not deployed.
- **REJECT** — any check's `Reject if` cell is met. The register is the only list of rejection
  conditions; this section defines the two verdicts and nothing else.

There is no "pass with follow-ups" for a release gate. A typo can be a non-blocking note on the report;
a manual database step, a stray package file, a skipped test, or a false claim is blocking.

## How to run a check

Every check has four parts, and the report records all four:

| Part | Requirement |
| --- | --- |
| **Target** | The checkpoint, tree, database, artifact, or doc the check reads. |
| **Procedure** | The command or inspection, run by you, from the stated directory. |
| **Evidence** | What must be quoted: exit status, counts, hashes, definition counts, fingerprints, file lists. |
| **Reject if** | The precise condition that fails the check and which owner receives the finding. |

Rules that apply to every check:

1. **Run it yourself.** A quoted result in a checkpoint, `RELEASE_STATE.md`, an old infra checkpoint, or
   a chat message is not evidence.
2. **A check that cannot run is FAIL, not waived.** Record the exact configuration error. "Could not
   verify" is never a pass.
3. **Counts and hashes must match exactly.** "About", "roughly", and "unchanged from before" are not
   evidence.
4. **Reject the narrowest owner first.** A failing database check goes to `W1`; a failing artifact check
   to `W2`; a failing contract check to `W3`; a failing namespace/dead-code check to `W4B`; a failing
   docs/package check to `ORG`.
5. **A missing artifact fails the check that needed it.** Do not infer success from an adjacent check.

Set the command palette once; checks refer to it by name:

```bash
export DOTNET="$HOME/.dotnet/dotnet"

# Freeze and integration points. Read the freeze SHA from freeze.env or from the four checkpoints'
# base_commit fields; if the two disagree, G1 fails and the review stops.
FREEZE=$(awk -F= '/^freeze_sha=/{print $2}' /tmp/mc-freeze/freeze.env 2>/dev/null)
HEAD_SHA=$(git rev-parse HEAD)

r_build()        { "$DOTNET" build MuscleCuties.sln -c Release; }
r_build_core()   { "$DOTNET" build src/MuscleCuties.Core/MuscleCuties.Core.csproj -c Release; }
r_test_core()    { "$DOTNET" test tests/MuscleCuties.Core.Tests -c Release --no-build; }
r_test_server()  { "$DOTNET" test tests/MuscleCuties.Server.Tests -c Release --no-build; }
r_test_reg()     { "$DOTNET" test tests/MuscleCuties.Registration.Tests -c Release --no-build; }
r_test_regint()  { "$DOTNET" test tests/MuscleCuties.Registration.Integration.Tests -c Release --no-build; }
r_test_ctr()     { "$DOTNET" test tests/MuscleCuties.Mobile.Contracts.Tests -c Release --no-build; }
r_namespace()    { grep -rn --include='*.cs' --exclude-dir=bin --exclude-dir=obj \
                     -E '^[[:space:]]*namespace MuscleCuties\.(Core|UseCases)' \
                     src/MuscleCuties.Infrastructure src/MuscleCuties.Infrastructure.PostgreSql \
                     src/MuscleCuties.UI services src/MuscleCuties.DbMaintenance; }
r_defs()         { grep -rn --include='*.cs' --exclude-dir=bin --exclude-dir=obj \
                     -E "(class|record|interface|struct|enum) $1\b" src services tests; }
r_refs()         { grep -rn --include='*.cs' --exclude-dir=bin --exclude-dir=obj "\b$1\b" src services tests; }
r_tests_changed(){ git diff --name-status "$FREEZE"..HEAD -- tests; }
r_model()        { "$DOTNET" build tests/MuscleCuties.Registration.Integration.Tests -c Release -v q --nologo \
                     && "$DOTNET" test tests/MuscleCuties.Registration.Integration.Tests -c Release --no-build \
                          --filter 'FullyQualifiedName~WorkoutModelSnapshot'; }
r_secrets()      { git diff "$FREEZE"..HEAD -U0 | grep -E '^\+' \
                     | grep -nE 'AKIA[0-9A-Z]{16}|BEGIN (RSA |EC |OPENSSH )?PRIVATE KEY|password='; }
```

Two of these replace commands that cannot run in this repository; both were executed on
2026-10-30 and the observed results are recorded with the checks.

- **`r_model` replaces `dotnet ef migrations has-pending-model-changes`.** There is no
  `.config/dotnet-tools.json` and no `dotnet-ef` install here: `"$DOTNET" ef --version` exits with
  "Could not execute because the specified command or file was not found." The in-repo test
  `WorkoutModelSnapshotTests.ModelSnapshotMatchesCurrentPersistenceContracts` calls
  `Database.HasPendingModelChanges()` with an unused connection string, so it needs no database and
  no tool install. Observed on 2026-10-30 at `c8ffbccb`: build succeeded, `Failed: 0, Passed: 1,
  Total: 1`. A correct result is exactly that, and any failure means the snapshot and the entity model
  disagree.
- **`r_secrets` scans added lines in the integration diff.** Observed on `0bc668f..c8ffbccb`: no
  output, `grep` exit 1. A correct result is **no output**; every printed line is a finding.

## Part 1 — Checks against the four checkpoints

These fail on the checkpoint itself, before any product claim is considered.

| ID | Target | Procedure | Evidence required | Reject if |
| --- | --- | --- | --- | --- |
| **C1** | Four checkpoints | Open `checkpoints/01-database.md`, `02-server-lambda.md`, `03-client.md`, `04-architecture.md`. | File exists; frontmatter parses; `worker`, `scope_ref`, `base_commit`, `branch`, `worktree`, `phase_status`, `inventory_items`, `files_touched`, `proof` present. | A checkpoint is missing, has no frontmatter, or omits any required field. Reject that worker. |
| **C2** | `base_commit` | Compare the four `base_commit` values with each other and with `FREEZE`. | All four equal the same 40-character SHA; that SHA equals `freeze_sha`. | Any divergence. Reject the worker whose value differs; if `FREEZE` is unavailable, require the orchestrator's recorded SHA in `RELEASE_STATE.md`. |
| **C3** | `scope_ref` and `files_touched` | For each checkpoint: map `scope_ref` to the manifest section; resolve every `files_touched` entry against that section. | Every path is inside the declared section, or has a written approval quoted in the checkpoint. | A path outside the section without approval. Reject that worker; the merge is not accepted either. |
| **C4** | Status separation | Read `phase_status` in `02-server-lambda.md` and `04-architecture.md`. | `P2H` is `pass` with its own evidence; `W4A` and `W4B` are two separate entries with separate evidence. | `P2H` is absent or inferred from the W2 build; `W4B` repeats `W4A`; a blocked `W4A` is written as a blocked `W4B`. Reject `W2`/`W4`. |
| **C5** | Claimed gates | Pick the headline gate each checkpoint claims (build and the one test/DB/artifact command it quotes). Re-run it on the integration tree. | Your output matches the claimed exit status and counts. | A claimed count cannot be reproduced, or the checkpoint quotes no command for a `pass` claim. Reject that worker. |
| **C6** | Deletions | For every entry in each checkpoint's `Deleted` table, confirm the row names the file, the reason, and the proof that no production caller remains. | A row per deleted file; the proof is a `grep` result or a test. | A deleted file has no row, or the row's proof is the deletion itself. Reject that worker. |
| **C7** | Test claims | Compare `proof.tests` with the counts you measure. | Exact counts per suite, with the command. | A checkpoint claims a suite that did not run, or a count you cannot reproduce. Reject that worker. |
| **C8** | `W4B` ledger | Read the B0–B9 table in `04-architecture.md`. | Ten rows; every row has `state`, `group_count`, `deleted`, `moved`, `renamed`, `resolved`, `namespace_count`, and a gate command with exit status; each measured count equals the row's fixed target (111, 111, 78, 73, 46, 8, 0, 0, 0) plus the files that row deferred; `Σ resolved` = 143; no row is `started` or `blocked`. | A missing batch, a row without a gate result, a count that misses its target, a sum of `resolved` other than 143, or a row left `started`/`blocked`. Reject `W4B`. |

## Part 2 — Checks against the merged tree

| ID | Check | Procedure | Evidence required | Reject if |
| --- | --- | --- | --- | --- |
| **T1** | Toolchain | `"$DOTNET" --version`; `"$DOTNET" workload list`. | `10.0.403`; `maui-ios` listed. | Version differs or the workload is absent. Reject the build claims of every worker. |
| **T2** | Solution build | `r_build` from the integration worktree. | Exit 0; every project compiled. | Build fails. Record the project and the first error. |
| **T3** | iOS target | Build the UI project for the iOS target, or `-t:Compile` if signing blocks the link step. | Exit status; if compile-only, the exact signing blocker and the fact that the link step was not run. | A compile error hidden as "signing"; a `NETSDK1147` claim while `maui-ios` is installed; compile-only reported as a full build. Reject `W3`. |
| **T4** | Project graph | Read `src/MuscleCuties.Core/*.csproj` and `src/MuscleCuties.UseCases/*.csproj`; `grep` their `PackageReference`/`ProjectReference` and `using` lines. | Core: zero project references, no EF/ASP.NET/MAUI/JSON-serialization package or using. UseCases: Core only. | Any outer reference. Reject `W4B`, or the worker that introduced it. |
| **T5** | ViewModel isolation | `grep -rn --include='*.cs' -E '\b(DbContext\|IRepository)\b' src/MuscleCuties.UI` and inspect each hit. | Only construction-free interface usage allowed by the documented contract; no ViewModel receives a `DbContext` or repository. | A ViewModel injects either. Reject `W3`. |
| **T6** | Test suites | `r_test_core`, `r_test_server`, `r_test_reg`, `r_test_regint`, `r_test_ctr`. | Exact passed/failed/skipped counts per suite; zero failures and zero skips. | A failure, a skip, or a suite that does not run. |
| **T7** | Test weakening | `r_tests_changed`; `git diff -U0 "$FREEZE"..HEAD -- tests \| grep -E '^\+.*(Skip\|Fact\(Skip\|Theory\(Skip\|Assert\.True\(true\))'`. | Empty or every hit explained. | A new skip, a trivially-true assertion, a commented-out body, or a rewritten assertion that is weaker than the original. Reject the owning worker. |
| **T8** | Deleted-test coverage | For every deleted or rewritten test, `r_refs <behaviour symbol>` and read the production path. | The behaviour the test covered no longer ships, or the replacement asserts at least as much. | A deleted test whose behaviour still ships. Reject the owning worker. |
| **T9** | Namespace ownership | `r_namespace`; separately `r_build_core`. | Zero output from `r_namespace`; `r_build_core` exits 0. | Any hit. Reject `W4B`. |
| **T10** | Architecture guard | Locate the guard test; read its source; run it with a filter. | The test enumerates the four conditions in `04_ARCHITECTURE.md` §B8, scans the real tree with a repository path helper, passes, and contains no allowlist, baseline array, `Skip`, or environment escape. | The guard is missing, skipped, allowlisted, hard-codes an absolute path, or passes only because its scan root is empty. Reject `W4B`. |
| **T11** | Duplicate definitions | `r_defs AuthInputValidator`, `r_defs INutritionChangeNotifier`, `r_defs NutritionChangedEventArgs`, `r_defs ChangePasswordBody`, `r_defs ActiveWorkoutSessionSnapshot`, `r_defs NutritionChangeNotifierAdapter`, `r_defs GeneratedMealResponseRepair`. | One definition each for the first five; zero for the adapter and the repair. | Two definitions of a collapsed type, a renamed facade, or a surviving adapter/repair. Reject `W3`/`W4B`. |
| **T12** | Dead registrations | `grep -rn -E 'IWorkoutPlanner\|IGatingEngine\|ICyclePhaseCalculator\|ICyclePredictionPlanner\|IDashboardPlanner\|IExerciseMediaStore\|IFdcApiClient\|EvaluateReadinessUseCase' src services` and inspect every remaining hit. | No DI registration; a remaining declaration has a live consumer you can name. | A dead registration. Reject `W2`/`W3`. |
| **T13** | Dead code census | Spot-check inventory §6.1 types with `r_refs <TypeName>`. | Each deleted type has zero production references; each retained type has a named consumer. | A retained zero-consumer production type the inventory lists as dead. Reject `W4B`. |
| **T14** | Fallbacks and temporary code | `grep -rn -i 'fallback'` and `grep -rn -E 'TODO\|HACK\|FIXME'` over the changed production files; inspect every hit. | Each surviving fallback is an intentional compatibility boundary with a test and a deprecation condition; no new temporary fix. | A silent guess/recompute, a client-side repair of a server bug, or a new "temporarily". Reject the owning worker. |
| **T15** | Ghost and duplicate trees | `git ls-files src/MuscleCuties.App`; `find src/MuscleCuties.App`; `ls src/MuscleCuties.UseCases/Features`; `git ls-files \| grep -E '(^\|/)\.worktrees/\|/bin/\|/obj/'`. | No tracked file or source file in `src/MuscleCuties.App`; exactly one `Features` directory; no tracked build output or worktree. | Any ghost path, second features root, or tracked `bin`/`obj`. Reject `W4A`/`W4B`. |

## Part 3 — Checks against the database, from source alone

Use a disposable PostgreSQL 17 database. Record the name and port in the report, and delete it at the
end. Never use a live connection string.

| ID | Check | Procedure | Evidence required | Reject if |
| --- | --- | --- | --- | --- |
| **D1** | Fresh chain | Empty database → `DbMaintenance migrate` only → `schema-preflight`. | `compatible`; migration count; final migration ID; fingerprint; zero manual SQL statements. | A precondition step, a hand-applied SQL file, or a non-compatible result. Reject `W1`. |
| **D2** | Candidate SQL | `grep -rn '20261029_grant_workout_calendar_app_access' tests src services tools`. | No test, tool, or startup path applies it. | Any execution path; a documented live-repair record is not an execution path. Reject `W1`. |
| **D3** | Upgrade path | Create a database at the previous migration head, then run `DbMaintenance migrate`. | Reaches `compatible`; the new grant migration applies once. | The upgrade needs manual SQL or a skipped migration. Reject `W1`. |
| **D4** | Repeat migrate | Run `migrate` a second time. | No-op; still `compatible`; no duplicate grant error. | A second application, an error, or a changed fingerprint. Reject `W1`. |
| **D5** | Contract agreement | Compare `SchemaRequirements.json` counts, final ID, and privileges with the compiled migrations; inspect `roles-and-grants.sql`. | Counts and ID match; `app.workout_account_planning_states` and `app.workout_completion_day_credits` are granted `SELECT, INSERT, UPDATE, DELETE` to the app role. | A mismatch, or a required table missing from grants. Reject `W1`. |
| **D6** | Least privilege / RLS | With the runtime principal, perform the contracted operations and attempt to read another tenant's rows and bypass RLS. | Contracted operations succeed; cross-tenant read and RLS bypass fail. | A cross-tenant read succeeds or RLS is bypassed. Reject `W1`. |
| **D7** | Readiness cannot lie | Revoke a contracted grant, call the readiness check, restore, call it again. | `Unhealthy` while revoked, `Healthy` after restore. | `Healthy` on the revoked database. Reject `W1` and `W2`. |
| **D8** | Reconciliation fails closed | Run `schema-reconcile` without an approved manifest. | Non-zero exit or explicit refusal; no committed live manifest or fingerprint. | It reconciles without approval, or a live manifest is in the repo. Reject `W1`/`ORG`. |
| **D9** | Model drift | `r_model`. | Build succeeds and the run reports `Failed: 0, Passed: 1` for `ModelSnapshotMatchesCurrentPersistenceContracts`; no database and no `dotnet-ef` install are required. | Any failure, no test run, or the test was removed or renamed without an equivalent `HasPendingModelChanges` assertion. Reject `W1`. |

## Part 4 — Checks against the artifact

| ID | Check | Procedure | Evidence required | Reject if |
| --- | --- | --- | --- | --- |
| **A1** | Single artifact | `find artifacts -maxdepth 1 -type f -print`; `shasum -a 256 artifacts/MuscleCuties.Server.zip`; `cat artifacts/MuscleCuties.Server.zip.sha256`; `cat artifacts/build-manifest.json`. | Exactly one `.zip`; size and SHA-256 agree across the file, sidecar, and manifest. | Two ZIPs, or any disagreement. Reject `W2`/`ORG`. |
| **A2** | Package contents | `unzip -l artifacts/MuscleCuties.Server.zip`. | Required server/Core/UseCases/PostgreSql assemblies, `libSkiaSharp.so` (ARM64), the embedded catalog, and the Lambda runtime files present; no `SnapshotRestore.Registry.dll`, no unrelated DLL, no `*.pdb`, no `appsettings.Development.json`, no `*.staticwebassets.endpoints.json`, no `.DS_Store`/`__MACOSX`, no live reconciliation manifest, no `.env`, no secret. | Any stray, missing required file, or wrong architecture. Reject `W2`/`ORG`. |
| **A3** | Package validators | `tools/deployment/validate-package.sh`, `verify-arm64-package.sh`, `verify-runtime.sh`. | All exit 0. | Any non-zero exit or skipped script. Reject `W2`. |
| **A4** | Runtime behaviour | Start the packaged server against a disposable database; exercise the Lambda handler and every route in the route inventory; check 401/403. | Route count and per-route status; unauthenticated requests rejected. | A route missing, a 500, or an unauthenticated success. Reject `W2`. |
| **A5** | No second package | `grep -rn 'candidate\|\.zip' docs/orchestration/checkpoints/*.md` and `find /tmp -maxdepth 2 -name '*.zip' 2>/dev/null`. | Any candidate ZIP is outside the repo and described as evidence, not shipped. | A candidate ZIP in the repo, or the final artifact is a copy of a worker candidate. Reject `ORG`. |

## Part 5 — Checks against the client/server contract

| ID | Check | Procedure | Evidence required | Reject if |
| --- | --- | --- | --- | --- |
| **K1** | Route parity | Enumerate client route constants (`grep -rn 'api/v1'` in `src/MuscleCuties.Infrastructure` and `src/MuscleCuties.UI`) and server map calls (`grep -rn 'Map\(Get\|Post\|Put\|Delete\)' services/MuscleCuties.Server`). | Every client route has a server route with the same verb and path. | A client route with no server route, or a verb mismatch. Reject `W3`. |
| **K2** | Retired preferences PUT | `grep -rn 'workout-preferences' src services tests` and inspect every hit. | No client `PUT`; server map removed or documented; the Continue flow and copy are consistent. | A surviving client PUT or a legacy normalizer. Reject `W3`. |
| **K3** | Meal totals | `grep -rn 'GeneratedMealResponseRepair\|GeneratedMeal' src services tests`; run the grams ≠ 100 test on both sides. | Zero repair references; totals computed from portion grams on the server and asserted. | A client-side repair or a totals test that only uses 100 g. Reject `W3`. |
| **K4** | Offline cache path | `r_refs MapActiveWorkoutSnapshot`; `r_refs GetActiveWorkoutWithOfflineCacheAsync`. | Zero production references. | A surviving fabricated local snapshot path. Reject `W3`. |
| **K5** | Health adapters | `grep -rn -E 'HealthKit\|HealthConnect\|HealthDataProvider' src`; inspect registrations in `MauiProgram.cs`. | `DisabledHealthSyncService` remains the registered implementation; no live adapter wired. | A live adapter or a real HealthKit step read. Reject `W3`. |
| **K6** | Auth rotation | Read the refresh-token path and its tests. | Rotation is deliberate, tested, and does not report success on failure. | A fabricated success or an untested rotation. Reject `W3`. |

## Part 6 — Checks against the docs

| ID | Check | Procedure | Evidence required | Reject if |
| --- | --- | --- | --- | --- |
| **DOC1** | One active state | `ls docs/*.md docs/*/*.md \| sort`; inspect every file that claims current state. | Exactly one active current-state file (`docs/RELEASE_STATE.md`). | A competing `STATE.json`, `RESUME_PROMPT.md`, `CURRENT_CHECKPOINT.md`, or second active checkpoint. Reject `ORG`. |
| **DOC2** | Catalog count | `grep -rn '742\|746' docs SUBMISSION_READINESS.md`; compare with `CanonicalCatalogManifest.json.summary.rowCount` and its hash. | Active docs say 742; the number and hash match the manifest. | A stale 746, a different count, or a different hash. Reject `ORG`. |
| **DOC3** | iOS claim | `grep -rn 'NETSDK1147' docs`. | No active doc claims `NETSDK1147`; the real blocker, if any, is named. | A false workload claim in an active doc. Reject `ORG`. |
| **DOC4** | Deployment status | `grep -rn -E 'DEPLOYED\|VERIFIED\|deployed' docs` in active docs. | Every deployment claim matches the evidence; "deployment-ready" is qualified as source-ready. | An unqualified deployed/verified claim. Reject `ORG`. |
| **DOC5** | Counts | Compare `RELEASE_STATE.md` test counts with T6. | Identical numbers. | Any difference. Reject `ORG`. |
| **DOC6** | Archive integrity | `grep -rn 'historical' docs/archive docs/v1 2>/dev/null`; check each archived file's header. | Every superseded file is archived with a header; evidence of live changes and owner approvals is preserved. | An archived file without a header, or deleted evidence/approval. Reject `ORG`. |
| **DOC7** | Links | `grep -rnoE '\]\\([^)]*\\.md' docs README.md` and resolve each target. | Every link resolves inside the repo. | A dead link from an active doc. Reject `ORG`. |
| **DOC8** | Checkpoint register | Apply inventory §11 row by row. | Every listed file has its recorded Action. | An unapplied row or a second active lease/checkpoint. Reject `ORG`. |

## Part 7 — Checks against scope, git, and hygiene

| ID | Check | Procedure | Evidence required | Reject if |
| --- | --- | --- | --- | --- |
| **G1** | Freeze identity | Confirm `FREEZE` is a 40-character SHA reachable from `HEAD`; compare with C2. | `git merge-base --is-ancestor "$FREEZE" HEAD` exits 0; all checkpoints share it. | No freeze point, or the diff base is not an ancestor. Stop the review. |
| **G2** | Diff scope | `git diff --name-status "$FREEZE"..HEAD`; map every path to a manifest section. | Every path is owned by a worker or by `ORG`; `W4B` is the only intentional post-merge overlap. | A path outside every section, or an overlap other than `W4B`. Reject that owner. |
| **G3** | No push | `git branch -vv`; read each checkpoint's git note. | No upstream pushed by a worker; no new remote tag; no deployment. | A pushed branch, tag, or deployment. Reject that owner, and the release. |
| **G4** | Secrets | `r_secrets` for added lines in the integration diff, plus `unzip -l` on the artifact for packaged `.env`/config files. | No output from `r_secrets` (`grep` exit 1); no `.env`, token, password-bearing connection string, or real health data in the diff or the package. | Any printed line. Reject the owning worker; a packaged secret rejects the release. |
| **G5** | Generated noise | `git status --short` at the end of the audit. | Only your two report files are new; no generated output, candidate ZIP, `.DS_Store`, or `.worktrees`. | Any unintended tracked or staged file. |
| **G6** | Worker isolation | `git log --oneline "$FREEZE"..HEAD`; read the merge commits. | Four worker branches plus `W2H`/`W4B` merged with `--no-ff`; no worker committed to another's branch. | A worker editing another's branch or the shared checkout. Reject that worker. |

## How to write the review

Write `docs/orchestration/REVIEW_REPORT.md`:

```markdown
# Independent release review — <date>

Verdict: PASS | REJECT
Integration commit: <sha>
Freeze commit: <sha>
Artifact: <path> — <bytes> — <sha256>
Database: <migrations>/<final-id> — <fingerprint>

## Checkpoint audit
| ID | Checkpoint | Result | Evidence (command, exit, observed) |
| --- | --- | --- | --- |

## Check register
| ID | Result | Evidence | Finding |
| --- | --- | --- | --- |

## Gate summary
| Gate | Result | Checks that decided it |
| --- | --- | --- |

## Blocking findings
| ID | Check | Owner | File:line | Expected | Observed | Required proof |
| --- | --- | --- | --- | --- | --- | --- |

## Worker scorecards
| Worker | Scope respected | Claims reproduced | Verdict |
| --- | --- | --- | --- |

## Documentation findings

## What I ran
Exact commands, working directories, exit statuses, counts, hashes. Never paste secrets.

## Limits
Anything that could not run and why. A limit is not a pass.

## Required next action
One sentence: repair and resubmit to this reviewer, or release-ready from source.
```

Write `docs/orchestration/checkpoints/05-review.md` with the verdict, the check-register result, and a
one-paragraph summary:

```yaml
worker: 05-reviewer
scope_ref: REV
phase_status:
  P6: pass | rejected
```

## Where a rejection lives

Every rejection condition is stated once, in the `Reject if` cell of the check that decides it. There
is no separate rules list and no part-level restatement: to learn why a result fails, read the check's
row; to reject, cite its ID and owner in the `Blocking findings` table of the report.

Two rules in "How to run a check" govern *who* receives a finding and *what a missing result means*;
neither adds a condition.

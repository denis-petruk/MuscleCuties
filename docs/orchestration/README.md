# MuscleCuties release cleanup — orchestration index

> Historical note (2026-11-01): this packet describes the October 2026 migration-era cleanup. The schema is now model-first (`PostgresDbContext` + `Persistence/Schema`; `DbMaintenance dbinit`/`dbcheck`), and the standalone reconciliation host has been removed. Use `STATUS.md` and `ROADMAP.md` for current release state; this packet is retained for provenance.

Status: **HISTORICAL — superseded; no worker should dispatch from this packet.**
Authority: [`OWNERSHIP_MANIFEST.md`](OWNERSHIP_MANIFEST.md) for who writes which paths;
[`docs/RELEASE_INVENTORY_20261030.md`](../RELEASE_INVENTORY_20261030.md) for the evidence.

Freeze rule: this package stores **no commit literal**. The coordinator captures `freeze_sha` at P0
in `/tmp/mc-freeze/freeze.env`. The shared checkout was dirty and moving during the inventory
(inventory §0); never reuse a SHA from an older note, including inventory §0. Do not edit the shared
checkout.

## 1. Files in this package

| File | Purpose |
| --- | --- |
| `README.md` | This index: the only phase plan, checkpoint format, review gates, exclusions. |
| `OWNERSHIP_MANIFEST.md` | **The only scope source.** Who writes which paths, and in which phase. |
| `00_ORCHESTRATOR.md` | Thin dispatch/merge prompt. It executes the phases here; it does not restate them. |
| `01_DATABASE.md` | Worker 1 — database, grants, schema contract, RLS, model drift. |
| `02_SERVER_LAMBDA.md` | Worker 2 — server, health, meal totals, ZIP, CI, build cleanup. |
| `03_CLIENT.md` | Worker 3 — active workout, meal repair, preferences, DI, iOS compile. |
| `04_ARCHITECTURE.md` | Worker 4 — W4A safe deletions, W4B post-merge architecture. |
| `05_REVIEWER.md` | Fifth agent — independent audit with rejection authority. |

Worker checkpoints go in `docs/orchestration/checkpoints/`. The directory must be created by the
first writer:

```bash
mkdir -p docs/orchestration/checkpoints
```

## 2. Non-negotiables inherited from the mission

Every worker prompt repeats these; the orchestrator and reviewer enforce them.

1. **No pushes.** Local commits on isolated worker branches are allowed for handoff. Never push,
   open a merge request, deploy, rotate a secret, run a live migration, or touch TestFlight.
2. **Database reproducible from migrations alone.** A fresh PostgreSQL 17 database migrated only by
   `DbMaintenance migrate` must reach `schema-preflight = compatible` with zero manually applied SQL.
   A reconciliation manifest is not an install prerequisite.
3. **Exactly one Lambda artifact.** `artifacts/MuscleCuties.Server.zip` contains no stray DLLs, PDBs,
   development config, macOS metadata, or unrelated live manifests. Its size/hash match its checksum
   and manifest. Every routed function works against a disposable database.
4. **Remove dead and duplicated code; never bypass it.** Deleting a caller while leaving the dead
   implementation, or adding a second adapter/repair/fallback, does not satisfy the mission.
5. **Fix the cause in place and in the right layer.** No sidecar normalizer, client-side repair of a
   server bug, manual JSON field guesser, test-only SQL fix, or fabricated default.
6. **Delete obsolete tests only when the behaviour is genuinely gone.** No skipped tests, weakened
   assertions, or deleted coverage for behaviour that still ships.
7. **Clean architecture and only necessary files.** Core has no outer dependencies; UseCases depends
   only on Core; UI/Controllers see DTOs only; no `Core.*`/`UseCases.*` namespace is declared in an
   outer project; no ghost directories or duplicate trees.
8. **Isolation.** Each worker uses its own worktree and branch. The shared checkout is read-only for
   workers. A path outside the manifest is a stop condition, not a judgement call.
9. **Evidence over documents.** `docs/v1/STATUS.md`, `ROADMAP.md`, the beta checkpoints, and the infra
   checkpoints contain claims the code does not support (inventory §8). They are leads, never proof.
   Re-run the command and record the real result.

There is exactly **one** `src/MuscleCuties.UseCases/Features` directory. The "duplicated features
folder" symptom is the parallel legacy model/service tree under `src/MuscleCuties.Infrastructure`
that declares `Core.*`/`UseCases.*` namespaces (inventory §2 and §5.1). Do not create a second
`Features` folder to work around it.

## 3. Ownership map

This table is an index, not a second scope list. All paths are in
[`OWNERSHIP_MANIFEST.md`](OWNERSHIP_MANIFEST.md); do not copy path lists into prompts or chat
starters.

| Owner | Manifest ID | Phase | Checkpoint |
| --- | --- | --- | --- |
| Worker 1 — database | `W1` | P1 | `checkpoints/01-database.md` |
| Worker 2 — server/Lambda/CI | `W2`; gate `W2H` | P1; P2H | `checkpoints/02-server-lambda.md` |
| Worker 3 — client | `W3` | P1 | `checkpoints/03-client.md` |
| Worker 4 — architecture | `W4A`, then `W4B` | P1; P4 | `checkpoints/04-architecture.md` |
| Orchestrator — docs/package | `ORG` | P5 | `docs/RELEASE_STATE.md` |
| Reviewer | `REV` | P6 | `checkpoints/05-review.md` |

### Why Worker 4 has two manifest entries

`W4A` is parallel and deletes only types with zero references outside its own files. `W4B` is the
cross-cutting namespace/dead-stack cleanup that must edit files previously owned by Workers 1–3.
`W4B` is a **post-merge phase**, not a parallel scope. It starts only after P3 and never while
another worker is editing. This is the one intentional overlap, and it is serialized.

## 4. Phase plan

This is the only place that defines phase order, entry conditions, and exit gates. The orchestrator
prompt executes these phases without restating them. The manifest defines who writes what.

| Phase | Owner | Entry | Exit |
| --- | --- | --- | --- |
| **P0 — Freeze, quiesce, bootstrap** | Orchestrator | Read inventory §0; run the toolchain precheck; require a quiescent tree. | `freeze.env` records `freeze_sha`, `quiescent_until`, and `diff_outcome=tracked-diff\|no-tracked-diff`; the untracked classifier has no unclassified path; four parallel worktrees are created and bootstrapped; the seven-worktree map is logged. If the tree moves, no worktree is created and P0 stops. |
| **P1 — Parallel worker passes** | `W1`, `W2`, `W3`, `W4A` | P0 complete; each worker has its own worktree/branch. | `W1`/`W2`/`W3` checkpoints have `phase_status.P1`, `W4` has `phase_status.W4A`; each is `ready-for-merge` or `blocked`; no worker wrote outside its manifest paths. |
| **P2 — Merge database and server** | Orchestrator | `W1` and `W2` are `ready-for-merge`; logs show no path violations. | `W1` then `W2` merged into `release/integration-20261030`; affected builds/tests rerun on the merged tree; `W2` checkpoint says `phase_status.P2H = not-run`. |
| **P2H — Fresh-database health gate** | `W2` | P2 complete; `W1`'s grant migration is in the integration tree. | On a disposable PG17 database migrated only from source: readiness is `Unhealthy` after a contracted grant is revoked and `Healthy` after restore; no candidate/manual SQL; `phase_status.P2H = pass`. |
| **P3 — Merge client and W4A** | Orchestrator | P2H pass. | `W3` then `W4A` merged; affected builds/tests rerun on the merged tree; no worker running. |
| **P4 — W4B architecture pass** | `W4B` | P3 complete; explicit `W4B — MERGE COMPLETE` handoff with the integration commit. | Every row of the B0–B9 ledger in `04_ARCHITECTURE.md` has a gate command, an exit status, and a count; no row is `started` or `blocked`; zero `Core.*`/`UseCases.*` namespaces outside their projects; legacy planner/model stack gone; duplicates collapsed; architecture guard passes; solution build (UI compile-only if iOS signing blocks, with the blocker recorded) and B9's six suites green; `phase_status.W4B = ready-for-merge` or `blocked`. |
| **P5 — Docs cleanup and single artifact** | `ORG` | P4 accepted and the W4B branch merged into integration; all worker checkpoints merged. | One active `docs/RELEASE_STATE.md`; historical docs archived; false active claims fixed; exactly one `artifacts/MuscleCuties.Server.zip` with matching sidecars, no strays, and a passing Lambda/route smoke test. |
| **P6 — Independent review** | `REV` | P5 complete; integration commit, artifact hash, DB fingerprint, and checkpoints handed over. | `REVIEW_REPORT.md` says `PASS`, or `REJECT` with blocking findings. |
| **P7 — Close** | Orchestrator | P6 report. | `PASS`: release-ready-from-source statement, no deployment. `REJECT`: findings routed to owners, fixed on the integration branch, rerun through P6. |

### Where the two open cleanup items execute

Neither is a phase of its own; each is already assigned, and this is the only place that says so.

- **Stale and no-longer-relevant checkpoint documents.** Owner `ORG`, phase **P5**: entry and exit are
  the P5 row above, the procedure is the P5 checklist below, and inventory §11 supplies the per-file
  actions. No worker edits existing docs.
- **Obsolete tests.** Owner: the worker that removes the behaviour, in its own phase — `W1`, `W2` (or
  `W2H`), `W3`, and `W4A` at P1, `W4B` at P4 — under non-negotiable 6, recorded in that checkpoint's
  `Deleted` table with proof that the behaviour is gone. No separate test-deletion phase exists, and
  no test is deleted outside its owner's scope.

### P0 — quiescence, freeze, and bootstrap (canonical)

P0 is a gate, not a snapshot. Run it before creating any worktree. If the tree moves, stop and return
it to the owner; do not freeze and do not retry automatically. There is no lease or lock in this
repository, so a quiet window and the owner's stop confirmation are the only available proof.

#### 1. Toolchain precheck

Use the pinned SDK by explicit path. Bare `dotnet` resolves the system install and fails before
compiling.

```bash
set -euo pipefail
export DOTNET="${DOTNET:-$HOME/.dotnet/dotnet}"
test -x "$DOTNET"
test "$("$DOTNET" --version)" = "10.0.403"
"$DOTNET" workload list | grep -q 'maui-ios'
```

If any step fails, stop. Record the exact `DOTNET` path and version in `freeze.env`.

#### 2. Quiescence gate

Ask the owner and the other agent to stop. Then require **20 consecutive samples, 15 seconds apart**
(five minutes total) with identical snapshots:

```bash
mkdir -p /tmp/mc-freeze
snapshot() {
  {
    git rev-parse HEAD
    git status --porcelain=v2
    git diff --binary HEAD | shasum -a 256
    git ls-files -s | shasum -a 256
    git ls-files --others --exclude-standard -z > /tmp/mc-freeze/untracked.z
    if [ -s /tmp/mc-freeze/untracked.z ]; then
      xargs -0 shasum -a 256 < /tmp/mc-freeze/untracked.z | shasum -a 256
    else
      printf 'no-untracked\n'
    fi
  } | shasum -a 256
}

prev="$(snapshot)"
stable=0
for _ in $(seq 1 20); do
  sleep 15
  cur="$(snapshot)"
  if [ "$cur" = "$prev" ]; then
    stable=$((stable + 1))
  else
    stable=1
    prev="$cur"
  fi
done

if [ "$stable" -lt 20 ]; then
  printf 'P0_BLOCKED: checkout changed during the quiescence window\n' > /tmp/mc-freeze/P0_BLOCKED
  git status --short > /tmp/mc-freeze/P0_BLOCKED.status
  git reflog -5 > /tmp/mc-freeze/P0_BLOCKED.reflog
  echo 'STOP: tree moved; no worktree created.'
  exit 3
fi
quiescent_until="$(date -u +%Y-%m-%dT%H:%M:%SZ)"
quiescent_snapshot="$prev"
```

If the gate fails, leave `/tmp/mc-freeze/P0_BLOCKED` for the owner, record the last status/reflog, and
stop. Do not create any worktree, do not overwrite a previous patch, and do not call the freeze
complete.

#### 3. Capture the freeze

```bash
freeze_sha="$(git rev-parse HEAD)"
freeze_branch="$(git branch --show-current)"
git diff --binary HEAD > /tmp/mc-freeze/tracked.patch

if [ -s /tmp/mc-freeze/tracked.patch ]; then
  diff_outcome=tracked-diff
  diff_sha="$(shasum -a 256 /tmp/mc-freeze/tracked.patch | awk '{print $1}')"
else
  diff_outcome=no-tracked-diff
  diff_sha=none
  rm -f /tmp/mc-freeze/tracked.patch
fi

printf 'freeze_sha=%s\nfreeze_branch=%s\nquiescent_until=%s\nquiescent_snapshot=%s\ndiff_outcome=%s\ndiff_sha=%s\ndotnet=%s\ndotnet_version=%s\n' \
  "$freeze_sha" "$freeze_branch" "$quiescent_until" "$quiescent_snapshot" "$diff_outcome" "$diff_sha" \
  "$DOTNET" "$("$DOTNET" --version)" > /tmp/mc-freeze/freeze.env

test "$(git rev-parse HEAD)" = "$freeze_sha"
test "$(snapshot)" = "$quiescent_snapshot"
```

`no-tracked-diff` is a normal, named outcome. It means there is no patch to apply; do not create an
empty patch, do not run `git apply --check` on it, and do not tell a worker to apply one.

Do not run `git apply --check` against this dirty capture tree: the patch is already applied here. A
worker checks it in its clean worktree before editing.

#### 4. Classify untracked paths

Bootstrap files are copied, not part of the in-flight patch. Noise is never copied. The remaining
untracked paths must be in-flight source, or P0 stops.

```bash
git ls-files --others --exclude-standard > /tmp/mc-freeze/untracked.all
grep -vE '^(\.run|\.tools|artifacts)/' /tmp/mc-freeze/untracked.all \
  | grep -vE '(^|/)(bin|obj)/' \
  | grep -vE '(^|/)\.env|(^|/)\.DS_Store$|^__MACOSX/|\.(zip|sha256|pdb|pfx|p12|patch|log)$' \
  | grep -vE '^docs/orchestration/|^docs/RELEASE_INVENTORY_20261030\.md$|^tools/rider/(db-maintenance|local-postgres|package-server)\.sh$|^tools/run-ios-simulator\.sh$' \
  > /tmp/mc-freeze/untracked.inflight || true
grep -vE '^(src|services|tests|docs)/' /tmp/mc-freeze/untracked.inflight \
  > /tmp/mc-freeze/untracked.unclassified || true

test ! -s /tmp/mc-freeze/untracked.unclassified
```

- **Bootstrap set** — copied to every worker/gate worktree, read-only, never staged, except the
  worker's own new checkpoint under `docs/orchestration/checkpoints/`:
  `docs/orchestration/**`, `docs/RELEASE_INVENTORY_20261030.md`,
  `tools/rider/db-maintenance.sh`, `tools/rider/local-postgres.sh`,
  `tools/rider/package-server.sh`, `tools/run-ios-simulator.sh`.
- **Noise** — never copied and never recorded as in-flight: `.run/`, `.tools/`, `artifacts/`, any
  `bin/` or `obj/`, `.env*`, `.DS_Store`, `__MACOSX`, package/checksum/PDB/signing files, patches,
  logs, and live manifests.
- **In-flight** — untracked paths under `src/`, `services/`, `tests/`, or `docs/` outside the
  bootstrap set. Copy each to the worktree of the single manifest owner **without staging it**; if a
  path does not map to exactly one owner, stop.

#### 5. Bootstrap every dispatched worktree

The bootstrap is copy-only. It does not stage, commit, or push. Snapshot the files once, then run
the generated script for every worker/gate worktree:

```bash
mkdir -p /tmp/mc-freeze/bootstrap/docs/orchestration /tmp/mc-freeze/bootstrap/tools/rider
cp -R docs/orchestration/. /tmp/mc-freeze/bootstrap/docs/orchestration/
cp docs/RELEASE_INVENTORY_20261030.md /tmp/mc-freeze/bootstrap/docs/
cp tools/rider/db-maintenance.sh tools/rider/local-postgres.sh tools/rider/package-server.sh \
  /tmp/mc-freeze/bootstrap/tools/rider/
cp tools/run-ios-simulator.sh /tmp/mc-freeze/bootstrap/tools/

cat > /tmp/mc-freeze/bootstrap-worktree.sh <<'BOOTSTRAP'
#!/usr/bin/env bash
set -euo pipefail
wt="${1:?usage: bootstrap-worktree.sh <worktree>}"
src=/tmp/mc-freeze/bootstrap
mkdir -p "$wt/docs/orchestration" "$wt/tools/rider"
cp -R "$src/docs/orchestration/." "$wt/docs/orchestration/"
cp "$src/docs/RELEASE_INVENTORY_20261030.md" "$wt/docs/"
cp "$src/tools/rider/"*.sh "$wt/tools/rider/"
cp "$src/tools/run-ios-simulator.sh" "$wt/tools/"
chmod +x "$wt"/tools/rider/*.sh "$wt/tools/run-ios-simulator.sh"
for f in docs/orchestration/README.md docs/RELEASE_INVENTORY_20261030.md \
  tools/rider/db-maintenance.sh tools/rider/local-postgres.sh tools/rider/package-server.sh \
  tools/run-ios-simulator.sh; do
  test -f "$wt/$f"
 done
BOOTSTRAP
chmod +x /tmp/mc-freeze/bootstrap-worktree.sh
```

No copied bootstrap file may be staged or committed. Only a worker's own checkpoint under
`docs/orchestration/checkpoints/` may be staged from that copy; code and test changes are added
normally. Run the script after creating `/tmp/mc-w2h` and `/tmp/mc-w4b`; do not run it after a worker
has started writing.

#### 6. Create the seven-worktree dispatch

P0 creates the four parallel worktrees. P2 creates integration; P2H creates `w2h`; P4 creates `w4b`.

| Worktree | Branch | Owner | Created |
| --- | --- | --- | --- |
| `/tmp/mc-w1` | `release/w1-db` | `W1` | P0 |
| `/tmp/mc-w2` | `release/w2-server` | `W2` | P0 |
| `/tmp/mc-w3` | `release/w3-client` | `W3` | P0 |
| `/tmp/mc-w4` | `release/w4a-architecture` | `W4A` | P0 |
| `/tmp/mc-integration` | `release/integration-20261030` | Orchestrator | P2 |
| `/tmp/mc-w2h` | `release/w2h-health` | `W2H` | P2H |
| `/tmp/mc-w4b` | `release/w4b-architecture` | `W4B` | P4 |

```text
W1 + W2 -> integration -> P2H(w2h) -> W3 + W4A -> integration
        -> P4(w4b) -> integration -> P5 -> P6
```

Create the four P0 worktrees, then bootstrap each one:

```bash
for wt in /tmp/mc-w1 /tmp/mc-w2 /tmp/mc-w3 /tmp/mc-w4; do
  test ! -e "$wt"
done
git worktree add /tmp/mc-w1 -b release/w1-db "$freeze_sha"
git worktree add /tmp/mc-w2 -b release/w2-server "$freeze_sha"
git worktree add /tmp/mc-w3 -b release/w3-client "$freeze_sha"
git worktree add /tmp/mc-w4 -b release/w4a-architecture "$freeze_sha"
for wt in /tmp/mc-w1 /tmp/mc-w2 /tmp/mc-w3 /tmp/mc-w4; do
  /tmp/mc-freeze/bootstrap-worktree.sh "$wt"
done
```

If a branch or path already exists, stop; do not delete another agent's tree. Record the freeze SHA,
`freeze.env`, classification files, seven-worktree map, branch names, and prompt paths in
`/tmp/mc-orchestrator-log.md`.

### Phase worktrees and merges

P2 and P3 run on the integration branch. P2H and P4 are not parallel scopes; each gets a worktree
from the current integration commit, and its commit is merged before the next phase:

- **P2H:** `/tmp/mc-w2h` on `release/w2h-health` from the P2 integration commit. Run
  `/tmp/mc-freeze/bootstrap-worktree.sh /tmp/mc-w2h` before dispatching. Worker 2 commits the checkpoint update
  (`phase_status.P2H`) and, only if the gate requires it, the already-owned
  `ServerReadinessSchemaTests.cs`. The orchestrator merges that commit into integration and then
  starts P3.
- **P4:** `/tmp/mc-w4b` on `release/w4b-architecture` from the P3 integration commit. Run
  `/tmp/mc-freeze/bootstrap-worktree.sh /tmp/mc-w4b` before dispatching. W4B commits its cleanup plus the separate
  W4B checkpoint block. The orchestrator merges `release/w4b-architecture` into integration, reruns
  the affected build/tests on the merged tree, and only then starts P5.

No other worker edits the integration branch. P5 and P6 read the merged integration branch, never a
worker or gate branch.

The two dependent phases are explicit:

- **P2H** exists because Worker 2's fresh-database readiness proof needs Worker 1's grant migration.
  It is owned by Worker 2, ordered after P2 (W1+W2 merged), and gates P3. It may not use manual SQL.
- **P4** exists because the 143-file namespace migration crosses Workers 1–3. It is owned by Worker 4
  under `W4B`, ordered after P3, and gates P5.

### P5 checklist — the only docs/package instructions

Use inventory §11 as the authority. Create `docs/RELEASE_STATE.md` as the only active
current-state/handoff file. It must contain the verified branch/commit/date, build/test counts with
commands, migration count/final ID/fresh-database status/fingerprint, artifact path/bytes/SHA-256,
manifest agreement, route count/smoke result, open blockers, and the exact next action.

Keep active: `AGENTS.md`; root `README.md`; `docs/README.md`; `SUBMISSION_READINESS.md` (fix 746 →
742); `BETA_PRODUCTION_READINESS_AUDIT_20261029.md`; `OWNER_RELEASE_GUIDE.md`; `WORK_CONTRACT.md`
(rewrite the obsolete GitLab lease/coordination section, keep the standing rules);
`BACKEND_HARDENING_TICKETS_20261030.md`; `evidence/**`; live infrastructure approvals/runbooks/specs;
`RELEASE_INVENTORY_20261030.md` as audit evidence.

Archive under `docs/archive/` with a "historical; superseded by RELEASE_STATE" header: `v1/ROADMAP.md`,
`v1/STATUS.md`, `BETA_EXECUTION_CHECKPOINT.md`, beta `CURRENT_CHECKPOINT.md`, `TASK_LEDGER.md`,
`RELEASE_READINESS.md`, `infra/checkpoints/**`, `infra/AWS_BACKEND_REPAIR_*`,
`workout-migration-checkpoints/**`, dated checkpoints, `BETA_ORCHESTRATOR_PROMPT.md`. For every file
in inventory §11 not named here, apply its Action exactly; do not leave a second active state, lease,
or checkpoint.

Delete only obsolete coordination files: `beta-orchestration/STATE.json`,
`beta-orchestration/RESUME_PROMPT.md`, duplicate active checkpoints, empty directories. Never delete
`STAGING_SCHEMA_CHANGE_APPROVAL_20261030.md`, `AWS_STAGING_REPAIR_AND_FALLBACKS_20261030.md`,
`evidence/**`, or an owner approval.

Update `docs/README.md` links. If `ROADMAP.md`/`STATUS.md` are archived, change only the
state-continuity line in `AGENTS.md` to point to `docs/RELEASE_STATE.md`; do not alter architectural
or privacy rules. Correct the active false claims in inventory §8 (742 not 746; iOS signing not
`NETSDK1147`; exact counts; deployed vs. not deployed; one lease/state). Do not rewrite historical
documents; label them historical.

For the artifact, inspect `artifacts/` first. Move any live reconciliation manifest, owner-approval
record, or database fingerprint to `/tmp/mc-live-evidence/` outside the repo; do not print, commit,
or package it. Then clear only build output and old package files:

```bash
rm -rf services/MuscleCuties.Server/bin services/MuscleCuties.Server/obj
mkdir -p artifacts
rm -f artifacts/*.zip artifacts/*.sha256 artifacts/build-manifest.json artifacts/.DS_Store
tools/rider/package-server.sh
```

Do not use a blanket `rm -rf artifacts` that destroys a live approval record. Regenerate
`artifacts/MuscleCuties.Server.zip.sha256` and `artifacts/build-manifest.json` from the same bytes.
Reject the artifact if `unzip -l` shows any `SnapshotRestore.Registry.dll` or DLL outside the required
allowlist, any `*.pdb`, `appsettings.Development.json`,
`MuscleCuties.Server.staticwebassets.endpoints.json`, `.DS_Store`/`__MACOSX`, or a live reconciliation
manifest. Require `libSkiaSharp.so` (ARM64) and the embedded catalog. Run
`tools/deployment/validate-package.sh`, `verify-arm64-package.sh`, `verify-runtime.sh`, and the Lambda
handler smoke test against a disposable database. Exactly one `.zip` remains; sidecar checksum and
manifest files are allowed. `W2`'s candidate in `/tmp` is evidence, not the final artifact.

## 5. Handoff checkpoint format

Every worker writes one Markdown checkpoint at the path in the manifest. It travels on the worker
branch and is merged with the code. A worker without a checkpoint is not done.

```markdown
---
worker: 01-database
scope_ref: W1
base_commit: <40-char sha>
branch: release/w1-db
worktree: /tmp/mc-w1
phase_status:
  P1: ready-for-merge
inventory_items: [4.1, 9]
files_touched:
  - path
proof:
  build: pass | fail | not-run
  tests: "989/989 passed"
  database: "fresh=compatible; migrations=<count>; fingerprint=<sha>"
---

# Worker 1 checkpoint

## Delivered
What changed, why, and which inventory item it closes.

## Evidence log
Exact command, working directory, exit status, and observed result. Quote counts, hashes, and
fingerprints. Do not write "all tests pass" without the command and count.

## Interface changes
Any type, route, migration, package, or contract another worker/reviewer must know. If none, say
"none".

## Deleted
File → reason → proof that no production caller remains. Include `grep` output or a test.

## Not done / blockers
Anything outside scope, any dependency on another worker, any check that could not run. A blocked
item is not a pass.

## Next action
The single next thing the orchestrator should do with this branch.
```

`phase_status` is a map, not one scalar:

- `W1`, `W3`: `P1: ready-for-merge | blocked`.
- `W2`: `P1` plus `P2H: not-run | pass | fail`. After P2H, `P2H` is the only status the orchestrator
  reads for acceptance; P3 does not start while it is `not-run` or `fail`.
- `W4`: **two separate entries in one frontmatter**, `scope_ref: [W4A, W4B]`:
  `W4A: ready-for-merge | blocked` and `W4B: not-started | ready-for-merge | blocked`. `W4B` stays
  `not-started` until P4 entry. Never copy the W4A result into W4B, and never report a blocked W4A
  as a blocked W4B.
- `ORG`: `P5: pass | blocked`.
- `REV`: `P6: pass | rejected`.

Merge criteria: diff limited to the manifest paths plus checkpoint; no generated output; no secrets;
no skipped tests; proof commands recorded; interface changes explicit; the relevant
`phase_status` is `ready-for-merge`/`pass`. `blocked` is a valid handoff, never completion.

## 6. What the reviewer re-verifies

The reviewer does not trust a checkpoint. It repeats these gates on the merged tree. The detailed
commands are in [`05_REVIEWER.md`](05_REVIEWER.md); this is the index summary.

| Gate | Minimum independent check |
| --- | --- |
| Build | Core, UseCases, Infrastructure, PostgreSql, Server, UI (iOS target; compile-only is acceptable only if signing blocks). |
| Tests | Core, Server, Registration (unit + integration), Mobile contracts, Deployment. Record exact counts. |
| Database | Fresh PG17, `migrate` only, `schema-preflight = compatible`; no candidate-SQL application in tests; previous-head → new-head upgrade; repeated migrate is a no-op; RLS/tenant negative; `schema-reconcile` fails closed. |
| Schema contract | `SchemaRequirements` count/final ID/privileges match source; `roles-and-grants.sql` includes the two calendar tables; no missing grant hidden by readiness. |
| Lambda | Exactly one ZIP; allowlist/denylist contents; hash/size/manifest agree; ARM64; embedded catalog; no PDB/dev config/SnapshotRestore/`.DS_Store`; handler and every route smoke-tested. |
| Health | `/health/ready` fails on a disposable database with a revoked grant, not just a ledger mismatch. |
| Client/server parity | Every client route has a server route; one `ActiveWorkoutSessionSnapshot`; no manual JSON mapper; no client meal repair; no retired preferences PUT; no dead offline cache path. |
| Dead code | No references to deleted types; no legacy planner/model stack; no ghost `src/MuscleCuties.App`; no dead DI registrations. |
| Architecture | Core has no outer references; UseCases only Core; UI no `DbContext`/`IRepository`; zero `Core.*`/`UseCases.*` namespaces outside their projects; guard passes and is neither allowlisted nor skipped. |
| Tests deleted | Only tests for behaviour that no longer exists. No new `Skip=`, no weakened assertions, no deleted live coverage. |
| Docs | One active state/checkpoint; historical docs archived; active claims match code (742, counts, iOS signing, deployment status); no competing `STATE.json`/`RESUME_PROMPT`. |
| Hygiene | No pushes, no live deployments, no secrets, no live manifests, no candidate ZIPs, no generated files, no ghost directories. |

The reviewer also verifies that `W4A` and `W4B` have distinct checkpoint statuses and that `W2H` is
`pass`, not inferred from the W2 build. Any failed gate is a blocking finding.

## 7. What must not be touched yet

- **Checkpoint/document cleanup** — no worker edits existing docs. Only `ORG` does this in P5, after
  all worker checkpoints are in.
- **The final single-artifact packaging decision** — `W2` proves a candidate in `/tmp`; it does not
  replace or delete `artifacts/`. `ORG` makes the one-artifact decision in P5.
- **Live AWS, RDS, S3, Resend, Apple, TestFlight, secrets, or a real database** — none are in scope.
- **Evidence of a live change or owner approval** — never delete or rewrite it.
- **A 143-file big-bang namespace rename** — `W4B` runs the ten numbered batches B0–B9 in
  `04_ARCHITECTURE.md` §B, building after each. `W4A` must not start the pass.
- **Any test suppression** — no `Skip`, no weakened assertion, no "temporarily" deleted live coverage.

## 8. Definition of done for the whole orchestration

1. `W1`, `W2`, `W3`, `W4A` checkpoints are merged; `W2H` is `pass`; `W4B` is `ready-for-merge`.
2. The integration build is green and the full test suite has real counts.
3. A fresh database migrated only from source is `compatible`; no manual SQL is part of the pass.
4. Exactly one `artifacts/MuscleCuties.Server.zip` exists, matches its manifest, and contains no
   strays; its functions are smoke-tested.
5. The active docs contain one current state and no claim the code does not support.
6. The fifth agent's independent report says `PASS`. Until then, the release is not ready.

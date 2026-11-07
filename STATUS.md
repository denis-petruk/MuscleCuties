# Status — MuscleCuties

Updated 2026-11-07. This is the compact continuation checkpoint. Dated audits, the gate register and the beta policy remain evidence at their own revision; this file reports only what was verified against the current tree.

## Candidate

- Rider HEAD `5054f6f3` on `main` (ahead 1 / behind 1 `origin/main`).
- The first-beta package work (B1–B5) is present in the working tree and **uncommitted**. Nothing in this task was committed, pushed, deployed, migrated on a shared target, signed, archived or uploaded.
- The backend release target is **Beta only**. No separate Staging API or database is provisioned, and no workflow offers one as an option.
- GitHub is the integration target; GitLab is read-only historical evidence. `tools/deployment/release.sh` and `release-engine.py` are the retired GitLab path.

## First-beta packages

| Package | State | Verification actually run |
| --- | --- | --- |
| **B1 Provisionable schema** | READY FOR REVIEW | A brand-new `initdb` PostgreSQL 16 cluster with no roles: `migrate` created the 10 `musclecuties*` roles, applied baseline `000_model_baseline.sql` plus `001`–`004` (5 ledger rows, `schema_version=4`); `seed` loaded 746 manifest rows; `dbcheck` returned `compatible` with 69 model tables, 46 RLS tables, 51 policies and zero failure codes. A second `migrate` reported `migrations_applied=0`; the second `dbcheck` returned the identical fingerprint `028eacf7…d861`. The disposable `dbinit` path converged on the same `schema_version=4` and fingerprint with 104 catalog exercises. |
| **B2 Honest release** | READY FOR REVIEW | 4 offline deploy tests pass (both digests verified; a digest mismatch and a missing alias cannot report `verified`; alias rollback restores version `6` and its digest on both functions). 11 legacy release-engine tests pass. Workflow YAML parses. Rollback is a first-class workflow mode fed by a prior run's report. |
| **B3 Real training capture** | READY FOR REVIEW | Core 1,222 / Mobile Contracts 109 / Registration 44 tests pass, including observed-set capture, progression-from-actual-sets, archive provenance and the migration chain contract. |
| **B4 Recovery and preview** | READY FOR REVIEW | Core tests cover all-prerequisite disclosure (new test) and the HIIT safer-review gate. iOS simulator compile of `net10.0-ios` succeeds with 0 warnings / 0 errors, validating the new XAML and bindings. |
| **B5 Consistency and docs** | READY FOR REVIEW | `PRODUCT_WORKFLOWS.md`, `STATUS.md`, `ROADMAP.md` and `CONTROL.md` reconciled; fasting and exercise media recorded as deferred; no image/video placeholder exists in the workout session UI. |

## Gate ledger

| Gate | Status | Evidence or open item |
| --- | --- | --- |
| G0 Baseline and scope | PASS | Candidate pinned at `5054f6f3`; package paths reserved in `CONTROL.md`. |
| G1 Architecture integrity | PASS | Core builds with 0 warnings; adapters unchanged in dependency direction. |
| G2 Domain safety | PASS | 1,222 Core tests, including coverage, dose authority, fatigue safeguard, pain handling and multi-reason limitation disclosure. |
| G3 Persistence | PASS (local) | Empty-DB `migrate` + `seed` + `dbcheck` rehearsed; `EnsureCreated` remains disposable-only. **Blocked:** isolated Staging rehearsal. |
| G4 Backend/API | NOT RE-RUN | No live endpoint call was made in this task. Deploy tooling is offline-verified only. |
| G5 Mobile UI | PARTIAL | iOS simulator compile passes; no device run, no screenshot review, no EN/RU human review. |
| G6 Performance | NOT RE-RUN | No measurement was taken in this task. |
| G7 Privacy/security | PASS (schema) | Roles, RLS and policies verified by `dbcheck`; no secret material was logged or written. |
| G8 Build/regression | PASS (run suites) | Core 1,222, Mobile Contracts 109, Registration 44, deploy 4 + 11; iOS simulator build 0/0. Budget/latency server filter not run. |
| G9 Documentation/handoff | PASS | Docs reconciled to the tree; deferred surfaces stated as deferred. |

## Not verified in this task

- Signed iOS archive, TestFlight upload and any device acceptance (QA01).
- Any live Staging or Beta deployment, negative-path deploy, or live rollback.
- The empty-DB migration rehearsal on a real isolated Staging target.
- Visual inspection of the new preview summary, rationale and timed-phase rows; EN/RU editorial review.
- Server latency/cold-start budget tests.

## Next action

Owner review of the five packages, then the separately authorized external gates: signed archive, device acceptance, isolated Staging qualification, and approval tied to source SHA, artifact digest, schema version, environment and rollback plan.

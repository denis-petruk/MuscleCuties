# Status — MuscleCuties

Updated 2026-11-08. This is the compact continuation checkpoint. Dated audits, the gate register and the beta policy remain evidence at their own revision; this file reports only what was verified against the current tree.

## Candidate

- `main` at `4ee9b4f2`, the merge of PR #39 (docs finalization); the B1–B5 package work landed in PR #38 at `fa1e6165`, and the local checkout tracks `origin/main`.
- The first-beta package work (B1–B5) is merged and CI-verified on GitHub. Nothing was deployed, migrated on a shared target, signed, archived or uploaded; those stay owner-authorized gates.
- The backend release target is **Beta only**. No separate Staging API or database is provisioned, no workflow offers a Staging target (the mobile release workflow builds `beta` only), and the client carries no Staging endpoint or allowlisted Staging host.
- GitHub is the integration target; GitLab is read-only historical evidence. The retired GitLab-era release scripts (`release.sh`, `release-engine.py`) and `.gitlab-ci.yml` were removed; they remain in repository history.

## First-beta packages

| Package | State | Verification actually run |
| --- | --- | --- |
| **B1 Provisionable schema** | READY FOR REVIEW | A brand-new `initdb` PostgreSQL 16 cluster with no roles: `migrate` created the 10 `musclecuties*` roles, applied baseline `000_model_baseline.sql` plus `001`–`005` (6 ledger rows, `schema_version=5`); `seed` loaded 746 manifest rows; `dbcheck` returned `compatible` with 69 model tables, 46 RLS tables, 51 policies and zero failure codes. A second, independent empty database rehearsed from zero produced the identical `schema_version=5` and fingerprint `ea4c7a60…a2b9`; a repeat `migrate` on the first reported `migrations_applied=0`. The disposable `dbinit` path converged on the same `schema_version=5` and fingerprint with 104 catalog exercises. Migration numbering was reconciled with `origin/main`: `004_session_distance.sql` keeps version 4 and the observed-set actuals script became `005_observed_set_actuals.sql`. |
| **B2 Honest release** | READY FOR REVIEW | 4 offline deploy tests pass (both digests verified; a digest mismatch and a missing alias cannot report `verified`; alias rollback restores version `6` and its digest on both functions). 11 legacy release-engine tests pass. Workflow YAML parses. Rollback is a first-class workflow mode fed by a prior run's report. Mobile release offers `beta` only; no Staging job, endpoint or allowlisted host remains. |
| **B3 Real training capture** | READY FOR REVIEW | Core 1,247 / Mobile Contracts 147 / Registration 46 tests pass, including observed-set capture, per-set session UI capture with RIR and technique, progression-from-actual-sets, archive provenance and the 6-script migration chain contract. |
| **B4 Recovery and preview** | READY FOR REVIEW | Core tests cover all-prerequisite disclosure, multi-reason limitation disclosure and the HIIT safer-review gate; 13 rendered-surface tests prove the preview rationale row appears only with real rationale content. iOS simulator compile of `net10.0-ios` succeeds, validating the new XAML and bindings. |
| **B5 Consistency and docs** | READY FOR REVIEW | `PRODUCT_WORKFLOWS.md`, `STATUS.md`, `ROADMAP.md` and `CONTROL.md` reconciled; fasting and exercise media recorded as deferred; no image/video placeholder exists in the workout session UI. |

## Gate ledger

| Gate | Status | Evidence or open item |
| --- | --- | --- |
| G0 Baseline and scope | PASS | Candidate pinned at `5054f6f3`; package paths reserved in `CONTROL.md`. |
| G1 Architecture integrity | PASS | Core builds with 0 warnings; adapters unchanged in dependency direction. |
| G2 Domain safety | PASS | 1,247 Core tests, including coverage, dose authority, fatigue safeguard, pain handling and multi-reason limitation disclosure. |
| G3 Persistence | PASS (local) | Empty-DB `migrate` + `seed` + `dbcheck` rehearsed; `EnsureCreated` remains disposable-only. **Blocked:** isolated Staging rehearsal. |
| G4 Backend/API | NOT RE-RUN | No live endpoint call was made in this task. Deploy tooling is offline-verified only. |
| G5 Mobile UI | PARTIAL | iOS simulator compile passes; no device run, no screenshot review, no EN/RU human review. |
| G6 Performance | NOT RE-RUN | No measurement was taken in this task. |
| G7 Privacy/security | PASS (schema) | Roles, RLS and policies verified by `dbcheck`; no secret material was logged or written. |
| G8 Build/regression | PASS (run suites) | Core 1,247, Mobile Contracts 147, Registration 46, deploy 4 + 11; iOS simulator build 0/0. Budget/latency server filter not run. |
| G9 Documentation/handoff | PASS | Docs reconciled to the tree; deferred surfaces stated as deferred. |

## Not verified in this task

- Signed iOS archive, TestFlight upload and any device acceptance (QA01).
- Any live Staging or Beta deployment, negative-path deploy, or live rollback.
- The empty-DB migration rehearsal on a real isolated Staging target.
- Visual inspection of the new preview summary, rationale and timed-phase rows; EN/RU editorial review.
- Server latency/cold-start budget tests.

## Next action

The [closed-beta audit](docs/release-audit/2026-11-08/CLOSED_BETA_AUDIT_AND_PLAN.md) found 9 P0 items, including a live API that serves `$LATEST` with no aliases, 1-day database backups and bodyweight plans without squat, lunge or pull work. Phase 0 split the uncommitted working tree into six local branches listed in the [decision record](docs/release-audit/2026-11-08/DECISIONS.md). Next: owner decisions OD-1–OD-8, review of those branches, then the plan's Phase 1 (Beta environment) and Phase 2 (app blockers) under separate authorization. The external gates (signed archive, device acceptance, approval tied to source SHA, artifact digest, schema version, environment and rollback plan) are unchanged.

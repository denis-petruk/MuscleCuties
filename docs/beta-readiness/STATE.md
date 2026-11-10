# State checkpoint — 2026-11-09

## integration/beta-polish-20261109-v2 (this run)

- Branch `integration/beta-polish-20261109-v2` from `origin/main` = `a99fd80c` (PR #46 merge), in
  `.agent-worktrees/beta-polish-20261109`. The parent checkout's uncommitted delta was not touched. Reviewer-facing
  summary: [REVIEW_PACKET.md](REVIEW_PACKET.md).
- **Stale branch removed:** the earlier `integration/beta-polish-20261109` branch is gone from GitHub; its local ref
  was removed. It held `4ee9b4f`, the session checkout's snapshot, and failed run 42673683274. Do not recreate it or
  open a PR from it; the reviewed commits live on `-v2`.
- Commits: `c3f050a` E-04 footer validation; `7718179` session empty-state gate (E-07); `ced0478` intensity label +
  Today phase copy localization (E-06, E-05 copy half); `b649a54` alarm SNS subscription (A11 repo half); `82b33c4`
  audit/readiness refresh; `516ee75` featured-card and count-tile localization (U-09/U-10) plus
  `scripts/beta-release-wizard.sh`; `d269c0b` localization corrections + owner wizard; `d9974e1` dedicated branch
  publish; `bbb791c` CI evidence; `ed9a23e` G11 CI evidence; `d81519c` dated simulator acceptance record. The
  `REVIEW_PACKET.md` commit is `47f045f`; a final docs-only correction sits on top.
- Verified in this environment: non-UI projects build with the MSBuild recipe; Core 1325/1325, Mobile Contracts
  183/183, Registration 54/54, Deployment policy tests 17/17 — all 0 failed, 0 skipped. `Core.Tests` still emits 4
  xUnit analyzer warnings (T-21). `LocalizationCatalogTests` 8/8 after the U-09/U-10 keys; a full EN/RU scan is
  2036/2036 keys with 0 missing, 0 extra and 0 format-argument mismatches. Edited XAML parses as XML;
  `observability.yaml` parses with CloudFormation tags; the wizard passes `bash -n`.
- **CI**: run [42686088233](https://github.com/denis-petruk/MuscleCuties/actions/runs/42686088233) at `47f045f`
  completed successfully; `verify_source`, `verify_plan_generation`, `verify_ios_compile` (xcode-27),
  `postgres_verify / verify_postgres_compatibility` and `required` are present and the branch badge reads `passing`.
  The `CI` workflow triggers on `integration/**` pushes, so the final docs-only correction is covered; see the live
  branch run list.
- **BLOCKED here**: simulator/device journeys (Linux, no Xcode). Attempting the simulator build fails with
  `NETSDK1147` (no workloads; no `xcrun`/`simctl`/`xcodebuild`), so acceptance is recorded as NOT RUN in
  [SIMULATOR_ACCEPTANCE_2026-11-09.md](../release-audit/2026-11-09/SIMULATOR_ACCEPTANCE_2026-11-09.md) rather than
  inferred. Also blocked: only a local PostgreSQL run (no PG16/root), since CI covers the gate; live AWS (not
  authorised). `gh` is unauthenticated, but the public run and commit pages are readable.
- The stale `integration/beta-polish-20261109` branch has been removed. It had failed run
  [42673683274](https://github.com/denis-petruk/MuscleCuties/actions/runs/42673683274) on `4ee9b4f`; it was unrelated to
  this work, has no PR, and must not be recreated.
- Owner decisions requested in `docs/release-audit/2026-11-09/WORKOUT_POLICY_RESEARCH.md`: S-03 load step vs set-only,
  decision 5 cycle dose (recommend amend to symptoms/readiness only), S-04 pain surface, S-05 seven-day window.
- Next action: the owner opens the PR from `-v2` (none exists) using REVIEW_PACKET.md, then runs wizard stage 3 on a
  Mac and folds the dated record into G12; S-03/S-04/S-05 and the live AWS items remain owner decisions.

---

# State checkpoint — 2026-11-09 (PR #46 run)

- Branch `integration/beta-rc-20261108`, base `main` `4ee9b4f2`; merges the six Phase 0 branches (PRs #40–#45) and
  four worker branches (W2 safety/dose, W3 planning UX/client, W4 CI/cleanup, W5 engine/stores) plus orchestrator
  fixes (`95b3785b`, `7aa53789`, `bbc27ada`, `438de156`, `3f45639e`).
- Main checkout still holds the original uncommitted 2026-11-08 delta; it is fully contained in this branch and was
  not modified.
- Local verification recipe: disposable PostgreSQL 16 on `127.0.0.1:55461`; local API on `127.0.0.1:5080`
  (Development, generated per-run keys, development email-code log delivery). Integration env vars as in
  `.github/workflows/db-verify.yml`, pointed at that cluster.
- 2026-11-09 (integration authority): `8162bfff` moves both Apple jobs from `macos-15` to the `xcode-27` runner (the
  `maui-ios` workload needs Xcode 27.1; `macos-15` has 16.4), `0f90627b` removes the CS0414 dead field, and `2497bb9a`
  records the real reasons the swap/reschedule guards were skipped (all three now run after `fe3f2356`/`5d7477ae`).
- Open: S-03, S-04, S-05, E-04…E-07, live AWS A1–A14, simulator journeys 4 and 6–15, device QA01.
- 2026-11-09: CI is green at the final code head `5ce8c077` — `gh pr checks 46` reports 10 successful, 0 failing,
  0 pending on both push and PR (`postgres_verify` 4m32s push / 4m54s PR, `verify_ios_compile` 3m6s / 1m37s on
  `xcode-27`, `required` green). The first green run was `dcb43f79`.
- 2026-11-09 `fe3f2356` fixes G-07: `catalog.workout_exercise_definitions.source_exercise_id` is now persisted
  (migration 006, nullable unique, 59/60 rows mapped to planning ids 101–182) and written by the reviewed catalog
  import. `GetSwapCandidatesAsync` and `SwapAsync` translate the day exercise's `ENGINE_<planningId>` code through it
  into the reviewed definition id space; candidates and saved preferences stay in definition ids (their foreign keys)
  and the target catalog row resolves by the derived `ENGINE_` code alone. Evidence: migration chain contract 7/7;
  model schema 4/4; planning fingerprint inputs unchanged.
- 2026-11-09 `5d7477ae` fixes the reschedule half of the same id-space defect: `exercise_muscle_contributions` holds
  reviewed definition ids, but `ValidateRescheduledWindowAsync` filtered them by the planning id, so every strength
  reschedule was rejected with `ScheduleStrengthMuscleMetadataMissing`. It now translates through `SourceExerciseId`,
  and the third guard is unskipped: `WorkoutSwapAndRescheduleSafetyTests` 3 passed, 0 skipped.
- 2026-11-09 `c97254b7` fixes E-03 (username auto-capitalization) and the `net10.0-ios` simulator build succeeds. Full
  integration suite at this commit: 99 passed, 3 failed, 0 skipped of 102; the failures are fixture-name guards
  (`mc_test_nutrition_*`, `mc_retry_disposable`, missing `MC_PG09_LOAD_POSTGRES`), not product assertions.
- 2026-11-09 `a4078485` closes S-07 (deload plan marker, migration 007), G-10/G-11 (one active offer per plan,
  store-level promotion coverage), the two DOB paths and C2/C3 (server-owned rest copy, no client fallback, RU
  mappings). Integration 102 passed / 3 fixture guards / 0 skipped; registration 54/54; iOS simulator build exit 0;
  schema head 7 with fingerprint `25ff237d…8c982c4b`.
- 2026-11-09 `7ca46512` removes the duplicate runtime localization key the C2/C3 copy change introduced (the
  "Recovery day" entry already existed), the only defect the re-run gates surfaced after `a4078485`.
  `LocalizationCatalogTests` 8/8, `BetaPlanningCopyContractTests` 10/10; docs-only commits follow at this code head.
- 2026-11-09 full integration suite at `c7b3a1ff` with all five CI fixture variables on PostgreSQL 16.15:
  **105 passed, 0 failed, 0 skipped**. The former 102/3 result was fixture provisioning, not product code:
  `mc_test_nutrition_ci`, `mc_retry_disposable` and `mc_pg09_disposable` (owner `mc_ci_migrator`, NOSUPERUSER
  NOBYPASSRLS) plus `MC_SCHEMA_COMPATIBILITY_POSTGRES`, mirroring `.github/workflows/db-verify.yml`. Proofs re-ran
  in the same session: S-07/G-10/G-11 integration classes 4/4, DOB 2/2, localization 8/8, copy contract 10/10.
- Next action: owner review of PR #46 and the owner-only actions in RELEASE_READINESS.md.

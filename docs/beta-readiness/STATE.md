# State checkpoint — 2026-11-09

## integration/beta-polish-20261109 (this run)

- Branch `integration/beta-polish-20261109` from `origin/main` = `a99fd80c` (PR #46 merge), in
  `.agent-worktrees/beta-polish-20261109`. The parent checkout's uncommitted delta was not touched.
- Commits: `ecf556a` E-04 footer validation; `17e5be0` session empty-state gate (E-07); `afb459a` intensity label +
  Today phase copy localization (E-06, E-05 copy half); `4cc0ddb` alarm SNS subscription (A11 repo half); `4ad99a2`
  audit/readiness refresh; `5a752d8` featured-card and count-tile localization (U-09/U-10) plus
  `scripts/beta-release-wizard.sh`; docs update follows this file.
- Verified in this environment: non-UI projects build with the MSBuild recipe; Core 1325/1325, Mobile Contracts
  183/183, Registration 54/54, Deployment policy tests 17/17 — all 0 failed, 0 skipped. `Core.Tests` still emits 4
  xUnit analyzer warnings (T-21). `LocalizationCatalogTests` 8/8 after the U-09/U-10 keys; a full EN/RU scan is
  2036/2036 keys with 0 missing, 0 extra and 0 format-argument mismatches. Edited XAML parses as XML;
  `observability.yaml` parses with CloudFormation tags; the wizard passes `bash -n`.
- **BLOCKED here**: iOS build and simulator journeys (Linux, no Xcode); PostgreSQL integration suite (no PG16 and no
  root); `gh` (sandbox TLS); live AWS (not authorised). CI on this branch has not run yet.
- Owner decisions requested in `docs/release-audit/2026-11-09/WORKOUT_POLICY_RESEARCH.md`: S-03 load step vs set-only,
  decision 5 cycle dose (recommend amend to symptoms/readiness only), S-04 pain surface, S-05 seven-day window.
- Next action: push, read CI, then build the branch on the simulator and re-check E-04…E-07 plus journeys 4, 6–15.

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

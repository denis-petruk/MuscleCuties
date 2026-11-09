# State checkpoint — 2026-11-09

- Branch `integration/beta-rc-20261108`, base `main` `4ee9b4f2`; merges the six Phase 0 branches (PRs #40–#45) and
  four worker branches (W2 safety/dose, W3 planning UX/client, W4 CI/cleanup, W5 engine/stores) plus orchestrator
  fixes (`bbecb7c6`, `184d1b11`, `dc72cd42`, `f53918bb`, `4f1461a5`).
- Main checkout still holds the original uncommitted 2026-11-08 delta; it is fully contained in this branch and was
  not modified.
- Local verification recipe: disposable PostgreSQL 16 on `127.0.0.1:55461`; local API on `127.0.0.1:5080`
  (Development, generated per-run keys, development email-code log delivery). Integration env vars as in
  `.github/workflows/db-verify.yml`, pointed at that cluster.
- 2026-11-09 (integration authority): `6cce5fc8` moves both Apple jobs from `macos-15` to the `xcode-27` runner (the
  `maui-ios` workload needs Xcode 27.1; `macos-15` has 16.4), `da93d6f9` removes the CS0414 dead field, and `a6eaa76b`
  records the real reasons the swap/reschedule guards stay skipped.
- Open: S-03, S-04, S-05, S-07, G-10, G-11, E-03…E-07, live AWS A1–A14, simulator journeys 4 and 6–15, device QA01.
- Newly proven defect: the swap request id space is ambiguous — `SwapAsync` matches
  `WorkoutExerciseDefinitions.Id` but resolves the catalog as `ENGINE_<id>`, so planning ids 141/142 cannot be reached.
  This blocks DB-level verification of G-06/G-07 and needs a decision on the contract before the guards can be un-skipped.
- Next action: confirm the `verify_ios_compile` re-run is green, then resolve the swap id-space contract, then owner review
  of the PR and the owner actions in RELEASE_READINESS.md.

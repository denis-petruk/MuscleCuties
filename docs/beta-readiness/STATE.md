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
- 2026-11-09: CI is green at `39053ecd` — `gh pr checks 46` reports 10 successful, 0 failing, 0 pending on both push
  and PR, including `verify_ios_compile` (3m28s push / 2m47s PR) on the `xcode-27` runner.
- Corrected swap diagnosis (AUDIT G-07 is REOPENED, not FIXED): definition ids are 1..60 with URN seed codes, and the
  reviewed importer already translates review source ids 141/142 into those definition ids via `databaseIdBySourceId`.
  `GetSwapCandidatesAsync` instead compares the day exercise's *source* id with `WorkoutExerciseDefinitions.Id`, so the
  candidate list is empty for every real plan; `SwapAsync` also keeps an `e.Id == request.NewExerciseId` fallback that
  can select a different catalog row (definition 31 → `ENGINE_141` Pull-Up). A persisted source-id mapping is a schema
  decision that must precede un-skipping the two guards.
- Next action: handoff handed to Claude in `docs/beta-readiness/HANDOFF-2026-11-09.md`; first item there is the swap
  contract decision, then the reschedule metadata seed, then owner review of PR #46.

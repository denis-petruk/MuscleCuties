# State checkpoint — 2026-11-09

- Branch `integration/beta-rc-20261108`, base `main` `4ee9b4f2`; merges the six Phase 0 branches (PRs #40–#45) and
  four worker branches (W2 safety/dose, W3 planning UX/client, W4 CI/cleanup, W5 engine/stores) plus orchestrator
  fixes (`bbecb7c6`, `184d1b11`, `dc72cd42`, `f53918bb`, `4f1461a5`).
- Main checkout still holds the original uncommitted 2026-11-08 delta; it is fully contained in this branch and was
  not modified.
- Local verification recipe: disposable PostgreSQL 16 on `127.0.0.1:55461`; local API on `127.0.0.1:5080`
  (Development, generated per-run keys, development email-code log delivery). Integration env vars as in
  `.github/workflows/db-verify.yml`, pointed at that cluster.
- Open: S-03, S-04, S-05, S-07, G-10, G-11, E-03…E-07, live AWS A1–A14, simulator journeys 4 and 6–15, device QA01.
- Next action: owner review of the PR, then the owner actions in RELEASE_READINESS.md.

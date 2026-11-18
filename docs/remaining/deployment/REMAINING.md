# Remaining Work — Deployment, Git & Release (`Gate 95`)

## Completed (`8dbbb388` / `STATUS.md`)
- [x] **Gate 95 Shared Staging/Beta Reset & Lambda Deployment (Owner-Authorized 2026-11-16):** Encrypted RDS snapshot `mc-g95-staging-pre-reset-20261116` and private S3 backup `mc-g95-staging-backup-028417007003` (`backup-20261116/`) retained; shared `musclecuties_staging` reset to schema 11 (`781` seeded rows, `0` users, `232` object grants restored); API `beta` alias (v15) and reconciliation worker `beta` alias (v2) deployed with matching SHA256 digests and `/health/live` + `/health/ready` returning HTTP 200; `Production` excluded.
- [x] **PR #50 opened:** `https://github.com/denis-petruk/MuscleCuties/pull/50` (`fix/qa-audit-followups`).

## Still Awaiting
- [ ] **Retain recovery backups** (`mc-g95-staging-pre-reset-20261116` & `mc-g95-staging-backup-028417007003`) until Beta sign-off; update future reset preflight scripts to preserve the 232 object grants automatically.
- [ ] **Commit doc updates & merge PR #50** into protected `main` once CI and UX acceptance checks are green.
- [ ] **App Store / TestFlight Release (Owner-Only):** TestFlight build upload and App Store Connect release actions (owner-only per [`AGENTS.md`](../../../AGENTS.md)).

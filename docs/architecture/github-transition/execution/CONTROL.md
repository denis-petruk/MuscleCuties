# Execution control ledger

Updated 2026-11-07 against Rider HEAD `5054f6f3` with the first-beta package work uncommitted in the working tree. This ledger records package lifecycle and reservations only. It is not release evidence and it does not authorize deployment, migration, signing or distribution.

## Authority and reservations

| Role | Owner | Notes |
| --- | --- | --- |
| Integration authority | Repository owner | Owns root AGENTS, shared DTOs, canonical docs, release manifests and migration numbering. |
| Package B1 (schema) | Platform B | Reserved paths: `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/**`, `src/MuscleCuties.DbMaintenance/**`, `.github/workflows/db-*.yml`. |
| Package B2 (release) | Platform B | Reserved paths: `tools/deployment/**`, `.github/workflows/backend-*.yml`. |
| Package B3 (capture) | Product A | Reserved paths: workout session contracts, store, session UI/VM and Core progression. |
| Package B4 (recovery/preview) | Product A | Reserved paths: `StrengthPlanPreviewRule`, strength generation page/VM/display layer, localization. |
| Package B5 (consistency/docs) | Integration authority | Reserved paths: `STATUS.md`, `ROADMAP.md`, `docs/PRODUCT_WORKFLOWS.md`, this ledger. |

One package has one accountable owner. No two packages edit the same shared contract or allocate the same migration number concurrently.

## Package lifecycle

| Package | Base | Status | Local verification | Blocking external gate |
| --- | --- | --- | --- | --- |
| B1 — Provisionable schema | `5054f6f3` | READY FOR REVIEW | Brand-new `initdb` cluster with no roles: `migrate` created 10 `musclecuties*` roles and applied baseline + 001–005 (6 ledger rows, `schema_version=5`), `seed` loaded 746 manifest rows, `dbcheck` compatible with 69 model tables, 46 RLS tables, 51 policies; second `migrate`/`dbcheck` reported `migrations_applied=0` and the identical fingerprint `ea4c7a60…a2b9`; disposable `dbinit` converged on the same `schema_version=5` and fingerprint with a 5-row ledger. | Staging empty-DB rehearsal on an isolated target. |
| B2 — Honest release | `5054f6f3` | READY FOR REVIEW | Offline fake-AWS suite: 4 deploy tests (both digests verified, mismatch cannot report `verified`, alias rollback restores both versions, missing alias blocks before mutation) and 11 legacy release-engine tests pass; workflow YAML parses. | Real OIDC deploy to Beta, negative-path run, live rollback rehearsal. |
| B3 — Real training capture | `5054f6f3` | READY FOR REVIEW | Core (1,247), Mobile Contracts (133) and Registration (46) tests pass, including observed-set/progression and the 6-script schema-migration contract. | Device log proving progression consumes actual sets. |
| B4 — Recovery and preview | `5054f6f3` | READY FOR REVIEW | Core tests cover multi-reason prerequisites; iOS simulator compile validates the new XAML and bindings. | EN/RU review on device; weekly-summary and timed-phase visual acceptance. |
| B5 — Consistency and docs | `5054f6f3` | READY FOR REVIEW | Docs reconciled to HEAD; deferred surfaces documented; no broken media placeholder exists. | Owner reading of the reconciled docs. |

No package is `SAFE TO PROMOTE` from local checks alone.

## Retired or superseded paths

- `tools/deployment/release.sh` and `tools/deployment/release-engine.py` are the GitLab-era path. GitHub releases use `tools/deployment/deploy_release.py`; the legacy scripts remain only as historical reference and their tests still run so the retirement is not silent.
- `004_privilege_separation_mc_admin_runtime.sql` is deleted. The authoritative role model is `musclecuties_app` / `musclecuties_auth` / `musclecuties_migrator`, declared by `security.sql` and verified by `dbcheck`.
- Migration numbers are unique across both lineages: `004_session_distance.sql` (from `origin/main` #37) keeps version 4, and the observed-set actuals script was renumbered to `005_observed_set_actuals.sql` when the two histories were reconciled. Existing environments that already recorded version 4 keep a valid ledger.
- `EnsureCreated` remains confined to explicitly disposable targets; `migrate` + `seed` is the provisioning path.

## Owner-authorized external gates (not started)

- Signed iOS archive and TestFlight upload.
- QA01 physical-device acceptance: themes, EN/RU, accessibility, timed and strength flows.
- Staging qualification on a genuinely isolated API and database, including an empty-DB migration rehearsal there.
- Owner approval tied to source SHA, artifact digest, schema version, environment and rollback plan.

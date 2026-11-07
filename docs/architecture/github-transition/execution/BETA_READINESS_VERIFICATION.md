# First-beta readiness verification (B1–B5)

Base SHA `5054f6f3` (local `main` before this work). Branch `integration/first-beta-clean`, rebuilt on top of
`origin/main` `5c7ccac7` so GitHub history is preserved rather than overwritten.

| Identity | Value |
| --- | --- |
| Base commit | `5054f6f3` |
| Reconciliation target | `origin/main` `5c7ccac7` (PR #37) |
| Branch tip (code commits before this record update) | `3dbf2eaf` |
| Changed paths vs base | 100 |
| Change-set diff SHA-256 vs base, excluding this record | `f8740ec5215d6c2f12aa60aa2079f0e21590c245dea2ded92424b5d9af99a82b` |
| Files changed vs `origin/main` | 62 |

Environment: macOS 26.6.2 arm64; .NET SDK 10.0.203; PostgreSQL 16.15 (Homebrew) on a throwaway `initdb` cluster
(`127.0.0.1:55441`, `--locale=C --encoding=UTF8`, no pre-existing roles); Python 3.9.6; Ruby Psych for workflow parsing.
Nothing was deployed, migrated on a shared target, signed, archived or uploaded.

Reproduce the change-set digest:

```bash
git diff 5054f6f3 HEAD -- . ':(exclude)docs/architecture/github-transition/execution/BETA_READINESS_VERIFICATION.md' \
  | shasum -a 256
```

## B1 — Provisionable schema (P0)

**Implemented.** A frozen EF model baseline `000_model_baseline.sql` applied only to an empty database, so `migrate`
no longer depends on `EnsureCreated`; `CREATE SCHEMA app` plus the catalog/ops schemas; a `seed` step for the canonical
manifest; `ops.schema_metadata` version/fingerprint written by both `migrate` and `dbinit`; the orphaned
`004_privilege_separation_mc_admin_runtime.sql` deleted so `security.sql` is the single role model
(`musclecuties_app` / `musclecuties_auth` / `musclecuties_migrator`); and the CI rehearsal in
`.github/workflows/db-verify.yml` that provisions an empty database, runs `migrate` + `seed` + `dbcheck`, and runs the
PostgreSQL integration suite on the GitHub runner.

**Commands and observed results** (fresh cluster, `musclecuties_roles_before=0`, `server_encoding=UTF8`):

```bash
initdb -D /tmp/mc-verify-pg -U postgres --auth=trust --locale=C --encoding=UTF8
pg_ctl -D /tmp/mc-verify-pg -o "-p 55441 -k /tmp/mc-verify-sock -h 127.0.0.1" start
createdb -h 127.0.0.1 -p 55441 -U postgres mc_verify
ConnectionStrings__PostgreSql='Host=127.0.0.1;Port=55441;Database=mc_verify;Username=postgres' \
  dotnet run --project src/MuscleCuties.DbMaintenance/MuscleCuties.DbMaintenance.csproj -c Release -- migrate
# ... then: seed, dbcheck, migrate (again), dbcheck (again)
```

| Step | Observed |
| --- | --- |
| `migrate` (first) | `schema_version=5`, `migrations_applied=6` (baseline 0 + 001–005) |
| `seed` | `seeded_rows=746` |
| `dbcheck` | `status=compatible`, fingerprint `ea4c7a607af0bcb6b5a401fcf223f75997873aec14eb607551b77f72fd40a2b9`, 69 model tables, 51 policies |
| roles created from zero | 10 `musclecuties*` roles |
| RLS tables / policies | 46 / 51 |
| catalog exercises | 104 |
| `migrate` (second) | `migrations_applied=0` |
| `dbcheck` (second) | `status=compatible`, identical fingerprint `ea4c7a60…a2b9` |

**Disposable `dbinit` convergence** on a separate empty database: `status=compatible`, `created_from_model=true`,
338 security statements, same fingerprint `ea4c7a60…a2b9`, `seeded_rows=746`, `ops.schema_migrations` ledger 5 rows with
`max(version)=5`, `ops.schema_metadata.schema_version=5`.

**Affected tests.** `SchemaMigrationsContractTests` (`MigrationScripts_MatchesExpectedCleanMigrationChain`,
`ObservedSetActualsMigrationIsAdditiveAndBackfillsUnobserved`, `SessionDistanceMigrationIsAdditiveAndNullable`,
`RehearseChainAsync_ProducesVerifiedChecksumsAndMonotonicOrder`, `BaselineScript_IsEmbeddedAndCreatesApplicationSchema`),
`ModelSchemaContractTests.MigrationChain_ContainsBaseSchemaFingerprintAndScheduleSeam`, and
`DisposableWorkoutDatabaseSetupTests.InitializeAndSeedExplicitlySelectedDisposableDatabase` (the reviewed V020 catalog
import; passed 1/1 in 9 s).

**Nuance, recorded honestly.** `migrate` writes 6 ledger rows because the frozen baseline is version 0; `dbinit` writes
5 rows (versions 1–5) because it adopts the model-first schema instead of re-running the baseline. Both report
`schema_version=5` and the same fingerprint. `migrate` applied 6 scripts; `dbinit` reports `migrations_applied` not at
all, only convergence.

**Nuance, encoding.** An earlier attempt failed with `Npgsql.PostgresException 22P05: unsupported Unicode escape
sequence ... could not be translated to the server's encoding SQL_ASCII`. The cause was the throwaway cluster being
initialised with `--locale=C` alone, which selects SQL_ASCII; Npgsql then escapes non-ASCII catalog text as `\uXXXX`,
which SQL_ASCII cannot represent. Re-initialising with `--encoding=UTF8` fixed it. CI uses `postgres:17-alpine`, whose
default encoding is UTF8, so this was a local rehearsal hazard, not a product defect.

## B2 — Honest, working release (P0)

**Implemented.** `tools/deployment/deploy_release.py` replaces the GitLab-only path on GitHub OIDC; both
`MuscleCuties.Server` and `MuscleCuties.WorkoutReconciliation` are updated and then re-read to confirm the running
`CodeSha256` and version match the candidate artifact; `deployment-report.json` is written only after that verification,
recording alias/version and a rollback target; `backend-release.yml` exposes `mode: deploy|rollback` with
`report_run_id`, so rollback is a first-class job; the misleading Staging job and its fallback URL are gone and the
target is Beta only; `validate-worker-package.sh` gates the worker bundle.

**Commands and observed results.**

```bash
cd tests/MuscleCuties.Deployment.Tests
python3 -m unittest test_deploy_release test_release_engine
# Ran 15 tests in 28.369s — OK
```

Green cases: `test_both_functions_are_verified`, `test_digest_mismatch_never_reports_verified`,
`test_missing_alias_fails_before_mutation`, `test_alias_rollback_restores_previous_version`,
`test_alias_race_blocks_cutover`, `test_object_mismatch_blocks_update`,
`test_waiter_timeout_is_partial_and_does_not_repeat_update`,
`test_report_persistence_failure_is_not_success`, `test_runtime_failure_needs_known_good`,
`test_schema_failure_blocks_update`, `test_stale_revision_blocks_update`,
`test_production_requires_exact_commit_and_tree_authorization`,
`test_partial_restart_revalidates_then_finishes_duplicate`, `test_verified_update_and_duplicate_retry`,
`test_verified_known_good_can_recover_failed_smoke`.

**Workflow parse.** `ruby -ryaml -e 'Dir[".github/workflows/*.yml"].each { |f| YAML.load_file(f) }'` → `parsed=6`.

**Not verified here.** No live OIDC deploy, no negative-path run against AWS, no live alias rollback. Those need the
owner-authorized Beta target.

## B3 — Real training capture (P1)

**Implemented.** Per-set rows bound in the session UI with `ObservedSets`, RIR and technique captured for strength;
row-less timed sessions send `ActivityActuals`; progression consumes the real training-experience tier and the exercise's
own muscle group instead of the hardcoded `MajorMuscleGroup.Chest` / `Tier2IntermediateOrEstablished`; synthetic per-set
history removed; progression, recommendation and skipped-summary copy localized EN/RU.

**Commands and observed results.**

```bash
dotnet test tests/MuscleCuties.Core.Tests -c Release            # Passed 1247, Failed 0
dotnet test tests/MuscleCuties.Mobile.Contracts.Tests -c Release # Passed 133, Failed 0
dotnet test tests/MuscleCuties.Registration.Tests -c Release     # Passed 46, Failed 0
```

**Affected tests.** `ObservedSetCaptureAndProgressionContractTests`, `ActivityActualsContractTests`,
`TimedActivityProgressTests`, `ObservedSetArchiveProvenanceIntegrationTests`, plus the localization gates
(`LocalizationCatalogTests.Every_code_key_literal_exists_in_the_catalog`,
`UiStyleGateTests.Catalog_copy_is_assigned_once_and_names_only_current_actions`).

**Not verified here.** The device log that proves progression consumes actual sets on hardware, and the EN/RU human
review.

## B4 — Generation recovery and preview completeness (P1)

**Implemented.** A prerequisite-recovery CTA when activity or weekdays are missing (with the ViewModel exposing
`NeedsPrerequisiteRecovery` / `PrerequisiteRecoveryActionText`); replacement acceptance resolved to one documented
policy shared by docs and UI; `StrengthPlanPreviewRule.Failure(params string[])` returns every limitation reason rather
than the first; the preview shows a weekly coverage/volume summary, per-exercise rationale and timed-phase detail.

**Commands and observed results.** Core 1,247 passed, including
`WorkoutPlanningPolicyV021UpdatesTests.Preview_ReportsEveryRecoverablePrerequisiteInsteadOfOnlyTheFirst`; the existing
HIIT gate test is unchanged, so a high-intensity strength block still requires safer-prescription review rather than an
experience question.

```bash
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug \
  -p:RuntimeIdentifier=iossimulator-arm64
# Build succeeded. 0 Warning(s), 0 Error(s)
```

**Not verified here.** Visual inspection of the summary, rationale and timed-phase rows, and EN/RU editorial review.

## B5 — Consistency and documentation (P2)

**Implemented.** `PRODUCT_WORKFLOWS.md`, `STATUS.md`, `ROADMAP.md` and `CONTROL.md` reconciled to the tree, including
this sitting's reconciliation: migration numbers are unique across both lineages (`004_session_distance.sql` keeps
version 4, observed-set actuals is `005_observed_set_actuals.sql`), the verified schema version is 5 and the recorded
fingerprint is `ea4c7a60…a2b9`.

**Deferred surfaces still read as deferred.** Fasting is not shipped and is documented as deferred; exercise media is
documented as deferred and no image/video placeholder exists in the workout session UI. Neither is presented as
complete.

**Not verified here.** Owner reading of the reconciled docs.

## Reconciliation performed in this sitting

`origin/main` had moved to `5c7ccac7` (#37, 48 files) while the work sat on local-only `5054f6f3`, and both sides
claimed migration version 4 for different scripts. The branch was therefore rebuilt on `origin/main` and the work
cherry-picked on top, rather than force-pushing local history over `main`.

Two defects were introduced and repaired during that reconciliation, and are recorded rather than hidden:

1. The first merge attempt produced a commit whose tree rewrote ~887 unrelated files (markdown reflow, XAML/SQL reflow).
   That commit was not pushed; the reconciliation was rebuilt from `origin/main`, and the change set vs `origin/main`
   is now 60 files.
2. The merge dropped `partial` modifiers on four MVVM classes, an implicit `Microsoft.Extensions.DependencyInjection`
   and `System.Globalization` using, the `AnimatedModalView.CompleteOpenTransition` API used by the nutrition handoff,
   four catalog keys still referenced by the session view model, and the direct PostgreSQL project reference needed by
   the migration contract test. All were restored, and every suite above was re-run afterwards.

## CI evidence (GitHub Actions, PR #38)

GitHub's pipeline ran the plan's checks on the reconciled tree: `verify_source` passed (Server
restore/build), `verify_plan_generation` passed (offline deployment tests, Core 1,247, Mobile Contracts
133, Registration 46, Server 112, model-catalog filter), `postgres_verify` passed (empty-database
`migrate` + `seed` + `dbcheck` on `postgres:17-alpine`, then the PostgreSQL integration suite), and
`required` passed as their aggregate.

Two real defects were found by that pipeline and fixed rather than worked around:

1. `test_deploy_release.py`/`test_release_engine.py` inherited `GITHUB_EVENT_NAME` from the runner, so
   the deployer correctly refused a non-`workflow_dispatch` event and the digest/alias cases were
   replaced by `manual_release_required`. The tests now clear `GITHUB_*` inputs, which is what the
   offline-fixture intent required.
2. `db-verify.yml` applies `services/postgresql/roles-and-grants.sql` before any migration has run, so
   `ALTER SCHEMA app/catalog/ops` failed on a fresh database. That script now creates the schemas
   idempotently, keeping it safe to run in either order.

Merging into `main` is still gated by the `muscle_cuties_protection` ruleset, which requires a
code-owner review and an approving review of the last push. Approval and merge are the owner's
decision; nothing was bypassed.

## Owner-authorized external gates (unverified)

- Signed iOS archive and TestFlight upload.
- Device build with signing keys (`ios-arm64` archive with a valid keychain identity).
- QA01 physical-device acceptance: themes, EN/RU, accessibility, timed and strength flows.
- Live Beta deploy and live alias rollback against real AWS targets.
- Staging qualification on a genuinely isolated API and database, including the empty-DB rehearsal there.
- Human EN/RU and visual review of the generation preview and session capture.

**No package is SAFE TO PROMOTE on local checks alone.** B1–B5 are READY FOR REVIEW: scoped, locally verified and
reproducible from this tree, with the gates above still open.

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

## Independent first-beta acceptance pass (merged `main` `4ee9b4f2` + local fixes)

Owner authorized MAUI simulator runs for this pass. Environment: macOS arm64, .NET SDK 10.0.203,
throwaway PostgreSQL 16.15 cluster (`initdb --encoding=UTF8`, trust auth, port 55441, database
`mc_beta_accept`), `MuscleCuties.Server` in the Development environment on `http://127.0.0.1:5080`,
iPhone 16 simulator `60DD8563-A1D6-4FE5-9F4F-2BFB975B194B` with `com.musclecuties.app` built from this
tree. Device interaction used `idb` (accessibility tree + taps); macOS accessibility automation was
unavailable for the shell, which is why an idb-backed driver was used instead of AppleScript.

Backend rehearsal on that cluster:

```
dotnet run --project src/MuscleCuties.DbMaintenance ... -- migrate
  {"schema_version":5,"migrations_applied":6, ... five scripts 000..005 plus baseline}
dotnet run ... -- seed        -> {"seeded_rows":746}
dotnet run ... -- dbcheck     -> status "compatible",
  schema_fingerprint_sha256 = ea4c7a607af0bcb6b5a401fcf223f75997873aec14eb607551b77f72fd40a2b9,
  recorded = same, model_table_count 69, policy_count 51, failure_codes []
```

Flows exercised on the simulator, with the API traffic confirmed in the server log:

1. Cold launch and the notification-permission prompt; login screen after an explicit sign-out.
2. Registration: `GET /api/v1/auth/email-exists` 200, `GET /api/v1/auth/username-exists` 200,
   `POST /api/v1/email-challenges/start` 200, `POST /api/v1/email-challenges/verify` 200, then the
   app advanced to onboarding (`Step 1 of 2: Profile setup`).
3. Profile setup (display name, birthday, units, height/weight, activity selection, wellness notice
   acknowledgment with its own validation message).
4. The 12-question personalized quiz, including the multi-select weekday question and the phase
   sliders, finishing at `Question 12 / 12`.
5. Plan preview after generation (`POST /api/v1/workouts/plan/strength-proposals` 200): the preview
   reported the volume note, the weekly plan, and **two** distinct limitation reasons at once
   ("This activity needs more information or a planning rule needs review." and "No safe strength
exercises remain ..."), with `Apply plan` and `Edit details`. That is the B4 all-reasons surface; it
   is not a dead end.

Defects found during the pass and repaired in this tree:

1. A local backend had no email channel at all, so registration could never complete offline.
   `LocalDevelopmentEmailChallengeDelivery` now writes the code to the server log and is registered
   only when `ASPNETCORE_ENVIRONMENT=Development` **and** no Resend key or first-party relay is
   configured. Beta/Staging behavior is unchanged.
2. The OTP screen rendered the raw server code `internalError` and server-authored English sentences.
   `OtpVerificationViewModel` now shows `Ui_Auth_Error_InvalidVerificationCode` and
   `Ui_Auth_Error_ResendCode`, which exist in both EN and RU catalogs.
3. OTP verification returned 500 because `EmailReceipts` signing material was absent. This is
   environment configuration, not a code defect; the local run provisions a throwaway RSA-2048 key,
   key id, issuer and audience.

Risk recorded, not fixed in this pass: the installed app read a persisted DEBUG preference
`backend_api_base_url` that pointed at the shared Beta API
(`https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`) and survived a reinstall, so a "fresh"
local install was silently writing to Beta. It was cleared to loopback for this pass. Before beta
testers receive debug builds this override should be surfaced in-app or restricted to loopback.

Not reached in this pass, and therefore still unverified by device evidence: a strength session with
per-set rows plus RIR/technique capture, a row-less timed session with activity actuals, the session
summary, the fasting surface, and the missing-activity recovery CTA. The weekday multi-select taps did
not register through accessibility automation, so the generated plan contained rest days only and the
session flows were unreachable. Two Cloud-runner restarts also interrupted the interactive run. B3's
contract remains proven only by the suite below, not by a device log.

Checks re-run after the repairs: Server Release build 0 warnings/0 errors, iOS simulator build
0 warnings/0 errors, Core 1,247 passed, Registration 46 passed, `UiStyleGateTests` +
`LocalizationCatalogTests` 12 passed, and the empty-DB `migrate` + `seed` + `dbcheck` rehearsal above
with an unchanged fingerprint. Mobile Contracts 133 passed, and `MuscleCuties.Server.Tests` 113 passed. The Server suite first failed
`ServerRouteParityTests.EveryMappedRouteHasMeasuredColdAndWarmP95WithinBudget` with 62 routes at
~1,000-1,100 ms against a 1,000 ms budget. That test reads its fixture from `MC_LATENCY_POSTGRES`;
with the variable unset it falls back to a synthetic unreachable PostgreSQL, so every database-backed
route paid the fail-fast stand-in and reported ~1 s. Pointing the fixture at a dedicated disposable
database (`mc_test_latency` on the same throwaway cluster) made the whole suite pass 113/113 in 47 s.
This was a missing local fixture, not a regression: the repairs add no route and touch no request
path.

Handoff state: `main` is untouched at `4ee9b4f2`; the repairs and this record are uncommitted working-tree
changes (`services/MuscleCuties.Server/ApiApplication.cs`,
`src/MuscleCuties.UI/ViewModels/Auth/OtpVerificationViewModel.cs`, the new
`LocalDevelopmentEmailChallengeDelivery.cs`, and this document). The disposable cluster and the local
server are still running and the app is installed on the simulator, so the owner's manual pass can
start from the plan preview.

### Second interactive sitting (after runner restarts): preview, weekday recovery, fasting decision

Environment was rebuilt from the same tree after the Cloud runner restarted twice. The disposable
cluster `/tmp/mc-accept-pg` was still listening on `127.0.0.1:55441`; the API was restarted in
`Development` against `mc_beta_accept` on `http://127.0.0.1:5080` and confirmed ready from
`Application started` plus a listening socket. The simulator had been rebooted, so `idb` initially
failed with `Mach port invalid, device disconnected`; stopping the stale `idb_companion` (PID from
`pgrep -fl idb_companion`), removing its Unix socket and starting a fresh companion restored tapping.
The previously suspected cause of the earlier unresponsive weekday taps was this stale companion,
not the app: after the restart, taps on the weekday rows registered normally.

Exercised in this sitting (merged `main` bundle, disposable database, registered account):

- Dashboard and the five-tab shell. The tab bar is exposed to accessibility only as a single
  `AXGroup "Tab Bar"`; individual tabs (Today, Cycle, Train, Nutrition, You) have no accessibility
  nodes, so navigation was driven by coordinates (x≈39/118/196/275/354 at y≈800). This is an
  accessibility gap to record for the QA01 pass, not a functional failure: the tabs switch pages
  correctly.
- `Train` → `Generate plan` → **Activity preferences**, the prerequisite editor. Weekday rows are
  `AXGenericElement`s with visible selection counts: selecting three days reported
  "3 weekdays selected"; continuing with none selected was refused with the in-page
  "Pick at least 2 days" message rather than a dead end.
- Saving the editor opened the **Plan preview** sheet, which showed, in one state: the reduced-volume
  notice ("Plan volume reduced for your schedule."), the weekly composition
  ("Weekly plan" / "Rest days · Tue · Wed · Thu · Sat · Sun"), both limitation reasons at once
  ("This activity needs more information or a planning rule needs review." and
  "No safe strength exercises remain. Every plan needs a safe strength session; …"), the `Plan
details` expander with the activity decision ("High-volume strength · 2 sessions per week ·
  Scheduled"), and `Apply plan` (disabled) plus `Edit details`. This is direct evidence for the B4
  item "return all limitation reasons, not the first match".
- **Fasting (B5)**: `Fasting_*` strings exist in both `UiTextCatalog` and `RussianUiText`, but no view
  references them (`Fasting` occurs only in the two catalogs and one unrelated profile view model),
  so the fasting timer is shipped hidden rather than half-visible. Nothing to exercise on device.

Diagnosis of the unreachable session flows. `catalog.exercises` has 104 rows,
`catalog.workout_exercise_definitions` 60, `catalog.exercise_muscle_contributions` 127 and
`catalog.workout_muscle_groups` 13, so the seed catalog is complete and the empty plan is not a
missing-data or migration problem. `app.workout_injury_logs` and `app.workout_injury_episodes` are
empty, and `app.workout_account_planning_states` holds `baseline_experience = effective_experience = 1`
(beginner) with no answers recorded. With **High-volume strength** selected, that profile yields a
plan whose two sessions have zero eligible exercises, so it returns `NoSafeStrengthStyleFit` +
`NoSafeStrengthContentRemaining`, `Apply plan` stays disabled and no session can be started. That is
coherent product behaviour for the stored profile and the UI explains it, but it means this account
cannot be used to exercise session capture. Switching the activity to `General strength training`
(or recording strength history so a high-volume style becomes eligible) is the prerequisite for the
next sitting.

No product defect was confirmed in this sitting and no source file was changed; the working tree still
carries only the three repairs recorded above plus this document. One item is left explicitly open for
the owner's manual pass: the preview sheet's notes area ends at the viewport edge mid-sentence, and it
was not confirmed whether the remainder is reachable by scrolling inside the sheet (swipes started on
the pinned footer did not scroll it). Treat it as "check by hand", not as a verified truncation bug.

Still unverified by device evidence, and therefore still open: a strength session with per-set rows and
RIR/technique capture, a row-less timed session with activity actuals, the session summary, and the B3
device log proving progression consumes observed sets with the real tier and muscle group. B3 remains
proven by the contract and registration suites only.

### Third interactive sitting: planner fit, preview notes, activity picker

The local stack was reused as-is: disposable PostgreSQL on `127.0.0.1:55441`, the API in
`Development` on `http://127.0.0.1:5080`, the simulator plus a fresh `idb_companion`. A logging HTTP
proxy was tried first to capture request payloads. It immediately proved that the client sends
request bodies with chunked transfer encoding, and a naive proxy that forwards only `Content-Length`
bodies turned `POST /api/v1/workouts/plan/strength-proposals` into a 500. That 500 was the probe, not
the product: the same call returns 200 directly. The proxy was dropped rather than tuned, so payload
capture for `ObservedSets`/`ActivityActuals` is still outstanding.

The preview notes question is settled without a layout change. Inside the plan-preview sheet, a
vertical swipe moved `Notes` from y=533 to y=210 and the second limitation from y=632 to y=309, and
the full sentence became readable. The sheet scrolls; the earlier impression of a clipped note was
the scroll viewport edge, not a truncated card.

Activity picker behaviour was verified directly. Selecting `General strength training` filled its
selection ring and highlighted the row; attempting to deselect the only remaining activity was
refused in-page with "Choose at least one activity to continue." (`/tmp/mc-hvs-off.png`). The picker
and its command binding work. Driving it by accessibility automation is what is unreliable: inside
`WorkoutActivityPreferencePicker` the accessibility frames are offset from the rendered rows (about
+100 pt at one scroll position, +21 pt at another), so taps aimed at an AX centre frequently land on
a neighbouring row. Two speculative fixes to the picker's gesture plumbing were written, tested and
reverted; the working tree carries no change to it.

Planner fit was then isolated. With `baseline_experience = effective_experience = 1` and
`preferred_workout_activity_types = {1}` (High-volume strength), the planner returns two scheduled
sessions with zero eligible exercises, `NoSafeStrengthStyleFit` plus
`NoSafeStrengthContentRemaining`, and `Apply plan` stays disabled. The stored preference on the
disposable database was changed to `{8}` (General strength) with a 45 minute session duration, and
the plan-setup page then reported `Strength plan type: General strength training`, confirming the
app reads that preference. A generated preview for that profile was still not obtained in this
sitting: the setup page left for the preferences editor before the preview was requested, and the
earlier previews were all served from a stale in-memory draft that still named High-volume strength.

Not reached, and therefore still unverified by device evidence: a feasible preview containing
strength exercises and its weekly coverage/volume, per-exercise rationale and timed-phase detail;
`Apply plan`; a strength session with per-set rows plus RIR and technique; a row-less timed session
with `ActivityActuals`; both session summaries; and the B3 device log showing progression consuming
the observed sets with the real tier and muscle group. No source file was changed in this sitting, so
the build, suite and empty-DB `migrate` + `seed` + `dbcheck` results recorded above were not re-run;
they remain the last measured values.

## Owner-authorized external gates (unverified)

- Signed iOS archive and TestFlight upload.
- Device build with signing keys (`ios-arm64` archive with a valid keychain identity).
- QA01 physical-device acceptance: themes, EN/RU, accessibility, timed and strength flows.
- Live Beta deploy and live alias rollback against real AWS targets.
- Staging qualification on a genuinely isolated API and database, including the empty-DB rehearsal there.
- Human EN/RU and visual review of the generation preview and session capture.

**No package is SAFE TO PROMOTE on local checks alone.** B1–B5 are READY FOR REVIEW: scoped, locally verified and
reproducible from this tree, with the gates above still open.

---

## Working-tree verification record over `4ee9b4f2` (uncommitted delta)

Recorded 2026-11-08. The first-beta packages are merged at `4ee9b4f2`; this section identifies the **uncommitted**
working-tree delta on top of that commit, splits it per package, and re-proves the five exit criteria against it.
Nothing here was committed, staged, pushed or deployed.

### Shared identity

| Identity | Value |
| --- | --- |
| Base and current HEAD | `4ee9b4f2f0867eb21ef27ed2e55f1d588bf422ac` (`main`, tracks `origin/main`) |
| Changed paths | 54 total; 53 excluding this document |
| Working-tree fingerprint, this document excluded | `2209aa9585cdbfae20b148db4bc6b2255f41492f64c5ae0ba15626cb906e5439` |
| Fingerprint scope | `git status --porcelain=v1`, the full `git diff`, and one `untracked <blob> <path>` line per untracked file, all excluding this document |
| Record file | `docs/architecture/github-transition/execution/BETA_READINESS_VERIFICATION.md` (excluded from the fingerprint to avoid self-reference) |
| Environment | macOS 26.6.2 arm64; .NET SDK 10.0.203; PostgreSQL 16.15 (Homebrew); Python 3.9.6; Ruby Psych; iOS simulator RID `iossimulator-arm64` |
| Disposable database | `mc_record_verify` on `127.0.0.1:55441`, created fresh for this record |

Reproduce the fingerprint (run from the repository root):

```bash
RECORD=docs/architecture/github-transition/execution/BETA_READINESS_VERIFICATION.md
{
  git status --porcelain=v1 | grep -v " $RECORD$"
  git diff -- . ":(exclude)$RECORD"
  git ls-files --others --exclude-standard | grep -v "^$RECORD$" | sort \
    | while read -r f; do printf 'untracked %s %s\n' "$(git hash-object -- "$f")" "$f"; done
} | shasum -a 256
```

Reproduce any file identity with `git hash-object -- <path>`. The package split below was resolved by reading each
diff, not by path prefix alone. Two kinds of shared files exist and are declared explicitly:

- **Mixed files** carry both a package hunk and a hunk from the earlier acceptance-pass repair (the removal of the
  per-exercise acknowledgment gate). The package owns the hunk named in its section; the remaining hunk is listed in
  the appendix.
- **Pre-existing acceptance-pass repairs** are in the tree for the reasons recorded earlier in this document (local
  email delivery, OTP copy, preference persistence, the acknowledgment-gate removal, catalog review import and the
  refreshed latency fixture). They are not B1–B5 deliverables, but they are part of the same tree and are therefore
  identified in the appendix so a reviewer can reconstruct the whole delta.

### B1 — Provisionable schema (P0)

**Scope as delivered.** The schema package itself is committed at the base commit: frozen baseline
`000_model_baseline.sql` (`CREATE SCHEMA app` plus 69 model tables), migrations `001`–`005`, the canonical `seed`, the
`dbcheck` fingerprint contract, the disposable-only `EnsureCreated` guard and the CI empty-database rehearsal. The
uncommitted delta contains **no B1 source file**; B1 is re-proved below by taking a brand-new database from zero
through the committed chain at this working tree.

| Package files | Status | Blob (SHA-1 via `git hash-object`) |
| --- | --- | --- |
| — (none uncommitted) | — | Schema files are committed at `4ee9b4f2` |

**Commands and observed results.** All commands exit 0.

```bash
export PGPASSWORD=postgres
psql -h 127.0.0.1 -p 55441 -U postgres -d postgres \
  -c "DROP DATABASE IF EXISTS mc_record_verify;" -c "CREATE DATABASE mc_record_verify;"
export ConnectionStrings__PostgreSql="Host=127.0.0.1;Port=55441;Database=mc_record_verify;Username=postgres;Password=postgres"
dotnet run --project src/MuscleCuties.DbMaintenance/MuscleCuties.DbMaintenance.csproj -c Release -- migrate
dotnet run --project src/MuscleCuties.DbMaintenance/MuscleCuties.DbMaintenance.csproj -c Release --no-build -- seed
dotnet run --project src/MuscleCuties.DbMaintenance/MuscleCuties.DbMaintenance.csproj -c Release --no-build -- dbcheck
psql -h 127.0.0.1 -p 55441 -U postgres -d mc_record_verify -tAc "SELECT count(*) FROM ops.schema_migrations;"
```

| Step | Observed |
| --- | --- |
| `migrate` | `status=compatible`, `schema_version=5`, `migrations_applied=6` (baseline 0 + `001`–`005`), six distinct checksums |
| `seed` | `status=compatible`, `seeded_rows=746` |
| `dbcheck` | `status=compatible`, computed and recorded fingerprint `ea4c7a607af0bcb6b5a401fcf223f75997873aec14eb607551b77f72fd40a2b9`, `model_table_count=69`, `policy_count=51`, `failure_codes=[]` |
| `ops.schema_migrations` | 6 rows |
| `ops.schema_metadata` | `schema_version=5`, fingerprint as above |
| Schema counts | 55 base tables in `app`, 46 with RLS, 51 policies |
| Seeded catalog | 104 `catalog.exercises`, 60 `catalog.workout_exercise_definitions`, 127 `catalog.exercise_muscle_contributions` |

**Covering tests.** B1's exit criterion is the rehearsal above; it is the same chain the CI job
`.github/workflows/db-verify.yml` lines 44–52 runs against `mc_migration_rehearsal`. The migration-contract test
`SchemaMigrationsContractTests.MigrationScripts_MatchesExpectedCleanMigrationChain`
(`tests/MuscleCuties.Registration.Tests/SchemaMigrationsContractTests.cs`) is included in that suite's 46 passing
tests (full suite below).

**Environment/target evidence.** Fresh local PostgreSQL 16.15 cluster on `127.0.0.1:55441`; no shared database, no
Staging/Beta target. `mc_record_verify` did not exist before the command above.

**Status: READY FOR REVIEW.** Not SAFE TO PROMOTE: the empty-database rehearsal has not run on a genuinely isolated
Staging target, and no recovery rehearsal on a protected target exists.

### B2 — Honest, working release (P0)

**Scope as delivered.** Beta is the only release target; the misleading Staging option, endpoint, allowlisted host and
compile switch are removed, and a contract test locks the absence. The GitHub OIDC deployer itself is committed at the
base commit and is exercised by the offline suite below.

| Package files | Status | Blob (SHA-1 via `git hash-object`) |
| --- | --- | --- |
| `.github/workflows/mobile-release.yml` | modified | `fcf328c048c2873b815d56ae8517fffba78822cc` |
| `src/MuscleCuties.UI/Services/Configuration/AppEndpointConfiguration.cs` | modified | `14e16dd7b1fa411a57dd3f7e1786c5f2400424cd` |
| `src/MuscleCuties.UI/MuscleCuties.UI.csproj` | modified | `9e3a498eb91806960635dd503902d9c88ec33e0a` |
| `tests/MuscleCuties.Mobile.Contracts.Tests/AppEndpointConfigurationTests.cs` | modified | `9c3db565ae652ae5b41a8cfe8c158faf27df6f99` |

**Commands and observed results.** All commands exit 0.

```bash
python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests -p "test_*.py"   # Ran 28 tests, OK
bash tests/MuscleCuties.Deployment.Tests/release-fail-closed.sh                        # release failure-injection checks passed
ruby -ryaml -e 'n=0; Dir[".github/workflows/*.yml"].each { |f| YAML.load_file(f); n+=1 }; puts "parsed=#{n}"'  # parsed=6
```

**Covering tests** (fully qualified):

- `test_deploy_release.DeployTests.test_both_functions_are_verified`
- `test_deploy_release.DeployTests.test_digest_mismatch_never_reports_verified` — the negative path that must not report `verified`
- `test_deploy_release.DeployTests.test_alias_rollback_restores_previous_version` — the alias rollback rehearsal
- `test_deploy_release.DeployTests.test_missing_alias_fails_before_mutation`
- `test_release_engine.ReleaseEngineTests.test_verified_update_and_duplicate_retry`
- `test_release_engine.ReleaseEngineTests.test_stale_revision_blocks_update`
- `test_release_engine.ReleaseEngineTests.test_production_requires_exact_commit_and_tree_authorization`
- `test_release_engine.ReleaseEngineTests.test_object_mismatch_blocks_update`
- `test_release_engine.ReleaseEngineTests.test_schema_failure_blocks_update`
- `test_release_engine.ReleaseEngineTests.test_runtime_failure_needs_known_good`
- `test_release_engine.ReleaseEngineTests.test_waiter_timeout_is_partial_and_does_not_repeat_update`
- `test_release_engine.ReleaseEngineTests.test_partial_restart_revalidates_then_finishes_duplicate`
- `test_release_engine.ReleaseEngineTests.test_alias_race_blocks_cutover`
- `test_release_engine.ReleaseEngineTests.test_verified_known_good_can_recover_failed_smoke`
- `test_release_engine.ReleaseEngineTests.test_report_persistence_failure_is_not_success`
- `MuscleCuties.Mobile.Contracts.Tests.AppEndpointConfigurationTests.No_Staging_Target_Exists` (inside the 147-test suite below)

**Environment/target evidence.** Offline fake-AWS fixtures only; no OIDC token, no AWS call, no Beta mutation. The
workflow YAML parses with Ruby Psych; the staging assertion is checked by `grep -c "options:.*staging"` → 0, and the
only remaining `staging` string in the mobile workflow is its description: “beta only; no separate Staging API or
database is provisioned”.

**Status: READY FOR REVIEW.** Not SAFE TO PROMOTE: no real OIDC deploy, no negative-path run against AWS, and no live
alias rollback has been executed; owner approval tied to the artifact digests is still missing.

### B3 — Real training capture (P1)

**Scope as delivered.** Per-set rows are bound in the session UI (load, reps, RIR, technique, warm-up, add/remove) and
feed `ObservedSets` only when edited; row-less timed sessions feed `ActivityActuals`; progression uses the persisted
training tier and the exercise's own muscle group. Progression/recommendation/skipped-summary copy is localized in both
catalogs.

| Package files | Status | Blob (SHA-1 via `git hash-object`) |
| --- | --- | --- |
| `src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml` | modified | `600f171e44495b06ccd3293fbd3c275fc1c5e29d` |
| `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.CycleWorkout.cs` | modified | `b35aac63bcef875e1ce67a528755a873cdc172e0` |
| `src/MuscleCuties.UI/Services/Localization/RussianUiText.CycleWorkout.cs` | modified | `11993e2cb90ee4736f379e1001685f28945bc27e` |

The seeded progression evidence also depends on the catalog review import listed in the appendix
(`PostgresCanonicalCatalogSeeder.cs` + importer); without it the seeded review columns stay empty.

**Commands and observed results.** All commands exit 0.

```bash
dotnet test tests/MuscleCuties.Mobile.Contracts.Tests -c Release --nologo   # Passed 147, Failed 0
dotnet test tests/MuscleCuties.Core.Tests -c Release --nologo              # Passed 1247, Failed 0
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug \
  -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64                   # Build succeeded
# EN/RU key parity over UiTextCatalog*.cs vs RussianUiText*.cs:
# parity_english=1990 parity_russian=1990 missing_ru=0 missing_en=0
```

**Covering tests** (fully qualified):

- `MuscleCuties.Mobile.Contracts.Tests.ObservedSetCaptureAndProgressionContractTests.EditedSetRows_ProduceContiguousObservedSets`
- `...ObservedSetCaptureAndProgressionContractTests.UneditedSetRows_DoNotSendObservedSets`
- `...ObservedSetCaptureAndProgressionContractTests.TimedActivity_BuildsRowLessActualPayload`
- `...ObservedSetCaptureAndProgressionContractTests.PersistedObservedSets_UseRealTierAndMuscleGroupForProgression`
- `...ObservedSetCaptureAndProgressionContractTests.AggregateOnlyHistory_ReturnsConservativeLogEachSetOutcome`
- `...ObservedSetCaptureAndProgressionContractTests.WeekWithMissingRir_IsNotUsedAsQualifyingEvidence`
- `...ActivityActualsContractTests.LogWorkoutSessionRequest_BackwardCompatible_WhenActivityActualsOmitted`
- `...ActivityActualsContractTests.LogWorkoutSessionRequest_SerializesAndRoundTripsActivityActuals`
- `...ActivityActualsContractTests.SwimmingActivityActualLog_SupportsMetersAnd100mPace`
- `...ActivityActualsContractTests.FinishWorkoutSessionCommand_RoundTripsDistance`
- `...ActivityActualsContractTests.FinishWorkoutSessionCommand_OmitsDistanceForLegacyPayload`

**Environment/target evidence.** iPhone simulator build only (`iossimulator-arm64`); no device run, no simulator
interaction and no network call in this pass. The XAML binding surface is exercised by strict XAML compilation and by
the 147-test contract suite.

**Status: READY FOR REVIEW.** Not SAFE TO PROMOTE: the required device log showing progression consuming actual sets
on hardware does not exist, and the EN/RU human review is outstanding.

### B4 — Generation recovery and preview completeness (P1)

**Scope as delivered.** The prerequisite-recovery CTA is wired for missing activity/weekdays; replacement acceptance
has one documented policy shared by docs and UI; limitation reasons are aggregated instead of returning the first;
the preview renders weekly composition, per-exercise rationale and timed-phase detail without empty rows.

| Package files | Status | Blob (SHA-1 via `git hash-object`) |
| --- | --- | --- |
| `src/MuscleCuties.Core/Domain/Workout/Planning/StrengthPlanPreviewRule.cs` | modified (rationale hunk; gate-removal hunk in appendix) | `6d2fe188c33362289f70bcd4298a7519d2461955` |
| `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationDisplayModels.cs` | modified | `5d796f337456438ad62ff78d46c9e195ce210fc4` |
| `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs` | modified (preview dismissal hunk; gate-removal hunk in appendix) | `3173df2c7de64804203b0a29cdf37962e0972bfa` |
| `src/MuscleCuties.UseCases/Features/Workout/Planning/StrengthPlanPreviewOutputV020.cs` | modified | `8f5fe281ce795ff6758892deb1056a6adef743b7` |
| `src/MuscleCuties.UseCases/Features/Workout/Planning/StrengthPlanPreviewMapperV020.cs` | modified (rationale mapping hunk; gate-removal hunk in appendix) | `484cbd6590309efe96fb4bb40c34ad9295a20f44` |
| `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.WorkoutGeneration.cs` | modified | `428bd770fb9a6a347edf1504872dfbdc0b24028f` |
| `src/MuscleCuties.UI/Services/Localization/RussianUiText.WorkoutGeneration.cs` | modified | `90b3ff3848aac3aa73100e3631ef67ed4c637c49` |
| `tests/MuscleCuties.Core.Tests/StrengthPlanPreviewRuleTests.cs` | modified (multi-reason test; gate-removal test edits in appendix) | `9ca14b85e6949d900e01e605b26302b9b240d7f0` |
| `tests/MuscleCuties.Mobile.Contracts.Tests/StrengthPlanPreviewRationaleTests.cs` | added | `7d0bc3fd947971970d06164bd1a51c5797234056` |
| `tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj` | modified | `b2284196801cfad6182442f25c48434419371253` |
| `tests/MuscleCuties.Mobile.Contracts.Tests/UiTextCatalogStub.cs` | modified | `1ce6d2f16b07fe2500cd4caac8dc961a6c358aa1` |

**Commands and observed results.** All commands exit 0.

```bash
dotnet test tests/MuscleCuties.Core.Tests -c Release --nologo \
  --filter "FullyQualifiedName~StrengthPlanPreviewRuleTests"      # Passed 15, Failed 0
dotnet test tests/MuscleCuties.Mobile.Contracts.Tests -c Release --nologo \
  --filter "FullyQualifiedName~StrengthPlanPreviewRationaleTests"  # Passed 13, Failed 0
dotnet test tests/MuscleCuties.Core.Tests -c Release --nologo       # Passed 1247, Failed 0
```

**Covering tests** (fully qualified):

- `MuscleCuties.Core.Tests.StrengthPlanPreviewRuleTests.Preview_DisclosesEveryLimitationReasonInsteadOfOnlyTheFirst`
  — the every-limitation-reason test: it forces a duration limitation and missing primary-muscle metadata in one
  evaluation and asserts both codes are returned, with `LimitationCodes.Count >= 2`.
- `MuscleCuties.Core.Tests.WorkoutPlanningPolicyV021UpdatesTests.Preview_ReportsEveryRecoverablePrerequisiteInsteadOfOnlyTheFirst`
  — prerequisite aggregation for the recovery CTA.
- `MuscleCuties.Mobile.Contracts.Tests.StrengthPlanPreviewRationaleTests` — 13 passing cases from six methods
  (`Slot_WithBlankRationale_HasNoVisibleRationaleAndNoRenderedText`,
  `Slot_WithRealRationale_KeepsExactText`, `Session_WithBlankSelectionReason_ShowsExerciseWithoutRationaleRow`,
  `Session_WithRealSelectionReason_PreservesExactRationaleText`,
  `Session_WithCatalogReasonWhoseCopyIsBlank_ShowsNoRationaleRow`,
  `Session_WithCatalogReasonThatHasCopy_RendersTheLocalizedCopy`), proving no blank rationale row can render.
- `MuscleCuties.Core.Tests.StrengthPlanPreviewRuleTests` — the remaining 14 cases cover the preview status, durations,
  omissions, high-intensity review gate and missing/invalid duration paths.

**Environment/target evidence.** .NET 10.0.203 on macOS arm64; no device/visual inspection in this pass. The iOS build
result is cited under B3.

**Status: READY FOR REVIEW.** Not SAFE TO PROMOTE: weekly-summary, rationale and timed-phase rows have not been
visually accepted on device, and the EN/RU editorial review is outstanding.

### B5 — Consistency and documentation (P2)

**Scope as delivered.** The canonical docs are reconciled to this tree: Beta-only release state, current test counts,
current schema version/fingerprint, and the deferred fasting/media surfaces stated as deferred. The design and infra
documents adjusted for the removed acknowledgment gate are listed in the appendix rather than being counted as B5
deliverables.

| Package files | Status | Blob (SHA-1 via `git hash-object`) |
| --- | --- | --- |
| `STATUS.md` | modified | `c605917831a6299d2ed6e729a95c5162587d4db6` |
| `ROADMAP.md` | modified | `45566cddc6e5c423521f178bb6da2c0e98dbde47` |
| `docs/PRODUCT_WORKFLOWS.md` | modified | `1164f836eeecac00aa9e1ed4e8e96e6b27832464` |
| `docs/architecture/github-transition/execution/CONTROL.md` | modified | `c8fb66c62a299baab54bd8fafd91e38e2de4d51b` |
| `docs/architecture/github-transition/execution/BETA_READINESS_VERIFICATION.md` | this document (excluded from the fingerprint) | hash changes with this append |

**Docs-vs-source parity checks.** All commands exit 0.

```bash
# Current counts present, stale counts absent:
grep -rn "Core 1,247\|Core (1,247)\|Contracts 147\|Contracts (147)\|Registration 46\|Registration (46)" \
  STATUS.md ROADMAP.md docs/architecture/github-transition/execution/CONTROL.md | wc -l   # 3
grep -rn "1,246\|Contracts 146\|Contracts (146)" \
  STATUS.md ROADMAP.md docs/architecture/github-transition/execution/CONTROL.md | wc -l   # 0
# Every canonical doc cites the same base:
grep -c "4ee9b4f2" STATUS.md ROADMAP.md docs/architecture/github-transition/execution/CONTROL.md # 1 / 1 / 1
# Beta only:
grep -ci staging src/MuscleCuties.UI/Services/Configuration/AppEndpointConfiguration.cs   # 0
grep -c "options:.*staging" .github/workflows/mobile-release.yml                          # 0
# Deferred surfaces have no rendered placeholder:
grep -ril fasting src/MuscleCuties.UI/Pages --include=*.xaml | wc -l                       # 0
grep -ril 'media\|video' src/MuscleCuties.UI/Pages --include=*.xaml | wc -l                # 0
```

The single surviving `staging` string in the mobile workflow is its description (“beta only; no separate Staging API or
database is provisioned”), not a selectable target. The counts above match the freshly reproduced suites recorded in
this section: Core 1,247, Mobile Contracts 147, Registration 46.

**Covering tests.** None; B5 is documentation. Its exit criterion is the parity checking above plus the review of this
record.

**Environment/target evidence.** Local working tree only.

**Status: READY FOR REVIEW.** Not SAFE TO PROMOTE: owner reading of the reconciled docs is still pending, and B5
cannot make the other packages release-ready.

### Appendix — pre-existing acceptance-pass repairs in the same tree (not B1–B5 deliverables)

These changes predate this record and are documented in the earlier acceptance-pass sections above. They are listed
so the working-tree fingerprint is reconstructable; they are exercised by the same suites reported here.

| File | Status | Blob (SHA-1 via `git hash-object`) |
| --- | --- | --- |
| `services/MuscleCuties.Server/ApiApplication.cs` | modified | `a46eb473054e6a06ebcd29929a37c4fcb80c5965` |
| `src/MuscleCuties.Infrastructure.PostgreSql/Account/EmailChallenge/LocalDevelopmentEmailChallengeDelivery.cs` | added | `41c776ffd144a0b49e4606826c3e04b7734e153e` |
| `src/MuscleCuties.UI/ViewModels/Auth/OtpVerificationViewModel.cs` | modified | `f958210d3ef56cfa61c7d2b7360332ae6671d411` |
| `src/MuscleCuties.UI/ViewModels/Profile/ProfileWorkoutPreferencesViewModel.cs` | modified | `b40732418eb2bb3ba534f668b9539e53a71927a5` |
| `src/MuscleCuties.UseCases/Features/Profile/WorkoutPreferences/Contracts/ProfileWorkoutPreferencesContracts.cs` | modified | `6b1c1e933c543b76476d0d9ad82873bdd214111f` |
| `src/MuscleCuties.UseCases/Features/Profile/WorkoutPreferences/IProfileWorkoutPreferencesStore.cs` | modified | `eeef7dd53bb09c58d817e74650897b8ee7e5897e` |
| `src/MuscleCuties.UseCases/Features/Profile/WorkoutPreferences/ProfileWorkoutPreferencesUseCase.cs` | modified | `c31855a593bc9b0e47bc578b4fd0cca90b39b357` |
| `src/MuscleCuties.Infrastructure/Cloud/CloudAccountAndProfileStores.cs` | modified | `c17c7be9f6527931f8487a684347add4529e1d77` |
| `src/MuscleCuties.Core/Domain/Workout/Planning/ExerciseEligibilityRule.cs` | modified | `81d29e56016bf37f9e255318678592621780b33d` |
| `src/MuscleCuties.Core/Domain/Workout/Planning/WorkoutPlanningFlowRule.cs` | modified | `4fa03e03fe589848836e58811bf0d01fb329a742` |
| `src/MuscleCuties.UseCases/Features/Workout/Planning/WorkoutPlanningFlow.cs` | modified | `08cdf93396217674b2e977b2b6cd09d8c581dc84` |
| `src/MuscleCuties.UseCases/Features/Workout/Planning/StrengthPlanPreviewV020.cs` | modified | `82b01242fe664d8bf69347be4878b1e01f98c282` |
| `src/MuscleCuties.Core/Services/Workout/WorkoutActivityPreferences.cs` | modified | `035a94398031d0db20e69708fe9a9cbaedd43c18` |
| `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/PostgresCanonicalCatalogSeeder.cs` | modified | `7c27eebf756e15f8e6e72ac81ce40f3373ac32a0` |
| `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresReviewedExerciseCatalogImporterV020.cs` | modified | `ace0124c1a4d1c0935748c1cb06b52d26f17b6f2` |
| `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresWorkoutPlanReconciler.cs` | modified | `95d20f063fb1b6af902bb06bcade2ae40a9d0567` |
| `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/WorkoutExerciseV020CatalogMapper.cs` | modified | `9dce4b1146fb57b40f29ed3fbdca7e364af3bacb` |
| `src/MuscleCuties.UI/MauiProgram.cs` | modified | `05579c85598435b511494bc8db0e04092c9c770b` |
| `tests/MuscleCuties.Core.Tests/ActivityPreferencePolicyTests.cs` | modified | `f36d5cbe7bc6fc64a85c570f5daca0df407bed7b` |
| `tests/MuscleCuties.Core.Tests/ExerciseEligibilityRuleTests.cs` | modified | `af712ba452ef0769e6375fc7830233f10ae26e33` |
| `tests/MuscleCuties.Core.Tests/ProfileWorkoutPreferencesRegenerationTests.cs` | modified | `7e8755cea45d0219381babd20ce9c6b30c06b5a8` |
| `tests/MuscleCuties.Core.Tests/WorkoutPlanningActivationRuleTests.cs` | modified | `76cd929d9d334177aee873a478499c93fe7806a1` |
| `tests/MuscleCuties.Core.Tests/WorkoutPlanningFlowRuleTests.cs` | modified | `296463f452ed837e50b149bb708b49702c5ef2b0` |
| `tests/MuscleCuties.Registration.Integration.Tests/InjuryCanonicalReconciliationTests.cs` | modified | `92990620ceb3b482ecbb98df8478be2c21f2788c` |
| `tests/MuscleCuties.Server.Tests/ServerRouteParityTests.cs` | modified | `cf9d01f97fc55020de281dca20a8695b4c282669` |
| `docs/performance/server-latency-report.json` | regenerated fixture | `991420ee0dd9d20a703d33f83b1a957833c286d3` |
| `docs/architecture/github-transition/04-ci-cd-verification-matrix.md` | modified | `f755efc24ce6b6477a962391f8e57bd5ce8147f5` |
| `docs/design/APP_WIDE_CONSISTENCY_AUDIT_2026-11-04.md` | modified | `762ecc3375ca291c71bf8b4dc2c881745dcc519c` |
| `docs/design/UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md` | modified | `5ee0ac873ee8dacc706e59461ca188d887260eea` |
| `docs/design/orchestration/RM08-CANDIDATE-HANDOFF.md` | modified | `75af1d70dc8ea2a7db1b9cae3aca1d3478c8ebc4` |
| `docs/infra/AWS_BACKEND_REPAIR_20261030.md` | modified | `be0b8f1a49e47b87dc149e8734fd3a444491baa1` |

### Environment-wide command summary

| Command | Exit | Result |
| --- | --- | --- |
| `dotnet test tests/MuscleCuties.Core.Tests -c Release --nologo` | 0 | 1,247 passed, 0 failed |
| `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests -c Release --nologo` | 0 | 147 passed, 0 failed |
| `dotnet test tests/MuscleCuties.Registration.Tests -c Release --nologo` | 0 | 46 passed, 0 failed |
| `python3 -m unittest discover -s tests/MuscleCuties.Deployment.Tests -p "test_*.py"` | 0 | 28 tests, OK |
| `bash tests/MuscleCuties.Deployment.Tests/release-fail-closed.sh` | 0 | failure-injection checks passed |
| `ruby -ryaml -e '…'` over `.github/workflows/*.yml` | 0 | 6 workflows parsed |
| Fresh `mc_record_verify`: `migrate` / `seed` / `dbcheck` | 0 / 0 / 0 | v5, 6 migrations, 746 rows, fingerprint match, 69 tables, 51 policies |
| `dotnet build … -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64` | 0 | Build succeeded |
| EN/RU catalog parity scan | 0 | 1,990 / 1,990, 0 missing |

### External gates (owner-authorized, unexecuted)

- Signed iOS archive and TestFlight upload.
- QA01 physical-device acceptance: themes, EN/RU, accessibility, timed and strength flows, including the device log
  that proves progression consumes observed sets.
- Staging qualification on a genuinely isolated API and database, including the empty-database rehearsal there.
- Owner approval tied to source SHA, both artifact digests, schema version, environment and rollback plan.

Every package above is **READY FOR REVIEW**. None is **SAFE TO PROMOTE** from local checks alone.

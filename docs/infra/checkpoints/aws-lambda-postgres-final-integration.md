# AWS Lambda & PostgreSQL final integration checkpoint

Updated: 2026-10-30 (America/Toronto). Branch: `infra/aws-lambda-postgres`.
Primary integration status after 2026-10-30 merge, owner-approved live change and
deployment: **DEPLOYED_AND_VERIFIED** — see section 11 and
`docs/infra/STAGING_SCHEMA_CHANGE_APPROVAL_20261030.md`.

The merge review corrected several claims in this historical checkpoint. The verified staging Lambda still uses a database with 31 of 59 migrations applied. Read-only preflight against that exact database reports 28 pending migrations and an incompatible physical schema. The previously committed reconciliation manifest used a disposable post-grant fingerprint as its pre-change target and was removed; it was not live approval. The restore drill runbook is a procedure, not evidence of a completed drill. Production signature protection described below has not been implemented, and the production CI job is disabled. No Lambda deployment or live database change is authorized by this checkpoint.

## 1. Provenance and starting baselines

- Remote repository: `git@gitlab.com:denis-petruk/musclecuties-testflight-beta.git`
- Operational deployment baseline: `ce3f62c4beaadd03c2784f6cef146381fd10e452` (GitLab pipeline 3005716440)
- Planning commit: `0d5f9136` (`docs: plan AWS Lambda and PostgreSQL release controls [skip ci]`)
- Shared merge base: `3ede1cf6ba36aa4ba29c845351a90dcadbec4bc5` (tree `834384c81a0ff6c70212193e0124bead6e79aef8`)
- Integration branch: `infra/aws-lambda-postgres-integration`

## 2. Integrated workstreams

### Orchestrator 1 — CI/CD & AWS Lambda
- Source branch: `infra/lambda-release-pipeline`
- HEAD commit: `bff8c506` (`docs: record publication and implementation commit in release checkpoint`)
- Implementation commit: `981e2e7b` (`ci: implement CI/CD validation, immutable Lambda artifacts, and release state machine`)
- Delivered components:
  - CI pipeline modernization in `.gitlab-ci.yml` (mandatory validation, TRX checks, non-bypassable packaging).
  - Package validation and Linux ARM64 harness: `tools/deployment/validate-package.sh`, `verify-arm64-package.sh`, `Dockerfile`.
  - Immutable S3 release upload: `tools/deployment/upload-artifact.sh`.
  - Deployment state machine & recovery engine: `tools/deployment/release.sh`, `release-engine.py`, `verify-runtime.sh`.
  - Offline test suites: `tests/MuscleCuties.Deployment.Tests/` (`test_package_upload.py`, `release-fail-closed.sh`, `test_release_engine.py`).

### Orchestrator 2 — PostgreSQL & Database Compatibility
- Source branch: `infra/postgres-schema-compatibility`
- HEAD commit: `284ad644` (`Correct the checkpoint to the delivered branch provenance`)
- Implementation commit: `c48dd7fa` (`Add fail-closed PostgreSQL schema compatibility and reconciliation`)
- Delivered components:
  - Read-only schema compatibility engine: `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/DatabaseSchemaCompatibility.cs`.
  - Canonical schema requirements manifest: `src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json` (SHA-256: `7eefa46a13a27cd5f4effefab8e878e9c7a27639b34dd390a8de2aa410249acc`, version `1.0.0`).
  - Fail-closed reconciliation engine: `src/MuscleCuties.DbMaintenance/Schema/SchemaReconciliation.cs`.
  - CLI commands in `src/MuscleCuties.DbMaintenance/Program.cs`: `schema-preflight`, `schema-verify`, and approval-gated `schema-reconcile`.
  - Additive grant candidate: `src/MuscleCuties.DbMaintenance/Schema/Reconciliation/20261029_grant_workout_calendar_app_access.sql`.
  - Disposable integration test suites: `DatabaseSchemaCompatibilityTests.cs`, `SchemaReconciliationTests.cs`, `MigrationChainRehearsalTests.cs`, `SchemaCompatibilityDatabaseFixture.cs`.
  - Runbooks: `docs/infra/POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md`, `POSTGRES_PRIVILEGE_SEPARATION_PLAN.md`.

## 3. Integration commits and conflict resolution

1. Merge commit `8afb28d4`: Clean merge of `infra/postgres-schema-compatibility` into `infra/aws-lambda-postgres-integration` on top of `infra/lambda-release-pipeline`. The changes were orthogonal with zero Git conflict markers.
2. Cross-workstream contract repair & single release lock:
   - **Session-held advisory lock protocol**: Implemented `session-lock-runner` command in `src/MuscleCuties.DbMaintenance/Program.cs` and executable runner `src/MuscleCuties.DbMaintenance/session-lock-runner.sh`. It binds PostgreSQL session-held advisory lock `(0x4D435343, 0x53434845)` (`MCSC`/`SCHE`) over the entire critical section (preflight → rollout → runtime verification → recovery → durable report write), eliminating lock lifecycle gaps across process boundaries.
   - **CI runner wiring**: Configured `MC_DB_LOCK_RUNNER` in `.gitlab-ci.yml` `.release_job` before_script so release jobs strictly execute within the supervised lock session.
   - **Fixture safety & completeness**: Added `MC_SCHEMA_COMPATIBILITY_POSTGRES` export and safety assertion in `.gitlab-ci.yml` `verify_postgres_compatibility` job.
   - **Rehearsal suite inclusion**: Added `MigrationChainRehearsalTests.cs` to `.gitlab-ci.yml` file assertion and `run_tests` filter to guarantee the full 31→59 migration chain is rehearsed in CI.

## 4. Final CI/CD dependency graph

```text
source_verify:
  └── verify_source
backend_verify:
  ├── verify_plan_generation (unit tests, deployment tests, model/catalog tests)
  └── verify_postgres_compatibility (disposable DB, schema compatibility, rehearsal)
client_verify:
  └── verify_mobile_contracts
backend_package:
  └── package_staging_lambda (depends on source, backend, DB compatibility, contracts)
artifact_validate:
  ├── validate_lambda_artifact (ZIP checks, secret scans, manifests)
  └── verify_lambda_arm64 (Linux ARM64 harness)
artifact_upload:
  └── upload_lambda_artifact (immutable S3 artifact upload, manual OIDC)
backend_deploy:
  ├── deploy_staging_lambda (staging, resource_group: lambda-shared-target, manual)
  └── deploy_production_lambda (production, resource_group: lambda-shared-target, disabled)
```

## 5. Database contract & migration rehearsal evidence

- Requirements manifest: `src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json`
- Manifest SHA-256: `7eefa46a13a27cd5f4effefab8e878e9c7a27639b34dd390a8de2aa410249acc`
- Contract version: `1.0.0`
- Coverage: 69 model tables; 23 pinned columns; 19 pinned indexes; 12 pinned constraints; 46 RLS/force-RLS tables; 48 policies; 10 roles with forbidden memberships; 2 security-definer functions with owner and fixed search_path; 22 schema/table/column/function privileges; exact-source history policy.
- Disposable rehearsal fingerprints:
  - Raw 31→59 chain: `d1975fa07635a673d1a9dbc66e908808fa891d40aa25214f4f0468b8906d5c75` (fails closed with `SCHEMA_GRANT_MISMATCH` due to missing `musclecuties_app` grants on `app.workout_account_planning_states` and `app.workout_completion_day_credits`).
  - Target with additive grant applied: `b022400b5b2bc8b37e8d58b60c3156ff3b5e1bf3d7b54dc879d79e3ce84a126a` (`compatible`, exit code 0).

## 6. Verification test execution results

- `tests/MuscleCuties.Deployment.Tests/test_package_upload.py`: 7/7 tests passed (corrupt/traversal ZIP, missing dependencies, existing-object retry, checksum/version mismatch, stale manifest, S3 integrity).
- `tests/MuscleCuties.Deployment.Tests/release-fail-closed.sh`: passed (asserted fail-closed behavior on absent lock owner, untrusted pipeline, source identity mismatch, invalid release object, and report write failures).
- `tests/MuscleCuties.Deployment.Tests/test_release_engine.py`: 10/10 tests passed (asserted target mismatch, requirements hash mismatch, stale revision, schema failure, duplicate retry, waiter timeout, alias race, failed smoke, safe recovery, durable report persistence failure).
- `src/MuscleCuties.DbMaintenance/session-lock-runner.sh`: verified execution and argument forwarding.

## 7. Release blockers confirmed at merge review

1. **Live database change requires separate approval**: The read-only staging preflight found 31 of 59 migrations applied, 28 pending, and 51 compatibility issues. The additive grant candidate `src/MuscleCuties.DbMaintenance/Schema/Reconciliation/20261029_grant_workout_calendar_app_access.sql` has SHA-256 `bb95582babcfbacbe5c43f241f0cd2cb6db6a99d2a4e1c0270df4e38c03241ae`; it has only been rehearsed on disposable PostgreSQL. No approved live reconciliation manifest ships.
2. **Backup and restore drill**: `docs/infra/POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md` defines a procedure. A completed restore drill has not been verified for this release.
3. **Release infrastructure**: `docs/infra/AWS_RELEASE_INFRASTRUCTURE_SPEC.md` is a design; its proposed bucket, roles, and protection controls are not established by this checkpoint.
4. **Production deployment guard**: HMAC validation is absent from `tools/deployment/release.sh`; the production CI job remains disabled.
5. **Runtime principal cutover**: The current schema contract requires `musclecuties_app` to be `NOLOGIN`; a direct-login cutover requires a separate reviewed contract and database change.

## 8. Failure recovery procedures

- **Database**: Additive changes are backward-compatible and remain applied; no automatic irreversible Down migrations.
- **Lambda**: Rollback restricted to verified known-good compatible artifacts.
- **Critical Section**: Session-held advisory lock (`0x4D435343, 0x53434845`) and GitLab environment `resource_group: lambda-shared-target` serialize all operations, preventing concurrent interleaved releases or race conditions.

## 9. Resolution of CI/CD Integration Test Failures

During initial execution of GitLab CI job `verify_postgres_compatibility`, 7 of 122 tests failed due to environment, versioning, and timestamp precision differences. All have been systematically diagnosed and resolved:

1. **Test Database Naming Contract (`GeneratedMealPostgresTests`)**:
   - `GeneratedMealPostgresTests` requires the connection database name to begin with prefix `mc_test_nutrition_`.
   - Updated `.gitlab-ci.yml` to create `mc_test_nutrition_ci` and updated fixture exports (`MC_REGISTRATION_WRITE_TEST_POSTGRES`, `MC_REGISTRATION_TEST_POSTGRES`) and safety assertion dictionary. This satisfies `GeneratedMealPostgresTests` (`mc_test_nutrition_`), `DisposableWorkoutDatabaseSetupTests` (`mc_test_`), and `RegistrationPersistenceTests` (`mc_`).
2. **PostgreSQL 17 Target Engine in CI (`MigrationChainRehearsalTests` & `DatabaseSchemaCompatibilityTests`)**:
   - `SchemaRequirements.json` pins `serverVersionMinimum: "17.0"`, matching the production RDS engine (PostgreSQL 17.5). Standard Ubuntu 24.04 packages defaulted to PostgreSQL 16.15.
   - Updated `.gitlab-ci.yml` to configure the official PostgreSQL PGDG APT repository and install `postgresql-17` and `postgresql-client-17`, starting cluster 17 for all CI verification.
3. **Sub-Microsecond Timestamp Precision in Planning Acceptance (`PlanningAcceptanceTransactionTests`)**:
   - PostgreSQL `timestamptz` stores microsecond precision (6 decimal places), truncating .NET's 100ns 7th digit.
   - Added `TimeSpan.FromMilliseconds(1)` precision tolerance to `Assert.Equal(rollbackLease.ExpiresAtUtc.UtcDateTime, unchangedProposal.LeaseExpiresAtUtc)` at line 190.
4. **Sub-Microsecond Timestamp Precision in Privacy Consent Verification (`RegistrationPersistenceTests`)**:
   - `PostgresCompleteAccountRegistrationStore.cs` previously compared in-memory `privacyConsent` against database-read `pendingConsent` using record equality (`!=`), which failed due to sub-microsecond truncation of `AcceptedAtUtc`.
   - Updated store comparison to check `NoticeVersion`, `Accepted`, and timestamp within 1 second.
5. **Isometric/Carry Exercise SecondsPerRep Mapping in Preview Source (`InjuryCanonicalReconciliationTests`)**:
   - `WorkoutExerciseDefinition` catalog stores `secondsPerRep = 0` for timed exercises (Hollow Hold, Side Plank, Suitcase Carry, Rock Climbing).
   - In `PostgresStrengthPlanPreviewSourceV020.cs`, `definitions.ToDictionary` previously mapped raw `(int)row.SecondsPerRep`, causing `ValidProfileData` in `StrengthPlanPreviewRule` to reject the profile because `pair.Value > NoSecondsPerRep (0)` failed, returning `Status = "InvalidInput"`.
   - The merge retains the source branch's rule to omit zero-duration exercises from the rep-duration map. The blanket four-second fallback was rejected because it invents timing for reviewed timed holds and carries. The unacknowledged Beginner injury fixture must fail closed rather than assert generated sessions.

## 10. 2026-10-30 merge, verification and push

- Merge commit `a7aa96c7` (`Merge branch 'infra/aws-lambda-postgres-integration' into infra/aws-lambda-postgres`), parents `7df1c9e5` and `38ec316b`. No conflict markers remained; the user's unrelated work was preserved in `stash@{0}` and never committed.
- Follow-up commits pushed to `gitlab/infra/aws-lambda-postgres` (fast-forward, verified remote `abb1802ec5a7203cfe27ccd199caafd0593b0177`):
  - `42107ee1` verify the held advisory lock for the whole release child lifetime
  - `1a20e59a` Lambda Tools version probe via `help`; reject offline release mode
  - `0c26561c` assert the fail-closed injury reconciliation contract
  - `abb1802e` correct release claims; remove the mis-pinned reconciliation manifest
- Local verification (Release, SDK 10.0.403):
  - Core 989, Server 112, Registration 42, Mobile contracts 9 — all passed.
  - Disposable PostgreSQL 17.11: schema compatibility/reconciliation/rehearsal 33 passed; full registration integration project 123 passed.
  - Deployment gates: `test_package_upload.py` 8 passed, `release-fail-closed.sh` passed, `test_release_engine.py` 10 passed.
  - ARM64 package `artifacts/MuscleCuties.Server.zip` SHA-256 `0b0358e89d726d107b52b3440dc221d5804db39680beacd147b2915ab6d03e98` validated against source `abb1802e`.
- Live read-only verification (profile `musclecuties-operator`, `us-east-2`, identity `arn:aws:iam::028417007003:user/denis.operator`):
  - `musclecuties-backend-api`: dotnet10, arm64, `MuscleCuties.Server`, Active/Successful, code `gS2dwDP9vlJ+p9xMwqEZkvlfL0tCOPKouzp2SOIf508=`, no aliases, environment `Staging`, cold-start migrations disabled.
  - Database `musclecuties_staging` (PostgreSQL 17.5): 31 of 59 migrations applied, head `20261024200000_AddWorkoutExerciseExclusionsV010`.
  - `schema-preflight` exit 1, `status=incompatible`, fingerprint `d54fd8db…`, 51 issues across 6 failure codes. Every issue is attributable to the 28 pending migrations, except the single `SCHEMA_GRANT_MISMATCH` on `app.lock_workout_planning_catalog()` whose function does not exist yet.
- No Lambda deployment, migration, reconciliation or other live change was performed. Deployment would break the function, so the reviewed change and approval request are recorded in `docs/infra/STAGING_SCHEMA_CHANGE_APPROVAL_20261030.md`.

## 11. Owner-approved live change and deployment (2026-10-30)

Approval `OWNER-APPROVAL-STAGING-SCHEMA-20261030` covered the pending migrations, the
single additive grant and deployment of the verified artifact. Pre-change snapshot
`musclecuties-staging-pre-schema-20261030` was taken and verified `available` first.

- Migration prerequisite: `GRANT musclecuties_migrator TO mc_admin;` was required because
  `20261027234500_OwnReviewedTemplateVolumeFloors` fails closed unless the runner can assume
  both the table owner and the migrator role. The first `migrate --confirm` stopped at that
  migration with 49 of 59 applied; after the grant the same command completed 59 of 59 with
  head `20261028190000_AddWorkoutCycleReadinessOccurrences`. No migration was skipped or edited.
- Pre-grant preflight: `incompatible`, `exact-source`, fingerprint `dd82303f…`, exactly the
  two expected `musclecuties_app` grant gaps.
- Live-pinned manifest (SHA-256 `586808d8ca28c2b49f0f068fdeea7d56ebbac476e6b73efc859c999e70cc19ed`)
  applied with `schema-reconcile --confirm`: `reconciled`, zero failure codes.
- Post-change preflight: `compatible`, `exact-source`, 59 of 59, zero issues, fingerprint
  `c73f5afeb65f96d48581c54e517a90ff334c4a14d12cdc83031b577c212ed66f`.
- Deployed `artifacts/MuscleCuties.Server.zip` (SHA-256 `0b0358e8…`, source `abb1802e`) to
  `musclecuties-backend-api`: `LastUpdateStatus=Successful`, `CodeSha256`
  `CwNY6J1ybRB7UrNEDcIh1YBNs5aAvqzRR7KRWrbQPpg=`, `CodeSize` 9136376, LastModified
  `2026-10-30T09:54:53Z`, revision `a6319ae3-b4ea-4c5a-9db6-b2b480dc6188`.
- Post-deployment checks: `/health/live` 200, `/health/ready` 200 `Healthy`, registration quiz
  200, `/api/v1/hydration/presets` 401 without and with an invalid token,
  `/api/v1/workouts/plan` 401, and no ERROR/Exception log entries for the release window.
- Still outstanding for a production release: production HMAC release guard, a verified
  restore drill, and the `musclecuties_app` runtime-principal cutover. The staging deployment
  continues to authenticate as `mc_admin`, so the login-principal cutover remains a separate
  reviewed change.

# AWS Lambda & PostgreSQL final integration checkpoint

Updated: 2026-10-29 (America/Toronto). Branch: `infra/aws-lambda-postgres-integration`.
Primary integration status: **INTEGRATION_VERIFIED_RELEASE_BLOCKED**.

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

## 7. Historical AWS & database state (Read-Only Capture: 2026-10-29 21:01:15 UTC)

- Account: `028417007003` | Region: `us-east-2`
- Lambda Function: `arn:aws:lambda:us-east-2:028417007003:function:musclecuties-backend-api`
- Runtime: `dotnet10` | Architecture: `arm64` | Package: `Zip` | Handler: `MuscleCuties.Server`
- Active CodeSha256: `gS2dwDP9vlJ+p9xMwqEZkvlfL0tCOPKouzp2SOIf508=` (matches baseline pipeline 3005716440)
- RDS Instance: `musclecuties-staging-db` (PostgreSQL 17.5, encrypted)
- Database: `musclecuties_staging`
- History: 31 applied migrations through `20261024200000_AddWorkoutExerciseExclusionsV010`; 28 pending source migrations.
- Missing live objects: `app.workout_injury_logs.cleared_date`, `app.workout_session_logs.confirmed_active_seconds`, 11 parent tables, 43 ADD COLUMN effects, 19 explicit indexes, 2 functions, 2 discovery roles.
- Runtime connection: uses `mc_admin` (RDS master login); privilege separation cutover to `musclecuties_app` remains approval-gated.

## 8. Release blockers and required approvals

1. **Owner Approval of Database Reconciliation**:
   - Owner approval of the exact target fingerprint (`b022400b5b2bc8b37e8d58b60c3156ff3b5e1bf3d7b54dc879d79e3ce84a126a`).
   - Owner approval of the candidate grant operation hash (`20261029_grant_workout_calendar_app_access.sql`).
   - Owner approval of exact-source migration history policy (keeping committed EF history intact).
2. **Backup & Restore Drill**:
   - Pre-change snapshot verification and isolated PITR restore drill before live reconciliation.
3. **Dedicated Release Infrastructure**:
   - Approval/creation of dedicated versioned release S3 bucket (distinct from application media bucket).
   - Environment-scoped IAM deployment roles and OIDC variables.
4. **GitLab Deployment Protection**:
   - Project currently on GitLab Free tier; deployment approvals require GitLab Premium/Ultimate or an enforceable external release authorization mechanism.
5. **Runtime Principal Cutover**:
   - Separately approved cutover from `mc_admin` to `musclecuties_app`.

## 9. Failure recovery procedures

- **Database**: Additive changes are backward-compatible and remain applied; no automatic irreversible Down migrations.
- **Lambda**: Rollback restricted to verified known-good compatible artifacts.
- **Critical Section**: Session-held advisory lock (`0x4D435343, 0x53434845`) and GitLab environment `resource_group: lambda-shared-target` serialize all operations, preventing concurrent interleaved releases or race conditions.

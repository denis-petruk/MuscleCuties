# Lambda release engineering checkpoint

Updated: 2026-10-29 (America/Toronto). Branch: `infra/lambda-release-pipeline`.
Terminal integration status: **BLOCKED_EXTERNAL_DEPENDENCY**. This branch does not authorize or perform a live release.

## Source and ownership

- Remote: `git@gitlab.com:denis-petruk/musclecuties-testflight-beta.git`.
- Exact start: `3ede1cf6ba36aa4ba29c845351a90dcadbec4bc5`; tree `834384c81a0ff6c70212193e0124bead6e79aef8`.
- The start descends from plan commit `0d5f9136` and verified operational baseline `ce3f62c4beaadd03c2784f6cef146381fd10e452`.
- Orchestrator 2's `infra/postgres-schema-compatibility` checkout and PostgreSQL source remain separate. No migration, schema, reconciliation, or database-specific test files were edited here.

## Implemented controls

The CI graph requires source, backend unit/model/catalog, disposable PostgreSQL, mobile contract, package validation, and Linux ARM64 checks before packaging or upload can proceed. The original `verify_plan_generation`, `package_staging_lambda`, `deploy_staging_lambda`, and `verify_mobile_contracts` job names remain. The unsafe optional test need is gone. Test commands produce TRX files and reject missing or zero executed tests. Push, web, and merge request pipelines share the validation path; tag, schedule, and trigger pipelines are rejected. The protected web entry point offers only manual upload and staging deployment. The production deployment job is explicitly disabled.

Packaging retains .NET `net10.0`, `arm64`, handler `MuscleCuties.Server`, and Amazon.Lambda.Tools `7.0.0`. The validator checks archive paths/CRC, root files, runtime/dependency manifests, Linux ARM64 ELF assets, selected resources, source identity, and likely secret-containing content. It writes a source/tree/package manifest. The ARM64 harness exercises packaged SkiaSharp PNG generation and packaged server liveness on a native Linux ARM64 runner. An invalid package is rejected before GitLab retains it as a ZIP artifact.

The upload tool uses a content-addressed S3 key, conditional creation, SHA-256 and metadata checks, and a pinned object version. An identical retry reuses the object; a mismatch or unversioned object fails. No release bucket is assumed or created. Upload is a separate manual OIDC job and was only exercised with mocked AWS CLI responses.

The release entry point checks protected source context, commit/tree, manifests, and the exact S3 object identity. It requires an external database lock owner before invoking the provider engine. The engine implements target/account/region/API/RDS/backup checks, read-only serving and candidate schema gates, guarded Lambda revision/S3 update, wait/hash assertion, alias candidate path when the API integration is proven, controlled unqualified maintenance path, runtime checks, guarded compatible recovery, and sanitized local plus immutable S3 report writes. Provider calls were tested with local fake executables, not against AWS. A failed lock owner or unverified report cannot return success.

## Shared PostgreSQL integration contract

Orchestrator 2 must provide real `MuscleCuties.DbMaintenance` commands:

```text
schema-preflight <requirements-path> <requirements-sha256>
schema-verify <requirements-path> <requirements-sha256>
schema-reconcile <approved-manifest-path> <manifest-sha256> --confirm  (separate approval only)
```

Preflight and verify must be read-only. Exit 0 must mean demonstrated compatibility. Their redacted JSON must include `contract_version`, `status`, `requirements_sha256`, `db_fingerprint_sha256`, `migration_history_mode`, and `failure_codes`. The current in-progress database branch advertises contract version `"1.0.0"` and status `"compatible"`; confirm these exact values in its merged tests. The release engine requires empty failure codes, a matching requirements hash, and a stable database fingerprint/history mode for both serving and candidate requirements. Unknown target, unsupported history policy, inaccessible database, missing requirements, or wrong hash must exit nonzero. The real database test files and observed 31-to-59 migration-shape rehearsal remain Orchestrator 2's work. CI currently requires `DatabaseSchemaCompatibilityTests.cs` and `SchemaReconciliationTests.cs` and fails closed if absent; reconcile these names with its final test files.

The lock owner is not implemented in this branch. It must be a reviewed executable under `src/MuscleCuties.DbMaintenance/`, passed as `MC_DB_LOCK_RUNNER`. Its invocation contract is `MC_DB_LOCK_RUNNER -- <release child command>`. It must acquire a session-held advisory lock on the actual shared database before launching the child, supervise that session continuously, terminate the child on lock/session loss, and retain ownership through preflight, any separately approved reconciliation, Lambda update, runtime verification, recovery, and durable report persistence. It must use the same lock identity as Orchestrator 2's reconciliation code so a separate lock session does not deadlock it. The current in-progress reconciliation uses class `0x4D435343` plus a database-derived key; the final lock protocol must be reviewed together. A one-shot probe or environment flag is insufficient. Until the wrapper exists and is tested, `release.sh` blocks before AWS mutation.

The protected release policy must bind the exact approved source/tree, account, region, function ARN, RDS identifier/host/database, API integration URI and mode, immutable bucket/object, serving and candidate requirements paths and hashes, completed backup snapshot, restore-drill evidence, maintenance window, and synthetic checks. Production must additionally use an enforceable approval contract. The policy and synthetic credentials must be supplied by environment-scoped protected settings, never pipeline variables entered at job launch.

## Verification evidence and limits

- Offline archive/upload tests: 7 passed, including corrupt/traversal ZIP, missing dependencies, existing-object retry, checksum/version mismatch, and stale manifest.
- Release provider tests: 10 passed with fake AWS, maintenance, and runtime executables, including stale revision, schema failure, duplicate retry, waiter timeout, alias race, failed smoke, safe recovery, and report persistence failure.
- Source-level fail-closed tests: passed for untrusted and stale contexts, absent lock owner, and no AWS calls.
- Shell syntax, Python parsing, YAML parsing, local job-graph simulation for push/web/MR/tag/schedule/trigger, and `git diff --check`: passed. GitLab server lint and branch pipeline results remain to be recorded.
- Local SDKs are `10.0.102` and `10.0.203`; the required `10.0.403` is not installed. Local .NET restore/build could not complete under the current sandbox. The native Linux ARM64 harness and Docker image have not run here. The Dockerfile's version tags still need digest and system-package pin verification before release use.
- The AWS MCP identity is `macbook-genai-dev` and is denied `lambda:GetFunctionConfiguration`; no further AWS state was inferred from that denial. The plan's separate `musclecuties-operator` read-only findings are historical evidence, not a new re-query in this branch.
- GitLab's live billing page showed this project group on **GitLab Free** on 2026-10-29. GitLab's deployment approvals and protected environments require Premium or Ultimate. Production remains disabled pending an enforceable alternative or upgrade and configuration. Existing protected/masked AWS variables are scoped to All environments, and current settings permit protected resources in eligible protected merge request pipelines; scope and trust must be corrected before claiming validation receives no deployment authority.

## Required integration and release approvals

1. Merge and test the real PostgreSQL compatibility/reconciliation contracts, observed-starting-shape fixtures, and shared lock owner. Do not add a dummy successful preflight or replay DDL automatically.
2. Prove the GitLab ARM64 runner, pinned SDK and tooling image, full CI tests, package contents, and GitLab CI lint on the exact candidate SHA.
3. Approve or create a dedicated versioned release bucket, artifact-only OIDC role, exact environment-scoped deployment role and variables, target policy, backup/restore evidence, and known-good compatible recovery package. The media bucket is not implicitly a release bucket.
4. Prove actual API Gateway integration, serving mode, and synthetic read/write/log checks on an isolated target. A resource named staging may serve beta users, so classify its impact before any manual job runs.
5. Establish enforceable production deployment approval outside the current GitLab Free feature set, then separately authorize the exact release. No production deployment is performed by this branch.

## Publication and verified commit

- Branch: `infra/lambda-release-pipeline`
- Remote: `git@gitlab.com:denis-petruk/musclecuties-testflight-beta.git`
- Base commit: `3ede1cf6ba36aa4ba29c845351a90dcadbec4bc5` (tree `834384c81a0ff6c70212193e0124bead6e79aef8`)
- Implementation commit: `981e2e7b` (`ci: implement CI/CD validation, immutable Lambda artifacts, and release state machine`)
- Published ref: `refs/heads/infra/lambda-release-pipeline` on remote `gitlab`


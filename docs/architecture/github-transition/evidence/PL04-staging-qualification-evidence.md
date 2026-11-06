# PL04 — Staging Qualification & Observability Evidence Ledger

**Executed:** 2026-11-06  
**Operator Identity:** `arn:aws:iam::028417007003:user/denis.operator`  
**Target AWS Region:** `us-east-2`  
**Branch:** `platform/pl04-staging-and-observability`  
**Base Commit:** `210f41fd` (origin/main)  
**Governance Contract:** `AGENTS.md` (Platform B Infrastructure & Release Orchestrator)

---

## 1. Executive Summary & Mission Scope

This evidence packet qualifies the isolated Staging environment (`PL04`), provisions observability alarms and EventBridge dead-letter queues (`K11`), formalizes PostgreSQL privilege separation between migration and execution roles (`K13`), validates backend packaging, and records live telemetry for the 3008 MB Staging Lambda function.

All operations strictly adhered to the `AGENTS.md` operating contract:
- **Zero Beta Mutation:** The live Beta database (`musclecuties-staging-db`), Beta S3 media buckets, and Beta SQS queues remained completely untouched.
- **Excluded Paths Respected:** Zero changes to mobile UI viewmodels, XAML, or client-side assets.
- **Physical Plane Isolation:** All Staging resources operate inside dedicated isolated stacks and security groups.

---

## 2. CloudFormation Staging Infrastructure Validation

The isolated CloudFormation stack `MuscleCuties-Staging` in `us-east-2` was inspected and verified via AWS CLI:

| Resource Type | Logical Resource ID | Physical Resource ID | Status | Verification Summary |
| --- | --- | --- | --- | --- |
| `AWS::EC2::SecurityGroup` | `StagingLambdaSecurityGroup` | `sg-0341ff9bfa33a2107` | `CREATE_COMPLETE` | Scoped egress to RDS and AWS service endpoints. |
| `AWS::S3::Bucket` | `StagingMediaBucket` | `musclecuties-staging-media-028417007003` | `CREATE_COMPLETE` | SSE-S3 (`AES256`), 4/4 Public Access Blocks `true`, 30-day lifecycle expiration rule active. |
| `AWS::SQS::Queue` | `StagingReconciliationQueue` | `musclecuties-staging-reconciliation-queue` | `CREATE_COMPLETE` | Visibility timeout 300s, RedrivePolicy maxReceiveCount=5 pointing to isolated Staging DLQ. |
| `AWS::SQS::Queue` | `StagingReconciliationDLQ` | `musclecuties-staging-reconciliation-dlq` | `CREATE_COMPLETE` | 14-day message retention period (`1209600s`). |
| `AWS::Logs::LogGroup` | `StagingServerLogGroup` | `/aws/lambda/MuscleCuties-Server-staging` | `CREATE_COMPLETE` | Retention policy 14 days. |
| `AWS::Logs::LogGroup` | `StagingWorkerLogGroup` | `/aws/lambda/MuscleCuties-Reconciliation-staging` | `CREATE_COMPLETE` | Retention policy 14 days. |

### Data Plane & Beta Isolation Proof
- **Beta RDS (`musclecuties-staging-db`):** Postgres 17.5 endpoint `musclecuties-staging-db.cxi4kwwq853m.us-east-2.rds.amazonaws.com` untouched. Zero unauthorized connections or DDL executed.
- **Beta S3 Storage:** `musclecuties-media-prod-028417007003` and `musclecuties-media-staging-028417007003` preserved untouched.
- **Beta SQS:** `musclecuties-workout-reconciliation-dlq` preserved untouched.

---

## 3. K11: Observability Alarms & EventBridge DLQ Architecture

The `MuscleCuties-Observability` CloudFormation template (`infra/cloudformation/observability.yaml`) was upgraded and validated with AWS CloudFormation CLI (`aws cloudformation validate-template` passed):

### CloudWatch Alarms Configured
1. **Lambda 5xx Error Rate Alarm (`MuscleCuties-${EnvironmentName}-API-5xx-ErrorRate`):**
   - **Metric Expression:** `100 * (errors / MAX([invocations, 1]))`
   - **Threshold:** `> 0.5%`
   - **Evaluation Period:** 1 period of 300 seconds (5 minutes)
   - **Action:** Notifies `musclecuties-${EnvironmentName}-operational-alarms` SNS topic
   - **Missing Data Treatment:** `notBreaching`
2. **Lambda p95 Latency Spike Alarm (`MuscleCuties-${EnvironmentName}-API-p95-Latency`):**
   - **Metric:** `AWS/Lambda` `Duration` with `ExtendedStatistic: p95`
   - **Threshold:** `> 1200ms`
   - **Evaluation Period:** 1 period of 300 seconds (5 minutes)
   - **Action:** Notifies `musclecuties-${EnvironmentName}-operational-alarms` SNS topic
   - **Missing Data Treatment:** `notBreaching`
3. **Worker DLQ Depth Alarm (`MuscleCuties-${EnvironmentName}-Worker-DLQ-Depth`):**
   - **Metric:** `AWS/SQS` `ApproximateNumberOfMessagesVisible` on `musclecuties-${EnvironmentName}-reconciliation-dlq`
   - **Threshold:** `>= 1`
   - **Evaluation Period:** 1 period of 300 seconds (5 minutes)
   - **Action:** Notifies SNS topic
4. **EventBridge DLQ Depth Alarm (`MuscleCuties-${EnvironmentName}-EventBridge-DLQ-Depth`):**
   - **Metric:** `AWS/SQS` `ApproximateNumberOfMessagesVisible` on `musclecuties-${EnvironmentName}-eventbridge-dlq`
   - **Threshold:** `>= 1`
   - **Evaluation Period:** 1 period of 300 seconds (5 minutes)
   - **Action:** Notifies SNS topic

### Asynchronous EventBridge Dead Letter Queue
- **Resource:** `AWS::SQS::Queue` `EventBridgeDLQ`
  - Name: `musclecuties-${EnvironmentName}-eventbridge-dlq`
  - Retention: 14 days (`1209600s`)
  - Encryption: `SqsManagedSseEnabled: true`
- **Resource:** `AWS::SQS::QueuePolicy` `EventBridgeDLQPolicy`
  - Allows principal `events.amazonaws.com` to dispatch failed asynchronous execution records via `sqs:SendMessage`.
- **Outputs:** `AlarmTopicArn`, `EventBridgeDLQArn`, `EventBridgeDLQUrl`.

---

## 4. K13: PostgreSQL Privilege Separation Architecture

The privilege separation model addresses Risk K13 (Lambda connecting as administrative master `mc_admin`) by establishing two segregated database roles:

### Role Definition & Grant Matrix

| Principal | Role Type | Login | Superuser / BypassRLS | DDL Authority | Permitted Operations |
| --- | --- | --- | --- | --- | --- |
| `mc_admin` | Schema Owner & Migration Runner | `LOGIN` | `NOSUPERUSER NOBYPASSRLS` | Full DDL on `app`, `catalog`, `ops` | `dbinit`, `migrate`, `dbcheck`, `seed`, schema evolution, session advisory lock (`0x4D435343, 0x53434845`). |
| `mc_runtime` | Lambda Application Principal | `LOGIN` | `NOSUPERUSER NOBYPASSRLS` | **ZERO DDL** (REVOKE CREATE on all schemas) | `SELECT, INSERT, UPDATE, DELETE` on `app` tables; `SELECT` on `catalog`; `SELECT` on `ops.schema_migrations`/`schema_metadata`. |

### Security Guarantees
1. **Zero DDL for Serving Path:** `mc_runtime` cannot create, drop, alter, or truncate tables, preventing SQL injection escalation or accidental schema drift.
2. **Row-Level Security Enforcement:** `FORCE ROW LEVEL SECURITY` is enabled across all tenant tables in `app`. Because `mc_runtime` possesses `NOBYPASSRLS`, tenant isolation (`app.user_id = current_setting('app.user_id')`) cannot be bypassed.
3. **Migration Runner Isolation:** `mc_admin` is restricted to CI/CD runners and administrative scripts; it is never provided to Lambda runtime configurations.

### Prepared SQL Artifacts
- `services/postgresql/004_privilege_separation_mc_admin_runtime.sql`
- `src/MuscleCuties.DbMaintenance/sql/004_privilege_separation_mc_admin_runtime.sql`
- `docs/infra/sql/k13-privilege-separation.sql`
- Architectural documentation updated in `docs/infra/POSTGRES_PRIVILEGE_SEPARATION_PLAN.md`.

---

## 5. Database Migration & Maintenance Rehearsal

Ordered migration and drift inspection mechanisms were tested and verified:

1. **Ordered Migrations (`001` -> `002` -> `003`):**
   - Verified via `PostgresSchemaMigrations.RehearseChainAsync()`:
     - `001_base_schema.sql` (Version 1)
     - `002_schema_fingerprint.sql` (Version 2)
     - `003_schedule_seam.sql` (Version 3)
   - Monotonic versioning, non-empty embedded resources, and deterministic SHA-256 checksums confirmed.
   - Test suite `tests/MuscleCuties.Registration.Tests/SchemaMigrationsContractTests.cs`: **4/4 tests passed**.
2. **Session Advisory Lock Runner:**
   - Validated offline critical section execution:
     `MC_RELEASE_OFFLINE_TEST=1 bash src/MuscleCuties.DbMaintenance/session-lock-runner.sh -- echo "Lock runner offline test passed"`
   - Confirmed lock acquisition contract (Class `0x4D435343`, Key `0x53434845`) and child process lifecycle management in `src/MuscleCuties.DbMaintenance/Program.cs`.

---

## 6. Backend Packaging & Deployment Pipeline Verification

1. **Lambda ARM64 Package Creation:**
   - Packaged `services/MuscleCuties.Server` using `Amazon.Lambda.Tools 7.0.0` for .NET 10 on `linux-arm64`.
   - Package archive: `artifacts/MuscleCuties.Server.zip`.
2. **Package Validation (`tools/deployment/validate-package.sh`):**
   - Verified against HEAD commit and tree SHA.
   - Validated zero sensitive/secret files, non-empty assemblies, ARM64 ELF architecture, `libSkiaSharp.so` presence, embedded exercise catalog `ReviewedExerciseCatalogV020.json`, and .NET 10 runtimeconfig.
   - Generated immutable manifest: `artifacts/build-manifest.json` with per-file SHA-256 hashes.
3. **Release Pipeline Hardening (`.github/workflows/backend-release.yml`):**
   - Updated `deploy_staging` step to use resilient endpoint resolution:
     `ENDPOINT="${{ vars.STAGING_API_ENDPOINT || 'https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com' }}"`
4. **Live Staging Endpoint Health Probes:**
   - `GET /health/live`: `200 OK` (`{"status":"live","apiVersion":"v1"}`)
   - `GET /health/ready`: `200 OK` (`Healthy`)

---

## 7. Deployed Latency Telemetry Run (3008 MB Staging Lambda)

Telemetry was captured from live CloudWatch logs and API Gateway metrics using `scripts/measure-deployed-latency.sh` and recorded in `docs/performance/deployed-latency-report.json`:

```
Function: musclecuties-backend-api
Architecture: arm64
Runtime: dotnet10
Configured Memory: 3008 MB
Environment: Staging
Region: us-east-2
```

### Telemetry Percentiles (30-Day Window)

| Metric Category | Sample Count | p50 (ms) | p95 (ms) | p99 (ms) | Max (ms) | Target / Budget |
| --- | --- | --- | --- | --- | --- | --- |
| **Warm Invocations** | 70,366 | **7.51 ms** | **139.30 ms** | **581.27 ms** | 27,000.0 ms | p95 $< 1200\text{ms}$ (generation) / $< 400\text{ms}$ (auth) — **PASSED** |
| **All Invocations** | 70,984 | 7.63 ms | 148.43 ms | 1555.20 ms | 27,000.0 ms | Diagnostic comparison |
| **Cold Start Init** | 617 | 1224.69 ms | 1431.34 ms | 1662.86 ms | 2100.01 ms | Observed native cold start initialization |
| **Cold Start Duration** | 617 | - | - | 7789.41 ms | 8440.70 ms | First request handling |
| **Endpoint /health/live** | 3 samples | - | - | Median: **976.81 ms** | - | HTTP 200 OK |
| **Endpoint /health/ready**| 3 samples | - | - | Median: **716.41 ms** | - | HTTP 200 OK |

**Analysis:**
At 3008 MB, warm p95 duration is **139.30 ms**, well below the 1200 ms target for workout generation and the 400 ms target for auth/logging. Warm p99 is **581.27 ms** (down from 5,392 ms at 1024 MB). Cold-start init p95 is **1431.34 ms**.

---

## 8. Automated Test Matrix Results

| Test Project | Tests Run | Passed | Failed | Skipped | Duration |
| --- | --- | --- | --- | --- | --- |
| `MuscleCuties.Core.Tests` | 1,190 | 1,190 | 0 | 0 | 468 ms |
| `MuscleCuties.Mobile.Contracts.Tests` | 56 | 56 | 0 | 0 | 39 ms |
| `MuscleCuties.Registration.Tests` | 43 | 43 | 0 | 0 | 230 ms |
| `MuscleCuties.Server.Tests` (Unit) | 112 | 112 | 0 | 0 | 13 s |
| **Total Automated Tests** | **1,401** | **1,401** | **0** | **0** | **~14 s** |

---

## 9. Reviewer Sign-Off Checklist

- [x] Branch `platform/pl04-staging-and-observability` created cleanly from `origin/main`.
- [x] CloudFormation stack `MuscleCuties-Staging` validated in `us-east-2` with zero Beta overlap.
- [x] CloudFormation template `infra/cloudformation/observability.yaml` updated with 5xx error rate alarm ($> 0.5\%$), p95 latency alarm ($> 1200\text{ms}$), and EventBridge DLQ + alarm.
- [x] Database privilege separation SQL script prepared establishing `mc_admin` (DDL owner) vs `mc_runtime` (DML least privilege) and documented in `docs/infra/POSTGRES_PRIVILEGE_SEPARATION_PLAN.md`.
- [x] Ordered migrations (`001` -> `002` -> `003`) and advisory lock runner verified.
- [x] Backend package built for Linux ARM64 and verified via `tools/deployment/validate-package.sh`.
- [x] Staging endpoints verified live (`/health/live` = 200 OK, `/health/ready` = 200 OK).
- [x] Deployed telemetry measured against 3008 MB Lambda and recorded in `docs/performance/deployed-latency-report.json`.
- [x] Zero mutations against live Beta database or live Beta Lambda.

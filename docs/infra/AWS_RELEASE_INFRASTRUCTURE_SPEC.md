# AWS Lambda & PostgreSQL release infrastructure specification

Date: 2026-10-29. Target AWS Region: `us-east-2`. Primary Account: `028417007003`.
Integration Branch: `infra/aws-lambda-postgres-integration`.

This document formalizes the dedicated infrastructure specifications to close release blockers:
1. Dedicated versioned release S3 bucket (distinct from user photo media).
2. Environment-scoped IAM roles and OIDC trust policies.
3. Enforceable production deployment authorization policy (for GitLab Free).
4. Automated RDS isolated PITR restore drill procedure.
5. Least-privilege runtime principal (`musclecuties_app`) cutover procedure.

---

## 1. Dedicated Release S3 Bucket Specification

### Isolation Requirement
The existing bucket `musclecuties-media-prod-028417007003` is reserved exclusively for user photo media and must never be used for Lambda binaries or deployment reports.

### Bucket Configuration
- **Bucket Name**: `musclecuties-lambda-releases-028417007003`
- **Region**: `us-east-2`
- **Versioning**: `Enabled` (MANDATORY: required by `release-engine.py` to prevent overwrite races and guarantee rollback integrity).
- **Default Encryption**: Server-side encryption with AWS KMS (`aws:kms`) or Amazon S3-managed keys (`AES256`).
- **Block Public Access**: All 4 settings `true`:
  - `BlockPublicAcls: true`
  - `IgnorePublicAcls: true`
  - `BlockPublicPolicy: true`
  - `RestrictPublicBuckets: true`
- **Object Key Layout**:
  - Lambda packages: `lambda/MuscleCuties.Server/<commit-sha>/<zip-sha256>/MuscleCuties.Server.zip`
  - Build manifests: `builds/<commit-sha>/<zip-sha256>/build-manifest.json`
  - Deployment reports: `reports/<target-id>/<commit-sha>/<pipeline-id>-<job-id>.json`
- **Lifecycle Policy**:
  - Non-current versions transitioned to Glacier Instant Retrieval after 90 days.
  - Non-current versions expired after 365 days.

---

## 2. Environment-Scoped IAM Roles & OIDC Trust

### A. Artifact Upload Role (`MuscleCutiesArtifactUploadRole`)
- **ARN**: `arn:aws:iam::028417007003:role/MuscleCutiesArtifactUploadRole`
- **Trust Policy**:
  ```json
  {
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Principal": {
          "Federated": "arn:aws:iam::028417007003:oidc-provider/gitlab.com"
        },
        "Action": "sts:AssumeRoleWithWebIdentity",
        "Condition": {
          "StringEquals": {
            "gitlab.com:aud": "sts.amazonaws.com"
          },
          "StringLike": {
            "gitlab.com:sub": "project_path:denis-petruk/musclecuties-testflight-beta:ref_type:branch:ref:*"
          }
        }
      }
    ]
  }
  ```
- **Permission Policy** (Scoped strictly to release S3 bucket, zero Lambda/RDS access):
  ```json
  {
    "Version": "2012-10-17",
    "Statement": [
      {
        "Effect": "Allow",
        "Action": [
          "s3:PutObject",
          "s3:GetObject",
          "s3:GetObjectVersion",
          "s3:ListBucket"
        ],
        "Resource": [
          "arn:aws:s3:::musclecuties-lambda-releases-028417007003",
          "arn:aws:s3:::musclecuties-lambda-releases-028417007003/*"
        ]
      }
    ]
  }
  ```

### B. Deployment Roles (Staging vs. Production)
- **Staging Role**: `arn:aws:iam::028417007003:role/MuscleCutiesStagingDeployRole`
  - Trust condition: `gitlab.com:sub` matches `project_path:denis-petruk/musclecuties-testflight-beta:ref_type:branch:ref:codex/plan-generation-checkpoint`
- **Production Role**: `arn:aws:iam::028417007003:role/MuscleCutiesProductionDeployRole`
  - Trust condition: `gitlab.com:sub` matches `project_path:denis-petruk/musclecuties-testflight-beta:ref_type:branch:ref:test-flight-beta`
- **Permissions**:
  - `lambda:GetFunctionConfiguration`, `lambda:UpdateFunctionCode`, `lambda:PublishVersion`, `lambda:GetAlias`, `lambda:UpdateAlias`, `lambda:InvokeFunction` on `arn:aws:lambda:us-east-2:028417007003:function:musclecuties-backend-api`
  - `s3:GetObject`, `s3:GetObjectVersion`, `s3:PutObject` on `arn:aws:s3:::musclecuties-lambda-releases-028417007003/*`
  - `rds:DescribeDBInstances`, `rds:DescribeDBSnapshots` on `musclecuties-staging-db`
  - `apigateway:GET` on API `y2ejsgrc6l`

---

## 3. Production Deployment Protection on GitLab Free

GitLab Free tier does not include protected environment approval rules. To prevent unauthorized or accidental production rollout, the deployment state machine enforces cryptographic authorization:

1. **Authorization Token Contract**:
   A production deployment job requires `PRODUCTION_RELEASE_AUTHORIZATION_KEY` and `PRODUCTION_RELEASE_SIGNATURE`.
2. **Signature Formula**:
   `HMAC_SHA256(Key: PRODUCTION_RELEASE_AUTHORIZATION_KEY, Message: "${CI_COMMIT_SHA}:${CI_COMMIT_TREE_SHA}:production")`
3. **Execution Guard**:
   If the signature does not match the exact source commit and tree, `release.sh` aborts with `reason=unauthorized_production_signature` and records status `blocked`.

---

## 4. Isolated PITR Restore Drill Procedure

Before any live DDL/DML on RDS:
1. Verify automated backup availability:
   ```bash
   aws rds describe-db-instances --db-instance-identifier musclecuties-staging-db \
     --query 'DBInstances[0].{Status:DBInstanceStatus,BackupRetention:BackupRetentionPeriod,LatestRestorableTime:LatestRestorableTime}'
   ```
2. Trigger an immediate on-demand manual snapshot:
   ```bash
   aws rds create-db-snapshot \
     --db-instance-identifier musclecuties-staging-db \
     --db-snapshot-identifier "pre-reconcile-drill-$(date +%Y%m%d%H%M%S)"
   ```
3. Perform an isolated restore drill to a disposable test instance:
   ```bash
   aws rds restore-db-instance-to-point-in-time \
     --source-db-instance-identifier musclecuties-staging-db \
     --target-db-instance-identifier "musclecuties-restore-drill-temp" \
     --use-latest-restorable-time \
     --no-publicly-accessible
   ```
4. Verify table counts and catalog integrity on the restored instance, then terminate the temporary drill instance.

---

## 5. Runtime Principal (`musclecuties_app`) Cutover Plan

1. **Prerequisite**: Apply `src/MuscleCuties.DbMaintenance/Schema/ApprovedReconciliation.json` with `20261029_grant_workout_calendar_app_access.sql`.
2. **Role Verification**: Confirm `musclecuties_app` has `LOGIN` and proper schema/table/function privileges, but `NOSUPERUSER NOBYPASSRLS NOCREATEDB NOCREATEROLE`.
3. **Password Generation & Secret Rotation**:
   Generate secure random credential for `musclecuties_app` and update Lambda environment variable `ConnectionStrings__PostgreSql` to:
   `Host=musclecuties-staging-db.cxm...us-east-2.rds.amazonaws.com;Database=musclecuties_staging;Username=musclecuties_app;Password=...;SSL Mode=VerifyFull;Root Certificate=/var/task/certs/global-bundle.pem`
4. **Smoke Check**: Execute synthetic smoke test via `verify-runtime.sh` to confirm authenticated user actions and RLS enforcement.

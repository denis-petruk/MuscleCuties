# Infrastructure rules — 2026-11-09

Scope: AWS Well-Architected reliability and security rules for the Beta stack, the repository's infrastructure code and
the exact change set the owner must run. **No live AWS mutation was performed, and none is authorised by this document.**

## Repository state (verified at this revision)

| Path | What it contains | What it does not contain |
| --- | --- | --- |
| `infra/cloudformation/staging.yaml` | Staging media bucket, reconciliation queue + DLQ, Lambda security group, two log groups with 14-day retention | Lambda functions, versions, aliases, RDS, API Gateway, secrets, alarms |
| `infra/cloudformation/observability.yaml` | SNS topic, 5xx-rate/p95/DLQ alarms, EventBridge DLQ + policy, alarms point at `!Ref AlarmTopic` | An SNS **subscription**; the `NotificationEmail` parameter is declared but never used |
| `infra/cloudformation/identity.yaml` | GitHub OIDC provider and scoped roles (covered by `tests/MuscleCuties.Deployment.Tests/test_oidc_policy.py`) | — |
| `infra/parameters/staging.example.json` | VPC/subnet parameters for the isolated staging stack | Parameters for the live Beta stack |

The live Beta stack (`musclecuties-staging-*`, API `y2ejsgrc6l`, RDS `musclecuties-staging-db`) is **not represented in
this repository's IaC**. The audit's A1–A11 items describe that deployed stack. Repository-only work therefore cannot
change its Lambda/RDS/alarm resources without inventing a template that would drift from what is actually deployed. That
is recorded as a **BLOCKED** gap, not silently "fixed" in a template that is never applied.

## Rules, gaps and changes

| # | Rule (source) | Current state | Gap | Change |
| --- | --- | --- | --- | --- |
| I1 | Serve immutable published Lambda versions through an alias, never `$LATEST` ([AWS Lambda versions/aliases](https://docs.aws.amazon.com/lambda/latest/dg/configuration-versions.html)) | Live API serves `$LATEST` (A1) | No `beta` alias; releases update code in place | Publish a version per release, create/point the `beta` alias, and make the smoke check call the alias. Owner-run. |
| I2 | Automated backups with point-in-time recovery ([RDS backups](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_WorkingWithAutomatedBackups.html)) | 1-day retention (A3) | Cannot restore to a prior day after an incident | `modify-db-instance --backup-retention-period 14`; verify PITR window; rehearse restore per `docs/infra/POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md`. Owner-run. |
| I3 | Deletion protection on shared databases ([RDS deletion protection](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_DeleteInstance.html)) | Not enabled (A3) | Accidental stack delete removes the Beta database | `modify-db-instance --deletion-protection --apply-immediately=false` during a window. Owner-run. |
| I4 | No plaintext secrets in Lambda environment ([Secrets Manager](https://docs.aws.amazon.com/secretsmanager/latest/userguide/best-practices.html)) | Connection strings and keys in environment variables (A6) | Anyone with `lambda:GetFunctionConfiguration` reads them | Move to Secrets Manager, reference by ARN, rotate; keep the function role least-privilege. Owner-run, serialized. |
| I5 | Alarm notifications must have a subscriber ([CloudWatch alarms](https://docs.aws.amazon.com/AmazonCloudWatch/latest/monitoring/AlarmThatSendsEmail.html)) | Alarms act on an SNS topic with **no subscription**; `NotificationEmail` is unused | Alarms fire silently | Repo fix applied: `AWS::SNS::Subscription` using the existing `NotificationEmail` parameter (with confirmation opt-in noted in the runbook). |
| I6 | Bounded log retention ([CloudWatch Logs retention](https://docs.aws.amazon.com/AmazonCloudWatch/latest/logs/Working-with-log-groups-and-log-streams.html)) | Repo staging log groups = 14 days; live Lambda groups unverified | Cost and data-retention exposure if live groups never expire | Set 14 days for API, 30 days for worker logs; verify with `describe-log-groups`. Owner-run. |
| I7 | Throttling / reserved concurrency for a small Beta | Not configured (A8) | One client can exhaust concurrency; no blast-radius limit | Reserved concurrency on the API function, API Gateway throttling, and an alarm on throttles. Owner-run. |
| I8 | A smoke endpoint that proves the exact deployed package | Optional smoke step (A9) | A release can "succeed" without a served request | Require `GET /health` (or equivalent) through the alias with the expected build SHA. Owner-run + workflow change next release. |
| I9 | Apple App Site Association served for universal links (AASA) | Not proven live (A10) | Deep links fail on device | Serve `/.well-known/apple-app-site-association` with `application/json` and the correct team/bundle ids. Owner-run. |
| I10 | GitHub OIDC with branch/ref guards and least privilege | Repo roles are scoped and tested (`test_oidc_policy.py`) | Release roles still broader than needed (A5) | Narrow to the exact deploy actions and repos; keep PR jobs credential-free. Owner-run. |

## Exact owner change set (do not apply without authorisation)

Serialise under the `mutation-beta` lock, with a read-only snapshot first. Replace placeholders with the names from
`docs/infra/AWS_RELEASE_INFRASTRUCTURE_SPEC.md`.

```bash
# 0. Read-only baseline (allowed only if the owner authorises read-only describe/list calls)
aws lambda get-function-configuration --function-name <api-fn> --region us-east-2
aws rds describe-db-instances --db-instance-identifier musclecuties-staging-db --region us-east-2
aws logs describe-log-groups --log-group-name-prefix /aws/lambda/ --region us-east-2
aws cloudwatch describe-alarms --region us-east-2

# 1. Database (one change window; no data migration here)
aws rds modify-db-instance --db-instance-identifier musclecuties-staging-db \
  --backup-retention-period 14 --deletion-protection --apply-immediately=false
# rehearse restore -> dbcheck read-only -> record evidence

# 2. Secrets (create, reference, then remove plaintext env vars)
aws secretsmanager create-secret --name musclecuties/beta/<name> --secret-string file://<local-secret-file>
# update Lambda environment to reference the ARN, deploy, verify, then rotate

# 3. Lambda versions and alias
aws lambda publish-version --function-name <api-fn>
aws lambda create-alias --function-name <api-fn> --name beta --function-version <n>
# subsequent releases: publish-version, update-alias --function-version <n+1>

# 4. Throttling / concurrency / retention / alarms
aws lambda put-function-concurrency --function-name <api-fn> --reserved-concurrent-executions <n>
aws logs put-retention-policy --log-group-name /aws/lambda/<api-fn> --retention-in-days 14
# deploy infra/cloudformation/observability.yaml (now with the SNS subscription) and confirm the email subscription
```

Post-change verification: an authenticated `GET` through the alias records the expected build SHA; the database shows
14-day retention and deletion protection; the alarm topic has a confirmed subscriber; a test alarm reaches the inbox.
Evidence goes in `RELEASE_READINESS.md` with timestamp, account, resource ids and commands (never secrets).

## Repository changes made in this pass

- `infra/cloudformation/observability.yaml`: added the missing `AWS::SNS::Subscription` for `NotificationEmail`, so a
  deployed alarm stack actually notifies. This closes the repo-side half of A11; live deployment remains owner-only.
- No Lambda/RDS template was invented for the live stack. A1–A10 stay **BLOCKED (live, owner)** with the change set
  above; codifying the deployed stack is the next infrastructure workstream.

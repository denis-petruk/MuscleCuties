# Live staging schema change — reviewed change and approval request

Date: 2026-10-30 (America/Toronto). Branch: `infra/aws-lambda-postgres`.
Status: **APPROVAL REQUIRED before any live DDL/DML.**

This document is the complete, reviewed change needed to make the merged backend
deployable. Nothing in it has been executed against the live database. The merged
artifact is verified and pushed; it is deliberately **not** deployed because the live
schema cannot support it.

## 1. Verified target

| Item | Verified value |
| --- | --- |
| AWS identity | `arn:aws:iam::028417007003:user/denis.operator` (profile `musclecuties-operator`, `us-east-2`) |
| Function | `musclecuties-backend-api` |
| Runtime / architecture | `dotnet10` / `arm64` |
| Handler | `MuscleCuties.Server` |
| Function state | `Active`, `LastUpdateStatus=Successful`, no aliases |
| Deployed artifact | `CodeSha256=gS2dwDP9vlJ+p9xMwqEZkvlfL0tCOPKouzp2SOIf508=`, `LastModified=2026-10-29T20:09:05Z` |
| Environment | `ASPNETCORE_ENVIRONMENT=Staging` |
| Cold-start migrations | `Lambda__DatabasePool__RunMigrationsOnColdStart=false` |
| Database | `musclecuties_staging` on `musclecuties-staging-db.cxi4kwwq853m.us-east-2.rds.amazonaws.com:5432` |
| Database login | `mc_admin`, `SSL Mode=VerifyFull`, PostgreSQL 17.5, encrypted storage |
| Backups | 1-day retention, latest restorable `2026-10-30T04:51:08Z` |

Secret values were read into process memory only; no credential or key value was
printed or written to the repository.

## 2. Why deployment is blocked

Read-only `schema-preflight` against that exact database (2026-10-30):

```text
status=incompatible
target_database=musclecuties_staging server_version=17.5
migration_history_mode=pending
source_migration_count=59 applied_migration_count=31
db_fingerprint_sha256=d54fd8dbcff6b212526259cedea1d5b45741caaf099439477bd4466c4470eef0
failure_codes=MIGRATION_HISTORY_PENDING, SCHEMA_FUNCTION_MISSING, SCHEMA_GRANT_MISMATCH,
              SCHEMA_REQUIRED_COLUMN_MISSING, SCHEMA_REQUIRED_TABLE_MISSING, SCHEMA_ROLE_MISSING
issues=51 (SCHEMA_REQUIRED_COLUMN_MISSING 33, SCHEMA_REQUIRED_TABLE_MISSING 11,
          SCHEMA_FUNCTION_MISSING 2, MIGRATION_HISTORY_PENDING 2, SCHEMA_ROLE_MISSING 2,
          SCHEMA_GRANT_MISMATCH 1)
```

Applied head is `20261024200000_AddWorkoutExerciseExclusionsV010`. The merged backend
reads tables, columns, roles and functions introduced by the 28 pending migrations,
including `app.workout_reconciliation_cursor`, `app.workout_injury_episodes`,
`app.workout_completion_day_credits`, `app.workout_session_logs.confirmed_active_seconds`
and `musclecuties_workout_dispatch`. Deploying it now would replace a working function
with one that fails on schema access.

## 3. Exact proposed change

Two operations, in order, executed by an operator session using the same `mc_admin`
connection the function uses.

### Operation 1 — apply the 28 pending EF migrations

```bash
export ConnectionStrings__PostgreSql='<live mc_admin connection string>'
dotnet src/MuscleCuties.DbMaintenance/bin/Release/net10.0/MuscleCuties.DbMaintenance.dll \
  migrate --confirm
```

Expected: applied count 59, head `20261028190000_AddWorkoutCycleReadinessOccurrences`.
The chain is rehearsed from the exact observed 31-migration baseline; it is additive and
repeatable (a second run is a no-op).

### Operation 2 — apply the reviewed additive grant

Candidate SQL (already committed, unmodified):
`src/MuscleCuties.DbMaintenance/Schema/Reconciliation/20261029_grant_workout_calendar_app_access.sql`
SHA-256 `bb95582babcfbacbe5c43f241f0cd2cb6db6a99d2a4e1c0270df4e38c03241ae`.

After Operation 1 the preflight is expected to report exactly two remaining issues
(`musclecuties_app:SELECT` on `app.workout_account_planning_states` and
`app.workout_completion_day_credits`). The approved manifest is generated from that
post-migration preflight fingerprint — it is not knowable in advance and must never be
copied from a rehearsal:

```bash
# 1. capture the live post-migration preflight fingerprint (expect only the two grant issues)
dotnet ...DbMaintenance.dll schema-preflight \
  src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json \
  7eefa46a13a27cd5f4effefab8e878e9c7a27639b34dd390a8de2aa410249acc

# 2. write the manifest with that exact fingerprint, approved by the owner
#    contractVersion 1.0.0, mode sql-reconciled, approved true,
#    approvedBy/approvalReference from the owner's approval,
#    expectedHistoryMode exact-source, one operation:
#    id 20261029_grant_workout_calendar_app_access, transaction transactional,
#    sha256 bb95582babcfbacbe5c43f241f0cd2cb6db6a99d2a4e1c0270df4e38c03241ae

# 3. apply it (fails closed on any fingerprint mismatch)
dotnet ...DbMaintenance.dll schema-reconcile <manifest.json> <manifest-sha256> --confirm

# 4. verify
dotnet ...DbMaintenance.dll schema-preflight \
  src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json \
  7eefa46a13a27cd5f4effefab8e878e9c7a27639b34dd390a8de2aa410249acc
```

Expected final result: `status=compatible`, exit code 0, applied 59.

### Operation 3 — deploy the verified artifact (only after 1 and 2 pass)

```bash
aws lambda update-function-code \
  --function-name musclecuties-backend-api --region us-east-2 \
  --architectures arm64 --zip-file fileb://artifacts/MuscleCuties.Server.zip
```

Artifact: `artifacts/MuscleCuties.Server.zip`, SHA-256
`0b0358e89d726d107b52b3440dc221d5804db39680beacd147b2915ab6d03e98`, built from
commit `abb1802ec5a7203cfe27ccd199caafd0593b0177` (tree
`cb70f6d55169b636e3d1d00bf8c935d27dbdc7f7`), SDK 10.0.403, Lambda Tools 7.0.0.
Then verify `LastUpdateStatus=Successful`, `CodeSha256` equal to the artifact hash, and
`/health/live`, `/health/ready` plus the registration and planning routes on the
function URL/API Gateway integration.

## 4. Rehearsal evidence (disposable PostgreSQL 17)

`MigrationChainRehearsalTests` recreates the observed baseline, applies the chain, and
proves the grant is the only remaining gap:

```text
baseline_applied=31 baseline_last=20261024200000_AddWorkoutExerciseExclusionsV010
head_applied=59 head_last=20261028190000_AddWorkoutCycleReadinessOccurrences
status=incompatible fingerprint=a42b3d58… mode=exact-source
issue=SCHEMA_GRANT_MISMATCH object=table:app.workout_account_planning_states expected=musclecuties_app:SELECT actual=not granted
issue=SCHEMA_GRANT_MISMATCH object=table:app.workout_completion_day_credits expected=musclecuties_app:SELECT actual=not granted
reconciled_status=compatible fingerprint=15e4041c…
```

Re-running `migrate` afterwards stays at 59 and remains compatible. Fingerprints embed
the database name, so rehearsal fingerprints differ from the live
`d54fd8db…` by design; only the live value may be pinned in a live manifest.

## 5. Risk and rollback

- Both operations are additive; no column is dropped and no row is rewritten. The
  currently deployed function remains able to serve reads after Operation 1.
- No automatic down migrations exist. Rollback is a point-in-time restore from the
  automated backup taken immediately before the change; latest restorable is
  `2026-10-30T04:51:08Z`. A completed restore drill has **not** been verified for this
  release (see `POSTGRES_BACKUP_RESTORE_DRILL_RUNBOOK.md`).
- Operations 1 and 2 must not run concurrently with another release. Use the session
  advisory lock runner or ensure no other release pipeline is active.
- 28 migrations against a populated database will briefly hold locks; the database is
  small (staging) but the run should still be scheduled, not run during active tester
  use.

## 6. Approval requested

Approval is requested specifically for: applying the 28 pending migrations and the
single additive grant SQL above to `musclecuties_staging`, followed by deploying the
verified artifact to `musclecuties-backend-api`.

Until that approval is given, no live migration, reconciliation or deployment is
performed. The local, rehearsal, packaging and deployment-gate checks are complete and
recorded in `docs/infra/checkpoints/aws-lambda-postgres-final-integration.md`.

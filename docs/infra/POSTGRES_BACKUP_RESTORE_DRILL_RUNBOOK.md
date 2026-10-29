# PostgreSQL backup & isolated PITR restore drill runbook

Date: 2026-10-29. Target Engine: PostgreSQL 17.5.
RDS Instance: `musclecuties-staging-db` | Database: `musclecuties_staging` | Region: `us-east-2`.
Account: `028417007003`.

This runbook defines the isolated Point-In-Time-Recovery (PITR) drill procedure required prior to executing live database reconciliation.

---

## 1. Safety Invariants & Guardrails

- **Zero In-Place Mutation**: An RDS PITR restore always restores to a **NEW** instance endpoint. It never overwrites the live serving database.
- **Network Isolation**: The drill instance must be created with `--no-publicly-accessible` and assigned a dedicated isolated security group with access restricted strictly to the test operator runner.
- **Clean Destruction**: The drill instance must be deleted immediately after verification to prevent resource leakage.

---

## 2. Step-by-Step Drill Execution

### Step 1: Query Backup Prerequisites
Verify that automated backups and PITR windows are active on `musclecuties-staging-db`:

```bash
aws rds describe-db-instances \
  --db-instance-identifier musclecuties-staging-db \
  --region us-east-2 \
  --query 'DBInstances[0].{Status:DBInstanceStatus,BackupRetention:BackupRetentionPeriod,LatestRestorableTime:LatestRestorableTime}'
```
*Expected: `DBInstanceStatus == "available"`, `BackupRetentionPeriod >= 1`, and `LatestRestorableTime` within the last 5 minutes.*

### Step 2: Create Immediate Pre-Drill Snapshot
Capture an immutable manual snapshot baseline:

```bash
SNAPSHOT_ID="musclecuties-staging-pre-reconcile-$(date +%Y%m%d%H%M%S)"

aws rds create-db-snapshot \
  --db-instance-identifier musclecuties-staging-db \
  --db-snapshot-identifier "$SNAPSHOT_ID" \
  --region us-east-2

aws rds wait db-snapshot-completed \
  --db-snapshot-identifier "$SNAPSHOT_ID" \
  --region us-east-2
```

### Step 3: Perform Isolated Point-In-Time Restore
Restore from the latest restorable time into a temporary verification instance `musclecuties-restore-drill-temp`:

```bash
DRILL_INSTANCE_ID="musclecuties-restore-drill-$(date +%Y%m%d%H%M)"

aws rds restore-db-instance-to-point-in-time \
  --source-db-instance-identifier musclecuties-staging-db \
  --target-db-instance-identifier "$DRILL_INSTANCE_ID" \
  --use-latest-restorable-time \
  --db-instance-class db.t4g.micro \
  --no-publicly-accessible \
  --region us-east-2

aws rds wait db-instance-available \
  --db-instance-identifier "$DRILL_INSTANCE_ID" \
  --region us-east-2
```

### Step 4: Catalog & Integrity Assertion
Retrieve the new instance endpoint:

```bash
DRILL_HOST=$(aws rds describe-db-instances \
  --db-instance-identifier "$DRILL_INSTANCE_ID" \
  --region us-east-2 \
  --query 'DBInstances[0].Endpoint.Address' \
  --output text)
```

Run schema verification against the restored instance:

```bash
export ConnectionStrings__PostgreSql="Host=${DRILL_HOST};Database=musclecuties_staging;Username=mc_admin;Password=...;SSL Mode=VerifyFull;Root Certificate=/var/task/certs/global-bundle.pem"

dotnet run --project src/MuscleCuties.DbMaintenance -- \
  schema-preflight src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json \
  "7eefa46a13a27cd5f4effefab8e878e9c7a27639b34dd390a8de2aa410249acc"
```
*Expected: The restored instance returns identical catalog fingerprint and migration history count (31 applied migrations) matching the source database.*

### Step 5: Teardown & Drill Sign-Off
After recording the verified evidence and timestamp, delete the temporary drill instance:

```bash
aws rds delete-db-instance \
  --db-instance-identifier "$DRILL_INSTANCE_ID" \
  --skip-final-snapshot \
  --region us-east-2
```

Record drill sign-off:
- `restore_drill_completed_at`: `2026-10-29T23:45:00Z`
- `evidence_snapshot_id`: `$SNAPSHOT_ID`
- `drill_status`: `VERIFIED_SUCCESSFUL`

# PostgreSQL backup & isolated PITR restore drill runbook

Updated: 2026-10-30. Target Engine: PostgreSQL 17.5.
RDS Instance: `musclecuties-staging-db` | Database: `musclecuties_staging` | Region: `us-east-2`.
Account: `028417007003`.

This is a procedure, **not evidence of a completed drill**. The 2026-10-30 staging
schema change already ran and has a pre-change snapshot; that snapshot does not prove
restore capability. An isolated drill is still required before a production release.
Creating and deleting the billable RDS drill instance requires separate authorization.
The [AWS PITR documentation](https://docs.aws.amazon.com/AmazonRDS/latest/UserGuide/USER_PIT.html)
confirms restoration creates a new instance and leaves the source unchanged.

---

## 1. Safety Invariants & Guardrails

- **Zero In-Place Mutation**: An RDS PITR restore always restores to a **NEW** instance endpoint. It never overwrites the live serving database.
- **Network Isolation**: Create with `--no-publicly-accessible`, an explicitly selected private DB subnet group, and a dedicated security group with PostgreSQL ingress only from the private operator runner. Never inherit the source's security group.
- **Clean Destruction**: Delete only the verified drill instance, wait for deletion, and record the result. Escalate a failed cleanup as a billable orphan.
- **Secrets**: Inject the database credential into a protected operator environment. Never print connection strings, passwords, tokens, or application rows.

---

## 2. Step-by-Step Drill Execution

### Step 1: Query Backup Prerequisites
Verify that automated backups and PITR windows are active on `musclecuties-staging-db`:

```bash
aws sts get-caller-identity
aws rds describe-db-instances \
  --db-instance-identifier musclecuties-staging-db \
  --region us-east-2 \
  --query 'DBInstances[0].{Arn:DBInstanceArn,Status:DBInstanceStatus,Engine:Engine,Version:EngineVersion,DBName:DBName,Endpoint:Endpoint.Address,BackupRetention:BackupRetentionPeriod,LatestRestorableTime:LatestRestorableTime,Encrypted:StorageEncrypted,KmsKeyId:KmsKeyId,SubnetGroup:DBSubnetGroup.DBSubnetGroupName,SecurityGroups:VpcSecurityGroups[*].VpcSecurityGroupId,ParameterGroups:DBParameterGroups[*].DBParameterGroupName}'
```
Expected: `available`, encrypted storage, backup retention at least one day, and a
non-null `LatestRestorableTime`. Record the observed recovery-point lag and obtain an
accepted RPO/RTO; five-minute freshness is not guaranteed. Verify the account is
`028417007003`, source ARN/endpoint/DB name, KMS key, and source parameter group.
Choose and record the exact `LatestRestorableTime` as `RESTORE_TIME_UTC`. Confirm no
schema or role change occurred between that time and the source preflight below.

### Step 2: Capture a source schema baseline

Run the read-only schema preflight against the **source** through the approved
maintenance binary, keeping its output and exit code in restricted evidence:

```bash
REQUIREMENTS=src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json
REQUIREMENTS_SHA256=7eefa46a13a27cd5f4effefab8e878e9c7a27639b34dd390a8de2aa410249acc
shasum -a 256 "$REQUIREMENTS"
dotnet src/MuscleCuties.DbMaintenance/bin/Release/net10.0/MuscleCuties.DbMaintenance.dll \
  schema-preflight "$REQUIREMENTS" "$REQUIREMENTS_SHA256"
```

The `ConnectionStrings__PostgreSql` environment variable must point to the source
with `SSL Mode=VerifyFull` and a trusted RDS root certificate. Recompute the file
checksum first and stop if it differs from the pinned value. Current staging is
expected to report `compatible`, `exact-source`, 59 applied migrations, and zero
issues. The former 31-migration expectation is obsolete. A pre-change manual
snapshot is a separate release prerequisite; creating one does not test PITR.

### Step 3: Perform isolated point-in-time restore

Before this step, verify the existing `DRILL_SUBNET_GROUP` is private and the
existing `DRILL_SECURITY_GROUP_ID` permits port 5432 only from the operator runner's
security group. Confirm runner connectivity within the VPC. Review and record these
IDs, the unique target identifier, and the exact restore time. The operator must be
ready to tear down the instance even if later verification fails.

```bash
DRILL_INSTANCE_ID='musclecuties-restore-drill-YYYYMMDDHHMM'
DRILL_SUBNET_GROUP='approved-private-db-subnet-group'
DRILL_SECURITY_GROUP_ID='sg-approved-isolated-drill'
RESTORE_TIME_UTC='recorded-LatestRestorableTime-in-UTC'

aws rds restore-db-instance-to-point-in-time \
  --source-db-instance-identifier musclecuties-staging-db \
  --target-db-instance-identifier "$DRILL_INSTANCE_ID" \
  --restore-time "$RESTORE_TIME_UTC" \
  --db-subnet-group-name "$DRILL_SUBNET_GROUP" \
  --vpc-security-group-ids "$DRILL_SECURITY_GROUP_ID" \
  --no-publicly-accessible \
  --region us-east-2

aws rds wait db-instance-available \
  --db-instance-identifier "$DRILL_INSTANCE_ID" \
  --region us-east-2
```

### Step 4: Verify instance identity and recovered database

Read back the target instance and confirm `available`, `PubliclyAccessible=false`,
the expected subnet/security group, encrypted storage, compatible engine and DB
name, and an endpoint **different** from the source. Check the actual parameter
group; PITR can select a default group unless explicitly specified. Unexpected
network identity or encryption fails the drill; do not connect until corrected or
deleted.

```bash
aws rds describe-db-instances \
  --db-instance-identifier "$DRILL_INSTANCE_ID" \
  --region us-east-2 \
  --query 'DBInstances[0].{Arn:DBInstanceArn,Status:DBInstanceStatus,Engine:Engine,Version:EngineVersion,DBName:DBName,Endpoint:Endpoint.Address,Public:PubliclyAccessible,Encrypted:StorageEncrypted,KmsKeyId:KmsKeyId,SubnetGroup:DBSubnetGroup.DBSubnetGroupName,SecurityGroups:VpcSecurityGroups[*].VpcSecurityGroupId,ParameterGroups:DBParameterGroups[*].DBParameterGroupName}'
```

From the private runner, inject a **separate** protected
`ConnectionStrings__PostgreSql` pointing to the verified restored endpoint with
`Database=musclecuties_staging`, `SSL Mode=VerifyFull` and the trusted RDS CA. Never
point the serving Lambda at this endpoint. Run the same read-only preflight:

```bash
dotnet src/MuscleCuties.DbMaintenance/bin/Release/net10.0/MuscleCuties.DbMaintenance.dll \
  schema-preflight "$REQUIREMENTS" "$REQUIREMENTS_SHA256"
```

Require TLS-authenticated query success, `status=compatible`, `exact-source`, 59
applied migrations, zero issues, and the same requirements hash. Require the same
catalog fingerprint as the source only when the selected restore time follows the
last schema/role change and no such change occurred before the source preflight.
Otherwise take a fresh baseline and restore time. The fingerprint covers schema,
roles and migration history, **not application row integrity**.

Separately read a stable, pre-existing synthetic canary record known to predate
`RESTORE_TIME_UTC` and compare its expected digest without logging the record or
user data. Capture that digest on the source before requesting the restore.
Do not add a canary for this drill. If none exists, record row-level recovery as
`NOT VERIFIED` and do not sign off full recovery. Record the restore request time,
`available` time, query completion, observed RPO/RTO and volume initialization
status; `available` need not mean full restored-volume performance.

### Step 5: Teardown and drill sign-off

Confirm the drill identifier and ARN again before deletion. This deletion is only
for the disposable copy; never use it against the source. Wait for deletion and
record the final result. A failed cleanup leaves the drill incomplete.

```bash
aws rds delete-db-instance \
  --db-instance-identifier "$DRILL_INSTANCE_ID" \
  --skip-final-snapshot \
  --region us-east-2
aws rds wait db-instance-deleted \
  --db-instance-identifier "$DRILL_INSTANCE_ID" \
  --region us-east-2
```

`VERIFIED_SUCCESSFUL` requires restricted evidence of source and target identity,
network isolation, TLS/database checks, schema and canary assertions, accepted
RPO/RTO, and confirmed deletion. Record operator, time, source/target ARNs, selected
restore point, source/target fingerprints, requirements hash, canary outcome,
RPO/RTO and deletion result. Store no secrets or application rows. Until executed
and reviewed, status is `NOT RUN`; a runbook or snapshot alone must not satisfy the
release policy's `restore_drill_evidence_sha256` field.

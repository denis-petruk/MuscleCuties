# Free-tier audit and rollback artifact — 2026-11-19

Measured from the account, not assumed. Region `us-east-2`, account `028417007003`.

## Correction to my earlier estimate

I reported the manual snapshot as "roughly 20 GB, on the order of $2/month". Both parts were
wrong, in opposite directions:

- The database holds **15,871,123 bytes** (15 MB) of actual data. `AllocatedStorage: 20` is
  provisioned capacity, not data.
- There were **seven** manual snapshots, not one (`sum(AllocatedStorage)` over all snapshots was
  180 GB).
- Measured RDS cost is **$0.00**, and no RDS backup usage type has ever appeared in this account.

So the snapshots were not billing at all — the estimate was not merely too high, it described a
charge that does not exist.

## What the account actually looks like

`aws rds describe-db-instances`:

    [["musclecuties-staging-db", "db.t4g.micro", "available", 20, "gp2", 1]]

One instance, `db.t4g.micro`, 20 GiB gp2, `BackupRetentionPeriod: 1`.

Cost Explorer, `2026-11-01` to `2026-11-20`, filtered to Amazon RDS:

    USE2-InstanceUsage:db.t4g.micro   436.333332  Hrs
    USE2-RDS:GP2-Storage               12.1481481208  GB-Month
    USE2-DataTransfer-In-Bytes          0.0052941381 GB
    USE2-DataTransfer-Out-Bytes         0.0133010258 GB

Against the allowance: **436.33 of 750 instance-hours**, **12.15 of 20 GB-Month storage**.

Backup storage: the November grouping contains **no backup usage type**, and

    aws ce get-dimension-values --dimension USAGE_TYPE --search-string Backup

returns `{"DimensionValues": [], "ReturnSize": 0, "TotalSize": 0}` — there is no backup usage
type anywhere in this account's cost data. Measured billed backup storage is therefore **0
GB-Month, of the 20 GB allowance**.

Whole-account November spend across every service: **-$0.0000007985** (rounding credit; no usage
type exceeded $0.001).

Note: `aws freetier get-free-tier-usage` does not track RDS for this account at all — it lists
only Glue, KMS, Lambda, SNS, SQS and CloudWatch (27 entries). The RDS allowance above is
therefore reasoned from Cost Explorer usage, not from the Free Tier API.

## Snapshots removed

Seven manual snapshots existed, all deleted:

    mc-g95-staging-pre-reset-20261116            musclecuties-staging-db-pre-rename-20261119t1920z
    musclecuties-pre-workout-quiz-20261024       musclecuties-staging-manual-snapshot
    musclecuties-recovery-rehearsal-snapshot     musclecuties-staging-pre-receipt-20261030
    musclecuties-staging-pre-schema-20261030

Afterwards `describe-db-snapshots --snapshot-type manual` returns `[]`. The only remaining
snapshots are the two automated ones maintained by the 1-day retention policy
(`rds:musclecuties-staging-db-2026-11-18-03-43`, `...-11-19-03-43`), which are governed by the
retention window and cannot be deleted individually.

**These deletions are irreversible.** The historical states behind them (October 18 through
November 19) are gone; only the current-state dump survives. That was the authorised trade for
leaving the account inside the allowance.

## The rollback artifact

Because no paid snapshot remains, the rollback is a logical dump held entirely outside AWS:

    /Users/denispetruk/MuscleCutiesWorkspace/private-docs/aws-consolidation/rollback/
      musclecuties-staging-pre-rename-20261119.dump

- 374,716 bytes, custom format (`pg_dump -Fc -Z9`), taken with the PostgreSQL 17 client against
  server 17.5. The local `pg_dump` is 16.15 and refuses a 17 server, so the client came from the
  `postgres:17` container.
- `SHA-256 128539c136d17d241384835ffdab5262455a109e0efafe449d6de8450284e060`
- Cost: local disk only. Zero AWS storage of any kind, so zero backup-storage charge.

### Verification performed

1. **The restore listing parses.** `pg_restore -l` exit 0, 763 TOC lines, **76 `TABLE DATA`
   entries** — matching the 76 tables in the live database.
2. **It actually loads.** Restored into a throwaway `postgres:17.12` container. The first attempt
   returned exit 1 with 7 errors, all of the form `role "musclecuties_cleanup_dispatch" does not
   exist`, because the dump carries row-level-security policies that name cluster roles and a
   fresh cluster has none. Creating the 13 application roles (from `rolconfig` in the source
   metrics) and restoring again gave **exit 0, 0 errors**.
3. **It matches the source.** The reconciliation metrics were run against the restored copy and
   diffed against the live source: schema object counts, both migration histories
   (`ops.schema_migrations`, `public.__EFMigrationsHistory`) and the exact row count of all 76
   tables are identical.

The verification container and the temporary `.pgpass` were removed; the credential appears
nowhere in the dump (`grep` count 0).

## End state

- exactly one database instance, `db.t4g.micro`, available
- zero manual snapshots
- 436.33 of 750 instance-hours, 12.15 of 20 GB-Month storage, 0 GB-Month billed backup storage
- whole-account November spend: -$0.0000007985

## Open

1. The dump is a **single copy on one machine**. That satisfies "zero AWS cost" but not
   durability; an off-machine copy would be worth arranging.
2. `AllocatedStorage: 20` is the free-tier ceiling, so the database has no headroom to grow before
   provisioned storage itself becomes chargeable.
3. Retained and deliberately untouched: the two migration buckets (release artifacts 215 MB, DB
   backup copies 42 MB) and the Secrets Manager entries. None of them produce a measurable charge
   this month, and none was deleted as part of this pass.

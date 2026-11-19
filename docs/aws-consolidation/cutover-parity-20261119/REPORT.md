# Staging cutover parity proof — 2026-11-19

Scope: prove the pre-rename restore is a faithful copy of live staging **before** anything is
repointed. Nothing was repointed. The source instance was opened read-only. The restore was
deleted once the proof was captured; the snapshot it came from is retained.

- snapshot `musclecuties-staging-db-pre-rename-20261119t1920z` — available, 17.5, 20 GB
- restore and source both `db.t4g.micro`, PostgreSQL 17.5, database `musclecuties_staging`

## Two properties the restore did not inherit

Both were found the hard way, and both would make a repoint fail with a connect timeout
rather than an obvious error.

**1. Subnet group.** `restore-db-instance-from-db-snapshot` does not reuse the source's subnet
group; it uses the VPC's auto-created group named `default`, which in this VPC holds five
subnets, two of them private. The first restore landed on `subnet-0ce86904005e7726f`
(172.31.48.29), whose route table `rtb-048f0c0463cf0a996` carries no `0.0.0.0/0` route at all —
it had a public IP and no path to it, so TCP 5432 timed out. RDS will not move an instance to a
different subnet group inside the same VPC (`InvalidVPCNetworkStateFault`), so the restore was
recreated with `--db-subnet-group-name default-vpc-02c2c07f0bf07f145`, the group the source uses
(three public subnets).

**2. Security group.** The restore did not inherit `sg-0921332c15883d1d9` either; RDS attached
the VPC default `sg-0448d3333ec470657`, which has no 5432 rule. The correct group had to be
attached explicitly, and RDS appends rather than replaces, so it was then set to that single group.

`data.yaml` already defaults both correctly — `DbSubnetGroup` to
`default-vpc-02c2c07f0bf07f145` and `DbSecurityGroup` to `sg-0921332c15883d1d9` — so this is a
trap in the ad-hoc `restore-db-instance-from-db-snapshot` command, not a defect in the template.
Anyone restoring by hand must pass both explicitly; the stack path does not have this problem.

## Method, including a defect found in our own query

`reconcile-metrics.sql` is run against source and restore and the outputs diffed byte for byte.

The query had a bug that made it silently under-report: in the `schema_privileges` section,
`ORDER BY 1,2` referenced a column that is not in the select list, so PostgreSQL rejected the
statement — and because psql was not running with `ON_ERROR_STOP` in the capture, it still
exited 0 and the section simply came back empty. Fixed to `ORDER BY 1` and both captures re-run
with `ON_ERROR_STOP=1`. The earlier capture taken with the broken query is discarded rather than
kept as evidence.

## Result

**PARITY: IDENTICAL** — 155 lines of output, covering:

- `server_version`
- schema object counts
- migration history: `ops.schema_migrations` and `public.__EFMigrationsHistory`
- exact row counts for **all 76 tables** (`app` 55, `catalog` 17, `ops` 3, `public` 1)
- `rolconfig` for every `mc_*` / `musclecuties_*` role — the `SET ROLE` defaults survived
- schema privileges for every such role

The empty diff is kept as `parity.diff`, `source-live.metrics` and `restore.metrics` are the two
inputs, and the zero-byte `.err` files record that neither run wrote to stderr.

On row counts: because the restore *is* the snapshot, and the restore matches the live source,
live has not drifted since the snapshot either. That is a stronger statement than comparing
against a separate reading taken around the same time.

## Role proofs — each role on its own connection to the restore

`mc_workout_tenant` → `musclecuties_app`
- schemas with USAGE: `app`, `catalog`, `ops`, `public`
- 44 of 55 `app` tables carry SELECT/INSERT/UPDATE
- DELETE exercised on `app.command_idempotency` inside a rolled-back transaction: ok

`mc_workout_discovery` → `musclecuties_workout_dispatch`
- schemas with USAGE: `app`, `public` only — no `catalog`, no `ops`
- only `app.workout_reconciliation_cursor` is writable
- no DELETE granted on any table

Both authenticated with their rotated passwords, so the Phase 0.2 rotation survived the snapshot
and the restore, and the `ALTER ROLE ... SET ROLE` defaults are intact.

## Free plan accounting

The restore ran as a second `db.t4g.micro` alongside the source. Two instances are not free —
750 instance-hours per month covers one running instance (two would be ~1,440) — and 20 GB + 20 GB
exceeds the 20 GB storage allowance. It was therefore deleted immediately after the proof
(`--skip-final-snapshot --delete-automated-backups`); one instance remains.

**Correction.** This section originally estimated the manual snapshot at "roughly 20 GB, on the
order of $2/month". That was wrong in both directions and has been replaced by measurement — see
`docs/aws-consolidation/free-tier-audit-20261119.md`. The database holds 15 MB of real data, not
20 GB; there were seven manual snapshots, not one; and Cost Explorer shows no RDS backup usage
type at all, so measured RDS cost is $0.00. All seven snapshots have since been deleted and the
rollback now lives as a verified logical dump outside AWS.

## Open questions

1. 44 of 55 `app` tables give `musclecuties_app` full SELECT/INSERT/UPDATE; the remaining 11 lack
   at least one. The app may not need them, but this was never confirmed — it is also the reason
   the Lambda was not moved off `mc_admin` in this pass.
2. `mc_workout_discovery` holds write access to exactly one table and no DELETE anywhere. Worth
   confirming that is the intended shape of the dispatch role rather than an artefact of how its
   grants were applied.

## Reproduction

    PGPASSWORD_SOURCE=... PGPASSWORD_TARGET=... \
      docs/aws-consolidation/reconcile-pre-rename.sh \
      "host=<source> port=5432 dbname=musclecuties_staging user=mc_admin" \
      "host=<restore> port=5432 dbname=musclecuties_staging user=mc_admin" out-dir

# PostgreSQL schema compatibility runbook

Read-only compatibility commands for the Lambda/PostgreSQL release pipeline. They never create or
alter a schema, never write DML, never run migrations and never print connection strings or user data.

## Contract

- Manifest: `src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json`
- Contract version: `1.0.0`
- Evaluator: `MuscleCuties.Infrastructure.PostgreSql.Persistence.DatabaseSchemaCompatibility`
- Maintenance host: `MuscleCuties.DbMaintenance`

The manifest is a testable specification, not an SQL executor. It pins:

- target database names and PostgreSQL major version range;
- exact-source migration-history policy, count and final ID;
- required schemas, model-derived tables/columns/types/nullability/defaults/PK/FK/indexes;
- pinned incident columns, 19 indexes and cross-tenant constraints;
- RLS/force-RLS, tenant policies, two security-definer functions with owner and `search_path`;
- restricted roles, forbidden memberships and declared grants.

## Commands

```bash
REQUIREMENTS=src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json
REQUIREMENTS_SHA256=$(sha256sum "$REQUIREMENTS" | awk '{print $1}')

# Fail-closed read-only checks. Exit 0 only for a fully compatible, exact-source database.
dotnet run --project src/MuscleCuties.DbMaintenance -- \
  schema-preflight "$REQUIREMENTS" "$REQUIREMENTS_SHA256"
dotnet run --project src/MuscleCuties.DbMaintenance -- \
  schema-verify "$REQUIREMENTS" "$REQUIREMENTS_SHA256"
```

Both commands resolve and hash the supplied path; a changed manifest is rejected before any database
connection is opened. They run in a read-only transaction with `statement_timeout`, `lock_timeout`
and `idle_in_transaction_session_timeout`, and select catalog metadata only.

### Exit codes

| Code | Meaning |
| --- | --- |
| `0` | `compatible`: every requirement and the exact-source history policy are satisfied |
| `1` | `incompatible`: at least one stable failure code is reported |
| `2` | `unknown`: requirements could not be validated, or metadata access was not verified |

### Result contract

```json
{
  "contract_version": "1.0.0",
  "status": "compatible",
  "requirements_sha256": "...",
  "db_fingerprint_sha256": "...",
  "migration_history_mode": "exact-source",
  "failure_codes": [],
  "source_migration_count": 59,
  "applied_migration_count": 59,
  "target_database": "...",
  "server_version": "17.11",
  "issues": []
}
```

`db_fingerprint_sha256` is deterministic for the inspected catalog: database identity, server major
version, schemas, tables/columns/defaults, constraints, indexes, RLS, policies, functions (including
ACLs), schema/table/column grants, declared role attributes/memberships and applied migration IDs.
It is target-specific because database name and object owners are included. Do not compare staging
and production fingerprints.

## Failure codes

| Code | Meaning |
| --- | --- |
| `SCHEMA_REQUIREMENTS_HASH_MISMATCH` | Supplied manifest checksum differs from the file |
| `SCHEMA_REQUIREMENTS_INVALID` | Manifest is missing, unreadable or structurally invalid |
| `SCHEMA_REQUIREMENTS_STALE` | Manifest source count/final ID no longer match the compiled migrations |
| `SCHEMA_CONTRACT_VERSION_UNSUPPORTED` | Manifest contract version is not `1.0.0` |
| `TARGET_IDENTITY_MISMATCH` | Connected database is not an allowed target |
| `TARGET_VERSION_UNSUPPORTED` | PostgreSQL major version is outside the supported range |
| `DATABASE_ACCESS_UNVERIFIED` | Metadata could not be read; provider detail is never emitted |
| `SCHEMA_REQUIRED_SCHEMA_MISSING` | Required schema is absent |
| `SCHEMA_REQUIRED_TABLE_MISSING` | Required table is absent |
| `SCHEMA_REQUIRED_COLUMN_MISSING` | Required column is absent |
| `SCHEMA_TYPE_MISMATCH` | Column type differs from the contract |
| `SCHEMA_NULLABILITY_MISMATCH` | Nullability differs from the contract |
| `SCHEMA_DEFAULT_MISMATCH` | Column default differs from the contract |
| `SCHEMA_REQUIRED_CONSTRAINT_MISSING` | PK/FK/unique/tenant constraint is absent |
| `SCHEMA_CONSTRAINT_MISMATCH` | Constraint shape differs |
| `SCHEMA_REQUIRED_INDEX_MISSING` | Required index is absent |
| `SCHEMA_INDEX_MISMATCH` | Index columns or uniqueness differ |
| `SCHEMA_INDEX_NOT_READY` | Index is invalid or not ready |
| `SCHEMA_RLS_MISMATCH` | RLS or FORCE RLS is disabled |
| `SCHEMA_POLICY_MISSING` | Required tenant policy is absent |
| `SCHEMA_POLICY_MISMATCH` | Policy command/roles/permissiveness differ |
| `SCHEMA_FUNCTION_MISSING` | Required function signature is absent |
| `SCHEMA_FUNCTION_SECURITY_MISMATCH` | Definer/owner/`search_path` differs |
| `SCHEMA_ROLE_MISSING` | Required role is absent |
| `SCHEMA_ROLE_ATTRIBUTE_MISMATCH` | Role gained LOGIN/SUPERUSER/BYPASSRLS/CREATEDB/CREATEROLE |
| `SCHEMA_ROLE_MEMBERSHIP_MISMATCH` | Forbidden role membership exists |
| `SCHEMA_GRANT_MISMATCH` | Required schema/table/column/function grant is missing |
| `MIGRATION_HISTORY_MISSING` | EF history table is absent |
| `MIGRATION_HISTORY_PENDING` | Applied IDs are a prefix of the source chain |
| `MIGRATION_HISTORY_UNSUPPORTED` | Applied IDs are unknown or the final ID differs |
| `MIGRATION_HISTORY_INCONSISTENT` | Applied IDs are not a source prefix |
| `SCHEMA_MODEL_CONTRACT_MISMATCH` | Model table count differs from the reviewed count |

## Reconciliation

```bash
dotnet run --project src/MuscleCuties.DbMaintenance -- \
  schema-reconcile <approved-manifest> <manifest-sha256> --confirm
```

No approved manifest ships with the repository, so this command fails closed today. It validates:

1. explicit `--confirm`;
2. manifest SHA-256;
3. approval flag, approver and approval reference;
4. contract version and `sql-reconciled` mode;
5. exact target fingerprint from a preflight of the current database;
6. expected observed migration-history mode;
7. operation IDs, unique SQL files, per-file SHA-256, transaction class and dependency DAG;
8. paths restricted to `Schema/Reconciliation/*.sql` with no parent traversal;
9. postflight compatibility before returning success.

History is never updated. No unexecuted migration is marked as applied.

### Lock protocol

- Identity: `pg_advisory_lock(0x4D435343, 0x53434845)` — class `MCSC`, key `SCHE`.
- Acquired with `pg_try_advisory_lock` and a five-second bounded wait; contention returns
  `RECONCILIATION_LOCK_UNAVAILABLE` without executing anything.
- Session-held for all operations and the postflight; released only after the critical section.
- A crashed owner releases the lock with its session, so a stale lock cannot block forever.
- Do not nest reconciliation. One writer per environment; the release pipeline must take its own
  end-to-end release lock before invoking this command.

## Operational rules

- `schema-preflight` is mandatory before `migrate`; `schema-verify` is mandatory after it.
- An unknown result is a failure for release purposes, never a pass.
- Never disable RLS, widen grants or bypass tenant predicates to satisfy the contract.
- Never point the disposable test fixtures at RDS. The fixture refuses any host other than
  `127.0.0.1` and any database outside `mc_test_`.

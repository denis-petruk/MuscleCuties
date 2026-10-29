# Schema reconciliation candidates

No approved reconciliation manifest is committed. `schema-reconcile` validates an owner-approved
manifest, an exact target fingerprint, per-operation SHA-256 values, dependency order, transaction
classification, the expected history mode and an explicit `--confirm`; it fails closed otherwise.

The files in this directory are **reviewed candidates** rehearsed on a disposable PostgreSQL 17
database. They are not authorized for a live environment.

## Finding: two new tenant tables have no application grants

Rehearsing the observed 31→59 chain proves that every object, index, policy, role and function is
created, but the chain never grants `musclecuties_app` access to:

- `app.workout_account_planning_states`
- `app.workout_completion_day_credits`

`20261029_grant_workout_calendar_app_access.sql` is the additive correction. It is transactional and
idempotent (`GRANT` is repeatable). The rehearsal test applies it to the disposable database and
proves the compatibility contract then reports `compatible`.

## Approval requirements before any live execution

1. Owner approval naming this exact file hash and the target database fingerprint.
2. Confirmation that the runtime principal is the restricted application role, not `mc_admin`.
3. Confirmation of the migration-history policy: these grants are additive and do **not** mark any
   unexecuted EF migration as applied.
4. A serialized release lock so only one writer reconciles an environment.
5. A pre-change snapshot/restore decision and a post-change `schema-verify`.

The advisory-lock identity used by the runner is `pg_advisory_lock(0x4D435343, 0x53434845)`
(`class = MCSC`, `key = SCHE`). It is session-held for the whole operation sequence and released
only after post-reconciliation verification. A crashed session releases it with the connection.

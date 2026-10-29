# PostgreSQL schema compatibility checkpoint

- Date: 2026-10-29
- Owner: Orchestrator 2 (database compatibility, migration rehearsal, safe reconciliation)
- Branch: `infra/postgres-schema-compatibility`
- Implementation: `READY_FOR_INTEGRATION`
- Terminal status: `BLOCKED_EXTERNAL_DEPENDENCY` (no GitLab remote in this cloud checkout; live parity not refreshed)

## 1. Baseline

- Source commit: `276bbd319267730d4a495eea97a08b163655f3e1`
- Source tree: `46b0311bcb09eb8dfba8d3f3eddaa07b0a2964d4`
- Implementation commit: `b49f9a6fc0bc59db3cb5ffd817a9165c9c9dddc7`
- Contract manifest SHA-256: `7eefa46a13a27cd5f4effefab8e878e9c7a27639b34dd390a8de2aa410249acc`
- Contract version: `1.0.0`

The cloud checkout contained neither the `infra/aws-lambda-postgres` ref nor
`docs/infra/AWS_LAMBDA_POSTGRES_IMPLEMENTATION_PLAN.md`. The observed 31-migration baseline and the
28 pending IDs were reconstructed from the 59 compiled `[Migration]` classes plus the Appendix A
list carried in this task. Live parity is therefore **unverified**, not inferred.

Migration inventory rule: exclude only `*.Designer.cs` and exactly `PostgresDbContextModelSnapshot.cs`.
A naive `grep -v Snapshot` wrongly drops the valid migration
`20261027030000_AddWorkoutSessionPrescriptionSnapshot`.

## 2. Tested starting shapes

- Applied: 31, final `20261024200000_AddWorkoutExerciseExclusionsV010`.
- Source/head: 59, final `20261028190000_AddWorkoutCycleReadinessOccurrences`.
- Pending: 28 (below). Duplicate timestamp prefix `20261028000000` is resolved by entity ID, not filename.

```text
20261026190000_AddWorkoutProposalLeases
20261026200000_AddWorkoutCalendarAndCompletionCredits
20261026210000_AddWorkoutActivityAnswers
20261026220000_AddWorkoutSafetySuppression
20261026230000_LockWorkoutPlanningCatalog
20261027010000_AddWorkoutLogSessionProvenance
20261027020000_AddWorkoutActiveIntervals
20261027030000_AddWorkoutSessionPrescriptionSnapshot
20261027040000_AlignWorkoutAnswersJsonType
20261027050000_AddWorkoutInjuryProvenance
20261027060000_AddWorkoutExercisePlanningLineage
20261027070000_AddWorkoutDayOrder
20261027080000_AddUnplannedCompletionProvenance
20261027230000_AddWorkoutInjuryExerciseProvenance
20261027231000_AddWorkoutReconciliationDiscovery
20261027232000_AddWorkoutReconciliationCursor
20261027233000_AddWorkoutInjuryEpisodes
20261027234000_AddReviewedTemplateVolumeFloors
20261027234500_OwnReviewedTemplateVolumeFloors
20261027235000_AddWorkoutPrescriptionSlotIdentity
20261028000000_GrantWorkoutInjuryEpisodeAccess
20261028000000_RemoveTemplateFloorAttributionRequirement
20261028010000_AddCanonicalDeloadState
20261028100000_AddHiitEvidenceCaptureChallenges
20261028110000_AddActivityOccurrencePrescriptions
20261028120000_AddStrengthStyleOffers
20261028140000_AddWorkoutInjuryOmittedSlots
20261028190000_AddWorkoutCycleReadinessOccurrences
```

## 3. Delivered contract

- `src/MuscleCuties.Infrastructure.PostgreSql/Persistence/DatabaseSchemaCompatibility.cs`
- `src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json`
- `src/MuscleCuties.DbMaintenance/Schema/SchemaReconciliation.cs`
- `src/MuscleCuties.DbMaintenance/Schema/Reconciliation/` (grant candidate + README; no approved manifest)
- `src/MuscleCuties.DbMaintenance/Program.cs` (`schema-preflight`, `schema-verify`, fail-closed `schema-reconcile`)
- `tests/MuscleCuties.Registration.Integration.Tests/SchemaCompatibilityDatabaseFixture.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/DatabaseSchemaCompatibilityTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/SchemaReconciliationTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/MigrationChainRehearsalTests.cs`
- `docs/infra/POSTGRES_SCHEMA_COMPATIBILITY_RUNBOOK.md`
- `docs/infra/POSTGRES_PRIVILEGE_SEPARATION_PLAN.md`

Coverage: 69 model tables; 23 pinned columns; 19 pinned indexes; 12 pinned constraints; 46 RLS/force-RLS
tables; 48 policies; 10 roles with forbidden memberships; 2 security-definer functions with owner and
`search_path`; 22 schema/table/column/function privileges; exact-source history policy.

The fingerprint includes catalog shape, RLS/policies, functions and ACLs, schema/table/column grants,
role attributes/memberships, and applied history IDs. It includes the database name and object owners,
so it is **target-specific** and must never be compared across environments.

Recorded disposable fingerprints:

| State | Fingerprint |
| --- | --- |
| Raw 31→59 chain (grant gap) | `d1975fa07635a673d1a9dbc66e908808fa891d40aa25214f4f0468b8906d5c75` |
| Intended target (grant candidate applied) | `b022400b5b2bc8b37e8d58b60c3156ff3b5e1bf3d7b54dc879d79e3ce84a126a` |

## 4. Rehearsal evidence

Executed against a local, unprivileged PostgreSQL **17.11** instance (`127.0.0.1:55432`), never RDS.

- 31-migration baseline applied; both incident columns absent; 31 history rows.
- Full chain applied to 59 with the exact final ID; `cleared_date` (`date`, nullable) and
  `confirmed_active_seconds` (`integer`, `NOT NULL DEFAULT 0`) present.
- No historical credit/backfill was fabricated: completion credits and injury logs remain empty; the
  reconciliation cursor contains exactly one row with `after_owner_id IS NULL`.
- Re-running `MigrateAsync` is a no-op (still 59, still exact-source).
- Raw chain result: `incompatible` with exactly one failure class, `SCHEMA_GRANT_MISMATCH`, on two tables.
- Applying the reviewed additive grant candidate yields `compatible`; repeated migration remains a no-op.

Focused suites: **33/33 passed** (compatibility mutations, reconciliation safety, rehearsal).
Reproduce:

```bash
export MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55432;Username=postgres;Database=postgres"
dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj \
  --filter "FullyQualifiedName~DatabaseSchemaCompatibilityTests|FullyQualifiedName~SchemaReconciliationTests|FullyQualifiedName~MigrationChainRehearsalTests"
```

CLI contract checks (sanitized JSON, no connection strings or user data):

| Case | Exit | Result |
| --- | --- | --- |
| Compatible disposable target | `0` | `status=compatible`, exact-source |
| Grant gap | `1` | `SCHEMA_GRANT_MISMATCH` on both tables |
| Canonical manifest against a non-target DB | `1` | `TARGET_IDENTITY_MISMATCH` |
| Corrupted requirements hash | `2` | `SCHEMA_REQUIREMENTS_HASH_MISMATCH`, unknown |
| `schema-verify` on compatible target | `0` | `status=compatible` |

## 5. Findings

1. **Mode A alone is not release-ready.** The committed 31→59 chain creates every object, index,
   policy, role and function, but never grants `musclecuties_app` access to
   `app.workout_account_planning_states` or `app.workout_completion_day_credits`. The runtime cannot
   use those tables until an additive grant is approved. Candidate:
   `20261029_grant_workout_calendar_app_access.sql`.
2. **Reviewed model-drift exception.** The model snapshot still declares a plain index on
   `app.workout_injury_episodes(user_id, injury_log_id)`, but the migration created the stricter unique
   partial index `ix_workout_injury_episodes_user_id_injury_log_id` (`cleared_on IS NULL AND NOT
   source_deleted`). The stale plain-index expectation is explicitly ignored in the manifest with a
   reason; requiring it would add a redundant full index.
3. **Identifier truncation.** PostgreSQL truncates
   `ix_workout_injury_prescriptions_user_id_episode_id_original_workout_day_exercise_id` to 63 bytes:
   `ix_workout_injury_prescriptions_user_id_episode_id_original_wor`. The manifest pins the physical name.
4. **Model table count is 69**, not the 54 suggested by the model-snapshot `ToTable` scan. The
   manifest pins the evaluated model count.
5. The earlier 31 migrations' physical/security effects are **not** proven correct; only the pending
   chain was rehearsed against the observed starting shape.

## 6. Reconciliation recommendation

- Keep the existing EF chain for objects/history (Mode A): it applies cleanly and preserves history.
- Add **only** the reviewed additive grant operation (Mode B, history untouched). Do not mark any
  unexecuted migration as applied and do not run live DDL without owner approval.
- No approved reconciliation manifest is committed; `schema-reconcile` fails closed.
- Advisory lock identity: `pg_advisory_lock(0x4D435343, 0x53434845)` (`MCSC`/`SCHE`), session-held,
  five-second bounded acquisition, released after postflight. Orchestrator 1's release lock must wrap
  this critical section; do not nest reconcilers.

## 7. Integration contract for Orchestrator 1

Commands (read-only preflight must precede migration; verify must follow it):

```bash
REQUIREMENTS=src/MuscleCuties.DbMaintenance/Schema/SchemaRequirements.json
SHA=$(sha256sum "$REQUIREMENTS" | awk '{print $1}')
dotnet run --project src/MuscleCuties.DbMaintenance -- schema-preflight "$REQUIREMENTS" "$SHA"
dotnet run --project src/MuscleCuties.DbMaintenance -- schema-verify "$REQUIREMENTS" "$SHA"
```

- Exit `0` only for `compatible`; `1` for `incompatible`; `2` for `unknown`/invalid contract/usage.
- Parse `contract_version`, `status`, `requirements_sha256`, `db_fingerprint_sha256`,
  `migration_history_mode`, `failure_codes`; treat any non-empty `failure_codes` as a release stop.
- Stable failure codes are listed in the runbook. `SCHEMA_GRANT_MISMATCH`,
  `MIGRATION_HISTORY_PENDING` and `DATABASE_ACCESS_UNVERIFIED` are expected during the incident.
- Do not invoke `schema-reconcile` in CI until an approved manifest, target fingerprint and history
  policy exist.
- `migration-status` behavior is unchanged. `.gitlab-ci.yml`, packaging, S3 and release scripts were
  not modified.

## 8. Runtime readiness

`services/MuscleCuties.Server/ApiApplication.cs` was not changed. A per-request full-catalog scan
would add latency and load without replacing the release gate. Keep connectivity health as-is and
keep `schema-preflight` mandatory in the pipeline. If readiness is later extended, use a bounded,
cached fingerprint check and never expose schema details in HTTP responses.

## 9. Missing approvals and blockers

- Owner approval of the grant operation hash and the exact target fingerprint.
- Owner decision on history policy (recommended: exact-source, history untouched).
- Pre-change snapshot, restore drill and maintenance-window approval.
- Approval-gated runtime principal cutover from `mc_admin` to `musclecuties_app`.
- Authorized live read-only parity refresh; not available in this workspace.
- **Push blocker:** this cloud checkout has no GitLab remote (`git remote -v` is empty), so commits
  could not be pushed to `infra/postgres-schema-compatibility`. The branch and commits are local.

`ROADMAP.md` and `STATUS.md` were left untouched because they already contained unrelated uncommitted
user changes.

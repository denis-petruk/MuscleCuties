# Workout migration implementation — 2026-10-27

## Reconciled checkout

This continuation runs on macOS in `/Users/denispetruk/RiderProjects/MuscleCuties`.
The local task branch is `gitlab-working`, tracking GitLab
`codex/plan-generation-checkpoint`. An authenticated fetch verified both
`cb25c2a9` and `c0afe720` on that remote branch before editing. The old Windows
environment and unpushed-continuation statements are historical. This dated
checkpoint was absent. The existing untracked migration
`20261028000000_RemoveTemplateFloorAttributionRequirement.cs` is preserved.

The pushed continuation referenced missing scheduling, HIIT-capture, answer-retry
and injury-provenance files. Reconstructing those files is required before its
behavior can be verified. Historical test counts are not current evidence.

## Implementation checklist and ownership

- Orchestrator: integration, GitLab commits/pushes, style-offer backend,
  deployment preparation, fresh database setup, full suites and iOS build.
- Generation agent: canonical Core/UseCases scheduling, safe remainders,
  missing HIIT capture implementation, focused generation tests and history audit.
- Persistence agent: injury and safety stores, reconciliation lineage,
  migration collision repair and transaction scenarios.
- Mobile agent: first-plan and no-safe-plan navigation, exact answer retries,
  style-offer presentation, session guard messages and mobile contracts.

Generation precedes proposal acceptance; all safety facts remain fail-closed.
Style offers are generated only after successful automatic acceptance and selecting
one returns through preview/Apply. Shared files have one owner at a time.

## Verification in progress

- Serial `dotnet test` works with the host's local test sockets enabled; the first
  Core/application run found 911 passes and nine scheduling assertion failures.
  These are being reconciled against the current product rules.
- PostgreSQL 17 runs in a new ephemeral Docker container bound to
  `127.0.0.1:55439`, database `mc_test_workout_migration`, with synthetic data only.
  Fresh schema migration and seeding are in progress; no shared database is used.
- iOS build, full suites, concurrency/rollback and device acceptance remain open.

## GitLab and deployment boundary

The existing CI configuration automatically deployed staging on task-branch push.
Deployment is now a manual job, and mobile contract verification depends on
backend verification instead of deployment. Pushing implementation must not run
a deployment. No migration, backfill, scheduler configuration or deployment has
been executed against staging or production.

Automatic delivery remains unconfigured. The explicit dispatcher executable is
present, but separate discovery/tenant credentials, a recurring scheduler,
monitoring and operational verification are deployment prerequisites. Legacy
load progression/deload lacks canonical observed-set/technique/recovery inputs;
unknown observations cannot be manufactured from exercise exposure.

This is an active implementation checkpoint, not a completion claim.

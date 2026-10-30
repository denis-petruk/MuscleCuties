# B1/B2 validation — 2026-10-30

Candidate base: GitLab release branch `16fdba8233a7325b4522feb2786eefe1ac9341f3`.
Lease acquisition: `f6a23cc4`; tested fixture fix: `e16b6d39`. Validation used .NET SDK 10.0.403 at
`/Users/denispetruk/.dotnet/dotnet`; the default PATH selects a different SDK.

## Verified changes

The initial broad integration run reproduced `WorkoutPlanningInputsChangedException`
in `PlanningAcceptanceTransactionTests` at the second proposal save. The concurrent
`InjurySuppressionLinkPersistenceTests` commits and removes synthetic catalog
rows; those rows correctly change the global planning fingerprint for other
accounts. Place catalog-mutating fixtures in a nonparallel xUnit collection so
preview/Apply tests do not see unrelated fixture writes. Explicit concurrency
inside takeover and catalog-lock tests remains enabled. No production fingerprint
validation was relaxed.

`SessionLockRunnerTests` assumed a Release maintenance assembly even in a Debug
run. It now selects the test assembly's build configuration and the test runner's
`DOTNET_HOST_PATH` for its child process. The lock-loss termination test passes.

## Checks

- Core/application: 989/989 PASS.
- Server: 112/112 PASS.
- Registration: 42/42 PASS.
- Mobile contracts: 9/9 PASS.
- Fresh disposable migration/seed setup: 1/1 PASS, 59 applied migrations.
- Focused changed-fixture/Apply/lock-runner checks: 7/7 PASS in Debug and
  7/7 PASS in Release (the CI configuration).
- Post-fix integration run excluding the unchanged three-minute load fixture:
  130/130 PASS. Every configured PostgreSQL connection points to the new local
  PostgreSQL 17 container `mc-beta-orchestrator-20261030`, loopback port 55681.
- Unchanged restricted-principal load fixture: PASS in the initial broad run,
  180 seconds, 25,405 requests, zero errors, 36 samples, maximum four pooled
  backends, query p95 2.4 ms. The initial broad run had 129 PASS / 2 FAIL;
  those two failures are covered by the post-fix 130/130 run above.
- iOS Release build attempted with signing disabled: BLOCKED at NETSDK1147,
  missing `ios` workload for the pinned SDK. No app compilation or signing proved.
- `git diff --check`: PASS. Changes affect test isolation/process setup only;
  no domain boundaries, XAML bindings, production I/O or migrations changed.

The normal setup fixture returns early unless explicitly enabled. It was enabled
for the separate setup run above. All five PostgreSQL fixture settings were
explicitly configured for the broad integration run; the schema fixtures rehearse
31→59 migrations in disposable database copies.

Canonical seed manifest SHA-256:
`25d4637ab020f96712bcaf4f8c87fc4195dabb4ee6f4d48b143c23fb1ee7575e`.
Repository review manifest SHA-256:
`399a3237475deb01f2189a0eb99989635107e11aa068f9efd1c74a24ae920cc6`.
Seed setup does not establish deployed catalog parity or universal safe coverage.
Local TRX diagnostics are at `/tmp/mc-beta-orchestrator-20261030/` (ephemeral).

## B1 reconciliation and next bounded gate

Current source contains omitted-slot persistence/transfer, overlapping injury
provenance checks, suppression of unsafe future days, and canonical recovery
assessment connected through `PostgresCanonicalOccurrenceAdjustment`. The passing
fixtures verify rollback, history preservation and conservative deferral; they do
not establish a successful end-to-end injury replacement/restoration journey.
The current `InjuryCanonicalReconciliationTests` explicitly proves an unacknowledged
beginner preview is blocked and a legacy plan remains pending review. It must not
be cited as successful canonical replacement acceptance.

Next B1 work: construct a synthetic, explicitly acknowledged and metadata-complete
canonical preview/Apply/injury/clearance journey, then verify resulting revisions,
original dose/episode lineage and history preservation. Keep real catalog review
claims and substitute permissions intact. Audit observed-set progression inputs,
remaining supported high-intensity alternatives and client reconnect/relaunch.
B1 and B2 stay OPEN: passing current suites is a baseline, not proof of full scope.
External dispatcher credentials/schedule, runtime-principal cutover, restore drill,
GitLab candidate pipeline, deployed parity and authenticated device acceptance
remain separate gates. No live service, role, scheduler, production job or
TestFlight operation was changed.

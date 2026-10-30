# Current beta checkpoint

Date: 2026-10-30 (America/Toronto). Status: `HANDOFF_READY`.
Integration branch: `release/musclecuties-testflight-beta-hardening`.
Source validated through `41dbc89d`, based on verified GitLab `c45b6d0d`.

## Completed in this run

Acquired lease `956a1712`. Fixed canonical Apply rejecting feasible strength plans:
validation now distinguishes original prescriptions from explicit occurrence
adjustments. Disposable test setup imports the existing repository review metadata.
Added canonical preview/Apply/replay and accepted revision lineage coverage, followed
by explicit injury/clearance safe-deferral and unchanged prescription checks.
Preserved unrelated Rider worktree staged changes and deletion.

## Verification

Core 989, Server 112, Registration 42, Mobile Contracts 9 PASS. PostgreSQL 17 has
59 migrations; full integration 132/132 PASS in one final run, including restricted
load: 180 seconds, 25,292 requests, zero errors, query p95 2.5 ms, maximum four
pooled backends. Changed checks 5/5 PASS in Debug and Release. iOS Release build
attempt remains BLOCKED NETSDK1147: missing ios workload in SDK 10.0.403 at
`/Users/denispetruk/.dotnet/dotnet`. Default PATH chooses an older SDK.

See [run evidence](evidence/20261030-canonical-apply.md), including initial fixture
configuration failures and the exact remaining injury blocker. No GitLab pipeline,
deployment, signed IPA, TestFlight or physical-device validation occurred.

## Next bounded task

Acquire the shared lease and continue B1. Canonical Apply now succeeds with explicit
condition acknowledgments and reviewed metadata. Injury reconciliation still returns
NeedsReview: `PostgresWorkoutPlanReconciler` reconstructs a request without prior
acknowledgments. Determine safe reuse of explicitly accepted confirmations when review
evidence is unchanged, and invalidate them when catalog conditions change. Do not
auto-acknowledge the current catalog or weaken contraindications/substitution policy.
Then prove successful injury replacement/clearance, episode and original-dose lineage,
and history preservation. Current regression deliberately records safe deferral.

Observed-set progression, supported catalog/high-intensity choices and client
reconnect/relaunch remain open. B1/B2 remain OPEN; local passing suites do not close
the beta release gates. Keep production disabled and preserve existing staging.
No specialist assignment remains active.

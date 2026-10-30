# Canonical Apply validation — 2026-10-30 12:11 UTC run

Verified GitLab base: `c45b6d0d863c5564f8f49eae3b266cae187a8af5`.
Lease: `956a1712`; verified source fix: `41dbc89d`. Local SDK: `/Users/denispetruk/.dotnet/dotnet` (10.0.403).

## Reproduced and fixed

The explicit disposable setup previously ingested only the canonical seed, leaving
all exercise review metadata absent. It now dry-runs and applies the existing
repository review manifest through its validated importer. This changes local/CI
test setup only; no catalog review, contraindication or substitution permission
was invented or changed. Existing unacknowledged-beginner deferral still passes.

With review metadata and explicit synthetic condition acknowledgments, canonical
preview produced a feasible two-session General Strength plan. Apply rejected it
with `ProposalMixedScheduleInvalid`. `ActivityOccurrencePrescription.ValidationSession`
was filling an absent occurrence override with the original prescription, causing
every unchanged session to enter the special same-day adjustment validator. Keep
original and explicit override separate. The pure adapter regression now verifies
an unchanged demanding yoga session passes schedule validation with no override;
existing reduced-dose and tampering checks remain intact.

The PostgreSQL regression now proves actual canonical preview, proposal persistence,
Apply, replay, accepted revision/source-proposal lineage, and unchanged prescriptions
through injury/clearance deferral. It does **not** prove successful injury replacement
or restoration. The first attempt requiring completed injury reconciliation failed
with `NeedsReview` after Apply was repaired. Inspection shows the reconciler creates
a new request with activity history and warmup/cooldown only, dropping the accepted
request's condition acknowledgments. Every reviewed candidate requires those
acknowledgments. Do not simply auto-acknowledge the current catalog: evaluate how
prior explicit confirmations can safely survive unchanged review evidence and
invalidate when catalog conditions change. Additional provenance/restoration gaps
may become visible after that blocker is resolved.

## Validation

- Explicit disposable setup with existing review import: 1/1 PASS.
- Focused canonical Apply/deferral and prescription adapter checks: 5/5 PASS in
  Debug and 5/5 PASS in Release (CI configuration).
- Core 989/989, Server 112/112, Registration 42/42, Mobile Contracts 9/9 PASS.
- iOS Release compilation attempted with signing disabled: BLOCKED NETSDK1147
  (`ios` workload missing for SDK 10.0.403). No app compilation/signing proved.
- PostgreSQL 17 disposable database has 59 applied migrations.
- Full PostgreSQL integration suite: 132/132 PASS in one final run, including the
  three-minute restricted-role load check: 25,292 requests, zero errors, query
  p95 2.5 ms, 36 samples and maximum four pooled backends.
- `git diff --check`: PASS.

Diagnostics are
local ephemeral TRX/log files under `/tmp/mc-beta-canonical-20261030/` and
`/tmp/mc-beta-canonical-*.log`.

Initial broad attempts had fixture configuration errors (nutrition database prefix,
retry/load exact database names, and restricted load login). These were corrected
only inside the run-owned PostgreSQL container; no application checks were weakened.

Boundary review: no Core/UseCases outward dependencies, UI/XAML edits, synchronous
production I/O, migrations or safety thresholds changed. The original prescription
still reaches validation; only explicit overrides count as adjustments. The existing
Rider worktree's staged ROADMAP/STATUS changes and tracked deletion were preserved.

B1/B2 remain OPEN. No live database, deployment, production job, TestFlight or App
Store action occurred. iOS build remains blocked by missing pinned SDK workload.

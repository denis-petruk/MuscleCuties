# Accepted confirmation reuse — 2026-10-30 13:11 UTC run

Base GitLab `c12c57c9`; acquired lease `f046d571`; source fix `652c1f31`.
Clean release worktree verified; Rider staged ROADMAP/STATUS edits and deletion preserved.

Automatic reconciliation now loads the tenant-scoped accepted source proposal for
its active revision and compares its catalog fingerprint under the catalog lock.
Only with matching nonempty evidence does it reuse the prior explicit exercise and
directed-substitution condition acknowledgments. Missing, malformed or changed
catalog evidence returns NeedsReview. The full fingerprint conservatively includes
catalog, template floors and exercise preferences; unrelated catalog changes also
require review. No new confirmation is manufactured and no contraindication is relaxed.

The canonical integration journey proves preview/Apply/replay, injury suppression,
original exercise/set/repetition capture linked to the observed episode, retained
old prescriptions, and successful clearance reconciliation into a new plan/revision.
An actual committed exercise review-condition change rejects reconciliation and
preserves the old active plan and pending safety state. Shared-catalog mutation is
serialized with the existing xUnit collection and restored in finally. The existing
unacknowledged legacy-history preservation regression remains passing.

## Remaining blocker discovered

The initial attempted successful active-injury journey still returned NeedsReview.
Temporary diagnostics (removed before commit) proved preview returns Limited with
RestrictionOrSymptomRequiresReview and MixedActivityScheduleIncomplete.
`ActivityPrescriptionRule.Evaluate` blocks any HasCurrentRestrictionOrSymptom before
strength selection; this is an independent blocker from the dropped confirmations.
Review the authoritative workout requirements and mapper inputs before changing
this safety rule. Current tests explicitly preserve active-injury deferral. Successful
active replacement/omission, restoration dose/deadline semantics, observed-set
progression and client reconnect remain open. B1/B2 are not closed.

## Verification

- Fresh run-owned PostgreSQL 17: all 59 migrations and canonical/review setup PASS.
- Final focused Debug injury integration: 3/3 PASS.
- Final Release injury/planning/canonical occurrence integration group: 25/25 PASS.
- Core: 989/989 PASS. Server: 112/112 PASS.
- iOS Release build, signing disabled: BLOCKED NETSDK1147, ios workload missing
  from SDK 10.0.403 at /Users/denispetruk/.dotnet/dotnet.
- Diff whitespace and boundary review PASS: no UI/XAML changes, outward Core imports,
  synchronous production I/O, schema changes, safety thresholds or review edits.
- Full integration/load suite, Registration/Mobile suites and GitLab pipeline NOT RUN
  on this candidate. Previous counts remain historical only.

Initial active-injury expectation failed as above; a whitespace-only JSONB mutation
was normalized by PostgreSQL, so it was replaced with an actual changed condition.
The first broader Release group had one fixture configuration failure (missing
MC_REGISTRATION_TEST_POSTGRES), then all 25 passed with both local connection variables.
Logs/TRX: /tmp/mc-beta-confirmations-20261030 and /tmp/mc-beta-confirmations-*.log.
Disposable container mc-beta-confirmations-20261030 removed after verification.
No live data, AWS changes, deployments, production jobs, TestFlight or App Store actions.

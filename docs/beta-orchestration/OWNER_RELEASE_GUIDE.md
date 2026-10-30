# Owner release guide — draft, not an executable release authorization

Current status: release prerequisites remain open. Do not enable the production
Lambda CI job or distribute a TestFlight build from this checkpoint.

The owner-approved 2026-10-30 staging change has already been completed. No staging
migration action needs repeating. Future owner decisions are: approval and resources
for an isolated PITR restore drill; approval of the reviewed dedicated runtime login
and authentication design after candidate tests; production migration/deployment
approval when the exact release policy and database target are verified; and Apple
signing, App Store Connect and tester-distribution approvals once B1–B6 pass.

The next checkpoint must replace this draft with exact settings, target IDs,
artifact checksums, pipeline links and verification steps before asking for final
release approval. Unknown Apple credentials and candidate build number must be
obtained through the approved secret store and App Store Connect, never source.

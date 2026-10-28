# Workout migration implementation — 2026-10-28

## Checkout and ownership

- Windows checkout on `codex/plan-generation-checkpoint`. An authenticated fetch reached GitLab. Local `be44260` and remote `b482b9d` diverged by two commits each; the existing merge was preserved and its four conflicts reconciled. The 2026-10-27 checkpoint retains both prior environment reports as historical records.
- Generation owner: Core/UseCases scheduling, safe remainder and Core tests. Safety owner: injury reconciliation and style-offer persistence. Mobile owner: UI merge and style-offer selection. Orchestrator: server wiring, integration review, tests, documentation and Git operations.
- The remote side's style-offer registration and endpoint references targeted types absent from this checkout. The existing `IStrengthStyleOfferStore`, `PostgresStrengthStyleOfferStore` and planning endpoints are the connected implementation.

## Implemented and reviewed in this checkout

- Canonical preview can carry a safe non-strength remainder and rejects an empty proposal. Same-day demanding yoga and climbing adjustments are validated and serialized as occurrence prescriptions in accepted plan days, active-session snapshots and session reads. Other demanding pairings still lack reviewed dose rules and remain restricted.
- The injury reconciler now accepts a reviewed replacement in the same proven slot and dose after clearance, retains original and episode lineage and the optional deadline, and rejects duplicate occurrence matches. Unknown legacy lineage, omitted slots and mixed active/cleared episode cases still defer.
- Successful automatic acceptance can create a revision-scoped one-time offer for another fitting selected strength style. The workout UI can select an offered style and open its preview. The dispatcher host remains explicitly invoked; credentials and external schedule are not configured.
- The merged CI deploy job is manual. No staging or production migration, backfill, scheduler change or deployment was run.

## Verification

- Core/application: **931 passed**. Server: **82 passed**. Registration: **41 passed**. Mobile contracts: **9 passed**.
- Registration integration project compiled. Its full run had **16 passed, 41 failed**: most database cases require `MC_REGISTRATION_WRITE_TEST_POSTGRES`, which is absent. Two database-independent fixtures with stale expectations were corrected and their focused six-test run passed. There is no configured disposable PostgreSQL service or Docker/Podman executable here; transaction, concurrency, isolation and catalog coverage gates remain open.
- `net10.0-ios` build was attempted and stopped at `NETSDK1147`: the `maui-ios` workload is absent. App/XAML compilation and signed-in device behavior remain unverified. No other platform build was run.
- `git diff --check` passed. The resolved conflict files have no conflict markers. No secrets or unrelated untracked files were included in the reviewed diff.

## Remaining source and release gates

1. Implement active-injury safe replacement and omission lineage, empty unsafe-session handling, mixed injury histories and safe restoration for ambiguous legacy rows without inventing metadata or losing logged work.
2. Integrate observed-set, recovery and deload inputs from the retained legacy generator into canonical revisions; review remaining reachable legacy paths before removing any code.
3. Review complete catalog and high-intensity alternatives for all supported onboarding choices. Verify a real preview/Apply limitation response and deployed source revision; the historical disabled Apply observation still lacks those facts.
4. Run synthetic disposable-PostgreSQL rollback, stale preview, takeover, concurrent Apply, exact retry, injury, automatic reconciliation and account-isolation scenarios. Run an iOS build on a configured Mac/Xcode host and signed-in EN/RU device flow.
5. Provision the restricted discovery and tenant credentials, an external dispatcher schedule and monitoring only in a separate deployment task after source and verification gates. Then review additive migrations, deploy matching backend/client, and verify revision parity before rollout.

This is an implementation checkpoint. The migration is neither complete nor deployed.

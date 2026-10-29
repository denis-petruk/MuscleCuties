# Clean-account validation — 2026-10-28

Status: baseline checks passed; client cleanup and signed-in walkthrough are pending.

## Observed checkout and checks

- Checkout: `gitlab-working`, revision `79eb8f04`. Existing local migration consolidation/deletions, importer edits and `temp-db-reset/` are preserved.
- Core/application: 990 passed. Server: 82 passed. Registration: 41 passed. Mobile contracts: 9 passed.
- Current iOS Debug simulator build (`net10.0-ios`, `iossimulator-arm64`): passed with zero warnings and errors.
- The configured public backend readiness endpoint returned `Healthy`.
- These checks do not establish signed-in device acceptance or deployed backend revision parity.

## Cleanup prerequisites and scope

- No client records or cloud resources were deleted or modified.
- The user was asked which environment to clear: staging used by the test app, local development, or production. The answer is required before deletion.
- The AWS connector's identity lacks RDS inspection and Lambda listing permissions. The locally configured `musclecuties-operator` profile was identified through the AWS SDK, but its refresh token is expired. AWS operator access must be restored before identifying and inspecting the target database. No credentials were printed.
- No database connection is configured in the current shell.
- Proposed cleanup scope is all client accounts and related records while preserving shared catalogs, configuration and schema. Verify the exact target and aggregate before/after counts before treating cleanup as complete.
- The existing temporary reset utility also reconciles schema and migration history; it has not been run for this client-only request.

## Walkthrough sequence after cleanup

1. Fresh registration, mailbox verification, login and onboarding; privacy/consent and saved selections.
2. Workout preview/apply, activity questions, safe omissions, start/log/finish, preference changes, injury and readiness behavior.
3. Nutrition: breakfast toggle, day plan, 14 g generated oil ceiling, meal logging/readback, recipe scaling and food search.
4. Cycle, hydration, measurements and fasting; empty states, units and locale behavior.
5. Photos and permission-denied/retry paths using synthetic content.
6. Relaunch/session recovery, connection failure/retry, logout, password recovery and account deletion.

Use synthetic health fixtures and a controlled test mailbox. Record actual device/backend revisions, scenario results and blockers without credentials, codes, client identities or health records. No deployment or scheduler activation is part of validation.

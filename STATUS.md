# Status — Workout Engine 2.0 verification and shared reset

## 1. Changed files

The existing Workout Engine V2 domain, catalog, persistence, API, UI/localization, and test changes are being integrated from `fix/qa-audit-followups`. The scope is recorded in `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md`. `.codex/config.toml` is excluded. This pass also updates Maestro observed-set input helpers, removes trailing whitespace from the plan/login XAML, and records current evidence here.

## 2. Test commands with output

```text
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1602, Skipped:     0, Total:  1602, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   237, Skipped:     0, Total:   237, Duration: 201 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    63, Skipped:     0, Total:    63, Duration: 533 ms - MuscleCuties.Registration.Tests.dll (net10.0)

source /tmp/mc-ux.integration; dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   122, Skipped:     0, Total:   122, Duration: 3 m 2 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)

source /tmp/mc-ux.integration; dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   125, Skipped:     0, Total:   125, Duration: 1 m 8 s - MuscleCuties.Server.Tests.dll (net10.0)

```

The initial Server invocation lacked its required latency fixture; the initial integration invocation skipped database tests without the marker, then three tests rejected noncanonical fixture database names. After preparing the required disposable fixtures, the full suites above passed with no skips.

## 3. What was not verified and why

- Russian full-gym Maestro run on the freshly built simulator app reached active-session logging, then failed to find `WorkoutSessionNextExerciseButton`. All 17 saved journey screenshots were visually reviewed. Cyrillic labels wrap in the captured layouts, but quiz questions/answers, Strength Focus/Strength & Sculpt, exercise names and RIR remain English. This is a localization acceptance failure, not a visual PASS. Evidence: `/tmp/mc-ux-ru-gym/2026-11-16_203214/`.
- The first English Home Dumbbells & Bands run was stopped after observed unilateral repetitions remained blank: the existing helper only populated weight. The helper now fills bilateral/per-side observed repetitions and captures the numeric keyboard; a full retry is in progress. Bodyweight-only full journey is pending.
- Shared reset completed on 2026-11-16 following explicit owner approval and confirmation that Development, Staging and Beta share the Staging deployment. Production was excluded. Resetting these environments sequentially is impossible while they share resources.
- Account `028417007003`, region `us-east-2`: shared RDS `musclecuties-staging-db` / database `musclecuties_staging`; API `musclecuties-backend-api`; worker `musclecuties-workout-reconciliation`; active media `musclecuties-media-staging-028417007003`. Inventory across all 17 enabled regions found no additional RDS instances or Lambda functions. The separate CloudFormation bucket `musclecuties-staging-media-028417007003` was empty and unused by the API. Production bucket `musclecuties-media-prod-028417007003` was not mutated.
- Fresh encrypted snapshot `mc-g95-staging-pre-reset-20261116` is available. Isolated RDS restore matched all 75 source tables by aggregated data fingerprint SHA256 `d86a65546b6d9bb0a4c94feced92e3152e46ab147608b866632ae55b4cb5d028`; reset rehearsal then passed migration 11, 781 seeded rows, compatible schema, zero users. Temporary restore instance deletion was requested.
- Versioned private encrypted S3 backup: `mc-g95-staging-backup-028417007003`, prefix `backup-20261116/`; all 10 object versions backed up and all 8 current objects restored under a separate drill prefix with matching sizes/ETags. Manifest SHA256 `c640baa264ada60bfd77a0296e073c3afc7d68ec401533c8c97c9fa8e5a4b8aa`. Two delete markers were also preserved in the manifest. Source reset deleted 12 versions/markers; zero remain.
- Shared database reset output: `{"status":"reset","schema_version":11,"seeded_rows":781,"compatible":true,"users_remaining":0}`. Both functions resumed their prior concurrency configuration. The older binary initially returned readiness HTTP 503 after the reset. Deployment of source `b2bd57b31a939c5741dfacc880f246e67186bbf6` restored liveness and readiness to HTTP 200. The API now invokes `musclecuties-backend-api:beta` → version 15; the reconciliation schedule invokes `musclecuties-workout-reconciliation:beta` → version 2. Exact candidate/serving ZIP digests matched and the release report returned `release_status=verified code=none`. Temporary restore RDS instance and security group are deleted. No TestFlight/App Store Connect actions occurred.

## 4. Next step or blocker

Backend deployment and health verification are complete. Complete the remaining Maestro journeys and visual review, then merge PR #50 only when required checks are green. Preserve the RDS snapshot and S3 backup. Gate 95 reset is executed; release readiness remains incomplete until API readiness and deployment evidence pass.

Worker package validation initially rejected the actual environment-configured worker because it required an unused `appsettings.json`. A regression test reproduced that failure; validation now requires its real runtime files and still rejects each missing runtime file. All 19 deployment tests pass. The regression is included in CI.

# CI integration-test clock repair — 2026-10-28

- Reported pipeline revision: GitLab `e305858a` on `codex/plan-generation-checkpoint`.
- Failure: `CS7036` at `InjuryCanonicalReconciliationTests.cs:117` because `PostgresCheckInAndInjuryStore` now requires a `TimeProvider` constructor argument. Earlier Core, Server and Registration jobs in the pasted log passed; the integration test project could not compile even though the job selected catalog tests.
- Fix: pass `TimeProvider.System` in the existing fixture. Its dates already derive from `DateTime.UtcNow`. Production behavior and clock injection remain unchanged.
- Verification ran in a clean managed worktree based on the latest GitLab checkpoint, on non-deploying branch `codex/ci-injury-clock-20261028`. Original local migration consolidation/deletions, importer edits and `temp-db-reset/` were preserved in the original checkout.

## Evidence

The complete integration-test project compiles in Release mode with the formerly failing injury fixture included. The same catalog filter used in GitLab passes **3/3**, with no failures or skips. `git diff --check` passes. The local SDK is 10.0.203; the reported GitLab runner uses 10.0.403. A fresh remote pipeline result is not claimed.

```sh
dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj \
  --configuration Release --filter FullyQualifiedName~ReviewedExerciseCatalogV020ImportTests \
  -p:CustomAfterMicrosoftCommonTargets=/private/tmp/mc-ci-verification.targets \
  -p:RestoreConfigFile=/private/tmp/mc-nutrition-nuget.config \
  -p:RestorePackagesPath=/private/tmp/mc-nutrition-clean-packages-20261028 \
  -m:1 /nodeReuse:false
```

The local temporary target removes only the integration project's unused direct `Microsoft.EntityFrameworkCore.Design` package reference to work around the previously recorded local restore issue. **No test source is removed or excluded.** Repository package references and CI rules are unchanged. The official-feed temporary configuration contains no personal credentials. This supersedes the injury-fixture exclusion used in the earlier nutrition checkpoint.

This is a test fixture and documentation correction. Database tests, production migrations, platform builds and deployment were not needed or performed for this change. The push branch is excluded by the existing GitLab workflow rules; staging deployment remains a separate manual action.

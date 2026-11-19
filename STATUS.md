# Status

## Changed files

- `src/MuscleCuties.Contracts/Account/CompleteAccountOnboardingContracts.cs` — added authenticated onboarding completion request/status/result contracts (`CompleteAccountOnboardingRequest`, `CompleteAccountOnboardingStatus`, `CompleteAccountOnboardingResult`).
- `src/MuscleCuties.UseCases/Features/Account/Authentication/AccountAuthenticationContracts.cs` — added additive `bool IsOnboardingComplete = true` property to `SessionTokens`.
- `src/MuscleCuties.UseCases/Features/Account/Registration/CompleteAccountOnboardingUseCase.cs` — added `ICompleteAccountOnboardingUseCase`, `ICompleteAccountOnboardingStore`, and `CompleteAccountOnboardingUseCase` validating full Profile Setup and Onboarding Quiz responses before persisting.
- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/PostgresAccountAuthenticationUseCase.cs` — set `IsOnboardingComplete = false` on newly created Apple accounts, reset legacy unfilled Apple accounts (no DOB/height/weight/goal and zero quiz responses) back to `IsOnboardingComplete = false` on sign-in, and propagated `IsOnboardingComplete` on `/api/v1/auth/password`, `/api/v1/auth/apple`, and `/api/v1/auth/refresh` session tokens.
- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Registration/PostgresCompleteAccountRegistrationStore.cs` — implemented `ICompleteAccountOnboardingStore.CompleteOnboardingAsync` reusing profile projection, initial snapshot, quiz response persistence, and initial cycle anchor creation atomically before setting `app.users.is_onboarding_complete = true`.
- `services/MuscleCuties.Server/Account/Registration/AccountRegistrationBody.cs` — added `CompleteAccountOnboardingBody` wire contract.
- `services/MuscleCuties.Server/ApiApplication.cs` — registered `ICompleteAccountOnboardingStore` / `ICompleteAccountOnboardingUseCase` and mapped authenticated `POST /api/v1/accounts/onboarding/complete`.
- `src/MuscleCuties.Infrastructure/ApiClient/MuscleCutiesBackendApiClient.cs` — added `bool IsOnboardingComplete = true` to `BackendSessionTokens`, parsed `isOnboardingComplete` on sign-in and token refresh (backward-compatible default `true`), and added `MobileJourneyCloudGateway.MarkOnboardingCompleteAsync`.
- `src/MuscleCuties.Infrastructure/Account/SignIn/CloudAccountSignInStore.cs` — fixed inverted `PlatformAccountOutcome` booleans and propagated server `IsOnboardingComplete` on both password and Apple sign-in.
- `src/MuscleCuties.Infrastructure/Services/Auth/CloudAuthService.cs` — propagated `IsOnboardingComplete` from `BackendSessionTokens` in `GetCurrentUserStateAsync` and `HandleAppleSignInAsync`.
- `src/MuscleCuties.Infrastructure/Cloud/CloudAccountAndProfileStores.cs` — enabled `CloudProfileSetupStore` and `CloudOnboardingQuizStore` to stage Profile Setup and submit `POST /api/v1/accounts/onboarding/complete` for authenticated accounts with `IsOnboardingComplete == false`, updating stored session tokens upon completion.
- `src/MuscleCuties.UI/MauiProgram.cs` — routed `QuizViewModel`'s Edit Profile action through `NavigateAuthenticatedAsync` so root activation returns cleanly to `ProfileSetupPage`.
- `docs/performance/server-latency-report.json` — regenerated measured route latency report including `POST /api/v1/accounts/onboarding/complete`.
- `tests/MuscleCuties.Registration.Tests/CloudAccountSignInStoreTests.cs`, `tests/MuscleCuties.Registration.Tests/SessionResponseTests.cs`, `tests/MuscleCuties.Registration.Tests/AuthenticatedOnboardingFlowTests.cs`, `tests/MuscleCuties.Registration.Integration.Tests/AppleSignInPostgresTests.cs`, `tests/MuscleCuties.Server.Tests/RegistrationAuthenticationRouteTests.cs` — added unit, HTTP route, and PostgreSQL integration tests; updated `AppleSignInPostgresTests.FirstAppleSignInCreatesAnAccountAndReturnsASession` to assert `IsOnboardingComplete == false` per the owner-requested onboarding enforcement.
- `STATUS.md` — updated verification and Beta `v21` deploy record.

## Test commands and pasted output

### Test-first RED verification (before implementation)

```text
$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release --filter "FullyQualifiedName~CloudAccountSignInStoreTests" -v minimal
Failed MuscleCuties.Registration.Tests.CloudAccountSignInStoreTests.SignInWithPlatformPropagatesServerOnboardingCompletionState(serverOnboardingComplete: True, expectedPendingOnboarding: False, expectedOnboardingComplete: True) [< 1 ms]
  Assert.Equal() Failure: Values differ
  Expected: False
  Actual:   True
Failed!  - Failed:     1, Passed:    10, Skipped:     0, Total:    11, Duration: 31 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~AppleSignInPostgresTests.FirstAppleSignInCreatesAnAccountAndReturnsASession" -v minimal
Failed MuscleCuties.Registration.Integration.Tests.AppleSignInPostgresTests.FirstAppleSignInCreatesAnAccountAndReturnsASession [635 ms]
  Assert.False() Failure
  Expected: False
  Actual:   True
Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 635 ms - MuscleCuties.Registration.Integration.Tests.dll (net10.0)
```

### Full Release test suites, PostgreSQL integration tests, and iOS Debug build (commit `1bf45d5b29890450bfef33e0c2aa6b94f3d6dddf`)

```text
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1760, Skipped:     0, Total:  1760, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 208 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    87, Skipped:     0, Total:    87, Duration: 591 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_WRITE_LATENCY_REPORT=1 MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   128, Skipped:     0, Total:   128, Duration: 55 s - MuscleCuties.Server.Tests.dll (net10.0)

$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" MC_REGISTRATION_WRITE_TEST_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~AppleSignInPostgresTests|FullyQualifiedName~RegistrationPersistenceTests" -v minimal
Passed!  - Failed:     0, Passed:    10, Skipped:     0, Total:    10, Duration: 8 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### AWS Lambda Beta deployment (commit `1bf45d5b29890450bfef33e0c2aa6b94f3d6dddf`, zero DB migrations)

```text
release_status=verified code=none
musclecuties-backend-api:beta -> version 21 (from 20), CodeSha256 sb0M464m1fWi0OFx/qYCBf/bRd4HtE76gdc1E6tkj40=
musclecuties-workout-reconciliation:beta -> version 8 (from 7), CodeSha256 pXa3+f/Gxwnv4R5QCfySX5J3Njl7ocRsg1/VYY3Fv2E=
/health/live: 200
/health/ready: 200
POST /api/v1/accounts/onboarding/complete (anon): 401
```

## What was not verified and why

- Interactive simulator walkthrough (Sign in with Apple -> Profile Setup -> Onboarding Quiz -> Dashboard/StrengthPlanGeneration) on the booted `iPhone 16` simulator requires rebuilding/relaunching the updated Debug iOS client from Rider and signing out of the previous cached simulator session once so the app fetches the new `isOnboardingComplete: false` session payload from Beta `v21`.

## Next step or blocker

- Rebuild and launch Debug from Rider on `iPhone 16`, sign out of the old cached session (if still signed in), and tap **Sign in with Apple** to walk through the complete **Profile Setup → Onboarding Quiz → Dashboard** flow against Beta `v21`.

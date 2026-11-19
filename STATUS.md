# Status

## Changed files

- `src/MuscleCuties.UI/Services/Configuration/AppEndpointConfiguration.cs` — scoped loopback overrides in `ResolveBackendBaseUrl` to `ActiveEnvironment == AppTargetEnvironment.Local` so stale simulator `backend_api_base_url` preferences cannot hijack Beta Debug builds back to `http://127.0.0.1:5080`.
- `src/MuscleCuties.UI/Services/Auth/PlatformAccountSignInAdapter.cs` — preserved user-facing `InvalidOperationException` messages from `AppleSignInService` (such as Apple Sign-In provisioning error 1000) instead of overwriting them with the generic connection error.
- `tests/MuscleCuties.Mobile.Contracts.Tests/AppEndpointConfigurationTests.cs` — updated `Debug_Build_Always_Targets_Beta_And_Ignores_Stale_Overrides` to verify stale loopback overrides (`http://127.0.0.1:5080`, `http://127.0.0.1:5090/`) resolve to `BetaBaseUrl` when Debug targets Beta.
- `STATUS.md` — updated verification summary.

## Test commands and pasted output

### Test-first RED verification (before implementation)

```text
$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Debug --filter "FullyQualifiedName~AppEndpointConfigurationTests" -v minimal
Failed MuscleCuties.Mobile.Contracts.Tests.AppEndpointConfigurationTests.Debug_Build_Always_Targets_Beta_And_Ignores_Stale_Overrides [2 ms]
Error Message:
 Assert.Equal() Failure: Strings differ
Expected: "https://y2ejsgrc6l.execute-api.us-east-2."···
Actual:   "http://127.0.0.1:5080"
Failed!  - Failed:     1, Passed:    17, Skipped:     0, Total:    18, Duration: 24 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)
```

### Verification after implementation

```text
$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Debug -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 440 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 316 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1757, Skipped:     0, Total:  1757, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Also removed the stale `backend_api_base_url` preference (`http://127.0.0.1:5080`) from the booted `iPhone 16` simulator (`60DD8563-A1D6-4FE5-9F4F-2BFB975B194B`).

## What was not verified and why

- Interactive Apple Sign-In sheet completion inside the Rider Debug session on `iPhone 16` was not triggered from this shell; ready for owner re-run from Rider.

## Next step or blocker

- Restart/rerun the Debug session from Rider so it picks up the updated `ResolveBackendBaseUrl` and cleared simulator preference, then test Sign in with Apple and password login against `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`.

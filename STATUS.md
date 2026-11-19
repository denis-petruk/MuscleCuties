# Status

## Changed files

- `src/MuscleCuties.UI/Services/Configuration/AppEndpointConfiguration.cs` — changed `ActiveEnvironment` in `#if DEBUG` builds to default to `AppTargetEnvironment.Beta` (`https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`) per owner request, while keeping explicit loopback debug overrides (`IsLoopbackOverride`) and strict `APP_ENV_BETA` enforcement for `Release` builds.
- `tests/MuscleCuties.Mobile.Contracts.Tests/AppEndpointConfigurationTests.cs` — updated the Debug endpoint contract test (justified by owner request to target Beta on every Debug build) to assert `ActiveEnvironment == AppTargetEnvironment.Beta`, default resolution to `BetaBaseUrl`, rejection of non-allowlisted overrides, and continued support for explicit loopback overrides.
- `STATUS.md` — updated verification summary for this change.

## Test commands and pasted output

### Test-first RED verification (before implementation)

```text
$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Debug --filter "FullyQualifiedName~AppEndpointConfigurationTests" -v minimal
Failed MuscleCuties.Mobile.Contracts.Tests.AppEndpointConfigurationTests.Debug_Build_Targets_Beta_By_Default_And_Allows_Loopback_Override [1 ms]
Error Message:
 Assert.Equal() Failure: Values differ
Expected: Beta
Actual:   Local
Failed!  - Failed:     1, Passed:    17, Skipped:     0, Total:    18, Duration: 22 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)
```

### Verification after implementation

```text
$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Debug -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 265 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 413 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1757, Skipped:     0, Total:  1757, Duration: 2 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

## What was not verified and why

- Interactive sign-in (Apple Sign-In and email/password) from a physical iOS device or Rider Debug session against `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com` was not executed from this shell; ready for owner testing directly in Debug.
- Server/worker test suites were not re-run because no backend files changed in this commit.

## Next step or blocker

- Launch the app in Debug from Rider (Simulator or physical iPhone) and test Sign in with Apple and password login directly against Beta (`https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`).
- Note that any previously stored `backend_api_base_url` loopback preference in Preferences would still override to loopback in Debug; clear/reinstall the app if an older local URL override was set.

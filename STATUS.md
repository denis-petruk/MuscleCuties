# Status

## Changed files

- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/PostgresAccountAuthenticationUseCase.cs` — bounded Apple authorization-code exchange during sign-in with `AppleTokenExchangeTimeout` (3s) and fallback to storing `apple-code:<authorizationCode>` when outbound calls to IPv4-only `appleid.apple.com` fail or time out from the Lambda VPC, logging only the exception type.
- `src/MuscleCuties.Infrastructure/Account/SignIn/CloudAccountSignInStore.cs` — distinguished credential rejection (`BackendApiStatus.Unauthorized` -> `null`) from backend/network failures (`OfflineRejected`, `Timeout`, `ServerError`, `RateLimited` -> `HttpRequestException`) for password sign-in, and reported connection/service errors distinctly for Apple sign-in.
- `tests/MuscleCuties.Registration.Integration.Tests/AppleSignInPostgresTests.cs` — added PostgreSQL integration regression tests covering network error and timeout fallback during Apple sign-in authorization-code exchange.
- `tests/MuscleCuties.Registration.Tests/CloudAccountSignInStoreTests.cs` — added contract tests verifying password and Apple sign-in error classification across `Unauthorized`, `OfflineRejected`, `Timeout`, `ServerError`, and `RateLimited`.
- `STATUS.md` — updated task verification and Beta Lambda deployment record.

## Test commands and pasted output

### Test-first RED verification (before fixes)

```text
$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj --filter "FullyQualifiedName~CloudAccountSignInStoreTests" -v minimal
Failed!  - Failed:     8, Passed:     1, Skipped:     0, Total:     9, Duration: 29 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj --no-build --filter "FullyQualifiedName~AppleSignInSucceedsAndStoresAuthorizationCodeWhenTokenExchangeThrowsNetworkError" -v minimal
Failed MuscleCuties.Registration.Integration.Tests.AppleSignInPostgresTests.AppleSignInSucceedsAndStoresAuthorizationCodeWhenTokenExchangeThrowsNetworkError [510 ms]
Error Message:
 System.Net.Http.HttpRequestException : Network is unreachable
Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1, Duration: 510 ms - MuscleCuties.Registration.Integration.Tests.dll (net10.0)
```

### Full Release test suites and iOS build (after fixes, commit `7ff40519d05d299e5b247413039c2d1865c23863`)

```text
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1757, Skipped:     0, Total:  1757, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 231 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    79, Skipped:     0, Total:    79, Duration: 745 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   125, Skipped:     0, Total:   125, Duration: 59 s - MuscleCuties.Server.Tests.dll (net10.0)

$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~AppleSignInPostgresTests" -v minimal
Passed!  - Failed:     0, Passed:     4, Skipped:     0, Total:     4, Duration: 4 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

### AWS Lambda Beta deployment and live endpoint verification

Deployed commit `7ff40519d05d299e5b247413039c2d1865c23863` (zero database migrations in diff; RDS automated snapshot present) via `tools/deployment/validate-package.sh`, `tools/deployment/validate-worker-package.sh`, `tools/deployment/upload-artifact.sh`, and `tools/deployment/deploy_release.py deploy`:

```text
release_status=verified code=none
- musclecuties-backend-api:beta -> version 17 (from 16), CodeSha256 gq/QylegX3erWD+9WE9xUpF8Cz7b8nJR73a806IaUYI=
- musclecuties-workout-reconciliation:beta -> version 4 (from 3), CodeSha256 qLpDSX9uDf61G3g1kJM6IHMeYji+tAdcLfY7xc+6K+E=
- /health/live -> HTTP 200 ({"status":"live","apiVersion":"v1"})
- /health/ready -> HTTP 200 (Healthy)
- POST /api/v1/auth/apple/nonce -> HTTP 200
- POST /api/v1/auth/apple (synthetic malformed token) -> HTTP 401 (CloudWatch v17: "Apple sign-in rejected before session lookup: InvalidFormat")
- POST /api/v1/auth/password (synthetic nonexistent account) -> HTTP 401
- API Gateway HTTP API y2ejsgrc6l integration -> arn:aws:lambda:us-east-2:028417007003:function:musclecuties-backend-api:beta
```

## What was not verified and why

- End-to-end live Sign in with Apple on a physical iOS device with a real Apple ID token was not executed from this workstation (requires an interactive iOS device signed into iCloud with a Release Beta / TestFlight build pointing to `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com`).
- Server-side Apple token revocation on account deletion (`POST https://appleid.apple.com/auth/revoke`) from inside the Lambda VPC was not verified end-to-end because `appleid.apple.com` publishes IPv4-only A records while Lambda route table `rtb-048f0c0463cf0a996` currently has only IPv6 egress (`::/0` via `eigw-09924dc9d1f54d31e`) and no IPv4 NAT route. No paid NAT infrastructure was provisioned without owner approval.
- The non-Apple tests in `MuscleCuties.Registration.Integration.Tests` were not run in this pass (only `AppleSignInPostgresTests` ran against local Postgres).

## Next step or blocker

- Retest Sign in with Apple and password login from a Beta iOS build (`-p:AppTargetEnvironment=Beta`, or TestFlight) against `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com` (note: Debug builds intentionally target `http://127.0.0.1:5080` and require `scripts/run-local-api.sh`).
- Decide whether to add IPv4 outbound egress (NAT Gateway / NAT instance) to VPC `vpc-02c2c07f0bf07f145` (`rtb-048f0c0463cf0a996`) so Apple account-deletion revocation can also reach `appleid.apple.com` from Lambda.
- Open and merge the PR for `fix/qa-audit-followups` into `main`.

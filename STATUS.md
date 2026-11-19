# Status

## Changed files

- `services/MuscleCuties.Server/Nutrition/FdcExternalFoodSource.cs` — switched USDA FoodData Central search from `GET /fdc/v1/foods/search` with comma/parenthesis-encoded `dataType` query parameters (which USDA's nginx proxy rejected over IPv6 with `HTTP 400 Bad Request`) to `POST /fdc/v1/foods/search` with a JSON body (`query`, `dataType` array, `pageSize`, `pageNumber`) and `X-Api-Key` header.
- `services/MuscleCuties.Server/ApiApplication.cs` — raised `FdcExternalFoodSource` HTTP client timeout to named constant `fdcSearchTimeoutSeconds = 8` (up from `4`).
- `tests/MuscleCuties.Server.Tests/FdcExternalFoodSourceTests.cs` — added regression tests verifying `POST /fdc/v1/foods/search` JSON payload, `X-Api-Key` header (never in URL), and graceful empty fallback on HTTP 400.
- `STATUS.md` — updated verification and Beta deployment record.

## Test commands and pasted output

### Test-first RED verification (before implementation)

```text
$ dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj --filter "FullyQualifiedName~FdcExternalFoodSourceTests" -v minimal
Failed MuscleCuties.Server.Tests.FdcExternalFoodSourceTests.FdcExternalFoodSource_SearchAsync_UsesPostWithJsonDataTypeArrayAndHeaderApiKey [2 ms]
Error Message:
 Assert.Equal() Failure: Values differ
Expected: POST
Actual:   GET
Failed!  - Failed:     1, Passed:     3, Skipped:     0, Total:     4, Duration: 22 ms - MuscleCuties.Server.Tests.dll (net10.0)
```

### IPv6 diagnostic against live `api.nal.usda.gov` (with `X-Api-Key` from local `.env`, value not printed)

```text
GET comma try 1: HTTP 400 time=0.401357s
GET comma try 2: HTTP 400 time=0.357430s
GET comma try 3: HTTP 400 time=0.401750s
POST json try 1: HTTP 200 time=0.623875s
POST json try 2: HTTP 200 time=0.743078s
POST json try 3: HTTP 200 time=0.595538s
```

### Full Release test suites (commit `2ed77904a74224c21938e81359dc88a4a9713f8f`)

```text
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1757, Skipped:     0, Total:  1757, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 252 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    79, Skipped:     0, Total:    79, Duration: 545 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   127, Skipped:     0, Total:   127, Duration: 1 m 3 s - MuscleCuties.Server.Tests.dll (net10.0)
```

### AWS Lambda Beta deployment (commit `2ed77904a74224c21938e81359dc88a4a9713f8f`, zero DB migrations)

```text
release_status=verified code=none
- musclecuties-backend-api:beta -> version 18 (from 17), CodeSha256 iLqZGAY1HvZqXYoPhdFLBXwj4E7DPKcnlQRpqS5m7zs=
- musclecuties-workout-reconciliation:beta -> version 5 (from 4), CodeSha256 kdiFsSH3SOpdrNg3D3j/CZHr8p5wzdLIU2x6pEQlxWA=
- /health/live -> 200
- /health/ready -> 200
```

## What was not verified and why

- An authenticated `GET /api/v1/nutrition/catalog/search?query=tempeh` call directly against Beta `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com` was not executed from this shell because creating a throwaway user in the shared Beta RDS database was avoided; ready to verify immediately after signing in from the app.
- The `MuscleCuties.UI` iOS build was not re-run for this server-only commit (previously verified with 0 warnings / 0 errors on `5884418d`).

## Next step or blocker

- Stop and relaunch Debug in Rider (now targeting `https://y2ejsgrc6l.execute-api.us-east-2.amazonaws.com` with stale loopback overrides ignored), sign in, and search a sparse generic food (for example `tempeh` or `seitan`) in Nutrition to verify live USDA FoodData Central fallback results from Lambda version 18.
- Note on FDC search scope: `ExternalFoodFallbackSearchStore` only queries USDA when local catalog hits are `< 5` AND the query matches `GenericFoodQueryRule` (English generic foods/descriptors); let the owner decide if non-English or broader generic food terms should also be supported.

# Status

## Changed files

- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/AppleIdentityTokenValidator.cs` — raised `MaximumIdentityLifetimeSeconds` from `3_600` (1 hour) to `86_400` (24 hours) after CloudWatch v18 showed Apple's live iOS `identityToken` passed signature, issuer, audience, subject, and nonce checks and was rejected solely with `LifetimeTooLong`. Server-side single-use nonce expiry remains 5 minutes.
- `tests/MuscleCuties.Registration.Tests/AppleIdentityTokenValidatorTests.cs` — added `TwentyFourHourAppleIdentityTokenIsAccepted` and updated the upper-bound rejection test to `TokenLifetimeBeyondTwentyFourHoursIsRejected` (25h rejected as `LifetimeTooLong`).
- `src/MuscleCuties.UseCases/Features/Nutrition/Overview/OpenSourceMealGenerator.cs` — added `OpenSourceMealGenerator` and bounded `OpenSourceIngredientCache` to generate portion-balanced meals (4 blueprints per slot for savoury breakfast, sweet breakfast, lunch, dinner, and snack) using live USDA FoodData Central open-source per-100g macros via `IExternalFoodSource`, enforcing `GeneratedMealPortionRule` calorie targets and oil caps.
- `src/MuscleCuties.Infrastructure.PostgreSql/Nutrition/Overview/PostgresNutritionOverviewStore.cs` — wired optional `IExternalFoodSource` + `OpenSourceIngredientCache` into `GetDayMealPlanAsync`, `RefreshDayMealAsync`, and `GetMealSuggestionsAsync` (with deterministic daily variety, cycling across all concepts on refresh, and built-in fallback when external source is unconfigured or unreachable).
- `services/MuscleCuties.Server/ApiApplication.cs` — registered singleton `OpenSourceIngredientCache` for per-container USDA ingredient reuse across requests.
- `tests/MuscleCuties.Core.Tests/OpenSourceMealGeneratorTests.cs` — added tests verifying USDA macro usage, calorie/oil portion accuracy, distinct concept cycling, caching, and unconfigured fallback.
- `STATUS.md` — updated verification and Beta v19 deploy record.

## Test commands and pasted output

### Test-first RED verification (before implementation)

```text
$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj --filter "FullyQualifiedName~AppleIdentityTokenValidatorTests" -v minimal
Failed MuscleCuties.Registration.Tests.AppleIdentityTokenValidatorTests.TwentyFourHourAppleIdentityTokenIsAccepted [45 ms]
Error Message:
 Assert.Equal() Failure: Values differ
Expected: Valid
Actual:   LifetimeTooLong
Failed!  - Failed:     1, Passed:     7, Skipped:     0, Total:     8, Duration: 375 ms - MuscleCuties.Registration.Tests.dll (net10.0)
```

### Full Release test suites and PostgreSQL integration tests (commit `33e9ff05c1e2881a823fe5d1e96480df98188c67`)

```text
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1760, Skipped:     0, Total:  1760, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 296 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    80, Skipped:     0, Total:    80, Duration: 586 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   127, Skipped:     0, Total:   127, Duration: 1 m - MuscleCuties.Server.Tests.dll (net10.0)

$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~AppleSignInPostgresTests|FullyQualifiedName~NutritionSuggestionPortionTests" -v minimal
Passed!  - Failed:     0, Passed:    26, Skipped:     0, Total:    26, Duration: 9 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)
```

### AWS Lambda Beta deployment (commit `33e9ff05c1e2881a823fe5d1e96480df98188c67`, zero DB migrations)

```text
release_status=verified code=none
- musclecuties-backend-api:beta -> version 19 (from 18), CodeSha256 CkX7NEkd0PzMDH+0MGgoITxIe3FUXw2eP/FqJejFhm8=
- musclecuties-workout-reconciliation:beta -> version 6 (from 5), CodeSha256 x+ro1ljArKkZ+bsMxUXyVoCTdPIZbLcYNCTmDAt/uNY=
- /health/live -> 200
- /health/ready -> 200
```

## What was not verified and why

- Live interactive Sign in with Apple and "Plan my day" meal generation from the iOS Simulator / device against Beta `v19` require interactive iOS Sign in with Apple authentication; ready to retest immediately without rebuilding the iOS app (server-only update on `v19`).

## Next step or blocker

- Tap **Sign in with Apple** in the already-running Beta Debug app (no iOS rebuild needed for `v19`), then open **Nutrition -> Plan my day** and tap Refresh on meal cards to verify live USDA FoodData Central generated meals and food search.

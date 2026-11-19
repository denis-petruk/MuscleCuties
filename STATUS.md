# Status

## Changed files

- `src/MuscleCuties.Infrastructure.PostgreSql/Profile/ServerProfile/DefaultUserProfileFactory.cs` — added internal factory producing an initial unfilled `UserProfileRecord` with valid JSONB defaults (`[]` / `{}`) and unset safety-critical fields (`DateOfBirth = null`, `EquipmentLevel = ""`, `WorkoutDaysPerWeek = 0`, etc.) so workout generation still enforces explicit user completion.
- `src/MuscleCuties.Infrastructure.PostgreSql/Account/Authentication/PostgresAccountAuthenticationUseCase.cs` — provisioned the initial `app.user_profiles` row inside `LoginWithAppleTransactionalAsync` for first-time Apple Sign-In accounts.
- `src/MuscleCuties.Infrastructure.PostgreSql/Profile/ServerProfile/PostgresProfileDataStore.cs` — backfilled missing `app.user_profiles` rows in `LoadAsync` (serialized via `SELECT ... FOR UPDATE` on `app.users`) for existing authenticated accounts created before this fix, while still returning `null` (`404`) for unknown user IDs.
- `src/MuscleCuties.Infrastructure.PostgreSql/Profile/UnitsDisplay/PostgresProfileUnitsStore.cs` — applied the same serialized initial-profile backfill in `LoadTransactionalAsync` so `/api/v1/profile/units` never returns `404` on first login.
- `tests/MuscleCuties.Registration.Integration.Tests/AppleSignInPostgresTests.cs` — added PostgreSQL integration regression tests verifying first-time Apple Sign-In profile provisioning, legacy Apple account backfill, unknown-user `null` preservation, and successful `UpdateWorkoutPreferencesAsync`.
- `STATUS.md` — updated verification and Beta `v20` deploy record.

## Test commands and pasted output

### Test-first RED verification (before implementation)

```text
$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj --filter "FullyQualifiedName~FirstAppleSignInProvisionsInitialProfileSoWorkoutPreferencesCanBeSaved|FullyQualifiedName~ProfileLoadBackfillsMissingProfileForExistingUserAndReturnsNullForUnknownUser" -v minimal
Failed MuscleCuties.Registration.Integration.Tests.AppleSignInPostgresTests.FirstAppleSignInProvisionsInitialProfileSoWorkoutPreferencesCanBeSaved [608 ms]
  Assert.NotNull() Failure: Value is null
Failed MuscleCuties.Registration.Integration.Tests.AppleSignInPostgresTests.ProfileLoadBackfillsMissingProfileForExistingUserAndReturnsNullForUnknownUser [337 ms]
  Assert.NotNull() Failure: Value is null
Failed!  - Failed:     2, Passed:     0, Skipped:     0, Total:     2, Duration: 947 ms - MuscleCuties.Registration.Integration.Tests.dll (net10.0)
```

### Full Release test suites and PostgreSQL integration tests (commit `51ab957c585e2272a062fcdd9fdf76185e75089f`)

```text
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1760, Skipped:     0, Total:  1760, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 289 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    80, Skipped:     0, Total:    80, Duration: 648 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   127, Skipped:     0, Total:   127, Duration: 1 m 2 s - MuscleCuties.Server.Tests.dll (net10.0)

$ MC_INTEGRATION_POSTGRES=1 MC_SCHEMA_COMPATIBILITY_POSTGRES="Host=127.0.0.1;Port=55461;Database=postgres;Username=postgres" dotnet test tests/MuscleCuties.Registration.Integration.Tests/MuscleCuties.Registration.Integration.Tests.csproj -c Release --filter "FullyQualifiedName~AppleSignInPostgresTests" -v minimal
Passed!  - Failed:     0, Passed:     6, Skipped:     0, Total:     6, Duration: 5 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)
```

### AWS Lambda Beta deployment (commit `51ab957c585e2272a062fcdd9fdf76185e75089f`, zero DB migrations)

```text
release_status=verified code=none
- musclecuties-backend-api:beta -> version 20 (from 19), CodeSha256 tmixWw3MGuvSfc5qhUs39DR1nhB/c97RFHNuliommPY=
- musclecuties-workout-reconciliation:beta -> version 7 (from 6), CodeSha256 wXOUNauZ3/mSXdzuuC51baWQOtDRwXPbTz42kM6DHzM=
- /health/live -> 200
- /health/ready -> 200
```

## What was not verified and why

- Interactive workout preferences save + workout plan generation from the already-signed-in `iPhone 16` simulator session against Beta `v20` requires tapping **Set up plan** in the simulator; ready to test immediately without rebuilding or signing out.

## Next step or blocker

- Tap **Set up plan** again in the already-running app (no rebuild or sign-out needed—`v20` backfills the missing `app.user_profiles` row automatically on `GET /api/v1/profile`).

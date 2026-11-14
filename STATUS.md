# Status

Branch `fix/qa-audit-followups`, on top of `d6615041`. The full solution gate is green and the
formatter, hydration and gate repairs are committed. Nothing has been pushed.

## Gate state

    source .env.integration && dotnet test --no-build --no-restore

    Passed!  - Failed: 0, Passed: 183, Skipped: 0, Total: 183, Duration: 285 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)
    Passed!  - Failed: 0, Passed: 60, Skipped: 0, Total: 60, Duration: 496 ms - MuscleCuties.Registration.Tests.dll (net10.0)
    Passed!  - Failed: 0, Passed: 1408, Skipped: 0, Total: 1408, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)
    Passed!  - Failed: 0, Passed: 120, Skipped: 0, Total: 120, Duration: 51 s - MuscleCuties.Server.Tests.dll (net10.0)
    Passed!  - Failed: 0, Passed: 105, Skipped: 0, Total: 105, Duration: 3 m 2 s - MuscleCuties.Registration.Integration.Tests.dll (net10.0)

    0 failed, 1,876 passed across the five test projects.

The test assemblies were built per project first, because a plain `dotnet test` that also builds
the MAUI head tries to sign the iOS app and fails with `No valid iOS code signing keys found in
keychain` on this host. The test gate itself (`--no-build`) passed; the app head was built
separately for the simulator.

    dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios \
      -p:RuntimeIdentifier=iossimulator-arm64 --no-restore

    Build succeeded.
        0 Warning(s)
        0 Error(s)

    dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj --no-restore \
      --filter "FullyQualifiedName~UiContractGuard|FullyQualifiedName~UiStyleGate|FullyQualifiedName~Xaml_buttons_use_shared_styles|FullyQualifiedName~DisplayFormat_clock"

    Passed!  - Failed: 0, Passed: 51, Skipped: 0, Total: 51

## Failures repaired

- `AppleHealthSyncIntegrationTests.DailyCheckInViewModel_ManualSliderAndChipChanges_OverridePrepopulatedValues`
  and `DailyCheckInViewModel_WithCachedLastNightSleep_PrepopulatesSleepHoursAndBadge` expected
  `7.5h` / `6.0h`. The formatter is the source of truth: `Common_Format_Hours` is `"{0:N1} h"` /
  `"{0:N1} ч"`, so the correct output is `7.5 h` / `6.0 h`. The assertions were updated. The test
  catalog stub was missing `Common_Format_Hours` entirely, so it also now supplies the real shape.
- `PersonalInfoNoFabricationTests.Save_never_writes_a_placeholder_birth_date_experience_or_cycle_length`
  failed on a null `cycleLength` while the stored profile had 30. `SaveWithSnapshotAsync` was
  sending null for an unset request, which erased the stored value. It now echoes a stored
  positive cycle length and sends null only when nothing is stored. The existing missing-values
  test (`Unchanged_birth_date_and_unset_values_are_not_written`) still passes.
- `ServerRouteParityTests.EveryMappedRouteHasMeasuredColdAndWarmP95WithinBudget` required
  `docs/performance/server-latency-report.json`, which did not exist. The report was measured
  with the disposable PostgreSQL fixture (`MC_WRITE_LATENCY_REPORT=1`) and is now checked in.
- `DisplayFormat.Time` passed the `HH:mm` pattern to `TimeSpan` for both overloads, which throws
  at runtime and put the Food Log into its retry state on days with meals. `DateTime` is now
  formatted directly and `TimeSpan` uses `hh\:mm`; a direct guard renders both overloads.
- `docs/release-audit/2026-11-05/WORKOUT_POLICY.md` is restored by `19887dca`; the file is
  tracked and matches HEAD.

## Commits

- `46a9f3d5` — consolidate display formatting across nutrition and history
- `e4beb7ca` — restore the hydration card layout and load lifecycle
- `5d2fdca0` — fix the formatter expectations, preserve stored cycle lengths, add the latency report
- `cc2d7dd1` — format clock values without a time-span pattern

## Evidence

The balance-card layout committed in `d6615041` was checked in the simulator at standard and
`accessibility-large` text sizes in light and dark appearance:

- `docs/qa-evidence/wave-a/nutrition-balance-light.png`
- `docs/qa-evidence/wave-a/nutrition-balance-dark.png`
- `docs/qa-evidence/wave-a/nutrition-balance-accessibility-large.png`

## Not verified

- Nothing was pushed and no PR or CI run was started.
- The simulator was not relaunched after the final formatter clock fix; that fix is covered by
  the direct formatter guard and the simulator build, not by a new visual pass.
- The hydrated water card was not re-captured after its final button-width trim.

## Next step

Start the remaining QA fix waves from this green baseline.

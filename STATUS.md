# Status

## Changed files

- `src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml` — decluttered the active workout logging layout:
  - Replaced `PresentationPageContentPadding` (96 pt tab-bar bottom inset) with `PresentationFormPagePadding` since the tab bar is hidden on this page.
  - Grouped all sets for the current exercise inside a single `CardSecondary` with 1 pt hairline dividers (`ShowDivider`) and a compact 44×44 remove control (`CanRemove`) instead of a separate card and 52 pt button per set.
  - Collapsed the secondary controls below the sets (`Tips & technique`, `Notes`, `Optional exercise`, and `Swap` / `Skip` / `This hurts`) into a single grouped `CardSecondary` with hairline dividers below the rest-timer row.
  - Moved `Notes` out of the exercise title row so long Russian exercise names have full width at accessibility text sizes, tightened the 8-dot progress strip (`UiDimension40` × `UiDimension36`, centered) so all 8 dots fit without right-edge clipping, hid `"Previous: No previous log"` via `HasPreviousLog`, hid `RecommendationText` when empty or superseded by `CalibrationPromptText`, hid the `Try again` reload button on inline form validation errors (`CanRetryLoad`), and switched unilateral side-rep placeholders to compact `Workout_Session_LeftSideShort` / `Workout_Session_RightSideShort` (`L`/`R`, `Л`/`П`) with `UiInsets4_0` padding while preserving full `SemanticProperties.Description`.
- `src/MuscleCuties.UI/Models/UI/Workout/ExerciseSetInputItem.cs` — aligned `DisplaySetNumber` and added `IsObserved`, `EnteredWeightKg`, `HasEnteredReps`, `IsCapturedWorkingSet`, `ShowDivider`, and `CanRemove`.
- `src/MuscleCuties.UI/Models/UI/Workout/WorkoutExerciseItem.cs` — aligned `DisplaySetNumber`, updated `RenumberSets()` to maintain `ShowDivider` and `CanRemove`, added `HasPreviousLog`, and added `HasRecommendationText` (suppressing duplicate starting-weight copy while `CalibrationPromptText` is shown).
- `src/MuscleCuties.UI/Services/Workout/WorkoutSessionPresentationMapper.cs` — aligned set hydration to `DisplaySetNumber`.
- `src/MuscleCuties.UI/ViewModels/Workout/WorkoutSessionViewModel.cs` — added `CanRetryLoad` so inline set/weight/reps/duration validation errors do not display the session-reload `Try again` button.
- `src/MuscleCuties.UI/Pages/Workout/WorkoutPage.xaml` — localized `InProgressWorkout.Title` through `LocalizedStringConverter` so the active-workout resume banner translates into Russian.
- `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.CycleWorkout.cs` and `RussianUiText.CycleWorkout.cs` — added `Workout_Session_LeftSideShort` (`L` / `Л`) and `Workout_Session_RightSideShort` (`R` / `П`).
- `tests/MuscleCuties.Core.Tests/WorkoutSessionLifecycleAndModalTests.cs` — added three guard tests covering `CanRetryLoad`, `HasPreviousLog` / `HasRecommendationText` / `ShowDivider` / `CanRemove`, grouped set layout without card-per-set clutter, and compact unilateral `L`/`R` placeholders with full accessible names.
- `tests/MuscleCuties.Core.Tests/LocalizationCatalogTests.cs` — added `WorkoutPage.xaml` `InProgressWorkout.Title` to `Workout_exercise_names_use_runtime_localization`.
- `tests/maestro/workout-engine-v2/browse-tabs.yaml`, `check-in.yaml`, `home-equipment-scenarios.yaml`, `log-current-exercise.yaml`, `log-set.yaml` — updated selectors for accessible `Workout Availability` descriptions, `"Set up plan"` / `"Настроить план"` CTA copy, asynchronous Apple Health prompt dismissal, and keyboard/scroll handling during set logging.

## Tests

All new unit/guard tests were written first, verified failing for the missing behavior, and then verified green along with the affected suites.

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:  1726, Skipped:     0, Total:  1726, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 236 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:    70, Skipped:     0, Total:    70, Duration: 433 ms - MuscleCuties.Registration.Tests.dll (net10.0)

$ MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal
Passed!  - Failed:     0, Passed:   125, Skipped:     0, Total:   125, Duration: 58 s - MuscleCuties.Server.Tests.dll (net10.0)

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal
Build succeeded.
    0 Warning(s)
    0 Error(s)
Time Elapsed 00:00:14.74
```

Simulator verification on iPhone 17 Pro (iOS 26.3):
- Light Mode + default text size (EN): verified decluttered `WorkoutSessionPage` top and scrolled states (8 unclipped dots, grouped sets card with crisp `L`/`R` placeholders and minimal remove controls, single grouped tools card below rest timer) and passed `tests/maestro/workout-engine-v2/home-equipment-scenarios.yaml` end-to-end (`16_workout_preferences`, `10_plan_generation`, `10b_plan_bw_gap`, `10c_plan_bw_sliders`, `10d_plan_bands_no_anchor`, `10e_plan_bands_with_anchor`).
- Dark Mode + `accessibility-large` (RU): verified `Train` tab (`InProgressWorkout.Title` localized to `"Общая силовая тренировка"`, `FeaturedWorkoutCard` rows wrapping cleanly without mid-word breaks), `Nutrition` tab, and decluttered `WorkoutSessionPage` top and scrolled states.

## Not verified

- `tests/MuscleCuties.Registration.Integration.Tests`: ran with `MC_SCHEMA_COMPATIBILITY_POSTGRES` (2 passed, 100 skipped because `MC_REGISTRATION_WRITE_TEST_POSTGRES` / `MC_PG09_LOAD_POSTGRES` were not set against the active API database).
- Full unattended `tests/maestro/workout-engine-v2/s26.yaml` completion in a single 5-minute shell invocation: `ensure-logged-out`, `register`, `profile`, `quiz`, `check-in`, `browse-tabs`, and exercise 1 logging completed before the 5-minute tool command limit; `home-equipment-scenarios.yaml` was verified separately and passed 100%, while full multi-exercise completion + `12_workout_history` in one uninterrupted `s26.yaml` run was not completed within that timeout.
- Physical iOS device testing and TestFlight upload (owner-only actions).

## Next step

- Review the pushed changes on `fix/qa-audit-followups` and open/update the PR into `main`.

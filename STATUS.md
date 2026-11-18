# Status

## Changed files

- `src/MuscleCuties.UI/Controls/Workout/WorkoutActivityCard.xaml` — the session-action row now spans
  both card columns (`Grid.Column="0" Grid.ColumnSpan="2"`). Measured against the title column alone
  it lost the day tile's width, so at accessibility sizes the longest word of the action label
  ("Обязательная") had less room than the word needs and broke inside itself.
- `src/MuscleCuties.UI/Pages/Workout/WorkoutPage.xaml` — the schedule summary and the week chip row
  are keyed to `HasPlanWeeks` instead of `HasScheduleDays`; the collection's `EmptyView` is replaced
  by an empty-state card in the page flow, bound to `ShowEmptyWorkouts`.
- `src/MuscleCuties.UI/ViewModels/Workout/WorkoutViewModel.cs` — added `HasPlanWeeks` and
  `ShowEmptyWorkouts`; the empty-state title, subtitle and button are catalog keys instead of English
  built from the localized chip label; removed the now unused `SelectedFilterLabel`.
- `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.CycleWorkout.cs` and
  `RussianUiText.CycleWorkout.cs` — `Workout_Page_EmptyFilterTitle`, `Workout_Page_EmptyFilterSubtitle`,
  `Workout_Page_EmptyFilterShowAll` in both languages.
- `tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs` — three guards:
  `Activity_card_actions_take_the_whole_card_at_large_text`,
  `Week_chips_stay_reachable_when_the_chosen_week_has_no_days`,
  `Empty_workouts_state_is_part_of_the_page_flow_with_catalog_copy`.

## Tests

All three guards were written first and failed for the missing behaviour (the chip row reported
`{Binding HasScheduleDays}`, the page still had an `EmptyView`, the action row had no column span).

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
Passed!  - Failed:     0, Passed:  1699, Skipped:     0, Total:  1699, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)
TEST_EXIT=0

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)
BUILD_EXIT=0
```

## Not verified

- The rebuilt app was not looked at on the simulator: the run that would have shown the empty card
  under Кардио, the week chips after choosing a week without sessions, and the action button at
  accessibility sizes was interrupted before it produced screenshots. The three defects themselves
  were reproduced on the simulator with the previous build (chips and the whole schedule block
  disappearing after a week tap, a blank list under Кардио, the action label breaking mid-word at
  accessibility-extra-extra-extra-large).

## Next step

- Install the current build on the MC Generation QA Clone and confirm the three states visually, then
  open the PR into `main`.

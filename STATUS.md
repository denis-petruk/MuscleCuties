# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. Changed files

This pass closed D7, D13 and the archive's mid-word wrap on top of `64a42f65`; D43 is untouched.

- `src/MuscleCuties.UI/Controls/Dashboard/ReadinessRecoveryCard.xaml`,
  `ViewModels/Dashboard/DashboardViewModel.cs` — the no-record state keeps the `Нужна отметка` pill and
  one explanation; the duplicate subtitle and tinted prompt box are removed.
- `src/MuscleCuties.UI/Controls/Dashboard/DashboardWorkoutCard.xaml` — the `ДЕНЬ ОТДЫХА` pill is gone;
  the title and guidance carry the state.
- `src/MuscleCuties.UI/Controls/Cycle/CycleCalendarCard.xaml`,
  `ViewModels/Cycle/CycleViewModel.cs`, `Services/Localization/{RussianUiText,UiTextCatalog}.CycleWorkout.cs`
  — the hero states the phase once; the calendar card keeps `Запись от 13 нояб.` through the new
  `Cycle_Calendar_LastLogFormat`, with no repeated `logged` sentence or phase title.
- `src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml` — one date on the page; the water card hosts
  only `HydrationLogView`; `Скопировать вчерашнее` and `Разбор питательных веществ` are two full-width
  `ButtonSecondary` controls in their own card; `Дневник за вчера` and the second empty sentence are
  removed.
- `src/MuscleCuties.UI/Controls/Nutrition/MealCardView.xaml(.cs)` — new `HasContent` flag hides the
  zero-calorie stack and macro line on an empty group.
- `src/MuscleCuties.UI/Controls/Profile/ProfileDetailsContent.xaml` — the vault shows one privacy
  status plus `Показать`.
- `src/MuscleCuties.UI/Pages/Workout/WorkoutHistoryPage.xaml` — the three-column `AdaptiveRow` is now
  three stacked label/value rows; `Продолжительность`, `Объем` and `Всего подходов` stay whole.
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` — `D7_surfaces_state_each_fact_once`,
  `D13_water_card_does_not_host_unrelated_actions` and
  `Archive_duration_header_does_not_wrap_mid_word`; each was run red against the old shape, then green.
  The D7 guard also asserts that the cycle card no longer repeats the phase title.
- `docs/qa-evidence/wave-a/` — final-head frames, light and dark:
  `d7-today-{light,dark}.png`, `d7-cycle-{light,dark}.png`, `d7-foodlog-{light,dark}.png`,
  `d7-foodlog-empty-light.png`, `d7-profile-{light,dark}.png`, `d7-archive-{light,dark}.png`,
  `d13-foodlog-actions-{light,dark}.png`. The stale diagnostic `d7-foodlog-empty-light.png` was
  replaced with the current empty-group frame.
- `QA_REPORT.md` — D7, D13 and D26 are closed with the guards and new frames; affected plan rows and
  the Food Log/Archive audit-matrix rows no longer claim the fixed defects.

## 2. Test commands and pasted output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1403, Skipped: 0, Total: 1403

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 45, Skipped: 0, Total: 45
```

Red then green, this pass:

```
D7_surfaces_state_each_fact_once
  RED   Failed  (duplicate prompt/pill/date/empty copy; repeated phase in the cycle card)
  GREEN Passed

D13_water_card_does_not_host_unrelated_actions
  RED   Failed  (water card also contained Copy-yesterday, Breakdown and the diary label)
  GREEN Passed

Archive_duration_header_does_not_wrap_mid_word
  RED   Failed  (three-column AdaptiveRow with no NoWrap on the duration header)
  GREEN Passed
```

Driven on simulator `678048F6-C4C7-406C-A970-7104AED3E00A`, installed from
`src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app` after the final
change. Screenshots were taken with `xcrun simctl io booted screenshot` after each flow settled; Today,
Cycle, Food Log (summary, empty groups, actions), Profile and the archive list were inspected in light
and dark.

## 3. What was not verified, and why

- **D43 is the only formatter-family item still open and is parked unchanged.** It needs a wording/design decision: the
  heading `Исследователь 4-фазы` and chip `NOW`, and which phase-list recipe is canonical (the
  explorer's four saturated bars or the date sheet's four pink cards). Its date line
  `среда, 4 ноября 2026 г.` can move to `DateText` once that is settled. No D43 file was edited.
- D12's full-card empty meal groups remain; this pass removed their duplicated content only. The
  nested-card cleanup (breakdown, check-in, cycle hero) is unchanged, so D12 is still open as
  structural debt.
- Other D-row surfaces were not re-driven in this pass; their cited frames predate this head and were
  not claimed as current. The frames retaken here cover the surfaces changed by D7, D13 and the archive
  layout.

## 4. Next step

The only product decision blocking the formatter family is D43's wording/recipe choice; D12's
structural cleanup can proceed without it. Once answered, switch
the phase list to the chosen recipe, route its date line through `DateText`, close D43 with light/dark
frames, and then continue with the remaining visual-consistency group (D8, D11, D14, D19, D21, D22,
D36, D44).

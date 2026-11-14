# Status — Wave A selection/disabled primitive, branch `fix/qa-audit-followups`

## 1. Changed files

This pass closes D11, D14, D36 and D49 on top of the previous Wave A work.

- `src/MuscleCuties.UI/Controls/Shared/SelectionCell.cs` — new shared `Border` with a bindable
  `IsSelected`; every selection surface projects its own flag through it.
- `src/MuscleCuties.UI/Controls/Shared/ModalSheetHeader.xaml` / `.xaml.cs` — one sheet header
  (title, optional subtitle, back slot, one close) used by the entry sheet, editor and picker.
- `src/MuscleCuties.UI/Resources/Styles/Styles.xaml` — `ChoiceCell` now owns the single filled
  selected state (solid `PrimaryDark`, transparent/zero stroke, `Common_Selected` hint);
  `DayStripCell` is `BasedOn ChoiceCell`; `ChoiceCellLabel`, `DayStripDayName`, `DayStripDayNumber`,
  `DayStripTodayDot`, `ChoiceCellIcon` and `ChoiceCellIconFill` read
  `SelectionCell.IsSelected` from the ancestor so a selected fill always pairs with light ink.
- `src/MuscleCuties.UI/Controls/Nutrition/MealEntryModal.xaml` — shared header; four meal types use
  `SelectionCell`/`ChoiceCell`; labels read the named cell.
- `src/MuscleCuties.UI/Controls/Nutrition/MealEditorModal.xaml` — shared header (back only while the
  finder is open); source tabs and meal types use the same cell; the dead value pills are replaced by
  `Тип приема пищи` + `Завтрак · Сегодня · HH:mm` + one `Изменить`; disabled `Приём пищи` carries
  `LogMealHint` visibly and as `SemanticProperties.Hint`; the time field exposes the formatter clock
  as its hint.
- `src/MuscleCuties.UI/Controls/Cycle/CycleQuickLogBottomSheet.xaml` — period toggles, flow and
  symptom choices moved to `SelectionCell`; per-screen selected colours removed.
- `src/MuscleCuties.UI/Controls/Injury/InjuryLogContent.xaml` — choice cells moved to the shared cell.
- `src/MuscleCuties.UI/Pages/Workout/WorkoutPage.xaml` — week, filter and day-strip cells use
  `SelectionCell`/`DayStripCell`; the day cell prints the day number, not `09 нояб.`, so seven cells
  fit instead of clipping the selected Saturday.
- `src/MuscleCuties.UI/Pages/Workout/WorkoutHistoryPage.xaml` — month chips use `SelectionCell`.
- `src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml` — date strip uses `DayStripCell` and shared
  day-name/number/dot styles.
- `src/MuscleCuties.UI/Models/UI/Workout/ScheduleDayItem.cs` — observable `IsSelected`.
- `src/MuscleCuties.UI/ViewModels/Workout/WorkoutViewModel.cs` — updates exactly one selected day.
- `src/MuscleCuties.UI/ViewModels/Dashboard/DailyCheckInViewModel.cs` — `CanSubmit` and `SubmitHint`.
- `src/MuscleCuties.UI/ViewModels/Nutrition/NutritionViewModel.Meals.cs` /
  `NutritionViewModel.Notifications.cs` — `CanSaveMeal`, `ShowLogMealHint`, `LogMealHint` and change
  notifications.
- `src/MuscleCuties.UI/Services/Localization/{UiTextCatalog,RussianUiText}.{Dashboard,NutritionProfileAuth}.cs`
  — English/Russian prerequisite hints.
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` — `D11_D36_D49_selection_uses_one_filled_recipe`
  and `D14_disabled_commits_publish_a_prerequisite_hint`; both were seen red on the old shape, then
  green. The shared-modal heading guard now accepts `ModalSheetHeader` and asserts that its title
  uses `ModalTitle`.
- `docs/qa-evidence/wave-a/` — final-head frames: `wave-a-entry-light.png`,
  `wave-a-editor-summary-{light,dark}.png`, `wave-a-picker-{light,dark}.png`,
  `wave-a-editor-context-dark.png`, `wave-a-checkin-{light,dark}.png`,
  `wave-a-train-{light,dark}.png`, `wave-a-foodlog-strip-{light,dark}.png`.

## 2. Test commands and pasted output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1406, Skipped: 0, Total: 1406

dotnet test tests/MuscleCuties.Core.Tests --filter "FullyQualifiedName~UiContractGuard|FullyQualifiedName~UiStyleGate"
Passed!  - Failed: 0, Passed: 48, Skipped: 0, Total: 48
```

Red then green, this pass:

```
D11_D36_D49_selection_uses_one_filled_recipe
  RED   old outline/tint recipe and month-length day labels
  GREEN Passed

D14_disabled_commits_publish_a_prerequisite_hint
  RED   no SubmitHint/LogMealHint bindings
  GREEN Passed
```

Driven on simulator `678048F6-C4C7-406C-A970-7104AED3E00A` from
`src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app` after the final
change, against a disposable local API/database:

- Entry light: selected `Завтрак` is a solid accent fill with a light label; other meal types keep one
  outline and no tinted second surface.
- Editor light/dark: `Тип приема пищи` / `Завтрак · Сегодня · 10:06` + `Изменить` renders as text,
  not three pills; disabled `Приём пищи` has `Добавьте хотя бы один продукт, чтобы сохранить приём
  пищи.` above it.
- Picker light/dark: one header with back and close; selected `Поиск` is the same filled cell.
- Check-in light/dark: disabled `Сохранить отметку` has `Ответьте на все обязательные вопросы,
  чтобы сохранить отметку.` fully above the footer.
- Train light/dark: selected week and filter chips are solid with white labels.
- Food Log light/dark: seven day cells fit without clipping; selected Saturday is solid with white
  `СБ`/`14` and the today dot.

## 3. Not verified

- The Train day strip is empty on the QA account because that account has no scheduled sessions, so
  its seven-cell fit was proven on the Food Log strip instead. Train's week/filter rows were driven in
  both themes.
- The Cycle quick log, injury choices and workout-history month chips were migrated to the same cell
  and covered by the guard, but were not re-driven in this pass.
- The meal editor's `Изменить` summary and picker were re-driven in light and dark; a context-expanded
  frame exists in dark only.
- D43 remains parked pending the owner's wording/phase-recipe decision.
- The local API needs PostgreSQL; this run used a disposable local database. No production/shared
  database was used.

## 4. Next step / blocker

Review PR #50 with the new head. The branch has 30 open findings (D43 still needs an owner decision),
21 closed and 3 withdrawn/retracted; no merge and no `main` push were performed.

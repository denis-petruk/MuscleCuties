# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. What this pass changed

The shared formatter family is closed in code and in the running app:

- `src/MuscleCuties.UI/Services/Formatting/NutritionFormat.cs` — `Grams`, `GramsShort`,
  `GramsWholeShort`, `Milligrams`, `Micrograms`, `Micronutrient`, `Calories`, `Macros`; every shape is a
  catalogue key (`Nutrition_Format_*`, `Nutrition_Unit_*`) so the decimal separator comes from the
  current culture and the suffix from the active language.
- `src/MuscleCuties.UI/Services/Formatting/DateText.cs` — `SessionDateFormat`, `SessionDate`,
  `SessionStamp`.
- `src/MuscleCuties.UI/Resources/Converters/SessionDateConverter.cs`,
  `ArchiveDurationConverter.cs`, `ArchiveSetsConverter.cs` — XAML bindings for the same shapes.
- Switched call sites: `NutritionViewModel` (draft summary, adjustment, remaining), `FoodLogViewModel`
  (edit drafts, remaining/over), `NutritionViewModel.Suggestions`, `DashboardViewModel.Trends`,
  `FoodLogViewModel.FastLogging`, `MealItem`, `NutritionPresentationModels`, `FoodLogUiModels`,
  `MealSuggestionItem`, `MacroBreakdownItem`, `MealIngredientItem`, `HistoryIngredientEditor`,
  `DailyMicronutrientItem`, `MacroProgressGrid` labels, `MacroProgressFormatter`,
  `NutritionBreakdownPresenter`, `WorkoutHistoryViewModel`, `WorkoutHistoryPage.xaml`.
- Guards in `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs`:
  `Quantity_strings_come_from_one_formatter`, `Archive_dates_come_from_one_formatter`,
  `Archive_metrics_use_localized_duration_and_plural`. Each was seen failing on the old literal or old
  binding before passing; no existing guard was weakened.

## 2. Test commands and final output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1397, Skipped: 0, Total: 1397

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 39, Skipped: 0, Total: 39
```

Red/green evidence, formatter guard:

```
=== RED   === Failed  Quantity_strings_come_from_one_formatter (P {Macros.Protein:N0}g literal)
=== GREEN === Passed  Quantity_strings_come_from_one_formatter
```

Red/green evidence, archive metrics guard:

```
=== RED   === Failed  Archive_metrics_use_localized_duration_and_plural (DurationLabel binding)
=== GREEN === Passed  Archive_metrics_use_localized_duration_and_plural (TotalDurationSeconds binding)
```

## 3. Frames from this build (`docs/qa-evidence/wave-a/`)

Light and dark for the surfaces the closed defects cite:

- Nutrition entry: `nutrition-entry-light.png`, `nutrition-entry-dark.png` — `0 г / 99 г`,
  `Белки / Углеводы / Жир`, `/ 2080 ккал`.
- Meal detail (suggestions): `meal-suggestions-light.png`, `meal-suggestions-dark.png` —
  `582 ккал`, `Б 32,8 г · У 45,2 г · Ж 30,0 г`.
- Meal entry sheet / 24-hour time: `meal-entry-light.png` — `01:01`.
- Archive list: `archive-list-light.png`, `archive-list-dark.png` — `30 мин`, `1 подход`,
  `Чт, 12 нояб. 2026`.
- Archive detail: `archive-detail-light.png`, `archive-detail-dark.png` — header
  `Чт, 12 нояб. 2026 · 09:00`, footer above the keyboard toolbar.
- Cycle quick log: `quicklog-light.png`, `quicklog-dark.png` — `суббота, 14 ноября`.

Driven with Maestro on simulator `678048F6-C4C7-406C-A970-7104AED3E00A`, installed from
`src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app`; frames taken with
`xcrun simctl io booted screenshot` after the flow settled.

## 4. Closed in `QA_REPORT.md`

D3, D10, D26, D28, D33 (both date halves and the keyboard footer) and D38 are marked closed against the
frames above and their guards. D43 remains open: it is the cycle phase-list visual recipe and the
machine-translated heading, which the formatter family does not touch; its section says so explicitly.

## 5. Not verified, and why

- D43 was not re-driven this pass; nothing in these commits changes it.
- Food-log rings and the breakdown donut still differ from Nutrition bars (D13/D7); only their quantity
  strings were switched.
- D32's hero entry remains one accessibility element; the quick-log action is driven by a point tap.
- Frames cited for D50, D37, D54, D51 and D52 come from earlier commits in this branch and were not
  re-shot this pass; the pages they show were not touched by the formatter commits.

## 6. Next step

The remaining Wave A group is D43's phase-list recipe plus the English `NOW` chip and heading; after that
the audit's visual-consistency group (D8, D11, D14, D19, D21, D22, D36, D44) is the next coherent unit.

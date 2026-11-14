# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. Changed files

D12's empty meal-group shell is closed on top of `b3d2d652`; D43 is untouched.

- `src/MuscleCuties.UI/Controls/Nutrition/MealCardView.xaml` — the `CardPrimary` surface is now shown
  only when `HasContent` is true. An empty group renders as `EmptyMealRow`: a plain `Grid` with the meal
  icon, meal type and one `Пока ничего не добавлено` statement. It has no inner `Border`, calorie or
  macro block, template action or `BelowContent`; the divider/action anatomy belongs only to a
  populated card.
- `src/MuscleCuties.UI/Controls/Nutrition/MealCardView.xaml.cs` — added `IsEmpty` and refresh it when
  `HasContent` changes.
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` —
  `D12_empty_meal_group_is_one_row_without_a_card_surface`, seen red on the old unconditional card
  surface and then green. Existing `D7_surfaces_state_each_fact_once`,
  `D13_water_card_does_not_host_unrelated_actions` and
  `Archive_duration_header_does_not_wrap_mid_word` remain green.
- `docs/qa-evidence/wave-a/` — final-head frames: `d12-foodlog-empty-{light,dark}.png` (all four empty
  sections, no card shells), `d12-nutrition-meal-{light,dark}.png` (populated shared card still
  renders), and refreshed `d7-foodlog-empty-light.png` / `d13-foodlog-actions-{light,dark}.png` so
  those earlier rows do not cite the old empty-card shape.
- `QA_REPORT.md` — row 19 and D12 now record the closed empty-group shape, the guard and the new
  frames. The meal-editor/breakdown rows no longer claim D12 for their separate nested-card debt.

## 2. Test commands and pasted output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1404, Skipped: 0, Total: 1404

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 46, Skipped: 0, Total: 46
```

Red then green, this pass:

```
D12_empty_meal_group_is_one_row_without_a_card_surface
  RED   Assert.Contains() Failure: card surface had no HasContent visibility binding
  GREEN Passed
```

Driven on simulator `678048F6-C4C7-406C-A970-7104AED3E00A`, installed from
`src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app` after the final
change. Food Log empty rows were inspected in light and dark; the populated `MealCardView` was
inspected on Nutrition in light and dark after logging a synthetic `Chicken feet` entry.

## 3. What was not verified, and why

- **D43 is still open and unchanged.** It needs the owner's wording/phase-recipe decision recorded in
  `QA_REPORT.md`; no D43 file was edited.
- Food Log displayed `Повторить загрузку этого дня` throughout the visual pass, so its meal data did
  not load. The all-empty row shape is verified there in both themes; a mixed populated/empty Food Log
  could not be driven. The populated shared-card branch was verified on Nutrition instead.
- The screenshots expose the still-open D4 `Snacks` heading in the Russian Food Log; it is not a D12
  regression and was not changed in this pass.
- `QA_REPORT.md` still records 34 open findings from earlier audit passes, including D43. D12 is no
  longer one of them. The report also records 17 closed and 3 withdrawn/retracted findings (D17, D31,
  D53); rows such as D1, D2, D4, D9, D11, D14, D15, D18–D25, D27, D29, D30, D34–D36, D39–D47, D49 and
  D52 are not claimed fixed by this pass.

## 4. Next step

D12's empty-group shell is complete. The only owner decision blocking the formatter family remains
D43's wording/recipe choice; after that, continue the remaining open QA rows above. D12's separate
sheet/hero nested-card examples are now recorded as their own consistency debt rather than folded into
this closure.

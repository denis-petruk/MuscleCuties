# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. Changed files

Nine commits stand unchanged on the branch, plus this pass on top of `41bb3f6f`:

- `src/MuscleCuties.UI/Pages/Nutrition/NutritionPage.xaml` — the `Дневник питания` entry is a real
  `Button` (`AutomationId="OpenFoodLog"`, `ButtonSecondary`, `HorizontalOptions="End"`) in the `Еда`
  section header beside `IconSectionHeader`, wired to `Clicked="OnOpenDailyFoodLogClicked"`. The
  previous `Label` + `TapGestureRecognizer` is gone.
- `src/MuscleCuties.UI/Pages/Nutrition/NutritionPage.xaml.cs` — `OnOpenDailyFoodLogClicked` routes
  through `Shell.Current.GoToAsync(NutritionNavigation.FoodLogRoute)` and calls
  `ReportDiaryNavigationFailure()` on any exception, so a dead route states itself.
- `src/MuscleCuties.UI/ViewModels/Nutrition/NutritionViewModel.cs` — `NavigateToFoodLogAsync` routes
  through `NutritionNavigation.FoodLogRoute`, logs and reports failures; new
  `ReportDiaryNavigationFailure()`.
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` — `The_daily_log_entry_is_a_real_button_that_routes`:
  the entry is a `Button` in the `Еда` header (sibling `IconSectionHeader`), carries the click handler,
  no `NavigateToFoodLog` gesture remains, and the handler routes and reports.
- `QA_REPORT.md` — D50 closed with the new screenshots; case 70 (toast placement) moved to PASS.
- `STATUS.md` — this file.

Evidence copied into `docs/qa-evidence/wave-a/`:

| File | What it proves |
| --- | --- |
| `nutrition-diary-entry-light.png` | `Дневник питания` as a button in the `Еда` row, beside `Добавить приём пищи` / `План на день` |
| `diary-cold-single-tap-light.png` | diary open after a cold launch and a single tap on that entry |
| `toast-foodlog-notes-light.png`, `toast-foodlog-notes-dark.png` | `Заметки сохранены.` raised by `Сохранить заметки` on the food log, above the tab bar, both themes |
| `toast-workouthistory-notes-light.png`, `toast-workouthistory-notes-dark.png` | `Заметки и РПП обновлены.` raised by `Сохранить заметки и РПП` on a history session, above the pinned sheet footer, both themes |
| `checkin-sheet-footer-dark.png` | check-in sheet footer at rest in dark (`Пока пропустить` / `Сохранить отметку`) next to the existing light frame |

## 2. Test commands and output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1392, Skipped: 0, Total: 1392

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 34, Skipped: 0, Total: 34
```

Guard red, then green (the entry reverted to `41bb3f6f`'s `Label`, then restored):

```
=== RED (entry reverted to HEAD) ===
   at System.Linq.Enumerable.Single[TSource](IEnumerable`1 source, Func`2 predicate)
Failed!  - Failed: 1, Passed: 0, Skipped: 0, Total: 1
=== GREEN (entry restored) ===
Passed!  - Failed: 0, Passed: 1, Skipped: 0, Total: 1
```

Simulator, cold launch, one tab tap, one tap on `id: OpenFoodLog`, assertion on `Вода`:

```
maestro test flows/T39.yaml   run 1 exit=0 0 failures
maestro test flows/T39.yaml   run 2 exit=0 0 failures
maestro test flows/T39.yaml   run 3 exit=0 0 failures
```

Instrumented run (temporary data-directory probe, removed before the commit) recorded the layer on a
cold launch: `enter source=Push target=FoodLogPage`, `pass-through verified=True`,
`FoodLogPage ctor`, `navigated Push location=//MainTab/NutritionPage/FoodLogPage page=FoodLogPage`,
`after await current=//MainTab/NutritionPage/FoodLogPage`. The shell pushed, the page was constructed
and shown; earlier screenshots had caught the push mid-animation.

## 3. Not verified, and why

- The diary was only photographed in light; the dark frame is the pre-existing `foodlog-dark.png`, not a
  fresh cold-launch capture.
- Toast screenshots were taken against the branch build immediately before the entry was restyled from
  its temporary link treatment to `ButtonSecondary`; that restyle touches only the Nutrition header, and
  the food-log toast was re-captured after it (`toast-foodlog-notes-light.png`). The dark food-log and
  both workout-history frames pre-date that two-file change.
- The D32 quick-log save-bar case was not re-driven in this pass; the toast capture covers the
  `WorkoutHistoryPage` host, not the cycle quick-log sheet.
- Maestro's `takeScreenshot` returns while the Shell push is still animating, so navigation frames are
  captured with `simctl io screenshot` after the flow instead.

## 4. Next step

Close the cycle wave (D27–D40): quick-log save bar, legend truthfulness, phase-explorer copy. Then
re-shoot the diary in dark on the current build.

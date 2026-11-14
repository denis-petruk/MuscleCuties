# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. Changed files

Nine commits on `fix/qa-audit-followups`; the D54 fix stays its own commit.

- `19887dca` — restored `docs/release-audit/2026-11-05/WORKOUT_POLICY.md` (D16).
- `cd65f85d` — one 24-hour `MealTime` formatter (D48's 12-hour half).
- `3a2daf1e` — the pre-existing shared-primitives base.
- `f28aa3b1` — **D54**, meal card header gap.
- `3b55255c` — modal frame: `ModalKeyboardInset`, one scroll body, keyboard padding.
- `7023dc81` — reveal content under the focused field, 12 pt sheet footer reserve, toast clearance
  token, picker search mode.
- this pass (`Wave A close-out`), uncommitted until the commit below:
  - `Controls/Shared/AnimatedModalView.cs` — `FindScrollBody` reuses a sheet's own nested `ScrollView`
    instead of wrapping it (the check-in sheet's pinned footer was being pushed off-screen by a
    second body).
  - `Resources/Styles/Styles.xaml` — `PresentationPageContentPadding` bottom 32 → 96 so page content
    clears the floating tab bar (D37 at rest).
  - `Controls/Nutrition/MealEditorModal.xaml` — named rows for search mode; the search pane gets an
    opaque background so the ingredient list cannot show through real result rows.
  - `Controls/Nutrition/MealEditorModal.xaml.cs` — search mode on focus/blur; submit no longer
    unfocuses the field.
  - `ViewModels/Nutrition/NutritionViewModel.Search.cs` / `.Notifications.cs` — 400 ms debounced
    search on typing, so hits appear while the keys are up (D51).
  - `Services/Nutrition/LoopbackFoodCatalogSearchStore.cs` (new), `UseCases/.../DevelopmentFoodCatalog.cs`
    (new), `Composition/ApplicationCompositionExtensions.cs` — a Debug loopback build falls back to a
    synthetic catalogue when the local API answers empty; production and Beta keep the cloud store.
  - `tests/MuscleCuties.Core.Tests/DevelopmentFoodCatalogTests.cs` (4 tests),
    `UiContractGuardTests.cs` (+4 guards).

## 2. Test commands and output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1391, Skipped: 0, Total: 1391

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 33, Skipped: 0, Total: 33
```

Screenshots in `docs/qa-evidence/wave-a/`:

| File | What it proves |
| --- | --- |
| `picker-keyboard-hit-light.png`, `picker-keyboard-hit-dark.png` | picker with `chicken` typed, keyboard up, `Поиск` and real rows (`Chicken feet`, `Chicken skin`) on screen |
| `picker-first-result-selected-light.png`, `picker-first-result-selected-dark.png` | tapping the first row lands on the portion sheet for `Chicken feet`, keyboard still up |
| `checkin-sheet-footer-light.png`, `checkin-sheet-footer-dark.png` | check-in sheet with `Пока пропустить` / `Сохранить отметку` inside the reserved footer, above the home indicator |
| `checkin-button-clear-of-tabbar-light.png` | `Заполнить отметку` fully above the floating tab bar after the padding fix |
| `entry-sheet-light.png`, `entry-sheet-dark.png` | entry sheet with its actions inside the sheet; time reads `23:26` |
| `editor-sheet-footer-light.png`, `editor-sheet-footer-dark.png` | editor with `Отмена` / `Приём пищи` pinned inside the sheet |
| `breakdown-sheet-light.png`, `breakdown-sheet-dark.png` | breakdown sheet, no tab-bar or footer overlap |
| `P6-foodlog-cards-light.png`, `Q9-foodlog-cards-dark.png` | D54 meal card header in both themes |
| `Q12`–`Q14` | the earlier no-match picker states, kept as before/after for D51 |

## 3. Not verified, and why

- **Toast (D50, D32) is still open.** Both hosts only show a toast on a failed request; the local API is
  healthy, and `FoodLogPage`'s daily-log link does not respond to taps in this build, so the notes-save
  toast could not be triggered. No toast screenshot exists, so D50 is not claimed closed.
- **`FoodLogPage` entry is unreachable from the Nutrition page** — the `Дневник питания` label does not
  navigate, by text tap or by point tap. This is a separate finding to triage, not fixed here.
- The picker search for a real hit required a local catalogue: the loopback API answers `401` without a
  local session and the local Postgres databases have no schema (`\dt` → no relations), so the Debug
  fallback catalogue is what the picker shows. That fallback does not exist in Beta or Production.
- Dark screenshots of the check-in sheet, entry sheet, editor and breakdown exist; the check-in
  **button at rest** is only photographed in light.

## 4. Next step

Trigger a real toast: reach `FoodLogPage` (fix the dead daily-log link first) and save notes, then capture
the banner in both themes. After that, close Wave A and move to the cycle wave.

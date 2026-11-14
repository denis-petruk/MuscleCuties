# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. Changed files

This pass proved the formatter family on the branch build and fixed the two things the frames exposed:

- `src/MuscleCuties.UI/Services/Localization/RussianUiText.NutritionProfileAuth.cs`,
  `UiTextCatalog.NutritionProfileAuth.cs` — `Nutrition_Format_GramsShort` now keeps the space before the
  unit (`{0:N1} г`), and `Nutrition_Breakdown_RemainingFormat` (`Осталось: {0}` / `Remaining: {0}`)
  replaces the English label wrapped around Russian numbers.
- `src/MuscleCuties.UI/ViewModels/Nutrition/NutritionViewModel.Suggestions.cs` — remaining summary goes
  through the new key.
- `src/MuscleCuties.UI/Controls/Shared/AnimatedModalView.cs` — the keyboard inset now pads the whole
  sheet surface, not only its scroll body, so a pinned footer (archive detail's
  `Сохранить заметки и РПП`) is fully above the keyboard toolbar. Shrinking only the body had left the
  save bar's label peeking above the toolbar while the button itself stayed behind it.
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` — three guards added this pass:
  `Grams_short_form_keeps_space_before_unit`, `Suggestion_remaining_label_is_localized`,
  `Modal_frame_insets_the_surface_so_a_pinned_footer_clears_the_keyboard`. Each was run red against the
  old shape before the fix, then green.
- `docs/qa-evidence/wave-a/` — every cited frame retaken from the installed build at this branch head,
  light and dark: `nutrition-entry-*`, `meal-suggestions-*`, `meal-detail-*`, `meal-entry-*`,
  `archive-list-*`, `archive-detail-*`, `archive-detail-save-light`, `workout-archive-*`,
  `quicklog-*`, and D43's `cycle-explorer-light`, `cycle-phase-sheet-{light,dark}`.
- `QA_REPORT.md` — D3, D10, D26, D28, D33 and D38 now cite those frames; D43 is the single open item
  with the exact strings and the decision it needs.

## 2. Test commands and pasted output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1400, Skipped: 0, Total: 1400

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 42, Skipped: 0, Total: 42
```

Red then green, this pass:

```
Grams_short_form_keeps_space_before_unit
  RED   Failed  (Nutrition_Format_GramsShort = "{0:N1}г")
  GREEN Passed

Suggestion_remaining_label_is_localized
  RED   Failed  ("Remaining: {NutritionFormat.Macros(...)}")
  GREEN Passed

Modal_frame_insets_the_surface_so_a_pinned_footer_clears_the_keyboard
  RED   Failed  (surface.Padding not set; only body.Padding)
  GREEN Passed
```

Driven on simulator `678048F6-C4C7-406C-A970-7104AED3E00A`, installed from
`src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app`, screenshots taken
with `xcrun simctl io booted screenshot` after each flow settled.

## 3. What was not verified, and why

- **D43 is still open.** It is the only formatter-family item not driven to a closed state, because
  closing it needs a wording and design decision: the heading `Исследователь 4-фазы` and chip `NOW`;
  and which phase-list recipe is canonical — the explorer's four saturated bars or the date sheet's four
  pink cards. Their date line `среда, 4 ноября 2026 г.` can switch to `DateText` once that is settled.
  Frames: `cycle-explorer-light.png`, `cycle-phase-sheet-light.png`, `cycle-phase-sheet-dark.png`.
- Food Log rings and the breakdown donut still differ from Nutrition's bars (D13/D7); only their
  quantity strings were re-verified.
- The archive's `Продолжительно/сть` label still breaks mid-word at default width; that is the
  AdaptiveRow column budget, not a format string.
- Dark mode was checked for the surfaces named in the request; other dark pages were not re-driven.

## 4. Next step

The only blocker is D43's wording/recipe decision above. Once answered, switch the phase list to the
chosen recipe, route its date line through `DateText`, close D43 with a light/dark frame, and then move
to the visual-consistency group (D8, D11, D14, D19, D21, D22, D36, D44).

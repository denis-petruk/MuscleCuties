# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. Changed files

Thirteen commits stand on the branch; this pass adds the quick-log save prerequisite on top of `1caa0278`.

- `src/MuscleCuties.UI/ViewModels/Cycle/CycleViewModel.QuickLog.cs` — `HasQuickLogInput`
  (period mark, flow or symptom), `SaveQuickLogHint`, and `SaveQuickLogCommand` now requires it.
- `src/MuscleCuties.UI/Controls/Cycle/CycleQuickLogBottomSheet.xaml` —
  `SemanticProperties.Hint="{Binding SaveQuickLogHint}"` on `Сохранить запись`.
- `src/MuscleCuties.UI/Services/Localization/RussianUiText.CycleWorkout.cs` /
  `UiTextCatalog.CycleWorkout.cs` — `Cycle_QuickLog_SaveRequiresInputHint`.
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` —
  `The_quick_log_save_states_its_prerequisite_when_disabled`.
- `QA_REPORT.md` — D32 now covers the disabled state; case 45 moved to PASS.
- `docs/qa-evidence/wave-a/quicklog-save-disabled-light.png`, `quicklog-save-disabled-dark.png`.

## 2. Test commands and output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1394, Skipped: 0, Total: 1394

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 36, Skipped: 0, Total: 36
```

Guard `The_quick_log_save_states_its_prerequisite_when_disabled`, red then green:

```
=== GREEN ===  Passed!  - Failed: 0, Passed: 1
=== RED   ===  Assert.Contains() Failure: Sub-string not found
               Failed!  - Failed: 1, Passed: 0
=== GREEN AGAIN === Passed!  - Failed: 0, Passed: 1
```

Simulator, this build: `T55` light and dark, one failure count 0 each, showing `Сохранить запись`
disabled with no selection.

## 3. Not verified, and why

- **The formatter family is not done.** D3, D10, D26, D28, D38 and D43 still render the old unit and
  date shapes, and D33's date half is open. This pass did not implement the shared formatter; no row for
  those defects claims otherwise.
- D32's hero entry is still one accessibility element, so the quick-log action is not individually
  reachable by assistive technology; the sheet itself is driven by a point tap.
- Frames cited for D50, D37, D54, D51 and D52 come from earlier commits in this branch and were not
  re-shot this pass; the pages they show were not touched by either of the last two commits.

## 4. Next step

Build `NutritionFormat`/`DateText` and switch every nutrition and archive surface to it, closing D3,
D10, D26, D28, D38, D43 and the D33 date half. That is the only remaining group in Wave A.

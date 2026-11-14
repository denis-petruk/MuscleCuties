# Status — QA audit fixes, branch `fix/qa-audit-followups`

## 1. Changed files

Ten commits stand on the branch; this pass sits on top of `574bf60b`.

- `src/MuscleCuties.UI/Models/UI/Workout/WorkoutArchiveUiModels.cs` — the archive exercise summary is
  formatted from one localized key instead of the hard-coded `1 working sets · 200 kg vol` (D33).
- `src/MuscleCuties.UI/Services/Localization/RussianUiText.CycleWorkout.cs` /
  `UiTextCatalog.CycleWorkout.cs` — `Workout_History_VolumeSummaryFormat`
  (`Подходы: {0} · Объём: {1} кг` / `Sets: {0} · Volume: {1} kg`).
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` — `The_archive_volume_summary_uses_localized_copy`.
- `QA_REPORT.md` — D32 closed with the quick-log frames; D33 narrowed to the date/keyboard halves;
  case 45 moved to PARTIAL and case 47 to PARTIAL with the new evidence.
- `STATUS.md` — this file.
- `docs/qa-evidence/wave-a/` — refreshed `diary-dark.png`, `toast-workouthistory-notes-light.png`,
  `toast-workouthistory-notes-dark.png`, `checkin-sheet-footer-dark.png`, and new
  `quicklog-top-light.png`, `quicklog-savebar-light.png`, `quicklog-top-dark.png`,
  `quicklog-savebar-dark.png`, `archive-volume-summary-light.png`.

## 2. Test commands and output

```
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)
    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1393, Skipped: 0, Total: 1393

dotnet test tests/MuscleCuties.Core.Tests --filter "UiContractGuard|UiStyleGate"
Passed!  - Failed: 0, Passed: 35, Skipped: 0, Total: 35
```

New guard red, then green:

```
=== RED ===
Assert.DoesNotContain() Failure: Sub-string found
Failed!  - Failed: 1, Passed: 0, Skipped: 0, Total: 1

=== GREEN ===
Passed!  - Failed: 1 -> 0, Passed: 1, Skipped: 0, Total: 1
```

Simulator, current binary:

```
maestro flows/T51.yaml   light 0 failures   quick-log scrolled to the last symptom row
maestro flows/T51.yaml   dark  0 failures
maestro flows/T52.yaml   dark  0 failures   diary after one cold-launch tap
maestro flows/T45.yaml   dark  0 failures   workout-history notes toast
maestro flows/T45.yaml   light 0 failures   workout-history notes toast
maestro flows/T42.yaml   dark  0 failures   check-in sheet footer at rest
maestro flows/T54.yaml   light 0 failures   archive detail with `Подходы: 1 · Объём: 200 кг`
```

## 3. Not verified, and why

- D33 is only half closed: the sheet header still prints `четверг, 12 ноября 2026 г. 09:00` against the
  list's `Чт, нояб. 12, 2026`, and `Сохранить заметки и РПП` still sits over the keyboard toolbar. Neither
  is claimed.
- The quick-log save button still has no disabled state; D32's layout half is fixed, its validation half
  is not.
- The remaining live findings in `QA_REPORT.md` (D1–D49 apart from those Wave A closed) are untouched;
  the next group is the shared formatter/selection pass, not started here.
- All screenshots come from the current build except the two FoodLog toast frames already captured on
  this branch (`toast-foodlog-notes-*`), whose page was not changed by this pass.

## 4. Next step

Add the shared quantity/date formatter and close D3, D10, D26, D28, D38, D43, then the narrowed D33 date
half. After that, the Quick Log validation state (D32 remainder).

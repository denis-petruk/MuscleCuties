# Status — Workout Engine 2.0, soreness copy closure and catalog build check

Branch: `fix/qa-audit-followups`; task started at HEAD `456f7b89`.

## Changed files

- `src/MuscleCuties.UI/ViewModels/Cycle/CyclePhaseDetailCatalog.cs` — the late-luteal training tip named
  soreness as a reason to pull back; it now names PMS symptoms and sleep and asks for optional work to
  be dropped, which is what A-4 actually permits.
- `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.RuntimeText.cs` — runtime map key and Russian
  value for that tip follow the new English sentence; the soreness wording is gone in both languages.
- `tests/MuscleCuties.Mobile.Contracts.Tests/BetaPlanningCopyContractTests.cs` — two guards: phase
  training copy must not mention soreness, and the runtime map must translate the current tip without
  the Russian soreness wording.
- `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` — evidence split for MC-WE2-REQ-006/023 into
  proven-by-test, compile-verified only, visually unexercised, and deliberately unchanged.
- `STATUS.md` — this checkpoint. The pre-existing `WorkoutActivityPreferencePicker.xaml` edit and the
  pre-existing staged deletions under `docs/qa-evidence/` and `docs/performance/` stay untouched and
  out of this commit.

No engine code changed in this pass.

## Verification

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:  1460, Skipped:     0, Total:  1460, Duration: 1 s

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:    60, Skipped:     0, Total:    60, Duration: 583 ms

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:   210, Skipped:     0, Total:   210, Duration: 245 ms

$ dotnet build src/MuscleCuties.Infrastructure.PostgreSql/MuscleCuties.Infrastructure.PostgreSql.csproj -c Release
Build succeeded.
    0 Warning(s)
    0 Error(s)

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Copy search evidence: `soreness`/`болезнен` now appears in the client only as the check-in answer label,
its accessibility labels, the readiness rating row, and the corrected score explanation that states
soreness is not scored. No string claims soreness affects a score, dose, load, set reduction or recovery
evidence.

## Not verified

- No simulator run, screenshot or device check, so the reworded tip, the changed recovery score and the
  Russian layout were not observed.
- No PostgreSQL-backed suite ran. The recovery-evidence projection change from `456f7b89` is
  compile-verified only, though the project now builds with zero warnings and errors.
- The Postgres project build verifies compilation, not that query behaviour against a live schema.
- Tests placeholders: no skipped or removed tests in this pass.

## Next step

P1 catalog validation: resolve the reviewed-catalog versus seed-manifest count question with evidence,
then pin the catalog integrity rules as tests before touching planner behaviour.

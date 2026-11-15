# Status — Workout Engine 2.0, P0 soreness exclusion closed

Branch: `fix/qa-audit-followups`; task started at HEAD `54dbd353`.

## Changed files

- `src/MuscleCuties.Core/Domain/Workout/Planning/ComposedReadinessCycleDoseRule.cs` — **deleted**. No
  production caller; its load step-down is withdrawn by A-4 ("planned prescriptions carry no load datum
  to step down from") and its extra-set/+2-rep cycle arithmetic by A-5. It was a second, contradictory
  dose authority, so Gate 94 removes it.
- `tests/MuscleCuties.Core.Tests/ComposedReadinessCycleDoseRuleTests.cs` — **deleted with the rule it
  tests**; every assertion in it encoded the withdrawn behaviour.
- `src/MuscleCuties.Core/Domain/Workout/Planning/CanonicalRecoveryEvidenceRule.cs` — `ObservedRecovery`
  no longer has a soreness field and weekly recovery evidence no longer averages one.
- `src/MuscleCuties.Core/Domain/Workout/Planning/DeloadTriggerRule.cs` — `WeeklyPerformanceAndRecoveryRecord`
  no longer has `AverageSoreness`; the predicate comment names A-8.
- `src/MuscleCuties.Core/Domain/Workout/ReadinessRule.cs` — the soreness adjustment and its breakdown
  component are gone; arithmetic version named `MC-READINESS-ARITHMETIC-0.2`.
- `src/MuscleCuties.Core/Domain/Workout/ReadinessEngine.cs` — safe-default breakdown matches the new keys.
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresCanonicalRecoveryEvidence.cs` —
  recovery evidence is projected without the soreness column.
- `src/MuscleCuties.UI/ViewModels/Dashboard/DashboardViewModel.cs` — the displayed recovery composite
  no longer blends soreness; sleep quality is its only check-in rating.
- `tests/MuscleCuties.Core.Tests/ReadinessSorenessInvarianceTests.cs` — new: score, tier and breakdown
  are identical across every soreness answer, the breakdown has no soreness component, and the health
  readiness evidence type carries no soreness member.
- `tests/MuscleCuties.Core.Tests/DeloadSorenessInvarianceTests.cs`,
  `CanonicalRecoveryEvidenceRuleTests.cs`, `DeloadTriggerRuleTests.cs` — rewritten for the removed field;
  the invariance assertions now sit on the record shape plus the deload outcome.
- `docs/release-audit/2026-11-05/WORKOUT_POLICY.md` — amendment A-8 scope, including the removed rule.
- `docs/PRODUCT_WORKFLOWS.md` — soreness exclusion covers readiness, recovery evidence and deload.
- `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` — proven/unverified split for MC-WE2-REQ-006/023.
- `STATUS.md` — this checkpoint. The pre-existing `WorkoutActivityPreferencePicker.xaml` edit and the
  pre-existing staged deletions under `docs/qa-evidence/` and `docs/performance/` are untouched and
  excluded from this commit.

## Verification

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:  1460, Skipped:     0, Total:  1460, Duration: 1 s

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:    60, Skipped:     0, Total:    60, Duration: 344 ms

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:   208, Skipped:     0, Total:   208, Duration: 173 ms

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

The Core total moved from 1464 to 1460: eight composed-rule tests were removed with the withdrawn rule,
and four invariance/guard tests were added. Search confirms no soreness operand remains in
`src/MuscleCuties.Core`: the rating survives only as a stored, validated answer on `DailyInputs` and
`TodayCheckInObservation`, where no arithmetic reads it.

## Not verified

- No simulator run, screenshot or device check. The dashboard recovery score is now computed without
  soreness and the arithmetic is proven, but the rendered screen was not observed; users with existing
  check-ins will see a changed recovery score.
- No PostgreSQL-backed suite ran, so the projection change in
  `PostgresCanonicalRecoveryEvidence` is compile-verified only, not executed against a database.
- No AWS, GitHub Actions, environment reset or TestFlight action.
- P1–P9 unstarted; no catalog or planner work was touched in this pass.

## Next step

Continue with P1 catalog validation under the plan's provenance register. The remaining readiness
question this pass did not touch: whether `DailyInputs.Soreness` should keep flowing into readiness
inputs at all now that nothing reads it; the invariance tests currently pin that it cannot affect a
result.

# Status — Workout Engine 2.0, P0 audit and soreness dosing fix

Branch: `fix/qa-audit-followups`; HEAD when the task started: `8b99f13d`.

## Changed files

- `src/MuscleCuties.Core/Domain/Workout/Planning/TodayCheckInScoreRule.cs` — soreness removed from the
  score; sleep quality, energy and reported stress rescaled onto the same 0–100 range under one
  derived denominator; ruleset `MC-CHECKIN-SCORE-0.4`.
- `src/MuscleCuties.Core/Domain/Workout/Planning/CycleReadinessPrescriptionRule.cs` — policy
  `MC-CYCLE-READINESS-0.4`; the readiness signal it consumes is the soreness-free score.
- `src/MuscleCuties.Core/Domain/Workout/Planning/PlanningReleaseApproval.cs` — approval cites both new
  rule versions, so an older approval cannot authorise the new arithmetic.
- `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.Dashboard.cs` and
  `RussianUiText.Dashboard.cs` — score-explanation copy now matches the scored inputs (EN/RU parity).
- `tests/MuscleCuties.Core.Tests/SorenessReadinessInvarianceTests.cs` — new regression test; failed
  before the fix for the intended reason.
- `tests/MuscleCuties.Core.Tests/TodayCheckInScoreRuleTests.cs`,
  `TodayCheckInScoreEnergyTests.cs` — arithmetic fixtures and bounds follow the approved behaviour;
  each rewritten assertion says why.
- `tests/MuscleCuties.Registration.Tests/CloudNutritionAndWorkoutContractsTests.cs` — check-in score
  fixture recomputed (87.5 → 83.33).
- `docs/release-audit/2026-11-05/WORKOUT_POLICY.md` — ratified amendment A-8 (2026-11-15).
- `docs/PRODUCT_WORKFLOWS.md` — soreness-is-not-a-dose-input rule.
- `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` — verified P0 state and the remaining soreness paths.
- `STATUS.md` — this checkpoint, which supersedes the earlier uncommitted document-delivery checkpoint.
  `src/MuscleCuties.UI/Controls/Workout/WorkoutActivityPreferencePicker.xaml` (pre-existing comment
  removal) is preserved untouched and is not part of this change.

## Verification

Baseline before the fix:

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
  Failed MuscleCuties.Core.Tests.SorenessReadinessInvarianceTests.Ordinary_soreness_alone_does_not_change_the_actual_readiness_prescription
Expected: CycleReadinessPrescriptionDecision { AdjustedSets = , ReasonCode = CycleReadinessNormalDose, ... }
Actual:   CycleReadinessPrescriptionDecision { AdjustedSets = 1, ReasonCode = DailyReadinessOptionalSetReduced, ... }
Failed!  - Failed:     1, Passed:  1462, Skipped:     0, Total:  1463, Duration: 1 s
```

After the fix:

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:  1464, Skipped:     0, Total:  1464, Duration: 1 s

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:    60, Skipped:     0, Total:    60, Duration: 540 ms

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:   208, Skipped:     0, Total:   208, Duration: 196 ms
```

The historical 1,417-pass/one-failure baseline named in the plan does not reproduce at this HEAD: the
modal lifecycle guard asserts the shipped ordered modal stack and passes.

## Not verified

- No iOS simulator build, run, screenshot or device check. The localization change is source-only and
  was not rendered.
- No PostgreSQL-backed suite, migration rehearsal, AWS or GitHub Actions run; no environment reset; no
  TestFlight or App Store Connect action.
- Only the check-in score → readiness prescription route is proven soreness-free. `ReadinessRule`
  still scores soreness for display/evidence, `ComposedReadinessCycleDoseRule` still accepts it and
  reduces load (unwired in `src/`), and `CanonicalRecoveryEvidenceRule` still averages it into a record
  the deload rule now ignores. MC-WE2-REQ-006 and MC-WE2-REQ-023 remain open.
- P1–P9 are unstarted. No historical numbering beyond the recovered material was invented.

## Next step

Decide the two remaining readiness decisions before touching more code: whether the display/evidence
readiness score keeps soreness, and whether the unwired `ComposedReadinessCycleDoseRule` is deleted or
reconciled with ratified A-4. Then continue with P1 catalog validation.

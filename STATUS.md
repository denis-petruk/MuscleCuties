# Status — Workout Engine 2.0: soreness copy closure and P1 catalog validation

Branch: `fix/qa-audit-followups`; HEAD before this commit: `484bbb19`.

## Changed files

- `tests/MuscleCuties.Core.Tests/ReviewedExerciseCatalogIntegrityTests.cs` — new P1 guards over the
  shipped review snapshot: 59 unique approved entries, complete generation metadata, substitution
  targets reviewed with decision/category agreement (a rejected automatic substitution can never be an
  eligible replacement), every template-slot candidate reviewed (including optional slots), and the
  named reconciliation of 60 planning definitions against 59 reviews.
- `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` — P1 section with the resolved 59/60 lead, what the
  guards prove, the surfaced-but-unfilled coverage gaps, and P1's partial exit status.
- `STATUS.md` — this checkpoint. The pre-existing `WorkoutActivityPreferencePicker.xaml` edit and the
  pre-existing staged deletions under `docs/qa-evidence/` and `docs/performance/` stay untouched and
  out of this commit.
- Earlier in the same session, `484bbb19` reworded the luteal-phase training tip so it no longer names
  soreness as a training trigger, in EN, RU and the runtime map, with a copy guard.

## Verification

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:  1465, Skipped:     0, Total:  1465, Duration: 1 s

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:    60, Skipped:     0, Total:    60, Duration: 329 ms

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:   210, Skipped:     0, Total:   210, Duration: 191 ms
```

Compile-only checks from this session: `MuscleCuties.Infrastructure.PostgreSql` and the MAUI iOS
simulator project both built with 0 warnings and 0 errors.

## Not verified

- No PostgreSQL-backed suite ran, so the review-catalog import and the recovery-evidence projection are
  compile-verified only.
- No simulator run, screenshot or device check: the reworded tip, the changed dashboard recovery score
  and the Russian layout were not observed.
- P1's "add evidenced gym, dumbbell, band and bodyweight coverage" is not done: closing the gaps listed
  in `CatalogCoverageGuardTests` needs reviewed catalog content, and inventing entries is forbidden.
- P2–P9 are unstarted; no planner, scheduling, progression, API, simulator or deployment work ran.

## Next step

P2 canonical planner, starting with the Gate 49 pattern selection and Gate 56 persisted glute focus that
the P1 audit left open on the generator side.

# Status — Workout Engine 2.0: P2 started (deterministic selection)

Branch: `fix/qa-audit-followups`; HEAD before this commit: `50e49da2`.

## Changed files

- `src/MuscleCuties.Core/Domain/Workout/Planning/StrengthTemplateResolver.cs` — the canonical selection
  engine used by `StrengthPlanPreviewRule` now rejects a catalog containing two rows for one exercise id
  (`DuplicateCatalogExerciseId`, status `Invalid`) instead of resolving by row order. This also removes a
  latent `SingleOrDefault` throw in duration estimation.
- `tests/MuscleCuties.Core.Tests/StrengthTemplateResolverDeterminismTests.cs` — new: all 120 permutations
  of a five-entry catalog must produce an identical slot prescription; a duplicate-id catalog must fail
  closed. The duplicate test failed against the previous behaviour (`Expected "Invalid"`, `Actual
  "Resolved"`) and passes now.
- `tests/MuscleCuties.Core.Tests/StrengthPlanPreviewRuleTests.cs` — regression guard that the preview
  reports `InvalidInput` with no sessions for a duplicated catalog row.
- `docs/workout-engine-v2/IMPLEMENTATION_PLAN.md` — P2 section with what is proven, what remains and why
  each remainder is blocked; P1 coverage gaps converted into four itemized decisions.
- `STATUS.md` — this checkpoint. The pre-existing `WorkoutActivityPreferencePicker.xaml` edit and the
  staged QA-evidence/latency-report deletions stay untouched and out of this commit.

## Verification

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:  1468, Skipped:     0, Total:  1468, Duration: 1 s

$ dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:    60, Skipped:     0, Total:    60, Duration: 333 ms

$ dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release
Passed!  - Failed:     0, Passed:   210, Skipped:     0, Total:   210, Duration: 165 ms
```

Pre-change evidence for the new guard: `Expected: "Invalid"`, `Actual: "Resolved"` on the duplicate-id
catalog before the resolver change.

## Not verified / not done

- No simulator, PostgreSQL-backed suite, or deployment run.
- P2 is **partial**. Reproducibility is proven and ambiguous input now fails closed, but Gate 49
  week-level pattern coverage needs reviewed template content, Gate 56 needs a persisted glute focus and
  a schema change in an area this task does not own, versioned input validation has no request-level
  version field yet, and the two unwired legacy planners were left in place rather than deleted
  unilaterally.
- P1 coverage gaps are recorded as four specific questions in the plan; no unreviewed exercise entry was
  authored.

## Next step

Decide the four P1 catalog items and the two P2 blockers (Gate 49 review content, Gate 56 persistence
ownership). P3 scheduling work is next only after Gate 56's persisted focus exists, because scheduling
frequency depends on it.

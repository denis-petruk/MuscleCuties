# Status — QA audit base branch

Branch: `fix/qa-audit-followups`, based on `4b4847ee`. Head before this status commit:
`afd78d9c`. PR: https://github.com/denis-petruk/MuscleCuties/pull/50 (base `main`).

No source file was changed in this verification pass. The pre-existing shared-UI work is preserved
on the branch, not reset, stashed or rewritten. `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`
is tracked and present at this head.

## Changed files

- `STATUS.md` — this record only.
- Existing branch work remains in place: `AnimatedModalView.cs`, `Styles.xaml`, `MealCardView.xaml`,
  `NutritionPage.xaml`, `WorkoutSessionPage.xaml`, the modal XAML and the shared controls.

## Gate

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj --no-restore
Passed!  - Failed: 0, Passed: 1416, Skipped: 0, Total: 1416, Duration: 816 ms

$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug \
    -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 --no-restore
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

The Core gate includes the UI contract guards, so the shapes named below are pinned rather than only
observed once.

## Findings checked in the simulator

Simulator `678048F6-C4C7-406C-A970-7104AED3E00A`, installed Debug build at `afd78d9c`.

- **D8 — already fixed by the existing diff.** Nutrition, default text size: `Завтрак` is at the
  leading edge and `215 ккал` / `10% дневной цели` at the card's trailing edge, with no overlap.
- **D19 — already fixed by the existing diff.** Train → `Начать тренировку`: the sheet title is one
  line; `Strength & Sculpt`, `0 из 5 отмечено` and `5 упражнений` occupy separate aligned lines.
  Tapping the row leaves the session screen in the next frame; the sheet is gone.
- **D21 — already fixed by the existing diff.** Profile → `Личная информация`, scrolled to the end:
  `Изменить пароль` and `Сохранить личную информацию` are fully visible. The save bar is a page-grid
  row below the scroll view, not an overlay. Nutrition settings, units and feedback use the same
  `Auto,*,Auto` grid.
- **D37 — already fixed by the existing diff.** Today: `Заполнить отметку` is fully above the tab bar
  and taps through. The check-in sheet keeps `Пока пропустить` / `Сохранить отметку` inside its
  footer.

No repair commit was needed for this set; duplicating patches would have risked the existing shared
primitives.

## Not verified

- D21's nutrition-settings, units and feedback pages share the fixed grid but were not re-opened on
  the simulator in this pass.
- D8's empty meal-card state was not re-captured; only a populated `Завтрак` card was observed.
- D19's dismissal was verified as "next frame is the session", not with a millisecond stopwatch.
- Thirteen commits that were already on the remote branch before this pass contain generated-tool
  trailers in their messages. They were not rewritten or force-pushed because the repository forbids
  rewriting remote history; all commits created in this pass, the PR title and the PR body are free
  of attribution.

## Next step

Continue the QA fixes from the Wave A base. The first owner decision still open is D43 (cycle phase
list wording and one canonical recipe).

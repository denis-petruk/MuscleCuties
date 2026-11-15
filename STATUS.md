# Status — QA audit base branch

Branch: `fix/qa-audit-followups`, based on `4b4847ee`. Verification head: `1ef6a70a`.
PR: https://github.com/denis-petruk/MuscleCuties/pull/50 (base `main`).

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
  Food Log with no entries in `Обед`, `Ужин` and `Snacks`: each empty group is one quiet row reading
  `Пока ничего не добавлено`; no `0 ккал` or `0% дневной цели` block is drawn, so the empty state has
  no header/calorie collision to repair.
- **D19 — already fixed by the existing diff.** Train → `Начать тренировку`: the sheet title is one
  line; `Strength & Sculpt`, `0 из 5 отмечено` and `5 упражнений` occupy separate aligned lines.
  Tapping the row leaves the session screen in the next frame; the sheet is gone.
- **D21 — already fixed by the existing diff.** All four named pages were opened and scrolled to the
  end. `Личная информация` keeps `Изменить пароль` and `Сохранить личную информацию` fully visible;
  nutrition settings keeps the calculated-goals helper and save button clear; units keeps the
  `Энергия` field above the save bar; feedback keeps the `Приоритет` field and attachment row above
  `Открыть черновик письма`. The save bar is a page-grid row, not an overlay.
- **D37 — already fixed by the existing diff.** Today: `Заполнить отметку` is fully above the tab bar
  and taps through. The check-in sheet keeps `Пока пропустить` / `Сохранить отметку` inside its
  footer.

No repair commit was needed for this set; duplicating patches would have risked the existing shared
primitives.

## History rewrite

The owner authorized a one-time rewrite of this feature branch. All 65 commits in
`4b4847ee..HEAD` were message-filtered; no commit in the range contains an attribution trailer.
Every rewritten commit's tree and subject matches its pre-rewrite counterpart (65/65, zero
mismatches), and the final tree and full diff are identical. The backup ref
`backup/qa-audit-followups-pre-rewrite` and a local bundle retain the pre-rewrite history; the
branch was force-pushed with an exact lease and no other branch was touched.

## Not verified

- The Food Log empty state was observed for three empty groups, not a whole day with every group
  empty; producing that would require deleting existing QA logs.
- D19's dismissal was verified as "next frame is the session", not with a millisecond stopwatch.

## Next step

Workout logic is scheduled for a rewrite, so workout-behaviour findings (D18, D20, D29, D30, D44) are
paused. Nutrition/UI work can continue from this base. The first owner decision still open is D43
(cycle phase list wording and one canonical recipe).

# Status — QA audit base branch

Branch: `fix/qa-audit-followups`, based on `4b4847ee`. Verification head: `acea8e0b`.
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

## History-rewrite limitation

`AGENTS.md:7` states exactly:

> Work on a branch and integrate through a PR into protected `main`; never push directly to `main`,
> force-push or rewrite remote history.

That rule forbids rewriting the thirteen commits below, all of which predate this pass and are
ancestors of `origin/fix/qa-audit-followups`. They carry attribution trailers in their messages:

```
2b0afa30 167a32da d2341f85 113c64ed 1b240bcb 97ea6395 f68bbabf
cc1b52fd f495de4e b27770a1 5c766e8a f75f5663 86c33f13
```

No rewrite or force-push was performed. This branch therefore remains non-compliant with the
no-attribution constraint until the owner permits a history rewrite or accepts a squash/replacement
merge. The two commits added in this verification pass, the PR title and the PR body are clean.

## Not verified

- The Food Log empty state was observed for three empty groups, not a whole day with every group
  empty; producing that would require deleting existing QA logs.
- D19's dismissal was verified as "next frame is the session", not with a millisecond stopwatch.

## Next step

Continue the QA fixes from the Wave A base. The first owner decision still open is D43 (cycle phase
list wording and one canonical recipe).

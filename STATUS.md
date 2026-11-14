# Status — QA plan implementation, branch `fix/qa-audit-followups`

## 1. Commits

Six commits on `fix/qa-audit-followups`, D54 kept separate from the Wave A work:

- `19887dca` — restored `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`. **D16 fixed.**
- `cd65f85d` — one 24-hour `MealTime` formatter. **D48's 12-hour half fixed.**
- `3a2daf1e` — the pre-existing 1,921-line primitives base, committed as its own base.
- `f28aa3b1` — **D54 fixed.** Meal card header declares `JustifyContent="SpaceBetween"`; the heading grows.
- `3b55255c` — **Wave A frame:** `ModalKeyboardInset`, one scroll body, keyboard padding.
- `7023dc81` — **Wave A close-out:** the frame reveals the content below the focused field, keeps a 12 pt
  footer reserve, the toast hosts use the `UiInsets20_0_20_72` clearance token, and the food editor drops
  its header/tabs/ingredient list while the search field is focused. Evidence images in
  `docs/qa-evidence/wave-a/`.

## 2. What the screenshots show

| Claim | Evidence |
| --- | --- |
| D54: `Завтрак … 0 ккал` with the gap, light | `docs/qa-evidence/wave-a/P6-foodlog-cards-light.png` |
| D54: same in dark | `Q9-foodlog-cards-dark.png` |
| D51: field, `Поиск` and the result row above the keyboard, light | `Q12-searchmode-light.png`, `Q13-results-light.png` |
| D51: same in dark | `Q14-results-dark.png` |
| D52 scroll half: entry sheet scrolls, footer clear of the edge | `R4-entry-sheet-dark.png` |
| D48: 24-hour time in the entry sheet | `R4-entry-sheet-dark.png` shows `22:08` |

**Not shown on screen:** the built-in food catalogue returned no matches in the simulator, so the result
row in the picker screenshots is the no-match state. No toast was triggered, so D50's clearance is a
code-level fix only. The check-in sheet itself was not re-photographed this pass (D5/D37 rest on the frame's
footer reserve and on the tab bar being hidden while a sheet is open).

## 3. Commands and output

```
dotnet build … -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64
    0 Warning(s)    0 Error(s)

dotnet test tests/MuscleCuties.Core.Tests
Passed!  - Failed: 0, Passed: 1384, Skipped: 0, Total: 1384
```

Guard tests added this pass (all green, 30 in the UiContractGuard/UiStyleGate slice):
`Modal_frame_reveals_the_content_below_the_focused_field`,
`Toast_hosts_clear_the_floating_tab_bar`,
`Food_picker_collapses_the_rows_that_do_not_belong_to_the_search`, on top of the earlier frame and meal-card
guards.

## 4. Still open

- **D51 result row with real hits** — the layout half is fixed and photographed against the empty state; a
  real search hit could not be produced in the simulator.
- **D5 / D37** — structural fix only, no screenshot of the check-in footer.
- **D50** — code-level fix, no toast photographed.
- **D52 template action** — `Использовать шаблон` is still a silent no-op.
- **Not started:** D1/D46 (accessibility tree and the edit action), D47, D29/D30/D20, the cycle wave
  (D39–D45), the copy wave, and the remaining formatter/selection/header primitives from the plan.

## 5. Next step

Give the food search a reachable catalogue (or seed a custom food) so the picker's result row can be
photographed with a real hit, then drive the check-in and a toast in both themes. After that, the cycle wave
and D46 — the frame is now stable enough to carry them.

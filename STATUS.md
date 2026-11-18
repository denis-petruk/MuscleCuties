# Status — workout activity card, accessibility text sizes

## 1. Changed files

- `src/MuscleCuties.UI/Controls/Workout/WorkoutActivityCard.xaml`
  - The day badge keeps the reviewed 36pt as a **minimum** instead of a fixed size, so a two-letter
    day ("СР", "ПН") outgrows the tile instead of losing its second letter under the edge; the day
    label no longer breaks inside itself (`LineBreakMode="NoWrap"`).
  - The two session-adjustment actions sit in a wrapping `FlexLayout` instead of a
    `HorizontalStackLayout`. The stack measured its children against an unbounded width, so a long
    Russian label ("Обязательная тренировка") was laid out wider than the card and clipped at the
    screen edge; the wrapping row measures against the card and the label wraps inside it.
  - Unchanged: the grid simplification already in the tree (`Auto,*` columns, icon under the day,
    title beside it), every binding, command wiring, style role, colour, localization key and
    accessibility semantic.
- `tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs`
  - New guard `Activity_card_badge_and_actions_grow_instead_of_clipping_at_large_text`: the day tile
    sets `MinimumWidthRequest`/`MinimumHeightRequest` to `UiDimension36` and no fixed
    `WidthRequest`/`HeightRequest`, its label is `NoWrap`, and both actions are children of a
    `FlexLayout` with `Wrap="Wrap"` that still uses `ButtonSecondary`.
- `src/MuscleCuties.UI/ViewModels/Workout/WorkoutViewModel.cs` — temporary `[DIAG]` console writes
  were added to `SelectFilter`/`SelectWeekChip` to diagnose the simulator input, and removed again;
  the file carries only the earlier stable-tag filter fix.

## 2. Test commands and output

Guard written first, red for the intended reason:

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj \
    --filter "FullyQualifiedName~Activity_card_badge_and_actions_grow"
  Failed MuscleCuties.Core.Tests.UiCleanlinessAndReachabilityTests.Activity_card_badge_and_actions_grow_instead_of_clipping_at_large_text [10 ms]
  Error Message:
   Assert.Null() Failure: Value is not null
Expected: null
Actual:   WidthRequest="{StaticResource UiDimension36}"
Failed!  - Failed:     1, Passed:     0, Skipped:     0, Total:     1
```

After the fix:

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj \
    --filter "FullyQualifiedName~Activity_card_badge_and_actions_grow"
Passed!  - Failed:     0, Passed:     1, Skipped:     0, Total:     1
```

Full suite:

```
$ dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
Passed!  - Failed:     0, Passed:  1696, Skipped:     0, Total:  1696
```

Simulator build:

```
$ dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj \
    -f net10.0-ios -r iossimulator-arm64 -c Debug
Build succeeded.
    0 Warning(s)
    0 Error(s)
```

Simulator (MC Generation QA Clone, iOS 26.3, Russian, `com.musclecuties.app`):

- `accessibility-extra-extra-large` and `accessibility-extra-extra-extra-large`: the day badge shows
  "СР"/"ПН" in full and the action button wraps its label inside the card instead of running past it.
  A relaunch is required after `simctl ui … content_size`; without one the CollectionView keeps the
  previous metrics and the capture is stale.
- Filter chips, selected with accessibility presses on the Simulator:
  - Сила → four strength cards in the week (three on ПН 16, one on СР 18); no other type appears.
  - Кардио → the list is empty; no strength card leaks through.
  - Восстановление → the list is empty; no strength card leaks through.
  - Все → the same four strength cards.

## 3. Not verified, and why

- A **cardio or recovery card** could not be observed. The clone's current week holds strength
  sessions only (ПН 16, СР 18; ВТ–ВС are rest days) and the today's-workout sheet lists a single
  "Сила и форма" activity, so those two chips have nothing of their own to show. No app or
  simulator data was changed to manufacture one; the tag isolation itself is covered by
  `WorkoutListFilterRule` tests.
- **Weeks 2–4** were not inspected: the week chips did not respond to synthetic taps or to
  accessibility presses in this session, so any cardio/recovery sessions scheduled there stayed out
  of reach.
- **Filter with no matches renders a blank list area**: with Кардио selected nothing appears — no
  empty-state title or hint. Not changed by this task.
- Simulator input caveat: Maestro point taps reach native buttons but not MAUI tap gestures
  (card and chip taps did nothing); the chip matrix was driven by accessibility presses on the
  Simulator window, confirmed by temporary console output (`SelectFilter tag=CARDIO`).
- No automated UI test exercises the rendered card; the guard reads the XAML, and the rendering was
  checked by screenshot only.

## 4. Next step

Commit the tree and push it to `fix/qa-audit-followups` (fast-forward from the branch's tip). Open
follow-ups for the two items found on the way: the missing empty-filter state, and the week chips
that did not respond to input during this session.

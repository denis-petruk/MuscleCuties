# Status

## Changed files

- `src/MuscleCuties.UI/Controls/Dashboard/DashboardWorkoutCard.xaml` — the header is now an
  `Auto,*` grid: the icon keeps its own column, while the badge, title, and support line share the
  text column. The rest-day guidance is an inline callout with `UiInsets14_12` and `MutedLabel`
  instead of a `CardSecondary` nested inside the primary card, removing the double inset and the
  12 pt caption type.
- `tests/MuscleCuties.Core.Tests/UiContractGuardTests.cs` — added
  `Today_rest_card_keeps_support_copy_aligned_and_guidance_compact`, which failed before the
  layout change and now guards the alignment, compact inset, and support-size guidance.
- `docs/qa-evidence/wave-a/rest-card-before.png`,
  `docs/qa-evidence/wave-a/rest-card-after.png`,
  `docs/qa-evidence/wave-a/rest-card-after-dark.png`, and
  `docs/qa-evidence/wave-a/rest-card-after-accessibility-large.png` — simulator evidence for the
  normal, dark, and accessibility-large rendering.

## Test commands and output

    dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj --no-restore \
      --filter "FullyQualifiedName~UiContractGuard|FullyQualifiedName~UiStyleGate|FullyQualifiedName~Xaml_buttons_use_shared_styles"

    Passed!  - Failed: 0, Passed: 52, Skipped: 0, Total: 52

    dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj --no-build --no-restore

    Passed!  - Failed: 0, Passed: 1409, Skipped: 0, Total: 1409

    dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -c Debug -f net10.0-ios \
      -p:RuntimeIdentifier=iossimulator-arm64 --no-restore

    Build succeeded.
        0 Warning(s)
        0 Error(s)

The new guard first failed with: `WorkoutSubtitle must sit in the same Auto,* grid as the icon so
it aligns with the title, not the icon.` It passes after the XAML change. The card was inspected in
the simulator in light, dark, and `accessibility-large` before restoring the simulator to light and
`large`.

## Not verified

- The five-project solution suite was not rerun for this UI-only change; the last full-solution
  gate belongs to the previous task, not this one.
- The training-day variant of the reflowed header was not opened in the simulator. The rest-day
  card and its guidance were checked; the workout-day pill and activity chips use the same new
  text-column grid but were not visually exercised.
- The primary action was not tapped after the layout change; its command and binding are unchanged.
- The existing untranslated `Rest Focus` subtitle was left alone; it comes from data/copy rather
  than the card typography.

## Next step

Review the four evidence frames and merge the card refinement. The untranslated rest subtitle and
the remaining QA waves are separate follow-ups.

# Status

## Changed files

- `src/MuscleCuties.UI/Pages/Workout/WorkoutPage.xaml` — Replaced `PresentationPageContentPadding` on `CollectionView.Header` with `UiInsets20_8_20_16` to eliminate the 96 pt gap between filter chips and workout cards; aligned week calendar strip tiles to uniform height (`UiDimension10` indicator row, `DayStripDayNumber` typography, circular `<Ellipse>` session dots, `AlignItems="Start"`); localized Reschedule Modal title and slot text.
- `src/MuscleCuties.UI/Controls/Workout/WorkoutActivityCard.xaml` — Placed status pill (`Not logged` / `Completed`) in Row 0 Col 1 opposite the weekday badge and gated Row 2's action `FlexLayout` with `IsVisible="{Binding CanMarkOptional}"` to eliminate empty top-right bands and phantom bottom spacing.
- `src/MuscleCuties.UI/Controls/Nutrition/NutritionBalanceCard.xaml` — Replaced linear progress bars and orphan right-aligned `Micronutrients` button with cohesive radial `DashboardProgressRing` gauges (Calories + adaptive 3-column Protein/Carbs/Fat via `AdaptiveRow`) and a full-width tappable `Micronutrients` disclosure row with chevron.
- `src/MuscleCuties.UI/Pages/Nutrition/NutritionPage.xaml` — Promoted daily balance and `Add meal` / `Plan my day` primary CTAs above the fold ahead of phase guidance; tightened header inset and raised `CollectionView.Footer` to `UiDimension96` to clear the floating tab bar.
- `src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml` — Gated Edit Modal Save/Delete/Cancel footer (`Grid.Row="3"`) with `IsVisible="{Binding IsManualEditorVisible}"` so secondary logging modes do not render duplicate Save actions; switched notes field to `EditorContainer`; raised `CollectionView.Footer` to `UiDimension96`.
- `src/MuscleCuties.UI/Controls/Nutrition/MealCardView.xaml` — Added horizontal padding (`UiInsets16_8`) to `EmptyMealRow` so empty meal rows align with card interiors and clear the vertical scrollbar.
- `src/MuscleCuties.UI/Controls/Nutrition/MealDetailModal.xaml` — Added `LocalizedStringConverter` to `SpiceBlendDetail.Name` and `Description`.
- `src/MuscleCuties.UI/Controls/Nutrition/MealSuggestionModal.xaml` — Removed fixed `WidthRequest` (`UiDimension152`) on the `Try again` button to prevent clipping in Russian.
- `src/MuscleCuties.UI/Controls/Cycle/CycleWarningPopup.xaml` — Moved action footer from `Grid.Row="2"` to `Grid.Row="3"` to eliminate overlap with the scrollable warning body.
- `src/MuscleCuties.UI/Controls/Cycle/CycleCalendarCard.xaml` — Updated `LastPhaseLogText` container margin to `UiInsets12_0` to clear the leading phase icon.
- `src/MuscleCuties.UI/Pages/Cycle/CyclePhaseDetailPage.xaml` — Pinned `PageHeader` in `Grid.Row="0"` above `<ScrollView Grid.Row="1">`, removed duplicate `PhaseWindow` row inside hero card, demoted duplicate `Level1` heading to `Level2`, and changed watch-out chevron column to `Auto`.
- `src/MuscleCuties.UI/Controls/Injury/InjuryLogContent.xaml`, `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.NutritionProfileAuth.cs`, `src/MuscleCuties.UI/Services/Localization/RussianUiText.NutritionProfileAuth.cs` — Localized `"Hide"` (`Injury_LogContent_Hide` / `"Скрыть"`) and centered empty-state detail copy.
- `src/MuscleCuties.UI/Controls/Dashboard/DashboardWorkoutCard.xaml`, `src/MuscleCuties.UI/ViewModels/Dashboard/DashboardViewModel.cs` — Removed broken nested `<Border>` inside the `TRAINING` pill and suppressed redundant status-only subtitles (`ShowWorkoutSubtitle`) already announced by `WorkoutBadgeText`.
- `src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml`, `src/MuscleCuties.UI/ViewModels/Workout/WorkoutSessionViewModel.cs` — Localized exercise cues and quick tips, switched movement notes to `EditorContainer`, and excluded remaining validation/refusal error keys from `CanRetryLoad`.
- `src/MuscleCuties.UI/Pages/Workout/StrengthPlanGenerationPage.xaml` — Pinned `PageHeader` above `ScrollView`, switched tabless page padding to `PresentationFormPagePadding`, and replaced horizontal stack checkbox row with `Grid ColumnDefinitions="Auto,*"` for clean wrapping.
- `src/MuscleCuties.UI/Pages/Workout/WorkoutHistoryPage.xaml` — Added `PageContentMaxWidth` constraint on header, localized month filter accessibility descriptions, and raised `CollectionView.Footer` to `UiDimension96`.
- `src/MuscleCuties.UI/Controls/Workout/FeaturedWorkoutCard.xaml`, `src/MuscleCuties.UI/Controls/Workout/WorkoutSwapPanel.xaml` — Added `LocalizedStringConverter` to `MovementNotes` and swap option accessibility descriptions.
- `src/MuscleCuties.UI/Pages/Auth/PasswordRecoveryPage.xaml` — Removed skipped Grid row in Step 2 and placed primary `Verify Code` button above secondary `Resend Code` / `Change Account` actions.
- `src/MuscleCuties.UI/Pages/Onboarding/QuizPage.xaml`, `src/MuscleCuties.UI/Pages/Startup/AppStartupPage.xaml` — Replaced tab-bar bottom padding (`PresentationPageContentPadding`) on tabless pages with `PresentationFormPagePadding` / `PresentationAuthPagePadding`.
- `src/MuscleCuties.UI/Pages/Profile/PhotoJourneyVaultPage.xaml`, `src/MuscleCuties.UI/Pages/Profile/ProfilePrivacyPage.xaml`, `src/MuscleCuties.UI/Pages/Profile/ProfileFeedbackPage.xaml`, `src/MuscleCuties.UI/Pages/Profile/ProfilePersonalInfoPage.xaml`, `src/MuscleCuties.UI/Controls/Profile/ProfileDetailsContent.xaml` — Added `PageContentMaxWidth` and tab-bar footer clearance on Photo Journey Vault and Privacy pages, replaced Unicode `▾` glyphs with vector `Path` chevrons on Feedback, styled inline `ActivityIndicator` on Personal Info, and normalized 4-column profile menu rows to 3 columns.
- `tests/MuscleCuties.Core.Tests/UiFullAuditEnhancementTests.cs` — Added regression tests locking in all cross-page layout, calendar, nutrition, modal, padding, and localization fixes.

## Verification

### `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release -v minimal`

```text
Passed!  - Failed:     0, Passed:  1730, Skipped:     0, Total:  1730, Duration: 1 s - MuscleCuties.Core.Tests.dll (net10.0)
```

### `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj -c Release -v minimal`

```text
Passed!  - Failed:     0, Passed:   248, Skipped:     0, Total:   248, Duration: 220 ms - MuscleCuties.Mobile.Contracts.Tests.dll (net10.0)
```

### `dotnet test tests/MuscleCuties.Registration.Tests/MuscleCuties.Registration.Tests.csproj -c Release -v minimal`

```text
Passed!  - Failed:     0, Passed:    70, Skipped:     0, Total:    70, Duration: 440 ms - MuscleCuties.Registration.Tests.dll (net10.0)
```

### `MC_LATENCY_POSTGRES="Host=127.0.0.1;Port=55461;Database=mc_test_glute;Username=postgres" dotnet test tests/MuscleCuties.Server.Tests/MuscleCuties.Server.Tests.csproj -c Release -v minimal`

```text
Passed!  - Failed:     0, Passed:   125, Skipped:     0, Total:   125, Duration: 1 m 1 s - MuscleCuties.Server.Tests.dll (net10.0)
```

### `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -r iossimulator-arm64 -c Debug -v minimal`

```text
Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:15.77
```

### iOS Simulator visual verification (iPhone 17 Pro, iOS 26.3)

- Verified Light Mode + Default Dynamic Type (`large`) across Today (`DashboardWorkoutCard`), Train (`WorkoutPage` week strip, filter-to-card spacing, `WorkoutActivityCard`), Nutrition (`NutritionBalanceCard` radial rings + full-width `Micronutrients` row + above-the-fold CTAs), Daily Food Log (`EditorContainer` notes + `EmptyMealRow` right padding + footer clearance), and Cycle (`CycleCalendarCard` icon-to-text spacing).
- Verified Dark Mode + `accessibility-large` Dynamic Type across Train and Nutrition (`AdaptiveRow` 3-ring vertical stacking, contrast, zero truncation/overlap, tab-bar clearance).

## Not verified

- `MuscleCuties.Registration.Integration.Tests` and live physical-device / TestFlight screenshots were not run in this pass.
- Interactive modal flows requiring specific account/plan state (`CycleWarningPopup` live trigger, `PasswordRecoveryPage` Step 2 live OTP, `PhotoJourneyVaultPage` populated photos, `ProfileFeedbackPage` live screenshot) were verified statically via XAML inspection and unit/contract tests rather than live simulator taps.

## Next step

- Review the PR on `fix/qa-audit-followups` and merge into `main` once CI checks pass.

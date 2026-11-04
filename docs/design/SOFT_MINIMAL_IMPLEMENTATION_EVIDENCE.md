# Soft Minimal implementation evidence — 2026-11-02

Status: **source implementation complete** for the approved [Soft Minimal plan](SOFT_MINIMAL_IMPLEMENTATION_PLAN.md). Owner device acceptance is a separate event. The MAUI client was not built, run, simulated or captured by an agent under the owner rule, so nothing here certifies rendered layout, font rendering, VoiceOver behaviour or keyboard interaction.

## Execution record

No code-editing subagent was available in this environment; only a web-research agent is exposed. Implementation, integration, repair and verification were therefore performed directly in the single working tree rather than delegated, and no agent execution is claimed. Ownership stayed disjoint by construction: shared resources (`Colors.xaml`, `Styles.xaml`, `MauiProgram.cs`, `Controls/Shared`) were edited centrally, and page work was limited to the surfaces named in the plan.

## Foundation implemented

| Requirement | Source | Verification |
| --- | --- | --- |
| Approved light palette | `Colors.xaml`: `PageBackground #FFF9FB`, `CardSurface #FFFFFF`, `CardSurfaceSecondary #F7EAF0`, `TextPrimary #2B1D24`, `TextSecondary #5F4550`, `ActionPrimaryLight #88435F`, `ActionSecondaryLight #67444F`, `WarningAccentLight #7B4E00`, `ErrorLight #A72E27` | Palette/contrast source test |
| Contrast corrections | `SuccessLight` darkened to `#2C7548` (4.80:1 on the secondary card); `ControlBoundaryLight #96707F` (3.65:1 on the secondary card) | Measured WCAG sRGB ratios in test |
| Warm dark pairs | `PageBackgroundDark #2B1D24`, `CardSurfaceDark #3A2931`, `CardSurfaceSecondaryDark #46313B`, `ControlBoundaryDark #C9A6B4`, `SuccessDark #7FD197`, `TextPrimaryDark #F8EEF4`, `TextSecondaryDark #D7BFCA` | Measured dark pairs in test |
| Open Sans semibold | `MauiProgram.cs` registers `OpenSans-Semibold.ttf` as `InterSemibold`; `AppFontSemibold` added; the historical `Inter` alias is unchanged | Font registration assertion |
| Typography scale | `PageTitle` 27, `SectionTitle` 18, `CardTitle` 16, `BodyLabel` 15, `MutedLabel` 13, `CaptionLabel` 12, `WorkoutNumber` 24, button labels 14 — all semibold roles use the real semibold face with `UiFontAttributesNone` | Shared style contract |
| Workout numbers | `WorkoutNumber` (label) and `WorkoutNumberEntry` (entry) share the 24-point semibold face, single-line, right-aligned value and centred input | Source; owner entry check outstanding |
| Geometry | Both cards: 16 radius, `UiInsets19`; buttons: `UiRadiusButton` 12 with `UiControlHeight` 48 minimum; touch minimum 44 retained | Style assertions |
| Rhythm | `PresentationFieldSpacing` 6→4, `PresentationFormSpacing` 18→16, plus `UiSpaceSection` 32 and `UiSpacePage` 40 on the approved 4/8/12/16/24/32/40 scale | Source |
| One page frame | `PageContentMaxWidth` 640, `PresentationPageHeaderPadding`, `PresentationPageContentPadding`, `PresentationFormPagePadding`, `PresentationAuthPagePadding`, `PresentationActionFooterPadding`, `PresentationCollectionItemMargin` | All references resolve |
| Exactly two buttons | `ButtonPrimary` (action fill, white label) and `ButtonSecondary` (accent surface, action-secondary label, corrected boundary) | 2-style assertion |
| Exactly two cards | `CardPrimary` (white surface) and `CardSecondary` (tonal surface, selection as a state) | 2-style assertion; retired names absent |
| Selection state | `CardSecondary` carries an `IsSelected` trigger that sets the selected stroke; selectable content also shows a checkmark or state text | Source |
| Reduce Motion | `MotionSettings` reads `UIAccessibility.IsReduceMotionEnabled` on iOS; `ModalTransition` skips fade/translate/scale and applies the end state directly | Source |
| Sheet focus | `AnimatedModalView` focuses the first interactive control on open, clears focus on close and restores an optional `RestoreFocusTarget` | Source; owner VoiceOver check outstanding |

## Page coverage (25/25)

Every page shares the page frame, typography roles and permitted cards, and declares no local `Style`. Binding expressions are preserved; the two pages whose expression set changed are recorded below.

| Page | Source state |
| --- | --- |
| Startup/AppStartupPage | Implemented — quiet progress/failure/retry |
| Auth/LoginPage | Implemented — shared header, persistent labels, reachable login footer |
| Auth/RegisterPage | Implemented — form groups, persistent commit |
| Auth/PasswordRecoveryPage | Implemented — focused request, nearby status |
| Auth/OtpVerificationPage | Implemented — code/resend/status footer |
| Onboarding/ProfileSetupPage | Implemented — labelled field groups |
| Onboarding/QuizPage | Implemented — one question, full-row choices, pinned actions |
| Dashboard/DashboardPage | Implemented — next action before counters |
| Dashboard/DailyCheckInPage | Implemented — one form rhythm, responsive choices |
| Workout/WorkoutPage | Implemented — current/resumable session first |
| Workout/StrengthPlanGenerationPage | Implemented — setup/preview/apply |
| Workout/WorkoutSessionPage | Implemented — exercise/set/units together, numeric inputs |
| Workout/WorkoutHistoryPage | Implemented — labelled flexible rows |
| Nutrition/NutritionPage | Implemented — log action first |
| Nutrition/FoodLogPage | Implemented — contextual entry, theme-aware selected date |
| Cycle/CyclePage | Implemented — logged/estimated context, quick log first |
| Cycle/CyclePhaseDetailPage | Implemented — readable explanation rows |
| Profile/ProfilePage | Implemented — cohesive settings rows |
| Profile/ProfilePersonalInfoPage | Implemented — labelled form and footer |
| Profile/ProfileNutritionSettingsPage | Implemented — goals/targets groups |
| Profile/ProfileWorkoutPreferencesPage | Implemented — choice rows, weekday selection state |
| Profile/ProfileUnitsDisplayPage | Implemented — unit choices and commit |
| Profile/ProfilePrivacyPage | Implemented — readable privacy groups |
| Profile/ProfileFeedbackPage | Implemented — focused form, nearby status |
| Profile/PhotoJourneyVaultPage | Implemented — quiet grid/empty state |

## Reusable control coverage (35/35)

Cycle: `CycleCalendarCard`, `CycleDailyTipsCard`, `CycleDatePhaseModal`, `CycleHeroCard`, `CyclePhaseExplorerCarousel`, `CyclePhaseSummarySheet`, `CycleQuickLogBottomSheet`, `CycleWarningPopup`.
Dashboard: `DashboardNutritionCard`, `DashboardPhaseCard`, `DashboardTargetsCard`, `DashboardTrendCard`, `DashboardWorkoutCard`, `ReadinessRecoveryCard`.
Injury: `InjuryLogContent`, `InjuryLogModal`.
Nutrition: `MealDetailModal`, `MealEditorModal`, `MealEntryModal`, `MealSuggestionModal`, `NutritionBalanceCard`, `NutritionBreakdownModal`, `NutritionPhaseFocusCard`, `QuickFoodItemRow`.
Profile: `ProfileDetailsContent`, `ProfileImageCropModal`, `ProfileWorkoutPreferencesChoices`.
Shared: `IconSectionHeader`, `PageLoadingOverlay`, `PhaseSaluteOverlay`.
Workout: `FeaturedWorkoutCard`, `WorkoutActivityCard`, `WorkoutActivityPreferencePicker`, `WorkoutFeaturedSection`, `WorkoutSwapPanel`.

## Style budget

- Button styles: **2** (`ButtonPrimary`, `ButtonSecondary`).
- Card styles: **2** (`CardPrimary`, `CardSecondary`).
- Retired definitions and their callers: `CardAccent` (no callers), `SelectableCard`, `WeekdayChoice`, `SelectionTag` — removed together with the obsolete four-card assertion, which now asserts two.
- Local `Style` declarations outside `Styles.xaml` across pages and controls: **0**.
- Modal, icon-button, input-container and badge styles remain their own shared utility families; they are not page cards and were not renamed or duplicated.

## Measured contrast (unrounded WCAG sRGB, asserted by test)

| Pair | Ratio |
| --- | ---: |
| `SuccessLight` on `CardSurfaceSecondary` | 4.80:1 |
| `ControlBoundaryLight` on `CardSurfaceSecondary` | 3.65:1 |
| `ControlBoundaryLight` on `PageBackground` | 4.10:1 |
| `TextSecondary` on `CardSurfaceSecondary` | 7.33:1 |
| `WarningAccentLight` on `CardSurfaceSecondary` | 6.13:1 |
| `ErrorLight` on `CardSurfaceSecondary` | 5.87:1 |
| White on `ActionPrimaryLight` | 6.97:1 |
| `TextSecondaryDark` on `CardSurfaceSecondaryDark` | 6.92:1 |
| `SuccessDark` on `CardSurfaceSecondaryDark` | 6.51:1 |
| `ControlBoundaryDark` on `CardSurfaceSecondaryDark` | 5.44:1 |
| `ActionSurfaceDark` fill with `TextPrimary` label | 9.56:1 |

These are source-pair calculations for the resource values, not measurements of rendered pixels.

## Executed checks

- `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj` → **1030 passed, 0 failed**. Run from `/tmp` because the repository pins SDK 10.0.403 while 10.0.203 is installed; `global.json` was left untouched.
- The suite includes the resource-reference scan (every `StaticResource`/`DynamicResource` key defined, no forward reference inside `Styles.xaml`), the shared-style and no-local-style checks, input-container conformance, the canonical-card override check, the two-button/two-card budget, the semibold registration, and the new palette/contrast test.
- All 65 XAML surfaces parse as well-formed XML.
- Binding reconciliation: 65 surfaces, 1482 distinct expressions, 1 added, 4 removed, **0 removed command expressions**; full inventory in [SOFT_MINIMAL_BINDINGS.md](SOFT_MINIMAL_BINDINGS.md).
- Backend, server and infrastructure projects were not rebuilt: no C# outside `MuscleCuties.UI` (and the test project) was changed.

## Intentional deltas and recorded decisions

- **Selected food-log date:** the converter-based selected background/stroke was replaced by a `CardSecondary`-style trigger with theme-aware selected surface, stroke and a localized selected hint. This intentionally corrects the audit's dark selected-date defect (was ≈1.03:1).
- **Phase colour:** `CyclePhaseToBrushConverter` usage was removed from two surfaces; `CyclePhaseToTextColorConverter` still binds `CurrentPhase` on those surfaces, so phase identity is unchanged.
- **Brand mark:** `ThemeAwareBrandLogoImage` is no longer placed on the auth pages; their shared header now carries the page title and supporting line only. The control class is retained. The owner may restore it if the brand mark should stay on sign-in.
- **Weekday selection:** seven-across weekday chips compose `CardSecondary` with an inner content inset (`UiInsets4`) rather than a separate card style; selected state comes from the shared trigger plus the existing checkmark.
- **Adaptive gutter:** the shared gutter is a single 20-point compact frame with a 640-point maximum content width. The proposed 24-point wide step is not implemented as a second Thickness resource because MAUI has no width-adaptive Thickness token without platform code; wide layouts centre at the maximum width instead. Recorded as a bounded deviation for owner review.
- **Tabular numerals:** MAUI exposes no font-feature-set API, so numeric alignment relies on the Open Sans semibold face in fixed-width numeric slots. The owner should confirm weight/reps column alignment on device.

## Polish pass — copy resolution and shared selection semantics (2026-11-02)

A cross-check of all 878 `loc:LocalizedText` references against both catalogs found 33 keys used by markup and defined in neither file. `UiTextCatalog.Get` returns the identifier on a miss, so those surfaces rendered raw keys: the quick-add-calories panel and its five field labels, the food-log quick-add / copy-yesterday / saved-template / saved-recipe rows, rest-day labels on the dashboard workout and featured workout cards, the three profile page subtitles, the startup reset control and its VoiceOver hint, and the shared `Common_Selected` / `Common_NotSelected` / `Common_SelectedFilter` state words used by nine surfaces.

| Correction | Source | Verification |
| --- | --- | --- |
| 33 missing keys defined in English | Matching family partials (`UiTextCatalog.Dashboard.cs`, `.CycleWorkout.cs`, `.NutritionProfileAuth.cs`) | Resolution scan: 878/878 references resolve |
| Russian counterparts for every added key | Matching `RussianUiText.*.cs` partials | EN/RU key sets identical; no duplicate registration added |
| Hard-coded English subtitle removed | `ProfilePrivacyPage.xaml` → `Profile_Privacy_Subtitle` | Both languages defined |
| Selection semantics owned by the shared card | `CardSecondary`'s `IsSelected` trigger sets the selected hint; duplicate triggers removed from `InjuryLogContent` (sites, statuses) and `WorkoutSwapPanel` | Resource and style scans; no third card style introduced |
| Copy and state rules recorded | [DESIGN.md](../../DESIGN.md) `Language and copy`; specification §5 language contract | Both documents state the unresolved-key failure mode and the shared-state ownership |

The no-third-card-style limit is what shapes the last row: rather than introduce a `ChoiceSurface` variant, the permitted `CardSecondary` carries the semantics. Surfaces whose choice state is not exposed as `IsSelected` (meal types, diet preferences, unit choice, quiz answers, food-log dates, workout filters) keep a local projection that reuses the same catalog keys and is recorded as migration debt toward that contract.

The Russian strings and the new rest-day guidance copy are a translated draft awaiting owner review. No client build, run or capture was performed, so focus order and the announced wording remain owner checks.

## Design authority approval — DESIGN.md rewrite (2026-11-02)

The owner approved all seven proposals in [the DESIGN.md proposal](DESIGN_MD_PROPOSAL_2026-11-02.md) and `DESIGN.md` was rewritten with them. As approved direction it now states: the Operate posture with stratified density and deliberately low variance; one selection recipe per family plus a stable status treatment; one statement per fact; header composition owned by the shared frame; adaptation for large text and EN/RU treated as a design input; motion reserved for state the user just caused, with a reduced-motion branch; and the governance rule that design authority changes only by an approved proposal under `docs/design/`.

Matching lines were added to specification §3 (header composition, adaptation), §4 (per-family selection, status treatment in the inline-status block) and §5 (one statement per fact), so the two authorities cannot contradict each other.

This step is documentation only: no XAML, style, token or catalog value changed, so nothing was built, run or captured, and no screen is claimed to comply. The audit's findings stay open as fixes — a rule states the standard, it does not certify existing screens.

## Fix pass — six surfaces, five root causes (2026-11-02)

The owner approved a fix pass after reviewing six device screenshots. Each defect was traced to its cause in source before editing; the UI was not built, run or captured.

| Defect | Cause | Fix |
| --- | --- | --- |
| Day tokens stacked one letter per line (Activity preferences) | Seven `CardSecondary` cells in a seven-across grid; the card's own 19pt inset left roughly 16pt for a three-letter day token | The chooser wraps four cells per row (`WeekdaysPerRow`); the item gained `CalendarRow`. A cell now has about 58pt of text width and the label grows downward at large text instead of stacking |
| Two selection languages on the training screen | The week picker repeated a fill recipe in four page-local triggers while the filters below used the shared card state | The week picker now renders through the same `FilterChipItem` + `CardSecondary` + `IsSelected` recipe with `Common_SelectedFilter` hints, driven by `SelectWeekChipCommand` |
| English announced by VoiceOver on the check-in screen | Code-behind rebuilt each description by interpolating English (`"Energy 1 of 5"`, `", Selected"`), and a catalog miss returns the input, so Russian users heard English; it also overwrote the curated localized hints | `UpdateChipGroup` now sets only the visual state; the state names are named constants and the authored markup keeps its descriptions and hints |
| Five sleep-quality chips announced identically | All five reused the field label as their accessible description | Five per-level keys exist in EN and RU and are bound per chip |
| Phase summary sheet filled the screen with a void, and hand-built two surfaces | The body was a `*` row, so a short sheet still occupied the viewport; the note block and tagline were built from raw colour/radius/padding | The body row is content-sized with `ModalSheetBodyMaximumHeight`; the note block uses `CardSecondary`; Close spans the footer |
| Birthday used a hand-written English date order | The only `DatePicker` with `Format="MMMM dd, yyyy"` while the other five use the culture short date | `Format="d"` |

**Finding for the design system:** a compact multi-up cell cannot be built from a card style. `CardPrimary`/`CardSecondary` own a 19pt inset by design, so any grid of them narrower than roughly 90pt per cell collapses its text underneath. Future compact grids either wrap to fewer columns or use the button/choice family, and a call site must not re-pad the card to work around it.

Verification: 880 distinct `loc:LocalizedText` references resolve with none unresolved; EN holds 1698 keys and RU 1694, the only four gaps being the pre-existing `Dashboard_Readiness_*` keys; the ten new sleep-quality keys exist in both languages; no `ChoiceSelectedSurface` fill trigger remains in `WorkoutPage.xaml`; all six edited XAML files parse; `git diff --check` is clean apart from trailing whitespace that the earlier redesign left in `ProfilePersonalInfoPage.xaml`; Core passes 1030/1030. The 4+3 day grid, the sheet height, the full-width Close and the restored VoiceOver wording remain owner device checks.

## Remaining limitations

- No client compilation, launch, simulator run or screenshot: layout, wrapping, font rendering, Dynamic Type, VoiceOver traversal, keyboard insets and Reduce Motion behaviour are unverified at runtime.
- `CardSecondary` selection uses an `IsSelected` data trigger. On tonal cards whose context has no `IsSelected` member, MAUI logs a benign binding diagnostic in Debug; no visual change occurs.
- Sheet focus restoration needs the optional `RestoreFocusTarget`; it is not wired on every sheet, so restoration is best-effort until the owner confirms each flow.
- Overlay and utility style families were not consolidated further; only the card and button budgets were required.
- No commit, push or deployment was performed.

## Owner device checklist

1. Launch each of the five destinations and the auth/onboarding flows on the smallest supported iPhone; confirm one consistent page style, gutters, safe areas and reachable primary actions.
2. Switch light/warm-dark with screens open; check cards, selected weekday chips, selected food-log date, success/warning/error text and secondary-button boundaries.
3. Repeat at the largest accessibility text size; confirm no clipped or shrunk essential action, and that labels wrap.
4. Switch EN/RU; confirm headings, helper text, state hints ("Selected") and button labels are translated and not truncated.
5. With Reduce Motion on and off, open and close several sheets; confirm no slide/scale animation with the setting on, and that focus enters the sheet and returns sensibly.
6. With VoiceOver, traverse a form, a sheet and the food-log date strip; confirm selected states are announced with labels rather than colour alone.
7. Log a food, save a profile change and finish a workout set; confirm busy, error, success and retained-input states, and that workout numbers (sets/reps/weight) align in their columns.
8. Resume an interrupted workout and reopen a sheet after a failed save; confirm session continuity and drafts survive.

## Redesign pass — injury sheet, week plan, streak rows, weekday chooser (2026-11-02, owner screenshots)

Four surfaces the owner reported as still unredesigned. Each cause was located in source before
editing; none was a colour or spacing preference.

| # | Surface | Proved cause | Change |
|---|---------|--------------|--------|
| R1 | Injury log sheet | `SheetSurface` had no height ceiling and a `*` body row, so the sheet grew to full screen; `Cancel`/`Save injury` were the last rows of a long inner scroll | `ModalSheetMaximumHeight` (640) caps the sheet, so the backdrop stays visible and only the body scrolls; the action pair moved out of the form into sheet row 3, visible with `ShowAddForm`, primary `Save injury` + secondary `Cancel` |
| R2 | Injury form chips | `FlexLayout` carried `UiInsets0_0_Minus8_0` with no per-chip margin, so wrapped chip rows abutted and their outlines bled together | outer margin is now `UiInsets0` and each chip carries `UiInsets2`; chips are separated on both axes |
| R3 | Weekday chooser | seven card cells at `UiInsets19` leave ~16pt of text, and the 4+3 wrap left a permanently empty fourth cell in the final row | the chooser is seven compact rows inside the existing `CardPrimary`: 16pt tick, `BodyLabel` day name, 44pt touch minimum, selected/not-selected hints from the catalog |
| R4 | Workout week strip | seven card cells cannot fit one 390pt row; the fourth card was cut mid-word | a seven-column week bar on one `CardPrimary`: day, date and a 7pt type dot per cell, `Column` derived in `ScheduleDayItem` from the date (Monday-first), tap still calls `SelectScheduleDayCommand` |
| R5 | Streak card | three 44pt rows with `UiSpaceTight` and caption-sized labels read as one undifferentiated block | `BodyLabel` names on a 12pt rhythm with 1pt `Gray200`/`Gray800` hairlines between metrics; values stay `MetricValue` |

Constraint confirmed by the build itself: `Canonical_cards_have_no_local_surface_overrides` rejected a
local `Padding` on the new week bar. The card owns its inset, so the seven cells inherit 19pt and the
bar is composed from the card rather than restyled around it. This is the same finding recorded earlier
— compact multi-up cells cannot be made by overriding a card — and it is now enforced by the suite.

Verification: all edited XAML parses; every `loc:` key in the edited files resolves in EN and RU;
`CalendarColumn`, `CalendarRow` and `WeekdaysPerRow` are gone with nothing referencing them; the form no
longer contains its own copy of the Save/Cancel pair; Core 1030/1030. No UI build by the agent.

## Consistency pass — onboarding option anatomy, session cards, food-log strip, nutrition (2026-11-02)

Owner review of the previous pass produced four consistency findings, each traced to source.

| # | Surface | Finding | Change |
|---|---------|---------|--------|
| C1 | Weekday chooser | Off-pattern: bare rows instead of the onboarding option the rest of the app teaches | Now the QuizPage anatomy exactly — `CardSecondary`, `AccentLabel`, `SelectionRadioRing` with the 8pt dot, `UiSpacing10` between options, selected surface/stroke and hints on the card |
| C2 | Workout session card | Two stacked full-width `ButtonSecondary` rows made a card with one adjustment shorter than a card with two, and the `BodyLabel` status pill pushed titles onto two lines | Both adjustments share one wrapping row, so the card height is stable; the status pill is `CaptionSmallStrong` on `UiInsets8_2` |
| C3 | Food-log date strip | Same clipped-cell problem as the old workout strip: a 52pt cell in a horizontal scroller cut the seventh day | A seven-cell week bar fills the row, no scroller and no clipped label; the window narrowed to the selected week, `DateStripDayItem.Position` supplies the column |
| C4 | Nutrition balance card | `RowDefinitions="Auto, 14, Auto, 14, Auto, 14, Auto"` encoded spacing as structure, with three hard-coded literals | Real rows with `RowSpacing="{StaticResource UiSpacing14}"`; a mid-edit row collision between the calorie line and the progress bar was caught and corrected to rows 0, 0, 1, 2, 3 |
| C5 | Nutrition phase card | The whole card was tinted by cycle phase — eyebrow, title and body — which read as one loud block | Phase colour stays on the eyebrow and the icon ring; title and copy use the standard text styles |

Verification: all five XAML files parse, every `loc:` key in them resolves in EN and RU, and Core remains at 1030/1030.
No UI build by the agent; the seven-cell strip, the onboarding option and the nutrition hierarchy all need owner device review.

## Density pass — meal sections, workout list, food-log header, check-in (2026-11-02)

Owner review of the shipped build. Each finding was traced to a structural cause, not adjusted by eye.

| # | Surface | Cause found in source | Change |
|---|---------|----------------------|--------|
| D1 | Meal sections | Every logged food was a `CardSecondary` inside the meal `CardPrimary`: two 19pt insets stacked, so item text sat 19pt right of the meal header and each item cost ~38pt of pure padding. The empty-state row also carried `UiInsets4_2`, a third left edge. | Items are hairline rows inside the meal card, aligned to the header, with a 44pt touch target. Empty state margin removed so it shares the header's left edge. |
| D2 | Workout filters | Filter and week chips were canonical cards, so a 19pt card inset put every chip at ~86pt tall. | Chips are pill selection cells (44pt minimum, `UiRounding999`) with selected surface/stroke triggers. No third card style and no local card override, so the style budget still holds. |
| D3 | Session cards | `CardTitle` set a 44pt day tile, a heading-weight title and a bordered 48pt action row — a page heading inside a list row. | Title is `BodyStrong`, day tile 36pt with `CaptionSmallStrong`, both adjustments still share one row. |
| D4 | Food-log header | One `CardPrimary` held date, calories, macros, water, diary shortcut and notes: six rows declared on a six-auto grid, so the card was ~500pt and mixed unrelated jobs. | Split into three cards — summary (numbers + macros), day actions (water + copy yesterday), notes. The consumed calories are now `MetricValue` with the goal as a caption beneath instead of an inline "706 / 2000 kcal" run-on. |
| D5 | Macro numbers | Consumed value and its `Target:` line used identical `CaptionLabel` weight. | Consumed value is `BodyStrong`, target stays a muted caption. |
| D6 | Check-in | Rating groups used `UiSpacing10` row gaps inside `UiSpaceControl` stacks, leaving the 1–5 scale floating in loose air. | Row gaps tightened to `UiSpacing6` and group spacing to `UiSpacing14`. |

Verification: all five files parse, every `loc:` key resolves in EN and RU, Core 1030/1030.
Not changed by design: the 1–5 and pain chips are `ButtonSecondary`, whose height is `UiControlHeight` (48) and whose selection states live in the shared style. Shrinking them needs a shared selection-cell style rather than a per-page override, which the button-style test forbids.
No UI build by the agent; device review still required.

## Correction — workout chip row, one shared chip style (2026-11-02)

Owner feedback on the previous pass: the filter row collapsed to intrinsic-width pills bunched at the left instead of spanning the screen, so revert that page work and make the block fill the width. The revert could not restore the old markup: it referenced `SelectableCard`, which `UiCleanlinessAndReachabilityTests` forbids by name, so it would have failed the suite. The chip appearance therefore moved into the styles file as one definition.

| Aspect | Before this pass | Now |
|--------|------------------|-----|
| Chip appearance | Restated inline on every chip: background, stroke, stroke thickness, shape, padding and minimum height repeated in both templates, and again on the food-log day cells | `ChoiceCell` (surface, 1pt `ControlBoundary` stroke, `UiRoundRectangleRadius999`, `UiInsets10_6`, 44pt floor) and `ChoiceCellLabel` (centred caption, muted → primary when selected) in `Styles.xaml`. Selected and unselected appear once, in the style, including the accessible hint. |
| Row width | `FlexLayout.Basis="Auto"` with wrapping, so four labels sat at their intrinsic widths and left the rest of the row empty | `Basis="0"` + `Grow="1"` per cell: the row always fills the content width and every cell is exactly the same width, for any label length or cell count. |
| Week cells | A fixed four-column grid addressed by `Grid.Column="{Binding Index}"` — and `Index` is an absolute week number, not a column, so the cells landed outside the grid | Same flex row as the filters; the column binding is gone. |
| Label weight | `MutedLabel` at body size, which overflowed the narrower cells | `ChoiceCellLabel` at caption weight, centred, wraps instead of clipping. |

Verification: `xmllint` clean, Core 1030/1030, including the two-style budget and the no-local-override checks. `DESIGN.md` not edited: the chip family entry needs owner approval.

## Food-log summary now uses the dashboard nutrition card design (2026-11-02)

Owner: the day summary on the food log must look exactly like the current-day nutrition card on the dashboard. It was a second, invented design for the same four numbers — a flat calorie bar and three linear macro bars.

| Element | Food log before | Now (dashboard anatomy) |
|---------|-----------------|--------------------------|
| Calories | `MetricValue` + goal caption next to a full-width progress bar | 76pt `DashboardProgressRing` around the fire icon, `Dashboard_Nutrition_Calories` label, `MetricValue` consumed, muted goal beneath — the same control, size, icon and type tiers as `DashboardNutritionCard` |
| Macros | Three linear `ProgressBar`s with value and target stacked inside each column | Three 60pt `DashboardProgressRing`s with the same 18pt line icons and Protein/Carbs/Fat ring colours, label above the value, value at `CaptionSmallStrong` in the macro's own colour |
| Grid | `ColumnSpacing UiSpacing14` | `UiSpacing6` on the macro row and `76,*` with `UiSpaceControl` on the calorie row, matching the dashboard |
| Date | `SubHeadline` date and remaining text inside the four-column header grid | Kept as the card's own header above the rings, since the food log is a dated view |

One deliberate difference: the dashboard prints macros as one tiny `40g / 130g` line from `MacroProgressFormatter.FormatGramsText`; `FoodLogViewModel` does not inherit `NutritionViewModel`, so the log keeps the value line plus its `Target: 130g` caption rather than losing the target. Collapsing it to one line needs a formatter property on the log view model.

The ring control is reused, not copied: no new control, style or colour token was introduced; the page gained only the `dashboard` namespace.
Verification: `xmllint` clean, EN/RU keys resolve, Core 1030/1030.

## Audit against the Soft Minimal handoff (2026-11-02)

Reference: `http://localhost:4173/#handoff`, direction A "Soft Minimal". Token comparison:

| Handoff token | Handoff value | App value | Verdict |
|---|---|---|---|
| PageBackground | `#FFF9FB` | `PageBackground` `#FFF9FB` | match |
| CardSurface | `#FFFFFF` | `CardSurface` `#FFFFFF` | match |
| CardSurfaceSecondary | `#F7EAF0` | `CardSurfaceSecondary` `#F7EAF0` | match |
| TextPrimary / TextSecondary | `#2B1D24` / `#5F4550` | same keys | match |
| ActionPrimary / ActionSecondary | `#88435F` / `#67444F` | `ActionPrimaryLight` / `ActionSecondaryLight` | match |
| Warning | `#7B4E00` | `WarningAccentLight` `#7B4E00` | match |
| Error | `#A72E27` | `ErrorLight` `#A72E27` | match |
| Success | `#2F7D4D` | `SuccessLight` `#2C7548` | deliberate: darkened for 4.5:1 on the card surface |
| ControlBoundary | `#A47F8D` | `ControlBoundary*` `#96707F` / `#C9A6B4` | deliberate: `#A47F8D` misses the handoff's own 3:1 non-text floor; the darker value meets it |
| Cards | 16 radius, 19 padding | `CardPrimary` / `CardSecondary` | match |
| Buttons | 12 radius, 48 height | `ButtonPrimary` / `ButtonSecondary` | match |
| Families | one treatment, at most one extra style | chip: `ChoiceCell` + `ChoiceCellLabel`; day strip: `DayStripCell` | within budget |

### In-place selection styling found and unified

1. `QuizPage` and `ProfileWorkoutPreferencesPage` each restated `CardSecondary`'s own selected state (surface, stroke, selected hint) and only differed by using a 2pt stroke. Both local trigger blocks removed; selection now comes from the card style alone.
2. The food-log day strip carried its whole anatomy inline — 14pt shape, hairline stroke, surface, padding, minimum height plus a selected trigger. Moved to `DayStripCell` in `Styles.xaml`; the page cell is now `Style` plus its column binding.
3. The two cycle quick-log toggles were private skins (radius, zero stroke, 48pt, padding, grey surface) with no style at all. They now take `ChoiceCell`.

### Remaining, and why they stay

`MealEntryModal` (4), `ProfileNutritionSettingsPage` (4), `ProfileSetupPage` (2) and `ProfileUnitsDisplayPage` (2) project a boolean flag (`IsEntryBreakfastSelected`, `IsVegetarian`, ...) rather than `IsSelected`, so the style's own trigger cannot fire. `CardSecondary` documents this case and sanctions a local trigger that reuses the `Common_Selected` / `Common_NotSelected` keys. The cycle toggles still project `IsPeriodStarted` the same way. Fully removing those means giving the view models an `IsSelected`-shaped surface — a view-model change, not a XAML one.

`CyclePhaseExplorerCarousel` reuses the selected surface as the "Now" badge colour; that is a badge, not a selection state.

Verification: `xmllint` clean on all four edited files, Core 1030/1030 (two-style budget and no-local-override checks included).

## Cards of different widths inside one list (2026-11-02)

Owner screenshot: the notes card is visibly narrower than the breakfast and lunch cards on the food log.

Cause, in the container chain rather than in either card: a `CollectionView` insets nothing by itself. The header `VerticalStackLayout` carried `PresentationPageContentPadding` (20 left and right), while the item template carried only `PresentationCollectionItemMargin` (`0,0,0,8`). So header cards were inset 20 and item cards sat flush — a 40pt width difference between two cards in the same scroll view. `NutritionPage` had exactly the same shape, which is why the two screens disagreed with each other as well.

| Token | Value | Owns |
|-------|-------|------|
| `PresentationCollectionHorizontalInset` | `20,0,20,0` | the collection container, once |
| `PresentationCollectionHeaderPadding` | `0,8,0,32` | vertical rhythm inside the header only |
| `PresentationCollectionItemMargin` | `0,0,0,8` | vertical rhythm between items only |

Both nutrition screens now put the inset on the `CollectionView` (`Margin`) and keep horizontal padding out of the header, so header cards and item cards share one left and right edge by construction, not by matching two literals.

Verification: `xmllint` clean, Core 1030/1030.
Not yet on the shared token: `WorkoutPage` and `WorkoutHistoryPage` inset their cards with page-local `UiInsets20_0_20_32` / `UiInsets20_0_20_12` margins. Their edges already agree with their headers, so they do not show the defect, but the numbers are still restated per page.

## Sheets open at their content height, capped (2026-11-02)

Owner: every modal should open exactly the space its content needs.

Cause: a sheet sized by its root grid. `MealEditorModal` and `MealDetailModal` declared `RowDefinitions="Auto,Auto,*,Auto"`; the star row makes the sheet claim its `MaximumHeightRequest` whatever the content, so a two-line confirmation opened as tall as the screen. Every other sheet had no ceiling at all, so content that grows (large text, a long ingredient list) could run past the screen edge.

| Layer | Owner | Value |
|---|---|---|
| Sheet ceiling | `MaximumHeightRequest` on the `ModalSurface` | `ModalSheetMaximumHeight` (640) |
| Body | `ModalSheetBody` (ScrollView) on an `Auto` row | `ModalSheetBodyMaximumHeight` (520) |

Both `*` body rows became `Auto` with `ModalSheetBody`, so a short sheet is short and a long one scrolls under the ceiling. The ceiling was applied to all twelve content sheets; `WorkoutSessionPage` stays full-height deliberately (it is a session overlay, not a dialog). `WorkoutPage`'s session sheet had its own `520` and `360` literals; both now use the shared tokens.

**Correction 2026-11-03:** The two Nutrition sheets use an explicitly sized surface (`BottomSheetSizing.Apply`) and grew enough content that the `Auto` body plus footer could exceed the ceiling; the meal-detail actions were cut off in the owner screenshot. `MealEditorModal` and `MealDetailModal` again use one star body row inside the fixed-height surface, with the body as the scroll owner and the footer outside it. `MealEditorModal`'s ingredient collection owns the remaining body height; `MealDetailModal` scrolls its content above the pinned Back/Use this meal footer. The Food Log editor uses `Auto,*,Auto` for the same pinned-footer outcome. Short sheets still use the content-fit `Auto` anatomy; this is the explicit-height exception, not a second sheet style.

Verification: all files parse, Core 1030/1030. No device check.

## State audit — busy unified, error already consistent (2026-11-02)

Inventoried every state surface in the app rather than sampling.

**Busy.** 30 `ActivityIndicator` elements across 20 files and **not one had a style**: four different sizes (none, 24, 28, 48), some with `Color=Primary` and some without, and four different margins (`0,24`, `0,16,0,0`, none). Now two roles in `Styles.xaml` — `StateBusyPage` (24pt, primary, `0,24`, centred) for a page waiting on data, `StateBusyInline` (18pt, primary, centred) for a footer or a control. All 30 take one; 0 unstyled remain, and the restated colour/size attributes were deleted rather than left to override the style.

**Error.** Already consistent: 25 uses of the `ErrorLabel` style, handled structurally by `PageLoadingOverlay` and `PageLoadExtensions` for `IsLoadError`. The single raw `TextColor` error label — the account error on `ProfilePage` — now uses `ErrorLabel`, with its hand-written `AppThemeBinding` colour removed.

**Empty.** Now one copy pair for the whole app — `StateEmptyTitle` and `StateEmptyBody` — adopted by `WorkoutPage`, `NutritionPage`, `WorkoutHistoryPage`, `InjuryLogContent`, `QuizPage` and both meal-editor empty views (5 title labels and 5 body labels across six surfaces). The *container* still follows each surface (a `CardPrimary` in the workout list, a bare grid inside the meal editor), which is correct: an empty state is a message plus an optional action, and its alignment belongs to the surface it sits in. `PhotoJourneyVaultPage`'s empty view is image-only and needs no copy pair.

Verification: every touched file parses, Core 1030/1030.

# MuscleCuties design

Design entry point for every UI task. The detailed authority is [the presentation specification](docs/UI_PRESENTATION_DESIGN_SPEC.md); executable token values belong to the existing MAUI resource dictionaries. This file routes agents to those sources rather than maintaining another token catalog.

**Design authority changes by owner decision.** A change to this file or to the presentation specification is proposed in a PR with the evidence that motivates it and applied only after owner approval. `UiStyleGateTests` and `UiContractGuardTests` enforce token-only values, duplicate definitions, catalog copy and declared contrast pairs; run them for every UI change.

## Overview

**Direction: a warm, reassuring wellness companion with capable fitness tools.** Confirmed by the owner on 2026-11-02. Preserve the current soft rose, warm neutral and phase palettes. Borrow the supplied references' information hierarchy, direct logging, compact data rows and clear choice flows.

**Approved visual values:** the Soft Minimal palette, type scale and geometry (owner-approved 2026-11-02) are implemented in the resource dictionaries with dark pairs and component states. Changing those values needs a new owner decision, not a reinterpretation.

**Hard consistency budget:** one shared page style across the app; at most two visual styles per component family. Buttons: `ButtonPrimary` and `ButtonSecondary`. Cards: `CardPrimary` and `CardSecondary`. Use one style by default; a second needs a distinct reusable purpose. No per-feature skins, third-style variants or renamed lookalikes. The former `CardAccent`/`SelectableCard` definitions are retired, and the card-budget assertion rejects any new Card-family Border style beyond the two permitted cards.

**Sanctioned colour exceptions.** A label may set `TextColor` inline only when it encodes a semantic role its typographic style does not own — accent emphasis, warning, success, inverse-on-accent, muted de-emphasis, or primary emphasis — and no sanctioned style provides that exact typographic role with that colour. It may never repeat the colour its own style already sets: that is a duplicate and is removed (one such duplicate, an unstyled label on `StrengthPlanGenerationPage`, was converged away; the implicit label style already paints `TextPrimary`).

A shared style for these combinations is **not available**. The style-count limit above caps each component family at two visual styles, so adding `AccentValue`/`WarningValue`/`SuccessValue` label variants would breach it, and bending the limit to absorb twenty colour-by-typography combinations would recreate exactly the third-style proliferation the budget forbids. The exception is therefore explicit, enumerated and guarded rather than implicit.

| File | Sites | Roles |
|---|---|---|
| `Pages/Nutrition/FoodLogPage.xaml` | 7 | protein accent, carbs warning, fat success, calorie values accent, barcode status accent, day label primary, on-accent status toast |
| `Pages/Workout/WorkoutHistoryPage.xaml` | 5 | lifetime and per-session volume accent, RPE accent, on-accent status toast |
| `Pages/Cycle/CyclePhaseDetailPage.xaml` | 3 | warning section title and chevron, warning card title |
| `Pages/Workout/WorkoutPage.xaml` | 2 | phase name and rehab heading accent |
| `Pages/Profile/ProfilePage.xaml` | 1 | phase pill primary emphasis |
| `Pages/Workout/StrengthPlanGenerationPage.xaml` | 1 | required tag accent emphasis |

Rule: a new exception site must be added to this table in the same change that introduces it. `UiContractGuardTests.Inline_text_colours_match_the_documented_exception_list` fails when the per-file counts drift from this table. It also resolves each site's applied style — including `BasedOn` chains and the implicit label style — and fails when an override repeats the colour pair that style already sets, so the table cannot drift into duplicate colouring. `Page_and_control_xaml_use_tokens_for_dimensions` keeps numeric sizes out of page and control XAML, and the card-budget assertion accepts no Card-family style beyond `CardPrimary` and `CardSecondary`.

Support women through training, nourishment, recovery and cycle awareness with respectful, adult language. Warmth comes from considerate flows, useful guidance and breathing room. Strength training remains central; cycle and injury context must preserve the domain safety rules in `AGENTS.md`.

## Expression

**One posture: Operate.** Every screen exists to complete a task. Expression comes from precision — alignment, spacing, weight and restraint — not from decorative layout. A quiet screen must still be informative; do not fill absence with oversized surfaces.

**Density is stratified, not uniform.** Repeated records — food rows, set entries, history — are compact and scannable. Summary, loading and empty states are calm, with whitespace that frames the next action. Density never falls below readable text or the specification's touch minimums, and never rises by shrinking essential text.

**Variance is deliberately low.** A new screen should look like a member of the existing set. If a layout needs to be explained as "the special one", the design is wrong, not the budget.

## Colors

[Colors.xaml](src/MuscleCuties.UI/Resources/Styles/Colors.xaml) owns palette values and existing bootstrap resources. Use semantic light/dark pairs. Soft surfaces carry warmth; strong ink carries information; rose identifies actions and selection. Category and phase colors convey meaning with text labels. Keep the darker primary action fill that provides readable button text.

**Role-only colour use.** Raw palette names (`Gray300`, `White`, `Black`, `DeepMauve`, …) are definition-only: no XAML, converter, view model or shared style outside `Colors.xaml` may resolve one. Consumers reference an intent-named role — `Surface…`, `Text…`, `Border…`, `Track…`, `Accent…` or a state role — whose light and dark values exactly match the palette value it replaces, so a migration cannot change rendering. `UiContractGuardTests.Value_shaped_colour_tokens_are_definition_only` fails on a consumer reference and on a newly added raw token. Exceptions, left as-is deliberately: `Transparent` is a structural sentinel rather than a colour choice; brand/action roles (`Primary`, `PrimaryDark`, `Secondary`, `SecondaryDarkText`, `Tertiary`, `Action…`, `Warning…`, `Error…`, `Success…`) already state intent; phase, readiness, recovery, water, sleep and macro palettes are semantic category roles. The frozen raw definitions remain for compatibility but no consumer resolves them.

## Typography

Use existing named text styles through `AppFontFamily`. The current `Inter` alias loads **Open Sans**, not Inter. Do not change the font or alias incidentally. Text must scale and wrap; dense references do not justify tiny essential information. The specification distinguishes today's type scale from the proposed typography migration.

## Language and copy

Every visible or accessible string resolves through the EN/RU catalogs in `Services/Localization`; no user-facing English literal belongs in XAML or in a ViewModel property. Markup uses `{loc:LocalizedText Key}` and C# uses `UiTextCatalog.Get("Key")`. An unresolved key is a defect, not a fallback: `Get` returns the identifier itself, so markup pointing at a key no catalog defines renders raw text such as `Nutrition_QuickAdd_Title` and announces it that way through VoiceOver. Check resolution whenever copy or markup changes, and keep the two languages key-identical.

Named keys belong to the family partials (`UiTextCatalog.<Family>.cs` and `RussianUiText.<Family>.cs`) registered by the matching `Register…Strings` method. The legacy `T…` hash keys are the generated XAML-derived set and are not a naming pattern for new copy. Reuse an existing key before adding one, and never mint a per-feature synonym for a state word that already exists.

Selection semantics belong to the shared card rather than to each page: `CardSecondary` owns the selected-state hint through its `IsSelected` trigger, so a choice surface binding `IsSelected` declares nothing itself. A surface whose choice state is not exposed as `IsSelected` projects it with a local trigger and reuses `Common_Selected` / `Common_NotSelected`; that projection is migration debt toward the shared contract, not a second design. Meeting the card budget never justifies a third card style.

**State a fact once per screen.** A fact — a privacy guarantee, a rest instruction, what comes next, a unit convention — appears in one place. Repeated reassurance reads as uncertainty and consumes the space that should hold the next action. Before adding a line, check whether the header, a card or an empty state already says it.

## Selection and status

**Selected** has one visual recipe per component family, and it comes from the style, not from the page. A screen must not express selection differently in two of its own groups: week pickers, filters and day tiles shown together are one language. Selection is always carried by more than colour — weight, stroke, indicator or explicit label.

**Status** — upcoming, in progress, complete, required — occupies a stable position and uses one treatment across screens. A status is a short label, never a sentence, and never tint alone. A qualifier such as "required" appears once per group, not on every row of the group.

## Layout

Present the user's next useful action before optional insights. Use compact labeled rows for repeated records, spacious grouping between tasks, and progressive detail. Keep the five existing Shell destinations.

Known context should not become a gate. In meal entry, the guessed meal and time are compact editable context before `Add food`; the four meal choices are label-only touch cells, not illustrated cards that push the entry action below the fold. A fuller chooser is warranted only when changing context genuinely needs more space. Every page uses the same page/header/body/optional-footer frame, surface, gutter and typography system. Content and navigation state vary; page styling does not. Respect real safe-area and keyboard insets.

**The shared frame composes the header.** Title, back affordance and the single contextual action come from the shared header; a page supplies content, not its own header composition. A title that needs two lines reflows without displacing or resizing the content beneath it, and the header action stays reachable when the title grows.

## Adaptation

**Large text and translation are design inputs, not verification.** Every layout is designed for the largest supported text size, the longer of English and Russian, and both appearances before it is reviewed as finished. No essential label sits in a fixed-width column, in a single-line container that cannot grow, or beside an element with a fixed size. Where a grid genuinely cannot adapt, its documented fallback is part of the design.

## Elevation & Depth

Use the two permitted card styles for meaningful groups. Prefer rows and separators within a group over a separate card for every item. Reserve elevation for useful grouping and overlays. Consolidate existing extra card styles and their callers together rather than extending them.

## Shapes

Reuse shared card, input, choice and sheet shapes. Value-shaped compatibility tokens are available for existing consumers; new components need descriptive semantic roles rather than another arbitrary dimension.

## Motion

**Motion explains state; it never decorates.** Native transitions are the default. Custom motion is reserved for state the user just caused — selection, save, sheet enter and exit — short enough to read as feedback, with a reduced-motion branch that carries the same information statically. If the information exists only in the animation, the design is incomplete.

**Shared sheet lifecycle.** `AnimatedModalView` owns the sheet contract. It captures the focused control before covering it, moves focus to the first actionable control after layout, excludes covered sibling content from the accessibility tree through a page-scoped isolation shared by overlapping sheets, announces the `ModalTitle` heading once, and restores isolation and focus on close. A monotonic transition generation makes a hide that loses a race with a reopen a no-op. All modal sheets present through this base. The only non-sheet exception is the non-dismissible day-overview state in `WorkoutSessionPage`: it has no close or backdrop action, is not an overlay entry point, and is not a second sheet style.

## Components

Reuse `Controls/Shared` and the existing feature controls before adding a component. [Styles.xaml](src/MuscleCuties.UI/Resources/Styles/Styles.xaml) owns shared presentation styles. The specification defines screen recipes, state coverage, accessibility, motion, localization and architecture contracts.

**Nutrition presentation seam.** Every Nutrition presentation file — pages, controls, code-behind, converters, UI models, view models, presentation services and the Nutrition navigation contract — consumes UI-local mirrors such as `FoodLogEntry`, `MealTemplate`, `RecipeSummaryItem`, `NutritionMealSummary` and `NutritionCyclePhase`, never a Core/UseCases nutrition data contract. `Services/Nutrition/NutritionPresentationMapper` (including its partial files) is the sole adapter and the only justified boundary; requests are mapped at the call site and responses immediately. The Core `4/4/9` calorie conversion stays in Core. `UiContractGuardTests.Nutrition_presentation_does_not_reference_core_or_use_case_contracts` scans the full inventory and excludes only mapper files; no contract is stored in a view-model property or bound by XAML.

Every block follows the specification's component catalog and measurable readability gate: consistent anatomy, typography/spacing roles, clear interactions and complete states. Normal text meets 4.5:1 contrast; qualifying large text and essential non-text indicators meet 3:1. Preserve scalable text, readable supporting copy and reachable touch targets in each appearance.

Nutrition views consume the UI-local macro/target models above; the mapper is the seam. Do not bind a Core or use-case contract directly, and do not recompute macro calories in a view or presentation model.

## Do's and Don'ts

- Read this file, the relevant section of the specification (not the whole file) and the nearest existing component before editing.
- Keep design in the UI layer, formatting in `.editorconfig`, business decisions in Core, orchestration and DTO contracts in UseCases.
- Reuse tokens/styles; do not add page-local styles, raw colors or unexplained geometry. Keep XAML-bound contracts intact.
- Route copy and selection semantics through the catalog and the shared styles; a per-page literal, ad-hoc key or restated state word is a defect to fix, not a local convention.
- Keep one expression per screen: one dominant action, one selection recipe, one status treatment. A screen needing a second of any of these needs a design decision, not another style.
- Design for large text and the longer language first; if a label cannot grow, the layout is the defect.
- Verify with the UI guard tests first. Simulator build, run and screenshots are allowed (`AGENTS.md` §4); take screenshots only for the final check. Device acceptance belongs to the owner.

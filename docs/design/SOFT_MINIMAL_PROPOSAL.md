# MuscleCuties visual direction: Soft Minimal

**Status: approved by the owner on 2026-11-02 and implemented in the resource dictionaries.** Owner handoff received 2026-11-02. This document preserves the selected prototype direction and its recorded contrast findings; the executable values now live in `Colors.xaml`/`Styles.xaml`. [The presentation specification](../UI_PRESENTATION_DESIGN_SPEC.md) remains the app-wide contract, and [the implementation evidence](SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md) records what was built and verified. The two contrast issues below were resolved by darkening the shared success foreground and control boundary while keeping every other supplied value unchanged.

## Intent

A calmer companion: breathing room, gentle tonal grouping and quiet rose cues. Preserve capable training, clear nutrition logging, supportive language and the existing five-destination navigation.

## Proposed light color tokens

These are the owner's supplied values, retained unchanged. They are conceptual prototype roles, not a new production resource catalog. Dark pairs and component/state assignments are not yet approved.

| Role | Proposed value |
| --- | --- |
| PageBackground | `#FFF9FB` |
| CardSurface | `#FFFFFF` |
| CardSurfaceSecondary | `#F7EAF0` |
| TextPrimary | `#2B1D24` |
| TextSecondary | `#5F4550` |
| ActionPrimary | `#88435F` |
| ActionSecondary | `#67444F` |
| ControlBoundary | `#A47F8D` |
| Success | `#2F7D4D` |
| Warning | `#7B4E00` |
| Error | `#A72E27` |

## Proposed typography

Font: **Open Sans**, through the current `AppFontFamily` resource. Source registration is `fonts.AddFont("OpenSans-Regular.ttf", "Inter")`; the historical `Inter` alias does not mean an Inter font asset is registered. Preserve the established alias unless a separately scoped migration changes all consumers and source contracts together.

Values below are retained in the handoff's **px** notation. Their mapping to MAUI logical sizing and scalable text must be settled before implementation; do not infer physical-pixel dimensions from this browser specification. Semibold and tabular numerals are requested treatments, not verified current font capabilities.

| Role | Supplied size | Weight / use |
| --- | ---: | --- |
| Page title | 27 px | Semibold |
| Section title | 18 px | Semibold |
| Card heading | 16 px | Semibold |
| Body | 15 px | Regular |
| Supporting text | 13 px | Regular |
| Button label | 14 px | Semibold |
| Workout number | 24 px | Semibold, tabular numerals |
| Caption | 12 px | Semibold; short metadata only |

If approved, this exact type scale replaces the earlier pending suggestion to increase reading body to 17. It does not change the existing runtime scale merely by being recorded here. Use one shared scale everywhere; no feature-specific typography system.

## Proposed geometry and rhythm

- Shared spacing scale: **4, 8, 12, 16, 24, 32, 40 px**. Use larger steps between content sections.
- Page gutter: **20 px** on compact phones; **24 px** when space allows. This is a single adaptive frame, not a second page style. The shared width rule is still to be specified; do not invent a breakpoint per page.
- Cards: **CardPrimary and CardSecondary only**, both with **16 px corner radius and 19 px internal padding**. The explicit 19 px card inset is a component-owned value, not permission to expand the spacing scale arbitrarily.
- Buttons: **ButtonPrimary and ButtonSecondary only**, both with **12 px corner radius and 48 px minimum height**. Minimum height must grow with content and text scaling.
- Other component families: one default treatment, at most one additional reusable style. No renamed families, feature skins or state aliases that evade the limit.
- Relaxed grouping and clear task separation. Schedule/history items are labeled rows grouped within a permitted card, not a new card style per feature.

The two cards share geometry; their permitted surface roles provide gentle tonal grouping. Exact primary/secondary button fill, foreground, border and state assignments remain to be confirmed. Two action colors alone do not define complete button treatments.

## Shared page and navigation

Every screen uses one page frame: app background, readable header, consistent adaptive gutter, scrolling body and safe-area-aware navigation/optional footer. Put the next useful action before optional insight.

Keep the existing Shell order: **Today, Cycle, Train, Nutrition, You**. Use labels with icons. Selected navigation uses weight and a visible accent rather than color alone. Preserve native navigation behavior, back gestures and meaningful accessible selection state.

## Interaction and states

- Pressed, selected, focused, disabled, loading, success and error are states of permitted styles, never feature-specific skins.
- A disabled action has a readable nearby explanation. Save failure preserves typed input and provides a direct retry path.
- Workout numbers, repetitions, units and completed state stay together. Announce important completion once.
- Honor Reduce Motion. Actions remain reachable by keyboard and assistive technology; sheets follow the shared focus-entry, isolation, dismissal and focus-return contract.
- Preserve view models, commands, bindings, localization, business rules and API contracts. UI styling must not alter strength-plan safety or health-data privacy boundaries.

## Accessibility and readability

- Normal text: at least **4.5:1** against its actual composed surface. Qualifying large text and essential non-text marks: at least **3:1** against adjacent surfaces.
- Keep supporting text readable, keyboard focus visible, field labels persistent and touch targets at least **44 px** in the prototype. Native unit translation remains an implementation gate.
- Allow rows to wrap, controls to grow and the screen to scroll with enlarged text. Never shrink or clip essential actions to preserve the default layout.
- Pair color with a label, shape or position. Status meaning remains understandable without hue.
- Check each appearance and functional state, including transparency and overlays. A palette calculation is evidence about that pair, not rendered or runtime certification.

## Source-color contrast review

Computed from the supplied opaque sRGB colors using the WCAG relative-luminance formula. Ratios below are rounded for display; pass/fail uses the unrounded value. No production UI was built, run, tested or captured.

| Foreground | PageBackground | CardSurface | CardSurfaceSecondary |
| --- | ---: | ---: | ---: |
| TextPrimary | 15.49:1 | 16.10:1 | 13.79:1 |
| TextSecondary | 8.23:1 | 8.56:1 | 7.33:1 |
| ActionPrimary | 6.70:1 | 6.96:1 | 5.96:1 |
| ActionSecondary | 8.03:1 | 8.34:1 | 7.15:1 |
| Success | 4.85:1 | 5.05:1 | **4.32:1 — below normal-text floor** |
| Warning | 6.89:1 | 7.16:1 | 6.13:1 |
| Error | 6.59:1 | 6.86:1 | 5.87:1 |
| ControlBoundary | 3.37:1 | 3.50:1 | **2.9976006:1 — below essential non-text floor** |

White on ActionPrimary measures **6.96:1**; white on ActionSecondary measures **8.34:1**. These are measured candidate pairs, not approved button foreground assignments.

Two issues must be resolved in the treatment specification before production approval:

1. **Success on the secondary card:** retain readable primary/secondary text with a labeled success mark, or approve a revised shared status pair. The supplied Success color cannot be normal-size text on that surface at the stated 4.5:1 minimum.
2. **ControlBoundary on the secondary card:** do not round the ratio up to 3:1. It may be decorative where the boundary is unnecessary for recognition; an essential boundary/focus indicator needs a compliant approved pair.

Neither issue requires changing the soft visual direction. The owner's original token values remain intact in this proposal.

## MAUI translation and approval gates

This remains a specification. Browser selection and this recorded handoff do not authorize app edits.

Before a production migration:

1. Obtain explicit design approval covering the proposed values and resolution of the two contrast issues. Define dark pairs and required component states before changing production resources.
2. Settle logical-unit translation, the shared adaptive-gutter rule, semibold rendering and tabular-numeral support using the existing Open Sans asset/alias. Do not assume a semibold asset is registered or change the font incidentally.
3. Map conceptual roles into the existing `Resources/Styles/Colors.xaml` resource system rather than create a parallel palette. `ActionPrimary`, `ActionSecondary`, `ControlBoundary` and unpaired status names are not all current resource keys; preserve their actual consumers and light/dark responsibilities.
4. Keep the one page frame and two button/card roles in `Resources/Styles/Styles.xaml`. Update role-specific geometry deliberately; globally changing a legacy numeric primitive could affect unrelated consumers.
5. Consolidate extra card definitions, their XAML callers and the existing four-card source assertion together. Do not claim compliance while legacy extra looks remain.
6. Preserve all bindings, routes, localization and DTO boundaries. Follow the backend-only agent verification rule; owner device evidence covers final appearance, enlarged text, EN/RU, focus, Reduce Motion, safe areas and keyboard interaction.

Mapping notes from current source:

- `ButtonPrimary` currently combines `PrimaryDark`/`ActionSurfaceDark` with separate light/dark text. `ButtonSecondary` combines `AccentSurface`/`Gray900` with text, border and functional-state roles. Neither is replaced completely by a single conceptual action color.
- `CardSurfaceSecondary` is not currently a resource key; both current card styles use `CardSurface`/`CardSurfaceDark`. A distinct secondary surface needs an approved mapping within the two-card budget.
- Input boundaries use `InputStroke`/`InputStrokeDark`; choice and secondary-button boundaries have other roles. Do not globally replace all strokes with `ControlBoundary`.
- Feedback uses `SuccessLight`/`SuccessDark`, `ErrorLight`/`ErrorDark`, and separate `WarningSurface*`/`WarningAccent*` roles. Preserve text/surface responsibilities and both appearances rather than inserting unpaired status keys.

The current task records and reviews the specification only. No runtime resource, production layout, font registration, view model, command or API contract is changed.

Contrast references: [WCAG text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html), [essential non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html).

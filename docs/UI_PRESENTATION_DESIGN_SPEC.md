# MuscleCuties presentation specification

App-wide design and interaction authority, revised 2026-11-02 using the Impeccable critique, native iOS guidance, existing source and the owner's four references. The owner selected **more warm and wellness-focused**, preserving the existing soft palette. [DESIGN.md](../DESIGN.md) is the agent entry point; it does not define a second token system.

This revision establishes conventions and an implementation sequence. It does not claim that current screens implement every convention. The [source audit](design/AUDIT_2026-11-02.md) records gaps. No UI build, execution or screenshots were performed in this pass.

The owner approved the [Soft Minimal proposal](design/SOFT_MINIMAL_PROPOSAL.md) on 2026-11-02. It records the selected visual values; [DESIGN.md](../DESIGN.md) is the design entry point, and the [implementation evidence](design/SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md) records the source changes and resolved contrast findings. This presentation specification remains the app-wide contract for interaction, accessibility, component, and implementation conventions.

## 1. Authority and source ownership

| Concern | Owning source | Contract |
| --- | --- | --- |
| Operating directive and context routing | [AGENTS.md](../AGENTS.md) | Compact always-on rules and on-demand references. |
| Architecture and data boundaries | [Architecture](ARCHITECTURE.md) | Verified project map and API, client, and persistence boundaries. |
| Health and sensitive-data constraints | [Safety](SAFETY.md) | Fail-safe behavior, privacy, and security. |
| Approved product flows | [Product workflows](PRODUCT_WORKFLOWS.md) | Exact approved workout-planning rules. |
| Risk-based evidence and gate closure | [Verification gates](VERIFICATION_GATES.md) | G0–G9 definitions and evidence states. |
| Design discovery and direction | [DESIGN.md](../DESIGN.md) | Entry point linking to this specification. |
| App-wide UX/component conventions | This specification | Normative behavior and patterns, distinguished from implementation evidence. |
| Palette and initialization dependencies | [Colors.xaml](../src/MuscleCuties.UI/Resources/Styles/Colors.xaml) | Sole executable color authority and light/dark pairs; some shared font/geometry resources already live here for initialization order. |
| Roles, styles, presentation geometry | [Styles.xaml](../src/MuscleCuties.UI/Resources/Styles/Styles.xaml) | Reuse `Presentation*`, semantic spacing roles, text and component styles. |
| Resource composition | [App.xaml](../src/MuscleCuties.UI/App.xaml) | Colors before Styles; referenced resources must exist before use. |
| C# theme resolution | [AppThemeResources.cs](../src/MuscleCuties.UI/Resources/Styles/AppThemeResources.cs), existing feature palette adapters | Resolve named resources. Theme changes must refresh existing views/converters/drawables. |
| Font/icon registration | [MauiProgram.cs](../src/MuscleCuties.UI/MauiProgram.cs) | Agree with resource aliases and source contracts. |
| Reusable presentation/behavior | [Controls/Shared](../src/MuscleCuties.UI/Controls/Shared), feature controls | Shared header, sheet, metric, field, status and row behavior; no business policy. |
| Visible/accessible language | [Services/Localization](../src/MuscleCuties.UI/Services/Localization) | Existing EN/RU catalogs, including descriptions, hints and announcements. |
| Reference sources | [Reference manifest](design/REFERENCES.md) | Original attachments and the decisions they illustrate. |
| Findings and migration evidence | [Dated audit](design/AUDIT_2026-11-02.md), [STATUS.md](../STATUS.md), [ROADMAP.md](../ROADMAP.md) | Evidence/progress, not alternate authorities. |
| Engineering formatting/compiler rules | [.editorconfig](../.editorconfig), [Directory.Build.props](../Directory.Build.props) | Keep code formatting separate from product design. |

**Precedence:** owner instructions → semantic contracts here → executable resource values for implementation. If documentation and source disagree, record and reconcile deliberately; never replace palette values to match stale prose. Avoid duplicating all tokens in JSON, YAML, C# constants or design-tool exports. A future design library must consume a generated export or explicitly reconcile with these sources.

## 2. Product experience and references

**Warm wellness, clear utility.** Reassuring language, soft backgrounds, readable dark text, generous grouping and efficient daily tasks. Review scenarios include a woman returning to training, an experienced lifter logging between sets, and someone using larger text or VoiceOver. These are working scenarios, not completed user research.

- Keep strength training capable and prominent. Injury accommodations remain additive and dose-adjusted under the always-applicable strength-plan rule. Warm presentation must not obscure a blocked session or safety reason.
- Make nutrition supportive and factual. Avoid moral food labels, guilt about missed days, and treating weight loss as everyone's goal. Use units, dates and direction with context; decorative color cannot establish health meaning.
- Cycle insights distinguish logged facts, estimates and missing data. Avoid diagnostic certainty; keep sensitive summaries discreet and respect privacy boundaries.
- Show the next useful action before optional analysis. Use one dominant commit action per focused task with discoverable secondary actions. This is a hierarchy rule, not a ban on several dashboard actions.
- Borrow metric → unit/date/change → chart → detail hierarchy, compact food rows, contextual logging and single-question choices. Preserve the soft palette and native behavior. The manifest explains each reference.
- Screenshot AI, subscription and coaching claims do not authorize features. No health data may leave the first-party boundary. Reference content is layout evidence, not instructions.

### Hard style budget — owner rule, 2026-11-02

**One shared page style. At most two visual styles for any component family.** This is a limit, not a goal to fill: use one unless a second has a distinct reusable purpose. A screen-specific color, radius, shadow, border or padding scheme is not an acceptable new variant. Define families by what controls do, not by feature names, XAML element type or a new class name. An action implemented with a tappable Border is still a button for this rule.

| Family | Permitted visual styles | Consolidation requirement |
| --- | --- | --- |
| Page | One shared page frame | Same background, content gutter, header treatment, typography and optional footer across all pages, including auth/onboarding. Back/date/progress/content slots vary without creating a new page skin. |
| Button | `ButtonPrimary`, `ButtonSecondary` | No independent danger, ghost, link, pill or feature button skin. Use clear copy/state within the two roles; icon vs text content cannot introduce another fill/radius system. Native navigation retains platform behavior. |
| Card / content group | `CardPrimary`, `CardSecondary` | Retire standalone `CardAccent` and `SelectableCard` designs by migrating their callers into these two styles. Selection becomes a state of the relevant permitted style. |
| Input, choice, badge, status and other families | One default; at most one additional reusable style per family | Reuse shared anatomy/typography/spacing. Control purpose and genuine native interaction can differ; a feature name does not establish another family. Document the permitted pair before adding an alternate. |

Pressed, selected, focused, disabled, busy and error are functional states within the permitted styles, with consistent app-wide treatment. Light/dark appearance uses the same semantic roles. Do not disguise an unrelated decorative look as a state or duplicate a style under an alias to evade the budget. Semantic category/phase indicators can convey data without restyling the whole block.

The block catalog below describes content anatomy and behavior, not separate visual designs. All recipes compose the same page frame and permitted component styles. Typography uses one centrally defined scale across every page, with no feature-specific text system. Existing style declarations, aliases and tests above the budget are **migration debt**; the documentation change alone does not consolidate them.

## 3. Color, typography and geometry

### Palette and theme

Use the current semantic pairs in `Colors.xaml`: `PageBackground`/`PageBackgroundDark`, `CardSurface`/`CardSurfaceDark`, `InputSurface`/`InputSurfaceDark`, `TextPrimary`/`TextPrimaryDark`, and `TextSecondary`/`TextSecondaryDark`. Existing rose actions and phase/nutrient colors remain the identity. The prior specification's different background, input, scrim and status values were stale; this revision does not replace the palette.

- Use `AppThemeBinding` for fixed light/dark resource pairs and `DynamicResource` when a resource can actually change at runtime. Do not replace static references mechanically. C# consumers use the existing resolver plus a lifecycle-aware theme refresh where needed.
- `ButtonPrimary` uses `PrimaryDark` with white text in light appearance, and `ActionSurfaceDark` with `TextPrimary` in dark appearance. Do not substitute the lighter brand token without checking contrast.
- Selection needs surface, indicator/boundary, text and accessible state as one contract. A converter returning a light-only surface is insufficient in dark appearance.
- Target at least 4.5:1 for normal text, 3:1 for qualifying large text. Essential boundaries/chart marks need 3:1 against adjacent colors where required for recognition. Check composed colors, including transparency, in each appearance. Color cannot be the only signal.
- Feedback colors express actual state, not a good/bad judgment on food, weight or adherence. Pair them with labels and next steps.

### Measurable contrast and readability gate

Apply these rules to **every block**, including metadata, placeholders, helper text, inputs, errors, selections and charts. Soft color is a surface treatment; it cannot weaken readable content.

| Content | Required floor | Review rule |
| --- | --- | --- |
| Normal text, field labels, helper/placeholder text, values, units and validation | 4.5:1 against the actual background | Aim for 7:1 for primary reading text where feasible; never lower support-text opacity until it becomes illegible. Placeholder text does not replace a persistent label. |
| Qualifying large text | 3:1 | Use the WCAG large-text exception only when actual font metrics/weight qualify. Default to the normal-text floor when uncertain; a style named `Headline` is not proof. |
| Essential icons, selected/focus indicators, chart lines/marks and boundaries required to identify a control | 3:1 against adjacent colors | Pair color with text, shape, stroke or position. Decorative separators do not carry essential meaning. |
| Disabled controls | Inactive controls have a WCAG contrast exception | Keep the explanation of the unmet prerequisite readable at the normal-text floor. Do not dim the entire explanation or screen. |

Check default, pressed, selected, focused and error states in light and dark appearance, and when appearance changes while the block is visible. Measure foreground against the composed surface after opacity, tint and overlays. Record the token pair, state and ratio; do not round a failing ratio into a pass. Source calculation is useful evidence, while owner review checks text scaling, font rendering and actual state composition.

Body and instructions are leading-aligned, use sentence case and wrap naturally. Values and units remain visually and semantically associated. Align comparable numeric columns consistently; use the existing unit/date/number formatters. Never communicate a health warning only with hue, tiny text or an unlabeled glyph. Reserve uppercase for short optional metadata. Avoid justified paragraphs, dense centered instructions and truncation of essential guidance or actions.

At the largest supported accessibility text sizes, allow stacked rows, taller controls, fewer columns and vertical scrolling. Preserve content and task completion rather than preserving the default composition. A narrow screen, long Russian label, keyboard and enlarged text are acceptance cases for each affected block. Do not disable font scaling or add a fixed text height to force consistency.

### Typography

The runtime family is **Open Sans** registered under the historical alias `Inter`; `AppFontFamily` owns consumption. Existing source tests enforce that alias. Retain compatibility until a dedicated migration updates registration, resource, callers and tests together. Do not describe the shipped face as Inter or add a display face incidentally.

Current role baseline from `Styles.xaml`:

| Role | Current implementation | Usage rule |
| --- | --- | --- |
| `PageTitle` | 27, semibold | Screen identity; allow wrapping. |
| `SectionTitle`, `CardTitle` | 17, bold | Group hierarchy; do not head every row. |
| `BodyLabel` | 15 | Current body role; do not shrink essential content further. |
| `MutedLabel`, `CaptionLabel`, `FormFieldLabel` | 13 | Supporting text and field labels with sufficient contrast. |
| `EyebrowLabel` | 11, bold | Short supplemental metadata only. |
| `MetricValue` | 24, bold | Pair with readable unit, period and meaning. |
| Primary/secondary button text | 15, bold | Short explicit action labels. |

These default device-independent values do not justify disabling scaling. Prefer a 17-point reading body in the planned typography pass, retaining compact metadata only where readable. That change is **pending**, not applied globally by this document. Fixed-height cards, single-line labels and fixed columns must not defeat accessibility sizes. Never fit essential text by shrinking it.

### Layout and shapes

- Target one page frame using shared page/content/header/optional-footer resources. `PresentationPageContentPadding` supplies the common content gutter. Existing form/auth inset differences are legacy implementation to reconcile with this shared frame, not permission for separate page styles. Sheets remain a presentation mode with their shared inset contract.
- The shared frame composes the header: a page supplies content, not its own header composition. A title that needs two lines reflows without displacing or resizing the content beneath it, and the contextual action stays reachable when the title grows.
- Large text and translation are design inputs. Design for the largest supported text size, the longer of EN/RU and both appearances before calling a layout finished: no essential label in a fixed-width column, a single-line container that cannot grow, or beside a fixed-size element. Where a grid genuinely cannot adapt, its documented fallback is part of the design.
- Base spacing: `UiSpaceMicro`, `UiSpaceTight`, `UiSpaceCompact`, `UiSpaceControl`, `UiSpaceContent`. Prefer these or a descriptive component role over another arbitrary value.
- Current implementation baseline: `CardPrimary` has an 18-point inset and 16-point radius; `CardSecondary` has a 14-point inset/radius; legacy `SelectableCard` radius is 14; inputs use `PresentationInputCornerRadius` (12). Buttons use `UiRadiusControl` (14). Only the two permitted card styles may remain after consolidation; do not propagate legacy shape differences.
- `PresentationMinimumTouchSize` is 44; `PresentationInputMinimumHeight` and the primary button minimum are 52. These are minimums, not caps for larger text. Small glyphs sit inside sufficiently large interactive targets.
- Use `CardPrimary` and `CardSecondary` only as the target card vocabulary. Preserve legacy bindings while migrating `CardAccent`/`SelectableCard` callers; then remove those definitions and update their existing four-card source assertion together. Use rows/separators within a meaningful group; no nested card shells or local surface-geometry overrides.
- Existing `UiDimension*`, `UiInsets*`, `UiFontSize*` and numeric radius resources are compatibility primitives. Do not bulk delete/rename them. New shared decisions receive semantic names describing purpose. A numeric resource name alone does not make a choice coherent.
- Prefer Auto/star sizing for content and descriptive component-owned geometry for tables. Document a large-text fallback for set-entry/history grids. Width caps improve measure; they do not establish iPad/landscape readiness.

## 4. Screen and component patterns

Keep native Shell navigation and existing labeled destinations **Today, Cycle, Train, Nutrition, You**. Tabs are destinations. Push for hierarchy; sheet for a focused subtask. Preserve back gestures; do not copy reference navigation for decoration.

| Pattern | Required hierarchy | Starting point |
| --- | --- | --- |
| Today | Greeting/context → next useful action → compact daily summary → optional insights | Existing Dashboard cards; demote streak counters. Views do not derive new readiness/business decisions. |
| Training overview | Resume/current session → schedule → preferences/history | Workout controls; clear strength content and injury safety reasons. |
| Active session | Current exercise → set inputs → save/next → secondary swap/skip | WorkoutSessionPage; preserve timing/persistence. |
| Nutrition overview | Daily nourishment summary → direct logging → shared meal cards → optional planning/insights | Nutrition controls and `MacroProgressGrid`; one `MealCardView` block reused by Nutrition and Food Log. Logged meals show meal type, calories and daily-target share, name, ingredient summary, macros, **Edit meal** and the save-template star; no expand chevron. |
| Food logger | Calorie/macro summary → compact 2×2 meal/time context → add/suggest/template → scrollable ingredients → persistent commit | Add meal mounts the chooser with **Add meal**, **Suggest meal** and **Use from template**; template rows can be added or deleted. MealEditorModal keeps its summary, four choice cells, inline source switch, virtualized results and pinned footer. An existing meal replaces the editable context with one read-only meal/time line. Only implemented scan/custom-food capabilities; no invented AI mode. |
| Food log | Day summary → shared meal cards → ingredient rows only when a meal is not editable as one block | `MealCardView`; a single grouped meal shows the full block with **Edit meal**, while multi-item and single non-grouped sections keep their food rows. No expand chevron and no saved-template shelf on this page. |
| Insight detail | Value/unit → period/change → chart → range control → explanation/data equivalent | DashboardTrendCard's summary/data alternative; never fabricate a trend. |
| Cycle | Logged/estimated context → log action → calendar → optional explanations | Existing cycle components; preserve uncertainty/privacy. |
| Onboarding | One question/cohesive group → full-width choices → selected state → persistent Next | QuizPage; whole-row targets and visible/accessible selection. |
| Profile/settings | Cohesive labeled row groups → detail form → Save | Existing subpages; avoid a decorative card per setting. |
| Sheet | Title/dismiss → one scroll body → pinned commit footer → safe-area/keyboard clearance | Shared modal infrastructure, MealEditorModal/MealDetailModal/DailyCheckInModal footers; the Food Log editor and meal detail keep their footer outside the scroll owner. Repair shared accessibility/motion before wider reuse. |

**Headers and icons:** every page uses the same shared header treatment; root/detail state supplies back, date or context through properties without a second skin or copied navigation mechanics. Use the same semantic icon for the same action. Fluent is incumbent; do not add another library or replace the icon set incidentally. A planned semantic registry can unify existing paths/images/glyphs.

**Lists and charts:** virtualize long lists with `CollectionView`; avoid unbounded repeated records in `ScrollView`. Compact rows still require readable labels/units and touch targets. Charts need units, textual summaries and accessible data alternatives; color alone cannot identify a series. Range controls retain clear selection state.

**Sheets:** one scroll owner, with header/footer outside it when persistence is needed. Respect actual safe-area/keyboard insets; fixed padding cannot prove clearance. Support dismissal, unsaved-input handling, initial focus, background accessibility isolation, announcements and focus restoration. The shared `AnimatedModalView` establishes this lifecycle: it captures the invoking control, moves focus into the sheet after layout, excludes covered content from the accessibility tree, announces the `ModalTitle` heading once, and restores focus and isolation on close; a rapid reopen cannot let a stale hide restore old state. All modal sheets present through this base. The only non-sheet exception is the non-dismissible day-overview state in `WorkoutSessionPage`, which has no close or backdrop action and is recorded in the dated audit; it is not a second sheet anatomy.

### Block consistency contract

Every visible block maps to the catalog below and composes the same permitted styles. A new content recipe may document different anatomy, never a third visual style. A block declares purpose, anatomy, text roles, surface/spacing owner, alignment, interactions, states, accessibility semantics and compact-width/large-text fallback. The same role uses the same contract across features; domain content can differ.

| Block | Consistent anatomy and hierarchy | Existing owner / reuse rule |
| --- | --- | --- |
| Page header, root state | Title plus optional date/greeting/context; secondary utility actions in a stable location | The one shared page header; existing copied roots are consolidation work. |
| Page header, detail state | Back, screen title, optional contextual action | The same header with back enabled; preserve native gesture and accessible action names. Existing `HeaderIconButton` must follow the button budget. |
| Primary summary group | `CardTitle` → main content/value → optional short explanation → related action | `CardPrimary`; group padding/shape stay owned by its style. No competing hero inside another hero. |
| Supporting group | Optional `CardTitle` → related supporting information | `CardSecondary`; do not add `CardAccent` or another tinted content-block skin. |
| Metric / daily status | Value + unit → meaning/period → optional change/target → optional detail action | `MetricValue`, `BodyLabel`, `MutedLabel`, existing Dashboard metric controls. Missing data is not zero; distinguish measured, logged and estimated values. |
| Food / exercise / history row | Optional semantic icon/image → name → compact relevant detail → value or trailing action | Existing feature rows; shared text/spacing roles. Prefer rows within one group; row tap vs trailing action must have distinct, clear outcomes. |
| Setting / disclosure row | Label → optional current value/help → disclosure or control | Existing Profile controls and `DisclosureHeader`; same alignment/affordance in each settings group. |
| Choice row / filter | Label → optional explanation → selected indicator | Choice behavior composes a permitted style with selection state. Migrate legacy `SelectableCard`; no separate card design. Whole-row target and localized semantics. Selected has one recipe per family: sibling groups on one screen (week pickers, filters, day tiles) must not express it differently. |
| Form field | Persistent label → input/value + unit → helper or adjacent validation | `FormFieldLabel`, `InputContainer`/`PickerContainer`/`EditorContainer`, `ErrorLabel`. Do not clear valid input on unrelated errors. |
| Search | Visible query field → applicable filters/modes → results/count/empty/retry | `SearchContainer`, existing `CollectionView` rows. Keep the query/context during edit, back and interruption. |
| Nutrition progress | Nutrient name → current/target + unit → progress indication | Existing `MacroProgressGrid`/nutrition controls; keep nutrient color meanings and always label values. |
| Insight / chart | Summary value/date → chart → range control → explanation/data equivalent | Existing Dashboard chart controls; same units/ranges/selected-state treatment across insight views. |
| Inline status / safety notice | State/title if useful → explanation → relevant correction/retry | Shared text/feedback resources and existing state controls. Safety reasons remain visible; ordinary information does not look destructive. A status (upcoming, in progress, complete, required) occupies a stable position and one treatment across screens: a short label, never a sentence, never tint alone, and a qualifier appears once per group rather than on every row. |
| Loading / empty / failure panel | Honest state → short useful explanation → one recovery/entry action when available | Existing loading and error patterns. Keep local work local; do not substitute fabricated data or an unrelated illustration for explanation. |
| Action footer | Optional status/prerequisite explanation → dominant commit → clearly secondary action | `PresentationActionFooterPadding`, `ButtonPrimary`, `ButtonSecondary`; existing fixed footers. Buttons grow for text; keyboard/home indicator cannot cover the action. |
| Sheet | Shared detail header → body blocks → action footer when needed | `ModalSurface`, shared modal/sizing infrastructure; follow the focus, dismissal and reduced-motion contract above. |

Use `UiSpaceTight` for closely related elements, `UiSpaceCompact` within a group, and `UiSpaceControl`/`UiSpaceContent` between form groups/sections as appropriate. Shared component styles own insets; do not re-pad them at call sites. Before adding the second permitted style, document its reusable purpose and owner-validation requirements. When a family already has two, reuse or consolidate them; there is no third-style exception under this rule.

Consistency means stable meaning, anatomy and behavior within the hard budget. It does not require a card around every surface, a title on every block or equal heights that clip text. Preserve native controls for familiar interactions; consolidate repeated content needs into the permitted shared styles.

## 5. States, accessibility, motion and language

| State | Required behavior |
| --- | --- |
| Loading | Preserve orientation; local progress for local work. Prevent duplicate submission. |
| Empty | Explain missing data and provide a useful next action; display absence honestly. |
| Error | Actual failure plus actionable retry/correction; preserve input. Use existing API failure taxonomy/request reference where available. |
| Offline/stale | Distinguish current/saved from cached/unsaved. Never imply a failed save succeeded. |
| Disabled | Explain unmet prerequisites nearby; command gating and display agree. |
| Selected/focused | Indicator beyond color, accessible state, localized name/hint, sensible order. |
| Success | Brief task confirmation without another mandatory acknowledgment; undo where feasible. |
| Destructive | Clear consequences and intentional confirmation where needed; retain established account/privacy behavior. |

- Accessibility text is product copy. Localize descriptions, hints and announcements through existing EN/RU catalogs. Do not concatenate English state words onto localized text.
- Every localized reference must resolve. `UiTextCatalog.Get` returns the identifier when no catalog defines the key, so an unresolved `LocalizedText` or `UiTextCatalog.Get` reference renders and announces the raw key (`Nutrition_QuickAdd_Title`, `Common_NotSelected`) and is a release defect, not a benign fallback. EN and RU must remain key-identical.
- State wording is owned centrally. `CardSecondary` states the selected semantics for any surface binding `IsSelected`; a surface that projects its own selection reuses `Common_Selected` / `Common_NotSelected` and is recorded as migration debt. Do not answer a budget or semantics gap with a third card style or a per-feature state key.
- Label icon-only actions, group repeated rows sensibly and announce important asynchronous outcomes once. Semantic properties alone do not prove VoiceOver usability.
- Shared motion honors iOS Reduce Motion. Prefer native transitions; custom motion communicates state. Remove slide/scale and decorative movement in reduced-motion mode. New motion roles describe purpose rather than another duration. Current modal motion lacks this branch.
- Copy is concise, supportive and specific: action/object, nearby validation, optional detail. Avoid guilt, body stereotypes, unsupported coaching claims and clinical certainty. Check EN/RU expansion without assuming a universal length ratio.
- State a fact once per screen. A fact — a privacy guarantee, a rest instruction, what comes next, a unit convention — appears in one place; a header, a card and an empty state must not repeat the same claim in three tones.

## 6. Clean architecture and AI-agent workflow

Resources, MAUI types, brushes, icons, visual states and layout stay in UI. Core owns deterministic health/training rules; UseCases orchestrates and exposes DTOs; infrastructure adapts storage/network services. Shared controls never call a repository/database. ViewModels adapt DTOs into presentation state and do not recreate domain math to choose an attractive screen.

For every UI task:

1. Read `AGENTS.md`, `DESIGN.md`, relevant specification sections and the nearest component. Search targeted symbols/XAML bindings first.
2. Use the one page frame and identify the content recipe. Reuse the permitted styles/controls. Check the family budget before adding any role; no local override, alias or renamed family may introduce a third look.
3. Keep layout/behavior in components, styles in resources, copy in catalogs and business conditions in their owning layer. No second palette/token file or page-local style system.
4. Cover affected states, both appearances, localized semantics and large text together. Preserve routes, bindings, commands, contracts and privacy/safety constraints.
5. Migrate one component/feature at a time. Prove zero XAML/C# callers before deleting or renaming a contract/resource. Retain compatibility keys until then.
6. Run appropriate backend/source checks only. The owner rule prohibits agents from building, running, testing or capturing the UI. Give the owner a precise device checklist when needed.
7. Update dated evidence and `STATUS.md`/`ROADMAP.md`. Distinguish implemented, source-checked and owner-validated rules. An empty web detector result is not native UI assurance.

Existing checks live in [UiCleanlinessAndReachabilityTests.cs](../tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs) and [CleanArchitectureBoundaryTests.cs](../tests/MuscleCuties.Core.Tests/CleanArchitectureBoundaryTests.cs). Reuse them where appropriate rather than adding an overlapping generic checker. Source checks cannot establish layout, accessibility or performance conformance.

## 7. Rollout and acceptance

The [dated audit](design/AUDIT_2026-11-02.md) records locations and rollout evidence. Sequence:

1. Repair dark selected-date treatment, reduced motion, sheet focus lifecycle and localized selection hints.
2. Consolidate cards/callers/source assertions to two styles and page shells to one. Align shared header/sheet/row/metric/choice behavior and semantic icons without changing domain behavior. Passing today's four-card assertion is not evidence of the new budget's completion.
3. Simplify Nutrition logging and Today hierarchy, then Training, Cycle and Profile; retain useful Quiz/footer patterns.
4. Review reading type scale, font identity and large-text table layouts as a dedicated migration, separate from feature edits.
5. Owner checks representative iPhone sizes, light/dark switching while screens are open, accessibility text sizes, EN/RU, VoiceOver, Reduce Motion, keyboard clearance, back/dismiss gestures and empty/error/offline/busy states. Record device/OS/scenario evidence; compilation does not establish these results.

This pass configures discovery, convention ownership and agent behavior. Runtime resource values, layouts and framework targets are unchanged. Existing Mac Catalyst/Windows targets contradict the iOS-only scope and remain recorded configuration debt, not evidence of supported layouts.

## 8. External guidance

Primary sources support these rules, not a claim of tested compliance:

- [Apple UI design tips](https://developer.apple.com/design/tips/): touch targets, readability, contrast and layout.
- [Apple accessibility guidance](https://developer.apple.com/design/human-interface-guidelines/accessibility): accessible experiences and adaptable content.
- [WCAG text contrast](https://www.w3.org/WAI/WCAG22/Understanding/contrast-minimum.html) and [non-text contrast](https://www.w3.org/WAI/WCAG22/Understanding/non-text-contrast.html): contrast thresholds and scope.
- [MAUI XAML styles](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/styles/xaml?view=net-maui-10.0): resource scope and style composition.
- [MAUI system-theme changes](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/system-theme-changes?view=net-maui-10.0): theme binding/update behavior.

---
target: App-wide native UI conventions, source-only
total_score: 24
max_score: 40
na_heuristics:
p0_count: 0
p1_count: 3
target_identity: "file:/Users/denispetruk/RiderProjects/MuscleCuties/src/MuscleCuties.UI"
timestamp: 2026-11-02T15-19-23Z
slug: src-musclecuties-ui
---
# UI conventions audit — 2026-11-02

Method: dual-agent (A: `/root/screen_patterns` · B: `/root/tokens_audit`), with a separate architecture/governance review. Assessment A did not receive detector findings. Source-level scores below are provisional; they are not runtime or visual-conformance scores.

## Status and scope

This is a dated, descriptive audit of the MuscleCuties mobile presentation layer. It is evidence for the design system, not a second design authority. Start with [DESIGN.md](../../DESIGN.md) and the [presentation specification](../../docs/UI_PRESENTATION_DESIGN_SPEC.md) for current rules. Later implementation can invalidate locations and counts recorded here.

The audit examines source and the four owner-supplied reference images. It does not establish the app's rendered appearance, screen-reader behavior, scroll performance, keyboard behavior, or device usability. No UI project was built, run, tested, or captured. The current pass establishes documentation and agent guidance; the UI corrections below remain pending unless explicitly marked otherwise. No clinical, workout, nutrition, authentication, or persistence behavior is changed by this audit.

### Coverage

| Inventory | Observed baseline | Interpretation |
| --- | ---: | --- |
| UI C# and XAML source files | 327 | 262 C# and 65 XAML; excludes generated/build output and binary assets |
| Page and control XAML surfaces | 60 | 25 pages and 35 controls; includes nested/modal surfaces, not 60 navigation destinations |
| Declared color resources | 178 | Existing light/dark, brand, phase, feedback, and utility resources |
| Keyed styles | 80 | Existing shared visual vocabulary, not 80 independently designed components |
| Value-shaped legacy tokens | 235 | Names encode values rather than intended use; reuse exists, but intent remains difficult to infer |
| Raw color literals outside `Colors.xaml` | 0 | Within audited UI C#/XAML source; project/platform packaging files are a separate scope |

Counts describe static inventory. They are not accessibility, contrast, rendering, or release certification.

| Screen family | Reviewed presentation concerns | Remaining owner evidence |
| --- | --- | --- |
| Startup and shell | Root composition, navigation, shared resource loading | Launch, safe areas, native navigation behavior |
| Authentication | Shared fields, action hierarchy, error and loading presentation | Keyboard reachability, autofill, focus and validation announcements |
| Onboarding | Choice-card hierarchy, progress, field conventions | Comprehension, selected-state announcements, long text |
| Today / check-in | Header density, summary counters, next-action hierarchy | First-viewport usefulness on a narrow iPhone and enlarged text |
| Train / session / history | Filter states, schedule hierarchy, primary actions, sheets | Reachable training actions, selection semantics, session continuity |
| Nutrition / food logging | Date selection, meal context, search access, editor footer | Dark selected-date contrast, logging steps, keyboard and scroll behavior |
| Cycle | Phase semantics, chart/calendar color ownership, shared overlays | Label legibility, non-color cues, accessible calendar traversal |
| Profile / settings | Grouped forms, rows, error and loading conventions | Long translated labels, text scaling, keyboard and focus order |
| Shared controls / injury sheets | Modal motion, focus lifecycle, localized state hints | Reduce Motion, modal isolation, focus restoration and announcements |

## Design interpretation

The reference images support a clear operational direction: useful information first, restrained framing, readable metric-and-unit pairs, compact rows, contextual entry methods, and an obvious reachable action. Their black-and-white palette is not the requested identity. Preserve MuscleCuties' soft rose, warm neutrals, and established phase colors while borrowing the interaction hierarchy.

For a women-focused fitness product, warmth should come through considerate language, calm surfaces, and control over personal information. Avoid inferring that every woman wants ornamental pink screens, weight loss, a menstrual-cycle feature, or motivational pressure. Training, food logging, and progress should remain capable and direct. Safety and injury states must retain the required strength-session behavior; layout work must not turn a strength session into rehab-only content.

The existing code already has a substantial shared design system. The principal problem is inconsistent application and incomplete behavioral contracts, rather than a need for another palette or component library.

## Priority findings

No source-proven P0 task blocker is established here. P1 identifies a material defect or shared accessibility risk to resolve before release; P2 identifies consistency, efficiency, or maintainability work. Runtime risks are explicitly distinguished from observed source defects.

### P1 — Selected food-log dates use a light surface with dark-theme text

**Evidence:** [FoodLogConverters.cs](../../src/MuscleCuties.UI/Resources/Converters/FoodLogConverters.cs), line 34, resolves `AccentSurface` without selecting a dark resource. [FoodLogPage.xaml](../../src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml), lines 158 and 165, selects dark-theme text resources for the date labels.

**Impact:** In dark appearance, the selected date can combine a pale selected fill with pale text. The calculated contrast of these source color pairs is approximately 1.03:1 for primary text and 1.57:1 for secondary text. These are source-pair calculations, not measurements of rendered pixels.

**Correction:** Give selection a coherent theme-aware surface/text pair through the existing shared theme resolver. Preserve the rose hue and a non-color selection indicator. Review all converter-produced colors for the same theme-selection gap.

**Acceptance evidence:** Source inspection establishes matching light/dark roles; the owner verifies selected and unselected dates in both appearances, including enlarged text and VoiceOver state.

### P1 — Shared modal behavior lacks a complete accessibility contract

**Evidence:** [ModalTransition.cs](../../src/MuscleCuties.UI/Controls/Shared/ModalTransition.cs), lines 16–34 and 58–67, always applies translation and scale with no Reduce Motion branch in that shared implementation. [AnimatedModalView.cs](../../src/MuscleCuties.UI/Controls/Shared/AnimatedModalView.cs), lines 35–50, changes visibility/input transparency and plays transitions without a shared focus entry, background accessibility isolation, or focus-restoration lifecycle.

**Impact:** These are shared-source omissions that can affect many sheets. The audit does not prove that every individual sheet leaks focus or that VoiceOver currently fails; individual surfaces may supply additional behavior. Treat modal focus leakage and disorienting animation as runtime risks to verify.

**Correction:** Define shared show/hide behavior that respects the system motion setting, establishes initial focus, isolates covered content, and returns focus to the invoking control. Keep cancellation and rapid reopen behavior deterministic.

**Acceptance evidence:** Owner verification with Reduce Motion enabled and disabled; VoiceOver enters the sheet, cannot traverse covered content, announces relevant state once, and returns to a sensible control after close. Test nested/rapid open-close cases on device.

### P1 — Design authority and verification claims had become stale

**Evidence:** At the start of this audit, [UI_PRESENTATION_DESIGN_SPEC.md](../../docs/UI_PRESENTATION_DESIGN_SPEC.md) described 67 XAML files, declared universal compliance, and treated tests/UI compilation as awaiting authorization. Older entries in [ROADMAP.md](../../ROADMAP.md), line 63, and [STATUS.md](../../STATUS.md), line 112, referenced a migration matrix that the November 2 cleanup had removed. `STATUS.md`, line 3, records that intentional consolidation.

**Impact:** Agents can repeat obsolete claims, recreate deleted documentation, or infer permission for UI execution. A canonical document is only useful when normative rules, observed implementation, and pending verification are distinct.

**Disposition in this pass — resolved for current guidance:** The canonical specification was corrected, `DESIGN.md` and root agent routing were established, and the original references were retained with an interpretation manifest. Historical status entries remain historical evidence. Do not read their old build claims as authorization or current certification.

**Acceptance evidence:** One explicit authority chain; no missing document presented as a current dependency; dated audit evidence; current owner-only UI verification rule visible in the agent workflow.

### P2 — Meal context selection competes with immediate food entry

**Evidence:** [MealEntryModal.xaml](../../src/MuscleCuties.UI/Controls/Nutrition/MealEntryModal.xaml), lines 46–195, places a substantial meal-type chooser before search/entry. [MealEditorModal.xaml](../../src/MuscleCuties.UI/Controls/Nutrition/MealEditorModal.xaml), lines 298–314, already provides a separate action footer.

**Impact:** Repeated logging can require scanning or scrolling past context that is usually already known. This is a source-based hierarchy concern; actual extra taps and time have not been measured.

**Correction:** Present the current meal/time as compact editable context, expose search and applicable entry methods early, and preserve the existing pinned editor footer. Reveal the fuller chooser when the user changes context. Do not invent scanner or AI capabilities absent from the product.

**Acceptance evidence:** Owner walkthrough from a known meal and from a changed meal; entry methods are discoverable, keyboard does not cover confirmation, and the selected context survives search and editing.

### P2 — Today's fixed three-counter header precedes next actions

**Evidence:** [DashboardPage.xaml](../../src/MuscleCuties.UI/Pages/Dashboard/DashboardPage.xaml), lines 49–105, gives three summary counters a fixed early grid position before the next actionable content.

**Impact:** The first viewport can favor historical counts over what the user can do now, particularly on narrow screens or with larger text. Clipping is not established by source inspection.

**Correction:** Give the next relevant action and concise status the strongest hierarchy; keep useful summaries compact and secondary. Let enlarged labels reflow rather than shrinking text to preserve the row.

**Acceptance evidence:** Owner checks the smallest supported phone with default and accessibility text, with/without an available workout or pending check-in. The next action is understandable and reachable without competing hero sections.

### P2 — Ten accessibility state hints remain English literals

**Evidence:** [WorkoutPage.xaml](../../src/MuscleCuties.UI/Pages/Workout/WorkoutPage.xaml), lines 291 and 297; [MealEntryModal.xaml](../../src/MuscleCuties.UI/Controls/Nutrition/MealEntryModal.xaml), lines 71, 102, 133 and 164; [InjuryLogContent.xaml](../../src/MuscleCuties.UI/Controls/Injury/InjuryLogContent.xaml), lines 102, 105, 134 and 137. Examples include `Selected`, `Not selected`, and `Selected filter`.

**Impact:** A Russian-language interface can announce selection states in English. Labels may be localized while their state descriptions remain inconsistent.

**Correction:** Route these states through descriptive EN/RU catalog keys and confirm whether state should be announced as a selected trait, description, or hint for the actual control. Do not concatenate raw state text into every label.

**Acceptance evidence:** Static lookup confirms both language entries; owner verifies useful, nonduplicated state announcements in each language.

### P2 — Value-shaped token names obscure component intent

**Evidence:** The source inventory identifies 235 legacy value-shaped tokens, alongside useful semantic `Presentation*` resources in [Styles.xaml](../../src/MuscleCuties.UI/Resources/Styles/Styles.xaml).

**Impact:** A reusable numeric value is centralized but still leaves agents deciding when it is appropriate. Multiple screens can choose different value tokens for the same role, creating drift without adding literals.

**Correction:** Use role-based resources for new work and migrate callers by component intent. A semantic token may reference an existing primitive; do not duplicate the primitive value into a new file. Avoid bulk renames and retain old keys until all XAML/C# callers are checked.

**Acceptance evidence:** Each migrated component has one documented role and owner; no undefined/forward resource references; protected bindings survive; newly introduced value-shaped aliases have a documented reason.

### P2 — Font alias and actual font identity disagree

**Evidence:** The application registers `OpenSans-Regular.ttf` under `Inter`; the pre-audit specification calls this an Inter family. [UiCleanlinessAndReachabilityTests.cs](../../tests/MuscleCuties.Core.Tests/UiCleanlinessAndReachabilityTests.cs), line 151, explicitly preserves this alias.

**Impact:** Designers and agents can make typography decisions using metrics or weights from the wrong family. The alias is a compatibility contract, not evidence that Inter is installed.

**Correction:** Document the actual Open Sans face and existing alias accurately. If the owner later chooses a new family, change font assets/registration, resource aliases, relevant source contracts, and visual acceptance together. Do not silently add a new font during this audit.

**Acceptance evidence:** Documentation matches registration; owner verifies readable weights, numeric alignment, localized text, and Dynamic Type after any font change.

### P2 — Project targets disagree with the iOS-only owner rule

**Evidence:** [MuscleCuties.UI.csproj](../../src/MuscleCuties.UI/MuscleCuties.UI.csproj), lines 4–5, declares iOS plus Mac Catalyst and conditionally Windows despite the adjacent iOS-only comment. No Android target is present in this declaration.

**Impact:** Future restore/build instructions can accidentally expand into unsupported targets. This is configuration debt separate from visual design.

**Correction:** Reconcile target configuration with the owner rule in a bounded change; do not accommodate unwanted targets by installing workloads. This documentation pass does not change the project file or verify UI compilation.

**Acceptance evidence:** Static project inspection shows the intended iOS-only target. The owner performs any required client compilation/device verification.

### P2 — Nutrition retains domain-type coupling in presentation code

**Evidence:** [NutritionViewModel.Breakdown.cs](../../src/MuscleCuties.UI/ViewModels/Nutrition/NutritionViewModel.Breakdown.cs), line 15, constructs `MacroNutrients`; [NutritionViewModel.Suggestions.cs](../../src/MuscleCuties.UI/ViewModels/Nutrition/NutritionViewModel.Suggestions.cs), line 405, constructs `MealNutritionTarget`; [MealItem.cs](../../src/MuscleCuties.UI/Models/UI/Nutrition/MealItem.cs), line 17, exposes `MacroNutrients`. The broader Core-import gate in [CleanArchitectureBoundaryTests.cs](../../tests/MuscleCuties.Core.Tests/CleanArchitectureBoundaryTests.cs), line 258, currently covers Workout, Cycle, Dashboard and Auth.

**Impact:** Visual refactoring can become coupled to business objects or accidentally move business calculations into UI code. This is existing architectural debt, not a defect introduced by the design documentation.

**Correction:** Keep visual resources and platform color types in UI. Address Nutrition's DTO boundary separately and incrementally, preserving XAML runtime contracts. Business decisions/calculations remain in Core and application orchestration remains in UseCases.

**Acceptance evidence:** Backend contract/architecture checks for the changed seam and owner UI verification; no domain objects leaked through newly introduced presentation contracts and no repository/DbContext injection into ViewModels.

## Strong foundations to preserve

### Independent Impeccable critique

| Nielsen heuristic | Source score / 4 | Main evidence / limitation |
| --- | ---: | --- |
| Visibility of system status | 3 | Loading/error states and nearby footer status; announcement timing unverified. |
| Match with the real world | 3 | Familiar Shell labels and meal/unit language; icon meanings vary. |
| User control and freedom | 2 | Explicit back/close controls; shared sheet focus/dismissal lifecycle incomplete. |
| Consistency and standards | 2 | Shared tokens/styles; copied headers, icon meanings and selection hints still drift. |
| Error prevention | 2 | Some actions gated; full destructive/validation flows were outside source sampling. |
| Recognition rather than recall | 3 | Visible choices, labels and chart data equivalent. |
| Flexibility and efficiency | 2 | Resume/virtualized results help; meal chooser precedes food entry. |
| Aesthetic and minimalist design | 2 | Source hierarchy gives Today counters/context priority over immediate action. |
| Error recovery | 3 | Explicit error/retry/reference patterns; runtime recovery not exercised. |
| Help and documentation | 2 | Local explanations exist; no complete contextual-help convention. |
| **Total** | **24 / 40** | **Diagnostic source baseline only.** |

The product has a distinct foundation in strength, nutrition and cycle context. The biggest opportunity is coherent workflows and priority rather than stronger decoration. Cognitive load rises when meal context, search, suggestions and planning compete at the logging entry, and when Today gives equal emphasis to counters, phase, readiness, training, food and insights.

Working personas are assumptions for review: a busy repeat logger needs direct entry and clear save feedback; a strength trainee managing changing energy/injury needs a capable session and understandable accommodations; an occasional user with large text or mixed units needs flexible rows, labels and safe re-entry. The desired emotional sequence is reassurance → useful action → clear completion → optional reflection. Preserve progress feedback without making streaks or body metrics judge the day.

Questions skipped: the owner supplied references and confirmed the warm wellness direction; no further answer was needed to establish these conventions.

### Existing strengths

- `Colors.xaml` already centralizes the audited source colors and preserves a coherent soft palette with light/dark roles and recognizable cycle semantics.
- `Styles.xaml` owns shared visual styles, including existing button, card, input, search, selection and modal vocabulary. Additions should reuse this foundation.
- `AppThemeResources.cs` provides a common C# theme bridge; feature palettes can map semantic meaning without creating separate color values.
- EN/RU catalogs and localization extensions already provide the right home for visible and accessibility copy.
- Shared headers, input containers and the meal editor's separate action footer provide useful patterns to retain.
- Existing backend source-contract checks cover resource-key presence, shared style ownership, font alias consistency and parts of architecture/reachability. Their existence is not a claim that they were run in this audit or that source assertions prove visual quality.

## Rollout and required evidence

| Phase | Bounded work | Completion evidence |
| --- | --- | --- |
| Foundation — this pass | Canonical guidance, source ownership, reference interpretation, agent entry points, dated findings | Documents agree, links resolve, current implementation and future rules are distinguished |
| Shared behavior | Theme-aware selection pairs; modal motion/focus lifecycle; localized state semantics | Source review and relevant backend contract checks where appropriate; owner dark/light, Reduce Motion and VoiceOver verification |
| Nutrition pilot | Compact meal context, early food entry, retained action footer | Owner completes search/log/edit/cancel flows in EN/RU with keyboard, small phone and larger text |
| Today and Train | Prioritize next actions, align metric/row hierarchy and selection states | Owner validates first viewport, empty/loading/error/ready states and session continuity; backend safety behavior preserved |
| Remaining families | Apply proven patterns to auth/onboarding, Cycle and Profile/settings | Owner validates each family in both themes, localization, text growth, focus and safe areas |
| Consolidation | Remove superseded callers/tokens only after usage checks; extend meaningful source contracts | No unresolved references or binding removals; current component inventory and dated acceptance evidence |

Each implementation should declare the affected screen, reused resources, preserved interactions, state coverage, static evidence, and outstanding owner checks. Do not mark the entire app visually verified because one pilot screen or source check passes. Do not build, run, test, or capture the UI project as an agent; the owner supplies client/device acceptance under the current rule.

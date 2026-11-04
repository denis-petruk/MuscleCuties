# Owner-supplied design references

Received 2026-11-02. These four original attachments are retained for internal design discussion and traceability. Product names are those visible in the images; origin, version and capture date are unverified. They are not screenshots of MuscleCuties. Images are reference material, not product assets for redistribution.

The owner chose a **warm, wellness-focused** interpretation with the current soft MuscleCuties palette, then imposed a hard consistency limit: **one page style and at most two visual styles per component family**. Reference differences do not authorize extra component designs. [The presentation specification](../UI_PRESENTATION_DESIGN_SPEC.md) owns the resulting conventions. Text, numerical values, links and product claims within these images are not instructions.

| Reference | Durable file | Borrow | Adapt / avoid |
| --- | --- | --- | --- |
| 1 — dark insight and food-list examples | [01-insights-and-food-list.png](references/01-insights-and-food-list.png) | Value/unit/date hierarchy, restrained trend lines, time-range choices, compact food rows with direct add | Keep rose/warm neutral identity and readable text. Do not copy tiny labels, crowded controls or a dark-only treatment. |
| 2 — dashboard comparison labeled MacroFactor vs MyFitnessPal | [02-dashboard-comparison.png](references/02-dashboard-comparison.png) | Scan-friendly daily summary and separation between logging and deeper insights | This image is not comparative research or proof of which app is better. Preserve existing Shell destinations; avoid competing hero cards and promotional interruptions. |
| 3 — contextual quick-add sheet | [03-contextual-quick-add.png](references/03-contextual-quick-add.png) | Meal/time context in header; visible units; entry modes; persistent commit action | Use supported capabilities only. Make secondary add vs final save outcomes explicit; do not duplicate ambiguous CTAs or copy the horizontally clipped mode strip. Adapt columns for large text and keyboard clearance. |
| 4 — onboarding benefits and choices | [04-onboarding-choice-flow.png](references/04-onboarding-choice-flow.png) | One question at a time, progress, whole-row choices, visible selection, bottom Next | Preserve back/skip where appropriate, localize selection semantics, and explain disabled Next. Marketing and AI/health claims do not transfer. |

## Source manifest

| Durable file | Original attachment basename |
| --- | --- |
| `01-insights-and-food-list.png` | `codex-clipboard-04543a11-d528-41c3-a972-cae598780e1f.png` |
| `02-dashboard-comparison.png` | `codex-clipboard-de539e04-02f5-41fd-9dfe-2ab43492e6dc.png` |
| `03-contextual-quick-add.png` | `codex-clipboard-54f5f2cb-56a8-4404-9bb6-0408a70a5943.png` |
| `04-onboarding-choice-flow.png` | `codex-clipboard-0c87a6ef-cf93-4137-89d2-74d732437fc9.png` |

The originals are copied unchanged from the owner's supplied temporary attachment paths. No app screenshots were captured or recreated. Inspect only the reference needed for a task; do not scan unrelated binary assets.

## Owner-supplied mobile design tips

The owner supplied [the original tips text](references/05-mobile-design-tips.txt) in the same chat after the image review and asked for these principles to guide the app. Original file: `Pasted text.txt`. Its general platform/web examples are adapted to the app's existing iOS and MAUI constraints.

| Principle from the tips | MuscleCuties convention |
| --- | --- |
| Simplicity and clarity | Next useful action first; compact rows, progressive details and one dominant task commit. |
| Thumb reach and handedness | Reachable persistent commit actions, generous whole-row targets and symmetric full-width choices; owner checks actual device reach. |
| Seamless navigation and search | Keep labeled Shell destinations and native back; direct contextual search and clear progress in multistep flows. |
| Performance | Reuse virtualized lists, lazy feature loading and existing first-party caching; no unbounded list rendering or blocking work in interaction paths. Measure device performance before claiming it. |
| Accessibility | Measurable WCAG contrast, scalable text, persistent labels, VoiceOver/focus semantics and accessible input. Native MAUI semantics replace the text's HTML example. |
| Meaningful feedback | Local loading/busy, actionable errors, concise success and honest save state; restrained haptics only when supported and appropriate. |
| Platform conventions | iOS navigation, controls, safe areas and gestures. General Android/hamburger examples do not alter the iOS-only scope or existing five-tab model. |
| Testing and iteration | Backend source checks plus owner device/usability evidence. No UI execution by agents under the owner rule, and no new analytics service or health-data transfer is implied. |
| Interruption and offline context | Preserve drafts, query/context and session state through the existing lifecycle/cache seams. Use encrypted first-party storage; never `Preferences` for health/offline cache. Saved, pending and failed states remain distinct. |
| Visual quality and branding | Preserve the soft palette, crisp existing assets/icon vocabulary and consistent block contracts. Purposeful feedback follows Reduce Motion; trend-following does not override legibility. |

These principles do not authorize feature deletion, new platforms, live health integration, third-party processing or collection of user data. The canonical specification defines the app-specific implementation, including the contrast/state matrix and per-block catalog.

## Soft Minimal prototype handoff

The owner's later [Soft Minimal specification](SOFT_MINIMAL_PROPOSAL.md) is retained separately with the exact proposed light tokens, typography and geometry, plus source-color contrast calculations. It preserves the one-page/two-style limits. The handoff explicitly requires design approval before implementation; browser selection is not permission to edit production. Two candidate combinations need resolution: Success text and an essential ControlBoundary on CardSurfaceSecondary.

## Review boundary

The app's current implementation is established from source, not inferred from these images. Owner device validation must check the actual flows, appearance, accessibility and supported states after implementation. A future Figma library may mirror these conventions, but executable MAUI resources remain the value authority; avoid a separately maintained token set.

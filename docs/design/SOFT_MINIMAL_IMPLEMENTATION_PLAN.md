# Soft Minimal implementation plan

**Approved by the owner on 2026-11-02, including the contrast corrections and repository implementation.** This plan covers every current page and reusable control. Source implementation is complete; see [implementation evidence](SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md) and the regenerated [binding inventory](SOFT_MINIMAL_BINDINGS.md). Client compilation, execution and screenshots remain prohibited for agents; owner device acceptance is a separate handoff.

## Deliverables and binding contract

- Approved design: [Soft Minimal](SOFT_MINIMAL_PROPOSAL.md), promoted from candidate to implementation authority for visual values.
- [Complete binding inventory](SOFT_MINIMAL_BINDINGS.md): every page/control's XAML class and declared view-model/item types, element/property binding expressions, command/source/parameter bindings, and C# binding/context/navigation wiring. Final inventory is regenerated from the completed source.
- Shared resources implement the palette, corrected contrast, exact typography, two card/button styles and one adaptive page policy.
- Per-page checklist below records actual changed surfaces and owner acceptance needs, not just a feature-level promise.
- Existing binding, route, command, DTO and domain behavior stays intact unless an intentional presentation change is explicitly recorded. Moving a bound control does not change its data source. Do not delete a C# runtime contract based only on missing C# callers.

The binding inventory compares the pre-redesign Git source with current markup. It records additions/removals for review, including changes caused by replacing custom tappable borders with actual buttons. It is a source contract, not proof of runtime binding evaluation.

## Approved system and bounded decisions

| Area | Implementation decision | Verification |
| --- | --- | --- |
| Light palette | Approved supplied roles, resolved through existing Colors.xaml | Source palette/contrast checks |
| Contrast corrections | Darken the shared success foreground and control boundary enough to clear normal-text/non-text floors on the secondary card | Unrounded WCAG sRGB ratios across page, card, secondary card and actual states |
| Dark appearance | Warm dark page, primary/secondary card surfaces, high-contrast text/actions/boundaries and matching semantic states | Source pairs first; owner switching while screens are open |
| Font | Existing Open Sans regular and semibold assets; retain historical regular alias through AppFontFamily | Source registration/style contracts; owner actual rendering |
| Typography | Title27, section18, card16, body15, support13, button14, workout24, caption12 using scalable MAUI logical values | Shared style roles; no essential-label shrinking |
| Numbers | Shared workout-number label/input treatment with native tabular-number configuration | Source/native API review; owner weight/reps entry |
| Geometry | Cards16 radius/19 inset; buttons12 radius/minimum48; touch minimum44 | Shared resource contracts |
| Rhythm | Shared 4/8/12/16/24/32/40 scale; larger grouping steps | Source geometry review |
| Page | One background/header/body/optional-footer policy, safe-area and keyboard avoidance, adaptive20/24 gutter | All pages use shared roles; owner phone widths/text sizes |
| Style budget | Exactly ButtonPrimary/ButtonSecondary and CardPrimary/CardSecondary; no feature skins | Remove legacy card callers/definitions and update source assertion together |
| States | Functional selected/focused/pressed/disabled/busy/error/success within permitted styles | Shared state/contrast checks; owner assistive/input review |

## Implementation sequence

1. **Inventory and binding baseline:** record current pages/controls, routes, XAML contexts and bindings before changing contracts; preserve all prior unrelated workspace changes.
2. **Shared foundation:** apply approved palette and contrast corrections, dark pairs, registered semibold, semantic typography, geometry, shared page policy and two-card/two-button contracts.
3. **Shared interaction/accessibility:** theme-aware selection, localized state hints, reduced-motion handling, sheet focus entry/background isolation/return, reachable footers, stable navigation meanings.
4. **Parallel feature migration:** Auth/Onboarding/Profile/Startup; Workout/Injury; Today/Nutrition/Cycle. Agents own separate feature paths; shared resources and contracts are integrated centrally.
5. **Binding reconciliation:** regenerate the complete inventory, compare pre-redesign expressions with final expressions and explain intentional changes. Preserve command targets/parameters and data-template item contexts.
6. **Backend and source verification:** existing architecture/source suites, meaningful new style/contrast regression checks, XAML well-formedness/resource ordering, command/binding coverage, local-style and raw-literal scans. No UI compilation or execution.
7. **Documentation and owner handoff:** update design authorities, dated evidence, STATUS/ROADMAP, final page matrix, binding report and precise owner device checklist. Leave all code reviewable in the current checkout.

## Page-by-page plan

All entries additionally require the shared palette/type/spacing/state policy and the binding inventory. Implementation state is updated after each owned feature pass finishes.

| Page | Redesign work | Binding / behavior to preserve | State |
| --- | --- | --- | --- |
| Startup/AppStartupPage | Quiet progress/failure/retry, consistent reset action | Startup status, retry/reset commands, loading/error state | Implemented (source); owner device check pending |
| Auth/LoginPage | Common header, persistent labels, reachable login, secondary registration/recovery | Email/password, validation, busy/login and navigation commands, autofill semantics | Implemented (source); owner device check pending |
| Auth/RegisterPage | Cohesive form groups, wrapped guidance, persistent commit | Account fields, validation, registration/busy/navigation | Implemented (source); owner device check pending |
| Auth/PasswordRecoveryPage | Focused recovery task, input retained, retry/status near action | Recovery fields, request/back commands, busy/error/status | Implemented (source); owner device check pending |
| Auth/OtpVerificationPage | Clear verification context, code/resend/status and footer | Code entry, resend countdown/command, verify/back and busy | Implemented (source); owner device check pending |
| Onboarding/ProfileSetupPage | Common page frame, readable field groups and continue | Profile input/unit/choice bindings, validation, continue/back | Implemented (source); owner device check pending |
| Onboarding/QuizPage | Single question, full-row choices, progress and pinned next/back | Question/item contexts, selected answers, progress, navigation/retry | Implemented (source); owner device check pending |
| Dashboard/DashboardPage | Next action before counters/optional insight; quiet groups | Card contexts, check-in/train/log navigation, status and summaries | Implemented (source); owner device check pending |
| Dashboard/DailyCheckInPage | One form rhythm, responsive choices, clear save/footer | Check-in inputs, selected state, save/cancel/busy/errors | Implemented (source); owner device check pending |
| Workout/WorkoutPage | Current/resumable strength session first; row-based schedule/filter | Day/activity selection, start/resume/history, injury and generation commands | Implemented (source); owner device check pending |
| Workout/StrengthPlanGenerationPage | Readable setup/preview/details, persistent apply | All plan preferences, preview conditions, apply/generate/retry and safety state | Implemented (source); owner device check pending |
| Workout/WorkoutSessionPage | Exercise/set/units/completion together; responsive numeric inputs | Timing, set input/update, finish/pause/resume/swap/skip, safety/error/reference | Implemented (source); owner device check pending |
| Workout/WorkoutHistoryPage | Flexible labeled history/edit rows instead of fixed numeric grid | History item contexts, filters, edit/save/delete, set identities and units | Implemented (source); owner device check pending |
| Nutrition/NutritionPage | Direct log action before optional phase/planning details | Daily summaries, log/editor/planning commands, meal contexts | Implemented (source); owner device check pending |
| Nutrition/FoodLogPage | Contextual search/entry, theme-aware dates, clear quick-add | Query/mode/date/results, portions, add/edit/save and quick-add fields | Implemented (source); owner device check pending |
| Cycle/CyclePage | Clear logged/estimated context, quick log/calendar before explanations | Date/phase/calendar selection, logging, details and uncertainty state | Implemented (source); owner device check pending |
| Cycle/CyclePhaseDetailPage | Readable phase explanation and flexible supporting rows | Phase/detail content, selected context and back/log actions | Implemented (source); owner device check pending |
| Profile/ProfilePage | Cohesive settings rows and concise account summary | Profile summaries, settings/privacy/photo/feedback/navigation commands | Implemented (source); owner device check pending |
| Profile/ProfilePersonalInfoPage | Consistent labeled form and save/footer | Account/body/cycle input, validation, units and save/back | Implemented (source); owner device check pending |
| Profile/ProfileNutritionSettingsPage | Cohesive goals/preferences/target groups | Goal/target/preferences, validation, save/back | Implemented (source); owner device check pending |
| Profile/ProfileWorkoutPreferencesPage | Consistent choice rows and clear save | Saved training preferences, selection/exclusions, save/back | Implemented (source); owner device check pending |
| Profile/ProfileUnitsDisplayPage | Clear unit choices, current state and commit | Unit preference, selected state and save/back | Implemented (source); owner device check pending |
| Profile/ProfilePrivacyPage | Readable privacy groups, explicit consequential actions | Existing privacy/localization and account/data actions | Implemented (source); owner device check pending |
| Profile/ProfileFeedbackPage | Focused feedback form, nearby status and send | Message/contact preference, validation, send/back | Implemented (source); owner device check pending |
| Profile/PhotoJourneyVaultPage | Quiet photo grid/empty state, clear private controls | Photo items, picker/crop/remove/open and loading/errors | Implemented (source); owner device check pending |

## Reusable-control coverage

The final binding inventory lists every control separately. Feature passes cover Dashboard cards/charts, Workout cards/swap/preferences, Nutrition rows and all editor/detail/suggestion/breakdown sheets, Cycle calendar/phase/explorer/logging/warning sheets, Profile content/choices/crop, and Injury log content/modal. Shared loading, header, macro progress and phase celebration use the same typography/state/motion rules. Native drawables retain data meanings and accessible equivalents.

## Required final evidence

- Per-page/control binding inventory with source locations, exact expressions and intentional deltas.
- Two-card/two-button source assertions, all resource references defined and no forward-reference hazards.
- Corrected light/dark text, boundary, selected and action contrast pairs; no raw colors outside palette authority.
- Backend/source checks actually executed, toolchain used and failures/limitations stated accurately.
- Owner checklist: supported iPhone widths, default/largest accessibility text, EN/RU, light/dark switching, keyboard, VoiceOver, Reduce Motion, navigation/dismissal, interrupted/failed saves, offline/stale/loading/empty states and workout continuity.
- No claim of verified runtime layout or binding evaluation until owner device evidence exists.

**Evidence:** [implementation evidence](SOFT_MINIMAL_IMPLEMENTATION_EVIDENCE.md) · [binding inventory](SOFT_MINIMAL_BINDINGS.md).

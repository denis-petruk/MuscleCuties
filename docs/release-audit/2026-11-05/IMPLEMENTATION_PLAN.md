# MuscleCuties implementation plan

**Updated November 6, 2026.** Complete the owner-reported iPhone issues on the current integrated candidate, then present evidence for approval. This document owns the corrective package acceptance criteria. Use the [orchestrator](ORCHESTRATOR_PROMPT.md) to dispatch work, the [handoff](HANDOFF.md) for inputs and owner decisions, and the [verification gates](../../VERIFICATION_GATES.md) for evidence. The screenshots are design and symptom references. Record the installed build and reproduce each defect before changing an already-integrated fix.

## Candidate and authority

- Rider `main` was observed at `0af0bef5ba434339cfb5d09f9e177d5c322c2038`, the merge of final review. The previously selected `6e385357` candidate, the [November 4 audit](../2026-11-04/README.md), and older build/test reports are history to reconcile. Recheck source, active worktrees, and the actual simulator binary at dispatch. Keep unrelated changes intact.
- The observed Rider working tree has changes in `WorkoutPlanAndSessionEndpointMappings.cs`, `WorkoutPresentationModels.cs`, and `WorkoutViewModel.cs`, plus untracked `BetaWorkoutScope.cs` and `BetaWorkoutScopeTests.cs`. Reserve affected workout files through Integration before writing. Start review from an isolated verified base with the owner's existing work preserved.
- The [RM07 checkpoint](RM07_HANDOFF.md) records the owner's November 5 P0 acceptance and excludes rescheduling from internal Beta 1.0.0. [STATUS.md](../../../STATUS.md) reports merged rescheduling code. Treat implementation presence and beta exposure as separate. Resolve a conflicting UI/API exposure against the recorded beta scope with Integration and the owner. Keep the deferred arbitrary-date food copying and the approved [MC-BETA-WORKOUT-1 policy](WORKOUT_POLICY.md) in their existing scope. PD00 product adoption is closed.
- This update is planning under `docs/`. Before implementing, apply current owner grants and [AGENTS.md](../../../AGENTS.md) to source edits, simulator use, integration, and release. Treat [older execution briefs](ORCHESTRATION_HANDOFF_NEXT_SESSION.md) as history for source and endpoint identity. Test with synthetic fixtures and disposable loopback services. Keep tester data, raw screenshots with personal information, and secrets out of the evidence.

## Execution order and reservations

| Checkpoint | Package and dependency | Reservation and handoff |
| --- | --- | --- |
| Baseline | P0 before end-to-end acceptance | Product A owns app identity and endpoint evidence. Platform B owns the disposable API/PostgreSQL runtime. Integration confirms the source and approved beta scope. |
| First working activity | UX02-RUN | One Product A owner for session view, view model, and affected route adapter. Coordinate Workout overview refresh with its current owner. Reproduce the Running and abandon problems before changing their transitions. |
| Disjoint UI corrections | RM-PREVIEW, RM-MEAL, RM-VAULT, RM-REGISTRATION, RM-RECOVERY | Start after P0 baseline triage where permissions and dependencies allow. One owner per feature. Registration and recovery use one reservation if they touch a shared auth component. If registration clipping blocks P0 on the same binary, raise that specific dependency for approval. |
| Activity rollout and presentation | UX02-ACT after UX02-RUN. RM-SESSION and RM-NOTICE use the reviewed session state. | Sequence shared session files and reservations. The notice owner can inspect independent hosts while session files are reserved. Reserve shared sheet, styles, or catalog files once. |
| Integrated acceptance | QA00 after applicable corrective packages | Integration combines reviewed diffs and hands one candidate to an independent reviewer. Attach the simulator outcomes to the exact binary before owner review. |
| Later release stages | PL01–PL05, QA01, TF00–TF02 | Platform and Integration close each applicable target gate using current evidence and candidate-specific authority. |

Product A owns UI, behavior, and adapters. Platform B owns environment, schema, and release. Integration owns shared DTOs, solution/project graph, migration numbering, release manifests, and canonical governance. A package that needs a shared file asks for an exclusive reservation. Use the established [Soft Minimal design system](../../../DESIGN.md) and [presentation specification](../../UI_PRESENTATION_DESIGN_SPEC.md), with one shared page frame, the two button/card families, role tokens, and EN/RU localization.

## P0 — Verify the simulator data path on the integrated build

1. Read the [simulator checkpoint](../2026-11-05-simulator-checkpoint.md) and RM07 acceptance. Capture the current checkout, installed binary and effective endpoint. Verify `AppEndpointConfiguration.cs` and `TransportCompositionExtensions.cs`, including the `backend_api_base_url` preference. A previous simulator that opened the login page against AWS has different provenance from a loopback data-load run.
2. Reuse the port 5080 correction when present. Reproduce the port collision only if the current endpoint fails. Use the permitted loopback API and disposable PostgreSQL with local secrets. Use an existing authorized synthetic account or agree the supported disposable registration/email fixture if provisioning blocks login. Preserve receipt and consent validation.
3. Record health/live and health/ready returning 200, anonymous session returning 401, and a successful synthetic sign-in loading the previously failing view. Check failure and retry with the approved local fixture. Save the build identity, effective URL, and sanitized correlation. The earlier owner acceptance remains valid for its recorded scope. Any unresolved authenticated load on the current binary blocks QA00 on that candidate.

## UX02-RUN — Running progression, save and abandon

**Touchpoints** [Workout session page](../../../src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml), [session state](../../../src/MuscleCuties.UI/ViewModels/Workout/WorkoutSessionViewModel.cs), and [active session endpoints](../../../services/MuscleCuties.Server/Workout/ActiveSession/WorkoutActiveSessionEndpointMappings.cs). Trace the existing API adapter and Workout overview reload before changing an endpoint.

1. **4H.** Reproduce Running from the schedule on the actual candidate. In the observed source, `StartActivityAsync` starts or resumes server timing. When `HasExercises` is false it sets `IsTimedActivityRunning` and returns before changing the current screen. Confirm the route and XAML visibility before updating that branch.
2. Present a visible timed activity state after successful start/resume. Reuse the strength flow's page frame, confirmed timing/checkpoint model, failure presentation, and completion lifecycle. Show the current prescribed warm-up, main, or cool-down phase, elapsed and remaining time where known, and Pause, Resume, Finish. Derive the phase from confirmed active time and available prescription data. Preserve duration-only or partial prescription as valid with an honest display. Avoid a second independent timer authority.
3. Show a completion summary and use the established session finish/save route. Persist supported actuals when available, then reload history and the originating workout context. Keep the draft and a stable completion/retry identity if the response is lost or save fails. Show success after the matching operation is acknowledged.
4. **4I.** Trace abandon through confirmation, API acknowledgment, timer cancellation, page navigation and the opener's refresh/overlay. Separate a successful abandon followed by a refresh failure from a failed abandon. Cancel or fence stale refreshes, clear busy and overlay state in every exit path, and perform one bounded return to the opener with date/session context. A retry after an uncertain response reconciles the server session instead of creating a second session or marking it complete. Cancellation preserves the active state. Keep route and transport contracts if their existing behavior can satisfy the fix.
5. Verify first start, pause/resume, background/foreground, phase transitions, finish, actuals/history, missing prescription, network failure with retry, canceled abandon, successful abandon, ambiguous response and repeated taps. Assert the opener settles with accurate status and neither path loops on “Loading your data.” Include a strength-session baseline.

## UX02-ACT — Extend to all supported timed activities

**Touchpoints** [activity catalog](../../../src/MuscleCuties.UI/Services/Workout/WorkoutActivityOptionCatalog.cs), [session presentation mapper](../../../src/MuscleCuties.UI/Services/Workout/WorkoutSessionPresentationMapper.cs), and the UX02-RUN state and views.

1. Inventory every activity available through the authorized beta schedule and start route. Check walking, cycling, swimming, rowing, elliptical, HIIT, climbing, yoga, mobility, and gentle recovery where eligible. Record any deliberately gated activity and its policy source. Preserve the strength exercise flow and Rest day as a no-start case.
2. Apply the same intro → active → summary lifecycle to supported non-strength activities using the existing type/prescription mapping. Present only applicable phases, timers, intervals, duration and actual metrics. Make duration-only activity usable without inventing metrics or requiring a new data field. Use the existing serialization and history paths. Request an Integration reservation and compatibility proof for any unavoidable shared contract change.
3. Verify each enabled activity reaches its active state, resumes without duplicate sessions, saves applicable performance, and reads it back in history. Cover HIIT history/safety gates, outdoor versus indoor metrics when supported, absent actuals, multiple activities in one day, and an abandoned activity. Review the catalog against approved beta exposure.

## RM-PREVIEW — Expandable plan blocks and separate navigation

**Touchpoints** [generation view](../../../src/MuscleCuties.UI/Pages/Workout/StrengthPlanGenerationPage.xaml), [generation view model](../../../src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs), its display mapper, and [Workout Preferences](../../../src/MuscleCuties.UI/Pages/Profile/ProfileWorkoutPreferencesPage.xaml).

1. Add one accessible disclosure for each existing ordered block. Expanded strength blocks show the available exercises. Timed blocks show the workout description, phases or duration when present. Keep expansion local to that preview identity, localized, and independent of the approval action. Render unavailable/withheld reasons from the existing response. Preserve scroll position and safe dismissal.
2. Trace every opener, including Workout and Preferences. Give Edit details an explicit Workout Preferences destination and return intent, preserving the draft context when allowed. On return, show a current preview or an explicit regeneration action if inputs or lease changed. Preserve the existing first-plan automatic acceptance exception and approved replacement confirmation before any activation. The [adopted policy](WORKOUT_POLICY.md) governs the explicit replacement decision and any automatic activation after it.
3. Keep Close, Back, and supported gesture dismissal tied to the opener. They dismiss the preview and release its lease without invoking Edit details, creating an extra route, or changing the active plan. Verify repeated entry, stale lease, edit and return, rejection and safety reasons, and explicit Apply/replacement behavior through normal entry points.

## RM-MEAL — Meal sheets, barcode copy and day navigation

**Touchpoints** [MealEditorModal](../../../src/MuscleCuties.UI/Controls/Nutrition/MealEditorModal.xaml), [MealEntryModal](../../../src/MuscleCuties.UI/Controls/Nutrition/MealEntryModal.xaml), [MealSuggestionModal](../../../src/MuscleCuties.UI/Controls/Nutrition/MealSuggestionModal.xaml), [MealDetailModal](../../../src/MuscleCuties.UI/Controls/Nutrition/MealDetailModal.xaml), [Food Log](../../../src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml), `NutritionViewModel.MealEntry.cs` and `NutritionViewModel.Barcode.cs`. Find actual callers and bindings before consolidating, including the grouped Food Log and main nutrition entry.

1. **4A.** Use screenshot #2 as the visual reference for Edit Meal: compact meal and time, macro summary, and the ingredient list with existing edit/remove and save. Show one meal/time context on manual Add Meal. Keep the picker/time editing behind the single context action where supported, and preserve any existing read-only context for a saved meal. Keep the working suggested-meal path as the control case. Preserve selected date, draft, and nutrition presentation mapper boundaries.
2. **4B.** Converge #1, #2, and #4 onto the existing `AnimatedModalView` lifecycle and a reusable meal sheet structure for header, body, and actions. Give the content one scroll owner, with header and commit footer outside the scroll region where needed. Preserve close behavior, focus restoration, keyboard clearance, safe-area and long ingredient lists. Use the existing component if it covers the states; extract the smallest repeated structure if required.
3. **4C.** Replace redundant text above barcode with one short localized prompt. Show scan, manual entry and state-specific help at the point of use. Preserve camera permission, offline, not-found, errors and retry. Distinguish page Back from previous-day navigation with the shared header's back affordance and labeled day controls. Check both previous/next day and date selection still work.
4. Verify manual/suggested/template entry and editing, saved data and macros, EN/RU wrapping, long lists, keyboard, close/reopen, synthetic camera/permission/error states, and correct date navigation. Reuse existing nutrition contract and persistence checks for affected behaviors.

## RM-VAULT — Compact Photo Vault and localized dates

**Touchpoints** [compact profile content](../../../src/MuscleCuties.UI/Controls/Profile/ProfileDetailsContent.xaml), [full Photo Vault](../../../src/MuscleCuties.UI/Pages/Profile/PhotoJourneyVaultPage.xaml), and `PhotoJourneyVaultViewModel.cs`.

1. **4D.** Reproduce the reported compact bullet or control against screenshot #6 and identify its actual rendered node. Remove a duplicate decorative marker if it carries no meaning. If it communicates state or performs a reveal action, replace the inconsistent presentation with the approved localized control treatment and preserve the action and accessible state.
2. Style the full vault date as a readable, localized section or item label using existing typography. Retain the timestamp and chronology, metadata, comparison selection, and privacy behavior. Make both comparison controls reachable and readable at narrow widths and large text. Verify hidden/revealed, unavailable and comparison states with synthetic photos.

## RM-SESSION — Session sheet wording, imagery and metadata

**Touchpoints** [Workout page session activities overlay](../../../src/MuscleCuties.UI/Pages/Workout/WorkoutPage.xaml), [session page](../../../src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml), and [activity catalog](../../../src/MuscleCuties.UI/Services/Workout/WorkoutActivityOptionCatalog.cs). Sequence these changes after the relevant UX02 transitions or have one owner carry the reservation.

1. **4E.** Audit repeated decorative SVGs in visible neighboring blocks. Reuse the existing `activity_*.svg` resources by activity kind. Reserve generic art for fallback and use one meaningful image when adjacent cards would otherwise repeat it. Reuse the same semantic navigation icons for the same actions.
2. **4F.** Use the shared sheet header, dismissal, content and footer patterns. Make session names and action labels agree across picker, intro, active screen and summary. Show the appropriate duration, phases or intervals for timed work and exercise/set details for strength. Use activity-specific bundled art with correct sizing and clipping. Decorative art stays out of the accessibility tree, and the localized title identifies the activity.
3. Verify the modal opening/closing and focus restoration, screen names, cards with distinct or absent art, long EN/RU names, light/dark, and consistent timed versus strength metadata across all affected surfaces.

## RM-NOTICE — Brief top feedback without layout shifts

**Touchpoints** [Food Log's existing toast](../../../src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml), [Workout History's existing toast](../../../src/MuscleCuties.UI/Pages/Workout/WorkoutHistoryPage.xaml), [session view](../../../src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml), and `StatusToastSurface`. Verify all notification entry points before extracting a common host.

1. **4G.** Render transient feedback in the page's existing overlay layer, within the top safe area and outside the focused field's visible area. Keep it non-modal and non-intercepting. Use the shared brief auto-dismiss behavior, or establish one owner-approved duration token if no shared behavior exists. Cancel/replace the old dismissal when a newer notice arrives or the page closes. Announce a notice once and keep focus on the current control.
2. Keep safety and actionable errors in a persistent place with correction or Retry. A brief success toast follows an acknowledgment. A failed save keeps the draft, actionable error, and retry regardless of whether the top notice is dismissed. Verify that the overlay, keyboard, accessibility focus and repeated notices do not displace or cover inputs, including Running and failed save/retry.

## RM-REGISTRATION — Privacy acknowledgment and action footer

**Touchpoints** [registration view](../../../src/MuscleCuties.UI/Pages/Auth/RegisterPage.xaml) and `RegisterViewModel.cs`.

1. **4J.** Reproduce the clipping from screenshot #15 on the identified build. Keep the acknowledgment in one readable, accessible group with clear checked state and a separate, fully tappable **Privacy Policy** action. Adjust the existing form/scroll and footer containment so Create account and Sign in with Apple leave the acknowledgment, fields, and validation reachable with the keyboard open. Reuse existing auth and button styles.
2. Preserve the existing registration and Apple sign-in acknowledgment guards, localized labels and notice navigation. Opening/returning from the policy preserves the existing checked state and entered draft. Verify narrow width, large text, EN/RU, VoiceOver focus, checked/unchecked, validation, and both footer actions with synthetic data. If this clip blocks the current P0 run, record the exact dependency and obtain matching scope before changing it.

## RM-RECOVERY — Place Back to Log In below the primary action

**Touchpoints** [password recovery view](../../../src/MuscleCuties.UI/Pages/Auth/PasswordRecoveryPage.xaml) and `PasswordRecoveryViewModel.cs`.

1. **4K.** In the existing `Auto,*,Auto` layout, move the step action group into the row 1 scroll body after the step content, then remove the dedicated footer row. Keep the header and auth padding. Use the existing shared action spacing and remove the extra margin above Back to Log In.
2. Keep Send recovery code, Verify code, and Update password as the existing step-specific primary actions. Place the existing Back to Log In immediately below the primary where displayed, aligned to its width with `ButtonSecondary`. Keep validation and status reachable in the same scroll flow. Preserve `GoBackCommand`, busy and visibility bindings, keyboard return commands, and the code-step Change account and resend actions.
3. Verify all three steps, a synthetic identifier, keyboard open/closed, narrow width, large text, safe area, EN/RU, and VoiceOver. Back to Log In reaches Login from the identifier and password steps. Change account returns from code to identifier entry. Error, retry, and resend stay reachable.

## QA00 — Integrated evidence and owner decisions

- Verify the actual post-fix SHA, app binary, target, and synthetic fixture in one run. Use affected G0–G9 checks and the [layer and environment verification matrix](../../architecture/github-transition/04-ci-cd-verification-matrix.md). Reuse existing Core UI guards, mobile contracts, server routes, and disposable PostgreSQL tests. Add focused coverage for any specific new regression that existing checks miss.
- Walk P0 sign-in and authenticated data load, registration and recovery, meal add/suggest/edit, barcode and day navigation, Photo Vault, preview expand/edit/dismiss and approval, Running and all exposed timed activities, strength, Rest day, history, retry and abandon. Verify multi-session ordering, offline/error handling, hydration, measurements, and account isolation where touched. Use both locales, light/dark, narrow screens, large text, keyboard/safe areas, VoiceOver and Reduce Motion.
- In [HANDOFF.md](HANDOFF.md), record the exact source SHA/tree, worktree, package paths/reservations, command/results, simulator and binary, endpoint/fixture, expected and observed outcomes, independent reviewer and any pending check. For each stage record the owner's plan approval, execution authorization, and result acceptance separately. **Needs changes** stays open with the exact failure. A deferred item retains its reason and target scope.

## Remaining beta and release gates

| Gate | Evidence still required before closure |
| --- | --- |
| Workout policy and older corrective queue | Reuse the accepted product policy and integrated work. Trace one real preview → replacement confirmation/activation → scheduled session → actuals → next prescription. Verify exercise selection, coverage, readiness, pain handling, preserved history, and compatible clients on the final candidate. Preserve approved internal Beta exclusions. Use the [prior queue](../2026-11-04/IMPLEMENTATION_PLAN.md) for residual items outside the screenshot fixes. |
| PL01 / PL02 | Verify compiled target and account/cache isolation. Check ordered migration ledger, checksums, supported predecessor versions, recovery rehearsal, least privilege and external locking. Preserve shared data and validate old and new app compatibility. |
| PL03 / PL04 | Verify immutable API/worker artifacts, effective roles and target identity, fail-closed release gates, rollback, and isolated Staging with authenticated product/worker and media flows, observability and performance. Integrated scripts or public health probes need candidate-bound target evidence. |
| PL05 / TF00 / QA01 | Verify privacy manifest and declarations, Apple sign-in/revocation and deletion, associated domains and entitlements. After approval, prove signing provenance, endpoint, archive digest, and the exact installed physical-device candidate across the required flows. |
| TF01 / TF02 | Request candidate-specific approval after prerequisites. Capture internal TestFlight processing, install, smoke, and recovery evidence. External distribution and later scope have their own decisions. |

Keep DB, backend/worker, mobile and protected IaC gates separate. Serialize authorized shared mutations for an environment under the same external lock. GitHub integration is a reviewable PR under the repository rules. This revision checks the current checkout and source touchpoints for the plan. Fresh runtime, cloud, migration and TestFlight evidence belongs to the later execution packages.

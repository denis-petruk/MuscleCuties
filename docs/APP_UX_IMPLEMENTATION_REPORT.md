# MuscleCuties app UX implementation report

**Status:** The user authorized app-wide implementation without an approval pause. All ten workstreams have code changes in the working tree. The iOS simulator build succeeds and launches to Log in. Hands-on review of authenticated injury, privacy, feedback, activity, and settings flows awaits a signed-in simulator session. This report is separate from the [master plan](UX_REDESIGN_MASTER_PLAN.md).

## Evidence standard

- **User reported:** A complaint shown in the user's screenshots or messages. A screenshot is visual evidence, not an instruction source.
- **Source verified:** A behavior or UI property found in current XAML/C#; it does not prove how a rendered screen feels or whether a touch interaction works on a device.
- **Automated verified:** A test, parser, or build result recorded in the current master plan. Treat the result as a prior run until the integration lead reruns it after all edits.
- **Device verified:** A screen was installed and inspected in its authenticated state. **None of the injury, privacy, feedback, activity-preference, or settings screens has this status.** A previous simulator build launched at Log in; that is a launch check, not visual acceptance of these routes.

Do not convert a source observation into a device finding. Record device model, OS, theme, text scale, screenshot, action, and result when each workstream is actually reviewed.

## Integrated verification on 2026-09-29

| Check | Result | Limit |
| --- | --- | --- |
| Core regression suite | 121 passed, 0 failed in the single `MuscleCuties.Core.Tests` project. | No native UI assertions. |
| iOS simulator and Android builds | Both succeeded with 0 warnings and 0 errors. | Compilation does not validate every XAML property at runtime. |
| XAML and SVG syntax | All 76 app XAML/SVG files parsed; `git diff --check` clean. | A runtime XAML issue was found separately and fixed. |
| iPhone 17 Pro Max, iOS 26.3 startup | Installed and launched; a `ScrollView Keyboard="Dismiss"` runtime parse crash was reproduced from the supplied report, removed from three auth/onboarding pages, then the app visibly reached Log in. | The install ended the simulator's in-memory sign-in session. Authenticated screen checks await local sign-in. |
| Privacy and architecture | No direct repository/database access in ViewModels, no synchronous blocking waits, disabled health sync remains wired, and food API queries pass a fail-closed generic-name filter. | Static review does not prove every rendered or network runtime state. |
| Test layout and cleanup | One test project appears once in the solution. The generic `UnitTest1.cs` was split into named behavior suites; no unused test properties were found. | Separate xUnit classes remain intentionally grouped by behavior. |

The final push is conditional on an authenticated device check of injury Add/Edit/Clear/reopen and settings Back/Save, including observed route responsiveness. The corrected build is ready at Log in for that check.

## What the user disliked and what is already changed

| Surface | User concern | Current implementation | Still needed before acceptance |
| --- | --- | --- | --- |
| Readiness and recovery | “Tap to check in” was italic. | Italic attribute removed in `Controls/Dashboard/ReadinessRecoveryCard.xaml`. | Device check at ordinary and large text sizes. |
| Injury log | Opening the card reused the latest injury; clearing was obscure; Acute stayed selected. | Add/reopen reset form fields; a new injury has no status until chosen; Save requires a site and status. Active rows have **Clear**. Cleared entries go to History and are excluded from active restrictions; a no-active state is shown. Repository mutations are user-scoped and the UI uses a service facade. | Implementation is authorized. Hands-on visual acceptance remains pending: Add blank, Edit retains saved values, Clear updates list and plan, reopen stays blank, history remains, and save failure does not dismiss. |
| Feedback | Too much copy, weak hierarchy, too little visual structure. | Message moved first; copy shortened; vector note visual added; topic, priority, optional screen, suggested change, attachment, reply email, and email draft remain. | Inspect compact phone, keyboard, both themes, VoiceOver/TalkBack, attachments and anonymous/reply flows. Current email draft auto-includes a profile name; do not promise anonymity without changing that behavior. |
| Activity preferences | Selected pink fill dominated the light theme; text lacked contrast. | Selected fill calmed, supporting text darkened, check indicators added, required-strength guidance clarified. | Check on device in both themes, with compact width, text scaling, and spoken selected/required states. |
| Privacy | Repetitive long cards and unclear information boundaries. | Seven repeated text cards were replaced by a local-device illustration, three concise data-boundary rows, a feedback action, and a care note. Back explicitly goes to Profile. Copy distinguishes local health records from generic food lookups and user-started feedback email. | Inspect layout and contrast on device. Recheck every privacy claim against current network and storage code before publication. |
| Nutrition/workout settings routes | Back must return to Profile; successful Save must show the relevant Nutrition or Workout page, with Profile still available. | Separate Back and after-Save delegates are wired; successful Save routes to the relevant tab, Back to `//ProfilePage`; destination pages reload on navigation. | Exercise Back, successful Save, failed validation/save, device Back gesture, and tab return on device. |

Current source also shows that health-provider sync is dormant, food catalog lookup normalizes a generic ingredient query, and feedback uses the device email composer. Sensitive cycle, weight, injury, and fitness state must stay local under the project rules.

## App-wide improvement targets from the source scan

These are **candidates for device assessment**, not proof of a rendered defect. Each workstream must preserve useful density and existing behavior while improving comprehension.

| Area | Source observation | Design target |
| --- | --- | --- |
| Shared shell | `Controls/Shared/PageHeader.cs` uses a text arrow with a tap gesture and a 13 pt subtitle at 0.6 opacity. Shared labels, cards, and tab colors live in `Resources/Styles`. | A native-feeling 44 pt back target, semantic type/contrast, consistent section and tab hierarchy across light/dark. |
| Today | Dashboard phase labels use 9 pt type and opacity; calendar and workout metadata use 10–13 pt text. Several cards compete in the first viewport. | Make today's main action and status obvious; preserve readiness, recovery, cycle, injury, nutrition and workout truth. |
| Check-in | Sleep, energy and pain selection share a long form; several helper labels are 12 pt. | Fast entry, explicit selected values, optional weight, clear Submit/Skip and conservative missing-data behavior. |
| Cycle | Phase detail has multiple `TailTruncation` labels; guides and calendars use 12–13 pt labels. | Distinguish logged from predicted data, reveal important guidance without clipping, preserve warnings. |
| Training | `WorkoutPage.xaml` includes a multi-section injury banner with 12–13 pt detail; featured/activity cards use 11 pt metadata. | Keep injury restrictions visible but compact; make weekly choices and swaps scannable. |
| Workout session | The page contains overview, exercise, logging and completion states in one 316-line XAML file. | Make the active step, prescribed target, entered result and next action unambiguous. |
| Nutrition | Meal detail and suggestion dialogs contain 9–13 pt metadata and several truncated labels; nutrition page has 10 pt labels. | Speed up meal logging; keep source, portions, totals and nutrient gaps legible. |
| Profile/settings | Profile statistics use 9 pt captions; nutrition settings is a 252-line form. | Group by user task, reduce copy and card repetition, disclose advanced targets progressively. |
| Auth/onboarding | Quiz spans 344 XAML lines and reuses activity choices. | Clear progress, optional fields, recoverable validation, and readable choices at large text. |
| Trust/support | Feedback, privacy and health sync are connected but show different kinds of data sharing. | Precise copy and actions, consistent visual structure, no false privacy or health claims. |

## Sequence and file ownership

1. The injury and settings-routing fixes are the implementation baseline. Core tests, XAML/SVG parsing, simulator compilation, source security checks and an authenticated device pass form the release gate.
2. Shared foundation and all ten surface workstreams have been integrated. The integration lead owns `MauiProgram.cs`, Core services/ViewModels, tests, and conflict resolution.
3. When an authenticated simulator session is available, review injury Add/Edit/Clear/reopen, settings Back/Save, privacy and feedback on device. Record observed behavior separately from the authorization already given to proceed.

Ownership includes each named XAML file's `.xaml.cs` only for presentation wiring and lifecycle cleanup. Changes to a shared file require coordination with Agent 1 and the integration lead. No agent owns injury-log files in this app-wide pass; the current injury implementation is a fixed input pending hands-on visual review.

## Delegate-ready prompts

Copy **one complete numbered prompt** to each agent according to the dependency order above. Implementation is authorized; do not wait for the separate injury visual review to dispatch work. All paths are relative to `src/MuscleCuties.App/` unless otherwise specified. The applicable mode in Impeccable is **Operate**: task completion, scanability, native affordances, and consistency outrank decorative novelty. The installed skill has no separate `taste-skill` command; use its context, Operate, native, and craft guidance for each owned surface.

### 1. Foundation: palette, type, header and tabs

> Workstream 1 is authorized and underway. Own only `Resources/Styles/Colors.xaml`, `Resources/Styles/Styles.xaml`, `Controls/Shared/PageHeader.cs`, `Controls/Shared/IconSectionHeader.xaml` and its code-behind, `AppShell.xaml`, and tab icon assets. Establish semantic light/dark text, surfaces, borders and status colors; replace tiny/low-opacity shared subtitles and make Back a labeled 44 pt touch target. Define consistent type steps, card spacing and selected/disabled/error affordances without changing routes or product meaning. Inspect five tabs and one secondary page at compact phone and tablet widths, normal and 200% text, both themes and screen-reader focus order. Measure contrast for body/placeholder and large text. Preserve MVVM, local health data, dormant sync, async SQLite and all XAML bindings. Run XML/diff checks and a targeted MAUI compile; report screenshots, measured contrast, changed files and remaining risks. Do not delete a bound C# member without searching every XAML file.

### 2. Authentication and onboarding

> Start after Agent 1 integrates. Implementation is authorized. Own `Pages/Startup/AppStartupPage.xaml`, `Pages/Auth/LoginPage.xaml`, `Pages/Auth/RegisterPage.xaml`, `Pages/Onboarding/ProfileSetupPage.xaml`, `Pages/Onboarding/QuizPage.xaml`, and `Controls/Profile/ProfileImageCropModal.xaml` with their presentation code-behind. Simplify first-run reading order, mark optional fields, show quiz progress and slider endpoints, and make error/retry/busy states recoverable. Consume the activity picker from Agent 6; do not edit its files. Verify login, registration, interrupted/resumed onboarding, quiz retry, keyboard completion, 200% text, compact phone/tablet, themes and spoken controls. Preserve all save/validation and offline health logic; route ViewModel data through services only, keep database calls async, and search XAML before removing any property/command. Run parse, diff and targeted compile checks; provide before/after captures and unresolved device findings.

### 3. Today dashboard

> Start after Agent 1 integrates. Implementation is authorized. Own `Pages/Dashboard/DashboardPage.xaml`, `Controls/Dashboard/DashboardCalendarCard.xaml`, `DashboardNutritionCard.xaml`, `DashboardPhaseCard.xaml`, `DashboardTargetsCard.xaml`, `DashboardWorkoutCard.xaml`, `ReadinessRecoveryCard.xaml`, and `DashboardMonthGrid.cs`, plus presentation code-behind. Arrange today's next action and current status before secondary information; raise 9–11 pt labels where they convey information, reduce competing card chrome, and retain readiness, recovery, phase, workout, nutrition and injury accuracy. Test empty and populated days, long copy, cleared injury state after returning from the log, tab navigation, large text, themes and screen-reader order. Do not change planning algorithms or access repositories from a ViewModel. Keep health state local and existing XAML bindings intact; use async persistence. Run parse, diff, focused tests and targeted compile, then report screenshots and any source-only assumptions.

### 4. Daily check-in

> Start after Agent 1 integrates. Implementation is authorized. Own `Pages/Dashboard/DailyCheckInPage.xaml` and `.xaml.cs` for UI wiring only. Make sleep, energy and pain choices readable and their selected values explicit; label weight optional and private; keep Submit and Skip reachable above the keyboard and at 200% text. Preserve conservative handling of null or absent biometrics, validation, save semantics, and existing local-only data flow. Test no selection, partial entry, full entry, failed save, busy, keyboard, VoiceOver/TalkBack, compact phone/tablet and both themes. Do not inject `AppDatabase` or a repository into a ViewModel; keep EF queries async and check XAML before removing any member. Run XML/diff checks, focused tests where behavior is touched, and targeted MAUI compile; report device evidence separately from source findings.

### 5. Cycle

> Start after Agent 1 integrates. Implementation is authorized. Own `Pages/Cycle/CyclePage.xaml`, `CyclePhaseDetailPage.xaml`, `Controls/Cycle/CycleCalendarCard.xaml`, `CycleDatePhaseModal.xaml`, `CyclePhaseGuideList.xaml`, `CycleWarningPopup.xaml`, `CycleMonthGrid.cs`, and presentation code-behind. Make selected day, logged versus predicted phase, warnings and guidance clear without hiding useful text behind truncation. Keep the existing medical meaning and uncertainty; invent no cycle facts or data. Inspect empty history, month navigation, phase detail, warning acknowledgement, compact phone/tablet, 200% text, themes and screen-reader narration. Preserve service facades, local cycle state, safe fallback math, async SQLite and XAML bindings. Run parse, diff, relevant Core tests and targeted compile; record clipped text and contrast findings with screenshots.

### 6. Training plan and activity choice

> Start after Agent 1 integrates; implementation is authorized. Coordinate picker reuse with Agent 2 and settings route with Agent 9. Own `Pages/Workout/WorkoutPage.xaml`, `Pages/Profile/ProfileWorkoutPreferencesPage.xaml`, `Controls/Workout/FeaturedWorkoutCard.xaml`, `WorkoutActivityCard.xaml`, `WorkoutSwapPanel.xaml`, `WorkoutActivityPreferencePicker.xaml`, `StrengthTrainingStylePicker.xaml`, and presentation code-behind. Make the weekly plan scannable, selections legible without relying on pink alone, strength minimum discoverable, and the injury banner compact with a clear path to the current log. Keep Back-to-Profile and successful Save-to-Workout semantics. Test active and cleared injuries, empty plans, swaps, required-strength validation, profile/onboarding reuse, compact light theme, dark theme, large text and spoken selection. Do not edit injury-log files, planning engines or repositories. Preserve local health rules, async DB, service facades and XAML bindings; run parse, diff, focused tests and targeted compile with device screenshots.

### 7. Active workout session

> Start after Agents 1 and 6 integrate. Implementation is authorized. Own `Pages/Workout/WorkoutSessionPage.xaml` and `.xaml.cs` for UI wiring only. Show a clear sequence from overview to exercise to logging to finish; make prescribed targets and entered results readable; explain blocked movements without burying the active action. Validate zero exercises, resume, previous/next, numeric keyboard, retry, busy overlay, finish and accidental back in both themes at compact/large widths and 200% text. Check spoken state and touch targets. Keep session and injury decisions local, avoid synchronous UI-thread I/O, use services rather than repository access in ViewModels, and preserve XAML-bound members. Run parse, diff, focused workout tests and targeted compile; supply action-by-action device evidence.

### 8. Nutrition tracking

> Start after Agent 1 integrates. Implementation is authorized. Own `Pages/Nutrition/NutritionPage.xaml`, `Controls/Nutrition/MealEntryModal.xaml`, `MealEditorModal.xaml`, `MealDetailModal.xaml`, `MealSuggestionModal.xaml`, `NutritionBalanceCard.xaml`, `NutritionBreakdownModal.xaml`, `NutritionPhaseFocusCard.xaml`, `Controls/MacroPieChart.cs`, `Controls/Shared/MacroProgressGrid.xaml.cs`, and presentation code-behind. Shorten the logging path, raise tiny metadata, reveal long food names and source details, and make portions/totals/nutrient gaps explicit. Test empty log, search, suggestion, add/edit/delete/cancel, long names, missing nutrients, offline use, keyboard and screen reader in both themes and at large text. External food queries may contain only generic ingredient strings, never user health state. Preserve MVVM service facades, async SQLite and every runtime XAML binding. Run parse, diff, focused nutrition tests and targeted compile; report which external-query behavior was source-checked and which UI states were device-checked.

### 9. Profile and settings

> Start after Agent 1 integrates; implementation is authorized. Coordinate with Agent 6 without editing its activity picker. Own `Pages/Profile/ProfilePage.xaml`, `ProfilePersonalInfoPage.xaml`, `ProfileUnitsDisplayPage.xaml`, `ProfileNutritionSettingsPage.xaml`, and presentation code-behind. Group settings by user task, improve the 9 pt profile-stat captions, reduce repetitive cards, and reveal advanced macro/micronutrient controls progressively while retaining every setting and saved value. Keep account, body and cycle fields clearly bounded and private. Verify unit switching, field labels, validation failure, persisted/reopened values, Back-to-Profile and successful nutrition Save-to-Nutrition, long forms, keyboard, themes, 200% text and screen readers. Do not put health state in Preferences/cloud or inject database/repositories into ViewModels; use async persistence and check all XAML bindings before deleting members. Run parse, diff, focused tests and targeted compile with route and device evidence.

### 10. Trust and support

> Start after Agent 1 integrates. Implementation is authorized. Own `Pages/Profile/ProfileFeedbackPage.xaml`, `ProfilePrivacyPage.xaml`, `ProfileHealthSyncPage.xaml`, `Resources/Images/feedback_note.svg`, `Resources/Images/privacy_device.svg`, and presentation code-behind. Refine the existing feedback/privacy drafts, avoid repeating claims, show data boundaries visually, and make optional reply email/attachment and dormant health sync unmistakable. Verify every privacy sentence against code: health state remains local; generic food names may reach the external catalog; feedback opens an email draft; provider sync stays disabled. Test attach/remove/send/cancel, reply-email on/off, privacy Back-to-Profile, email composer failure, sync unavailable, compact phone/tablet, themes, 200% text and spoken labels. Do not enable health providers, alter network payloads, or promise anonymous feedback while the draft auto-includes a profile name. Preserve bindings, service facades and async DB; run parse, diff, focused tests and targeted compile, and distinguish source review from device review.

## Repeatable self-check and integration loop

Use a **bounded two-pass** review per workstream: one batched inspection of compact and larger layouts in light/dark, then one fix batch and one confirmation. Stop polishing after confirmation and move remaining suggestions into the handoff. Reopen a pass only for a concrete failing requirement.

1. **Reason from the task:** List the actual user action, current state, desired state, error/empty states and files to change. Read the Impeccable context and relevant Operate/native reference. Screenshot the incumbent route if available; otherwise mark the conclusion source-based.
2. **Architecture and privacy:** Check App/Core separation and service facades. Search for direct `AppDatabase`/`IRepository` use in changed ViewModels, new cloud/network paths, health data in `Preferences`, and any production health-provider wiring. Confirm external food queries remain generic.
3. **Bindings and lifecycle:** Search **all `.xaml`** for any property, command or UI carrier before rename/removal. Check unsubscribe/disposal of changed event handlers and avoid synchronous `.Wait()`/`.Result` in preload or initialization.
4. **Data safety:** Ensure every changed EF query is async. For readiness/cycle logic, check nulls, empty history, zero/negative durations and conservative fallback records. Verify injury Clear changes only the signed-in user's record, keeps history and refreshes the local plan.
5. **Accessibility and visual QA:** Inspect both themes, compact phone and tablet, normal and 200% text, keyboard, 44 pt targets, contrast (body/placeholder at least 4.5:1; large text at least 3:1), focus order and spoken selected/error states. Capture before/after images and name any state not reached.
6. **Behavior and regression:** Run targeted Core tests for changed logic, XAML/SVG parse, `git diff --check`, and a targeted MAUI compile. At integration, run full Core tests and iOS/Android compile where available; separate native packaging limitations from compilation failures. Exercise Back, successful Save, failed Save, tab return, injury Add/Edit/Clear/reopen and offline food search on device.
7. **Dead code:** Identify candidates with `rg`; inspect C# references **and XAML strings/bindings** before removal. Delete only demonstrated dead code and run the same tests again. Record hypotheses without deleting them.
8. **Handoff:** Give the integration lead changed files, exact commands/results, device/OS/theme/text scale, screenshots, privacy/architecture findings, and any unresolved risk. The lead reconciles cross-surface components before accepting a workstream.

Acceptance is complete only when the ten owned surfaces and the integrated five-tab app have device evidence, no privacy or MVVM regressions, accessible primary actions, and passing appropriate checks. The user has authorized this broader implementation. Hands-on injury visual review remains a separate pending acceptance check.

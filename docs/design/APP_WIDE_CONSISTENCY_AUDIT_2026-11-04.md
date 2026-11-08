# App-wide consistency and feature coverage audit — 2026-11-04

## Result and scope

Preserve the approved Soft Minimal system and finish its shared composition, interaction states and feature coverage. A new visual direction is unnecessary. The strongest remaining gaps are inconsistent page anatomy, ambiguous or unconfirmed data states, incomplete accessibility semantics, and implemented capabilities with no mobile journey.

This is a **source audit and proposed implementation plan**, not a UI migration or release approval. No application code, design authority, tests, cloud resources, databases or Notion records were changed. No MAUI build, app execution, new screenshot capture or test suites were run. “Connected” below means a concrete source path exists, not that the journey works on a device. A later owner-supplied screenshot/crash follow-up is recorded in the addendum below and the [remediation plan](UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md).

### Scoped handoff

| Field | Recorded value |
| --- | --- |
| Package | UI-AUDIT-20261104 |
| Accountable role | Current audit orchestrator; implementation ownership remains with the repository's product/integration roles |
| Checkout | `/Users/denispetruk/RiderProjects/MuscleCuties` |
| Branch / baseline HEAD | `docs/github-transition-governance-20261104` / `acdb131140dec58c4625132682982b7baba4a4c7` |
| Initial state | Clean; no pre-existing changes to preserve in this checkout |
| Owned write path | This dated audit only |
| Excluded writes | All application/test/configuration source, canonical DESIGN/specification/STATUS/ROADMAP, remote Git, cloud and database state |
| Dependencies | Current approved design and product contracts; no dependency on the parallel rebuild's unreviewed changes |
| Shared-file reservations | None; this report does not reserve or edit shared contracts |
| Next checkpoint | Rebase/revalidate the findings against the rebuild candidate before implementing a bounded package |

The owner-designated Rider checkout is the implementation baseline. The Notion project page was read and confirms that repository plans stay in the repository and that Notion is outside CI, release approval and recovery. This repository-specific report is therefore kept here. The current request authorizes this audit; proposed fixes remain proposals.

## Review method and authority

Read [AGENTS](../../AGENTS.md), [architecture](../ARCHITECTURE.md), [product workflows](../PRODUCT_WORKFLOWS.md), [safety](../SAFETY.md), [verification gates](../VERIFICATION_GATES.md), [DESIGN](../../DESIGN.md), the [presentation specification](../UI_PRESENTATION_DESIGN_SPEC.md), existing bindings/evidence and the [UI gate register](GATE_REGISTER.md).

- **Impeccable:** ran its context loader once; used native iOS audit and Operate guidance. Missing PRODUCT.md does not block this review of existing code. No new visual world or product interview was introduced.
- **Design Taste:** used its complete app-screen reference as a composition, density and data-honesty lens. This is a native MAUI app, so no website draw, CSS import, marketing imagery/motion quota or browser inspector was applied. Its desktop study cannot certify iPhone layouts.
- Existing approved palette, typography, Fluent icons and component budget take precedence over generic skill defaults. Do not incidentally replace Open Sans with Inter/SF, remove semantic phase colors, or choose another palette.
- Current-source evidence outranks dated migration counts. Historical green checks are useful context, not current execution evidence.

**Native conformance verdict: not assessed from rendered evidence. Audit score: not assigned.** Accessibility, performance, appearance, platform conformance and adaptivity cannot honestly receive a complete `/20` score from this pass. No committed current app goldens were located in the searched source/test/docs inventory; the four reference screenshots are design inputs, not current app captures. Device gates remain unverified under the task-specific authorization rule in AGENTS/G5.

## Current system inventory

The non-generated UI tree contains **24 page XAML files, 38 control XAML files, 67 XAML files in total, and 52 ViewModels-directory C# files**. These are file counts, not counts of independent visual families or proven features. Total XAML includes non-page resource/application files and a legacy platform application file. The active target is `net10.0-ios` (`MuscleCuties.UI.csproj:4`).

What is already established and should be retained:

- `Resources/Styles/Colors.xaml` owns semantic light/dark roles and bootstrap resources; `Styles.xaml` owns shared styles/geometry. Do not create a second token catalog.
- Exactly `ButtonPrimary` / `ButtonSecondary` and `CardPrimary` / `CardSecondary` provide the permitted visual styles. Selection, error, disabled and busy are states, not new skins.
- The five Shell destinations are Today, Cycle, Train, Nutrition and You (`AppShell.xaml:45` onward). All 24 page class names appear in the Shell template/registration source. That is an inventory fact, not proof of entry-point reachability.
- `PageHeader` supplies wrapping title, back action and heading semantics. Eight pages use it; sixteen compose their own structure or have no conventional header.
- Shared modal lifecycle, localized catalogs, token guards, nutrition presentation mapping and several real virtualized lists already exist. Preserve them and strengthen gaps rather than restarting the old migration.
- `MauiProgram.cs:128-129` registers **Open Sans** under the historical `Inter` aliases; `Colors.xaml:258` routes `AppFontFamily` to that alias. The shipped face must not be described as Inter.

### Complete page inventory

Paths here are under `src/MuscleCuties.UI/Pages/`. “Custom” identifies source composition, not a rendered failure. Startup and auth/root/detail states need different content slots within the same frame, not identical controls on every page.

| Page | Shared PageHeader | Review focus |
| --- | --- | --- |
| Auth/LoginPage.xaml | No | Shared auth/root frame; sign-in, recovery and registration entry |
| Auth/OtpVerificationPage.xaml | No | Verification state, retry and return |
| Auth/PasswordRecoveryPage.xaml | No | Request/reset progression and input preservation |
| Auth/RegisterPage.xaml | No | Registration, validation and keyboard/footer |
| Onboarding/ProfileSetupPage.xaml | No | Form frame, required context and commit |
| Onboarding/QuizPage.xaml | No | Progress/choice slots, selection and Next |
| Startup/AppStartupPage.xaml | No | Bootstrap/loading/recovery state within shared frame |
| Dashboard/DashboardPage.xaml | No | Next useful action, heading, check-in and summaries |
| Cycle/CyclePage.xaml | No | Calendar/logging, known vs estimated vs stale |
| Cycle/CyclePhaseDetailPage.xaml | Yes | Detail/back and phase uncertainty |
| Nutrition/NutritionPage.xaml | No | Header width, meal planning and logging |
| Nutrition/FoodLogPage.xaml | No | Header actions, date navigation, selection and save states |
| Workout/WorkoutPage.xaml | No | Plan/schedule, resume, injury and archive entry |
| Workout/StrengthPlanGenerationPage.xaml | Yes | Preview, Apply, stale/lease/retry and first-plan exception |
| Workout/WorkoutSessionPage.xaml | No | Session modes, logging, safe exit and resume |
| Workout/WorkoutHistoryPage.xaml | No | Header width, detail/correction and return |
| Profile/ProfilePage.xaml | No | Heading and settings/disclosure groups |
| Profile/ProfilePersonalInfoPage.xaml | Yes | Editable values, units and missing measurements journey |
| Profile/ProfileNutritionSettingsPage.xaml | Yes | Setting → save → read-back |
| Profile/ProfileWorkoutPreferencesPage.xaml | Yes | Preferences → plan review |
| Profile/ProfileUnitsDisplayPage.xaml | Yes | Saved units consistently applied elsewhere |
| Profile/ProfileFeedbackPage.xaml | Yes | Validation and supported delivery |
| Profile/ProfilePrivacyPage.xaml | Yes | Clear privacy/account actions and consequences |
| Profile/PhotoJourneyVaultPage.xaml | No | Accessible back action, heading and media states |

## Prioritized findings

Twelve grouped findings: **0 P0 established, 4 P1, 7 P2, 1 P3**. Priorities describe user impact and implementation order; a source risk is not a claimed observed device failure.

### F01 — P1: Injury answers have three meanings but two visible states

`Controls/Injury/InjuryLogContent.xaml:169-172` binds the clinician answer to a boolean Switch. `ViewModels/Workout/InjuryLogViewModel.cs:418-420` displays nullable answers as false/off while retaining the null separately; `:323-330` preserves that original answer when saving. `WorkoutInjuryContracts.cs:27-44` explicitly allows null.

**Impact:** Unanswered and explicit No appear identical, and a new untouched off switch saves unanswered. Persistence safeguards are present; this finding does not establish data loss. **Proposal:** use the shared choice anatomy for Unanswered/Yes/No, preserve existing safety policy and verify all three round trips. Suggested skill: `impeccable harden`.

### F02 — P1: Hydration can appear saved after persistence fails

`ViewModels/Nutrition/FoodLogViewModel.cs:755-779` changes the displayed total before awaiting persistence and catches failures silently. There is no rollback, pending/error indicator or serialized write behavior in these methods. Contrast food commit handling, which uses writing/loading state.

**Impact:** The shown total can disagree with saved state; repeated input needs explicit ordering. **Proposal:** one shared pending/saved/failed contract, with confirmed read-back or recoverable optimistic state, retained input and retry. Resolve this before extending hydration features. Suggested skill: `impeccable harden`.

### F03 — P1: Cached cycle data loses freshness and changes uncertainty

`src/MuscleCuties.Infrastructure/Cycle/Overview/CloudCycleOverviewStore.cs:31-62` accepts stale cache, returns an ordinary snapshot, defaults invalid phase to Follicular and reconstructs ovulation/fertile dates with fixed offsets. The live mapper at `:69-88` preserves Unknown and server nullable dates. Gateway freshness metadata exists (`ApiClient/MuscleCutiesBackendApiClient.cs:420-431`) but does not survive this presentation path.

**Impact:** An offline view can present a different degree of certainty from the live view. No clinical outcome was tested or inferred. **Proposal:** preserve Unknown, provenance, timestamps and unavailable values; show cached/stale status and use the established domain/server contract for estimates. Do not invent new health rules in UI. Suggested skill: `impeccable harden`.

### S01 — P1: Custom interactions omit accessible names or selection state

Food Log's PreviousDay/NextDay tappable Borders (`FoodLogPage.xaml:104-118`, `:168-183`) and Photo Journey's back Border (`PhotoJourneyVaultPage.xaml:22-34`) contain only icon content with no action name. `HeaderIconButton` supplies no name (`Styles.xaml:785-800`). `QuickFoodItemRow.xaml:16-31` names the food but does not expose selected/not-selected state; its checkmark changes visually. Compare the shared `ChoiceCell` semantics (`Styles.xaml:1302-1313`).

**Impact:** Source does not communicate the action/state needed for nonvisual operation; actual VoiceOver traversal/activation remains unverified. **Proposal:** localized action names and selected state on the interactive target, then device validation of focus, activation and announcements. Suggested skill: `impeccable audit`, then `impeccable harden`.

### S02 — P2: Shared tokens do not yet produce one shared page composition

Only 8/24 pages consume `PageHeader`. Its API (`Controls/Shared/PageHeader.cs:10-19`) has Title/Subtitle/BackCommand but no root/back visibility or contextual-action slot. Food Log composes four columns with Today/date controls (`FoodLogPage.xaml:22-81`); History composes another header with no width cap there (`WorkoutHistoryPage.xaml:20-70`); Nutrition's header is uncapped above capped content (`NutritionPage.xaml:19-29`).

Seven explicit title sites also lack level-one heading semantics: Dashboard `:25`, Photo Journey `:37`, Profile `:19`, Food Log `:49`, Nutrition `:20`, Workout `:29`, History `:46`. `PageTitle` does not own heading level (`Styles.xaml:460-465`); `PageHeader` sets it (`PageHeader.cs:68`).

**Impact:** Composition and screen identity can drift even when every dimension is a token. **Proposal:** complete one frame/header contract with root/detail/auth/progress content slots, then migrate callers while retaining commands, native back gestures and bindings. Do not blindly insert today's back-only header into root pages. Suggested skill: `impeccable layout`, then `impeccable adapt`.

### F04 — P2: Several implemented capabilities have no complete mobile journey

- Reschedule: endpoint `WorkoutPlanAndSessionEndpointMappings.cs:32`, use case `WorkoutOverviewUseCase.cs:24-28`, adapter `CloudNutritionAndWorkoutStores.cs:460-462`; no UI caller located.
- Movement-note editing: session endpoints `:176`, `:201`, `WorkoutSessionActionUseCase.cs:78-92`, adapter `:560-567`; no ViewModel/editor caller located. A read-only `FeaturedWorkoutCard.xaml:297-300` label does not provide editing.
- Personal records: archive endpoint `WorkoutArchiveEndpointMappings.cs:171-182` and `ServerWorkoutArchiveUseCase.cs:89-92`; omitted from mobile `IWorkoutArchiveStore` and no UI surface located.
- Circumference measurements: CRUD/history endpoints `HydrationAndFastingEndpointMappings.cs:147-195` and `BodyMeasurementUseCases.cs:5-82`; only dormant fields/collection at `ProfilePersonalInfoViewModel.cs:284-287`, no mobile load/save/bindings located.
- Rich hydration: persistent presets/reorder/intake/correction/undo/reminder implementation exists, while current UI only changes daily total by 250 ml. See the capability ledger.
- Readiness history and per-day session-attempt history have contracts/use cases but no corresponding history surface located; current summaries/global workout history are different capabilities.

**Impact:** Written code alone does not deliver these user outcomes. **Proposal:** disposition every capability as intended UI work, internal support, intentionally dormant/excluded or legacy. Prioritize explicit preservation requirements such as measurements; do not add a dashboard tile for every API method. Suggested skill: `impeccable shape` for approved missing journeys.

### F05 — P2: Advanced diary copying is only partially surfaced

Server preview/commit and Merge/Overwrite exist (`NutritionBatchAndCopyEndpointMappings.cs:105-155`). The client uses older `/copy` (`CloudNutritionAndWorkoutStores.cs:180-188`). Food Log has a source-date command/property (`FoodLogViewModel.cs:721-745`) with no XAML consumer located; `CopyOverwriteMode` does not reach the copy request. The visible shortcut copies yesterday (`FoodLogPage.xaml:356`).

**Impact:** Arbitrary-date preview/commit and overwrite semantics are not the same feature as the connected yesterday shortcut. **Proposal:** choose one intended copy workflow; show destination, preview and conflict behavior before committing; preserve existing logging. Suggested skill: `impeccable shape`, then `impeccable harden`.

### F06 — P2: Units preferences are saved but not consistently honored

`ProfileUnitsDisplayViewModel.cs:69-75,91-105` persists metric/imperial defaults; `DashboardViewModel.Trends.cs:50-68` still explicitly formats kg/km. Personal information uses HeightCm/WeightKg (`ProfilePersonalInfoPage.xaml:81-98`).

**Impact:** Settings and displayed/entered values can disagree. **Proposal:** a shared presentation-unit formatter/converter with unit labels, locale formatting and input round trips to unchanged canonical domain units. Verify setting → entry → persistence → read-back across all relevant features. Suggested skill: `impeccable harden`.

### G01 — P2: Existing source guards overstate behavioral coverage

`UiContractGuardTests.cs:561-614` scans for a page name outside its own page; it includes `AppShell.xaml.cs`, whose registration already contains `nameof(Page)`. Thus registration can satisfy its own entry-point check. Navigation-contract declarations also count without callers. Back-command substrings do not establish an enabled visible return action.

The header guard only checks strings in `PageHeader.cs` (`:183-190`); accessibility checks localize attributes when present without requiring names/state (`:194-240`). Contrast gates check 22 declared semantic pairs, not every composed state or rendered pixel (`UiStyleGateTests.cs:66-90,295-318`).

**Impact:** Green source checks can coexist with missing UI journeys or semantics. **Proposal:** retain useful structural checks, add checks for actual entry/control/command relationships and required semantic coverage, and keep rendered/device acceptance distinct. Prove a guard rejects a deliberately disconnected fixture rather than merely matching declarations. Suggested skill: `impeccable audit`.

### G02 — P2: Historical documentation can recreate retired styling

`UI_PRESENTATION_DESIGN_SPEC.md:97-114` contains old typography/button/card values and retirement wording. Current approved resource values and implementation evidence differ: section/card 18/16 semibold, caption 12, button 14 semibold/48 minimum/radius 12, cards inset 19/radius 16 (`Styles.xaml:468-480,514-520,558-559,588-589,914-916`; `Colors.xaml:266,273`). Do not change source to match stale prose.

`SOFT_MINIMAL_BINDINGS.md` still includes removed `Pages/Dashboard/DailyCheckInPage.xaml` and omits current `DailyCheckInContent.xaml`, `DailyCheckInModal.xaml` and `MealCardView.xaml`. Historical 25-page/35-control counts are not today's inventory.

**Impact:** Rebuild agents may reintroduce old values or mistake a historical binding ledger for current coverage. **Proposal:** submit a narrow authority-document correction under the existing approval rule, then refresh the binding inventory at the exact rebuild revision. This audit does not edit either authority. Suggested skill: `impeccable document`.

### G03 — P2: Health integration needs a precise dormant-scope record

iOS DI registers real `AppleHealthDataProvider` / `HealthSyncService` (`MauiProgram.cs:264-268`). The provider performs one-shot read-only StepCount (`Platforms/iOS/AppleHealthDataProvider.cs:7,25-67`). `ProfileHealthConnection.ConnectAsync` exists (`Services/Profile/ProfileHealthConnection.cs:35-48`), but no ViewModel/XAML consumer was found. Existing connected summaries can be read by dashboard/check-in adapters.

**Impact:** “No health implementation” and “active user-facing integration” would both be inaccurate. Repository policy says to keep health synchronization dormant without scoped owner authorization. **Proposal:** preserve the dormant UI boundary and reconcile historical authorization/provenance before changing acquisition or permissions. Do not enable it to raise feature-coverage counts. This is a scope question, not evidence of unauthorized data acquisition.

### D01 — P3: Legacy and equivalent definitions need explicit disposition

`IWorkoutPlanner` remains DI-registered (`MauiProgram.cs:308`) without an application consumer located; do not turn it into a silent API fallback or retire it without equivalence evidence. Some shared label styles are effectively redundant: CaptionSmall repeats CaptionLabel's size and CaptionStrong repeats its bold setting (`Styles.xaml:514-520,1263-1270`). The style guard compares immediate setters/BasedOn rather than resolved inheritance (`UiStyleGateTests.cs:321-334`).

**Proposal:** characterize real callers and effective style values, retain semantic roles, and remove only proven redundancy in a separate bounded cleanup. Forty-four Label styles do not automatically mean forty-four competing visual families. Suggested skill: `impeccable distill`.

## Capability-to-UI ledger

Evidence paths are under `src/MuscleCuties.UI/` unless otherwise specified. This is a capability-level audit, not a claim that every server route/domain method has been individually accepted. No percentage of “all features used” is justified yet. Every row below still needs current-candidate behavioral/device evidence.

| Capability | Disposition at this revision | Actual entry/trace or evidence gap |
| --- | --- | --- |
| Sign-in, registration, OTP, password recovery | Connected in source | LoginPage.xaml:83,102,115-117,131; composition at MauiProgram.cs:383-389,466-505 |
| Profile setup and onboarding choices | Connected in source | ProfileSetupPage/QuizPage plus onboarding use cases and composition |
| Preferences → generate plan | Connected in source | WorkoutPage.xaml:333 → WorkoutViewModel.cs:643-646 → preferences; MauiProgram.cs:644-649 injects generation navigation |
| Proposal preview → Apply | Connected in source | StrengthPlanGenerationPage.xaml:494,726 → VM commands :189-190 → proposal Create :754 / Accept :1168 → cloud/server |
| First-plan automatic acceptance exception | Connected; preserve | VM :421-426 delegates to WorkoutPlanningFlow.CanAutomaticallyApplyFirstPlan; not a general replacement-plan exception |
| Replacement decline/stale preview/lease | Connected in source | VM :1096-1187,1365-1430; takeover control in generation XAML :492 |
| Mixed schedule, reasons, omitted work, duration alternatives | Connected in source | Generation XAML :542-690 presents distinct preview sections |
| Schedule day/open, optional session, easier alternative | Connected in source | WorkoutPage.xaml:305-307 → WorkoutViewModel.cs:692-741 |
| Workout rescheduling | Implemented, unsurfaced | F04; endpoint/use case/adapter without UI caller located |
| Open/resume workout | Connected in source | WorkoutPage.xaml:100,305 → VM :683-701 → WorkoutPage.xaml.cs:61-76 opens day overview; fallback session route retained |
| Session start, timing, set logging, finish, abandon | Connected in source | WorkoutSessionPage.xaml:103-108,460,530 → VM :491,615,826,866,984 → active-session adapter/API |
| Exercise swap and optional sets | Connected in source | Session XAML :366-381,452 → VM :186-193,1093,1140 |
| Workout archive, set correction, session notes | Connected in source | WorkoutPage.xaml:82 → VM OpenHistory; History XAML :167,396,438 → VM :299,378,437 |
| Per-day session-attempt history | Client capability, unsurfaced | IWorkoutSessionActionStore.cs:8 / use case :34 / cloud adapter :472-474; global archive is separate |
| Exercise movement-note editing | Implemented, unsurfaced | F04; read-only label is not an editor |
| Personal records | Backend-only pending disposition | F04; server archive has it, mobile archive port does not |
| Injury management and plan impact | Connected with ambiguity | WorkoutPage.xaml:121 → VM :143; InjuryLogContent commands; F01 must distinguish null/No/Yes |
| Account calendar/time zone, reconciliation, validation | Internal support | App.xaml.cs:285, generation VM :445, WorkoutAccountCalendarClient.cs:21-36; needs outcome tests, not a standalone screen |
| Nutrition overview, meal suggestions/planning, targets | Connected in source | NutritionPage.xaml:48-55,123-128; MauiProgram.cs:433-446 composition |
| Diary search, barcode, quick calories, recipe building/templates | Connected in source | FoodLogPage.xaml:583,624,630,721-724,808-824,938-944 → food/catalog/recipe adapters; runtime provider behavior unverified |
| Copy yesterday | Connected in source | FoodLogPage.xaml:356; do not equate with arbitrary-date preview/overwrite |
| Arbitrary-date copy, Merge/Overwrite preview/commit | Partial | F05 |
| Basic water daily total | Connected with save-state defect | FoodLogPage.xaml:337-346 fixed ±250 ml → VM :755-779 → daily-log metadata; F02 |
| Water presets, intake corrections/undo, reminder settings | Implemented, unsurfaced | HydrationAndFastingEndpointMappings.cs:32-145; dormant reminder fields at FoodLogViewModel.cs:724-726 have no UI/configuration consumers located |
| Circumference measurements/history | Backend implemented, mobile absent | F04; do not confuse with profile weight or body-weight trends |
| Cycle calendar, phase/symptoms, period start/end | Connected with cache-state defect | CycleViewModel.cs:99-113, QuickLog.cs:193-240; CloudDailyCheckInAndCycleStores.cs:339-445; CyclePage.xaml:71-100; F03 |
| Sleep/energy/soreness/stress check-in and current recovery summary | Connected in source | DailyCheckInViewModel.cs:125-128; ReadinessRecoveryCard.xaml:105-152 |
| Readiness history | Use case, unsurfaced | Cycle/ReadinessHistory/ReadinessHistoryUseCase.cs:3-11; DashboardViewModel.Trends.cs:13-26 offers nutrition/weight/workout, not readiness history |
| Profile/personal/nutrition/workout settings | Connected in source | MauiProgram.cs:395-416,621-658; ProfileViewModel.cs:76-81,167-177,197+ |
| Unit preferences | Partial application | F06 |
| Account deletion and photo vault | Connected in source | Profile/Privacy/PhotoJourney pages and composition; authorization, storage and completion outcomes unverified |
| Check-in/cycle reminders | Connected scheduling code | DailyCheckInNotificationService.cs:23-40; CyclePhaseNotificationService.cs:55-142; notification permissions/delivery not tested |
| Offline/cache presentation | Partial | Infrastructure exists; cycle loses provenance/freshness; test failure/pending states per feature |
| Apple Health StepCount | Native adapter; UI dormant | G03; no authorization or expanded integration inferred |
| Fasting | Intentionally excluded/dormant UI | Backend and legacy ProfilePersonalInfoViewModel.cs:289-292 state remain; do not activate |
| Local legacy planner | Legacy/DI-only candidate | D01; preserve until characterized |

## How to make the whole app consistent

### One shared implementation system

Use the current semantic resources and exactly the existing permitted families. Complete the frame's header/body/optional-footer ownership, with slots for title, back/root state, contextual action and optional progress/context. Common composition owns insets, width, heading semantics, safe-area/keyboard behavior and focus entry; a feature supplies content and commands.

Use a small set of content recipes already defined by the presentation specification: compact record row, labeled field, choice, summary metric, status/safety message, loading/empty/error panel, action footer and sheet. Do not require every feature to use identical density: logging between sets needs compact rows; a decision or empty state needs room for its action. Equal styling means stable meaning and anatomy, not equal heights or cards around everything.

Each recipe must carry its full state contract: loading, empty, unavailable, offline/stale, pending save, confirmed save, failed save/retry, disabled prerequisite, selection and success. Keep zero, missing, estimated and cached data distinct. Accessibility names, state, units and EN/RU copy belong to the contract, not an optional page polish pass.

### One acceptance row per intended user capability

For each implemented capability, record:

`User goal → visible entry → enabled control → command → use case → adapter/API → persisted result → refresh/read-back → return path`.

Add: owner, source revision, shared recipe/styles, units, localization keys, safety/privacy constraints, loading/empty/error/offline/conflict states, meaningful automated cases and native evidence. Missing links keep the row partial. Internal infrastructure gets an outcome/test link; intentional exclusions get a reason. Do not count DI registration, route declaration, a property or an unused command as completed UX.

The capability ledger becomes the check on feature preservation during refactoring. Extend existing repository evidence rather than create a separate Notion workflow or competing design system.

### Proposed order for the rebuild

| Package | Scope / purpose | Completion evidence |
| --- | --- | --- |
| UI-01 | Revalidate this audit at the rebuild candidate; give every capability an explicit disposition; propose the narrow authority-doc correction | Exact SHA, reviewed changes, current bindings/page inventory; integration authority handles canonical-file reservations |
| UI-02 | Fix F01-F03 data/state ambiguity without changing health policy | Null/No/Yes round trip; failed/rapid hydration writes; stale/invalid/unknown cycle cache; visible recovery paths |
| UI-03 | Complete shared frame and custom-control semantics; migrate representative root, detail, auth and logging screens, then the remaining pages | One composition contract, all applicable pages covered, contextual actions preserved, headings/names/selection state present |
| UI-04 | Connect intended missing journeys in separate vertical slices: measurements, schedule reschedule, movement notes, rich hydration; decide on personal records/history | Entry through persistence/read-back/return; no third style; existing data/history preserved; API compatibility |
| UI-05 | Finish copy workflow and app-wide units; refresh bindings and effective-style/legacy dispositions | Actual preview/conflict behavior, conversion round trips, no removed XAML contract, no silent fallback |
| UI-06 | Native audit/adapt/harden and final polish on the integrated candidate | Authorized iPhone evidence below; regression/source gates on the same candidate |

Product/mobile work belongs to the product orchestrator. Shared DTO/solution/canonical-design changes require the integration authority's exclusive reservation. Platform/schema changes are separate requests; none is assumed necessary merely to style the UI. No package is authorized to deploy by this report.

Skill sequence after approved fixes: `impeccable harden` → `impeccable layout` / `impeccable adapt` → `impeccable shape` for missing journeys → repeat `impeccable audit` → `impeccable polish`. Apply Taste's app-screen lens during composition review, without introducing a new visual direction. These can be run as bounded slices in the order the rebuild needs; final polish follows behavioral closure.

## Verification and acceptance

### Source and automated gates for future implementation

- Keep existing UiContractGuard, UiStyleGate, UiCleanliness/Reachability, localization, architecture and mobile/API contracts. Extend their actual coverage; do not add another overlapping generic checker.
- Make registered entry checks exclude registration/declaration-only evidence and validate bound callers. Require a semantic screen heading, localized icon action names and accessible custom-selection state where applicable.
- Resolve style inheritance before declaring variants distinct or redundant. Check semantic usage and real composed contrast; token membership alone is insufficient.
- Add meaningful behavior cases for the changed seams: failed/rapid writes, stale data, unknown values, units, declined replacement, stale preview, resume/finish/cancel and retry/conflict. Use synthetic/disposable data. No tests were added or run by this documentation audit.
- Preserve first-plan automatic acceptance only under the existing domain rule; replacement acceptance and injury restrictions stay intact. Do not rewrite them to make a visual flow simpler.

### Required native evidence on the eventual candidate

Record device/OS/build/revision, scenario and outcome for each acceptance row. Cover supported iPhone sizes, light/dark including switching while open, EN/RU, largest supported Dynamic Type, VoiceOver focus/activation/announcements, Reduce Motion, keyboard/safe-area clearance, native back/dismiss, rapid sheet reopen and interrupted network. Simulator evidence gives layout breadth; physical-device evidence is needed where gestures, permissions or performance matter.

Walk through first plan, replacement decline/Apply, stale preview, schedule changes, session resume/log/finish/abandon, history correction, three injury-answer states, meal/search/barcode/copy, failed hydration save, cycle live/offline/unknown, units, measurements and privacy/photo actions. Do not mark a row complete because the page compiled or a token test passed.

The device rule comes from repository [AGENTS](../../AGENTS.md) and [G5](../VERIFICATION_GATES.md): MAUI build/run/screenshots require task-specific owner authorization. This audit did not seek it because it completed source analysis without app execution; native verification remains open. The prior one-time build authorization is not reused.

### Evidence from this audit

| Gate | This task's status |
| --- | --- |
| G0 baseline/scope | Recorded HEAD, clean initial state, bounded write path and unchanged application scope |
| G1/G4/G5 source analysis | Source inventory, actual command/adapter traces and existing guard inspection; no claim of runtime pass |
| G2/G3/G6/G7 behavioral/target evidence | Not collected; no domain, persistence, performance or deployed-compliance gate closed |
| G8 build/regression | Not run; documentation only |
| G9 handoff | This dated report; local source/link references and whitespace reviewed before delivery |

Canonical STATUS/ROADMAP/DESIGN were deliberately left to the current integration owner; this scoped handoff records the findings without competing with parallel governance/refactoring work.

## External guidance checked

The recommendations use current primary guidance, checked 2026-11-04, alongside repository contracts:

- [Apple UI Design Dos and Don'ts](https://developer.apple.com/design/tips/) supports readable, aligned touch interfaces and the 44 × 44 pt target used by the app's review contract.
- [MAUI accessibility and semantic properties](https://learn.microsoft.com/en-us/dotnet/maui/fundamentals/accessibility?view=net-maui-10.0) explains control semantics and platform-specific screen-reader behavior; names in source still require native validation.
- [MAUI fonts](https://learn.microsoft.com/en-us/dotnet/maui/user-interface/fonts?view=net-maui-10.0) documents registered font aliases and automatic font scaling. Do not mistake a literal default FontSize for proof that scaling is disabled.

These references support acceptance criteria; they do not establish tested conformance of this app.

## Owner screenshot and crash addendum — 2026-11-04

After the initial source audit, the owner supplied six cropped app screenshots and a native crash stack. Their installed build/source revision is unknown. The images were inspected locally; no real screenshots, private images or health/meal/history values were copied into the repository. These are observed visual defects and reported interactions, not a new agent-run device walkthrough or proof of a generator cause.

The [remediation plan](UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md) is the owner-requested implementation backlog for this follow-up. It preserves the original twelve findings above and adds:

- **PV01/AH01:** remove the rectangular privacy-message background within the existing hidden-photo surface, and align archive header/summary/filter widths with session rows.
- **NM01:** repair MealEditorModal's expanded context/ingredient viewport while preserving the compact meal/time state the owner prefers. Context locking and disclosure are separate responsibilities.
- **NM02:** repair FoodLogPage's grouped editor: flexible amount fields, locale-aware draft precision, content-fit/long-form sizing and a full-width sole Save. This is a different modal from NM01.
- **NM03:** add selected-date micronutrient breakdown parity to Food Log through shared UI-local presentation; explicitly address partial/unavailable nutrient coverage rather than reusing today's singleton state or treating missing data as measured zero.
- **PG01/CR01:** diagnose plan preflight/API/generator outcomes separately from native button-layout crashes, and repair Edit details/retry state/lifetime behavior. The repository's MAUI 10.0.60 fix comment is contradicted by primary version evidence in the plan; actual crashing binary identity and a controlled compatible fix candidate must be verified.

CR01 is a **reported P0 blocker, reproduction/root cause unverified**; PG01 and the obstructed/poor meal editors are priority functional work. Prior source-check passes do not close these observed/reported runtime findings. G5 is reopened for this follow-up; no corrected build, screenshot comparison or runtime acceptance exists yet.

Two prepared execution prompts divide Product A into coordinated [A.UI presentation/lifetime](orchestration/01-ui-ux-remediation-orchestrator.md) and [A.API wiring/diagnostics](orchestration/02-backend-wiring-orchestrator.md) sublanes. Existing Platform B and integration authority retain their responsibilities. Writing these documents launches no implementation and grants no MAUI, remote Git, database or release permission.

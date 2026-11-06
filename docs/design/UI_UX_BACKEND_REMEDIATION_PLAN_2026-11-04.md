# UI, UX and backend wiring remediation plan — 2026-11-04

## Status, scope and handoff

**Pending implementation.** This plan combines the owner's six screenshots and reported plan/retry crash with the [app-wide audit](APP_WIDE_CONSISTENCY_AUDIT_2026-11-04.md). It specifies fixes and acceptance; it does not claim they have been implemented or authorize a release.

| Field | Value |
| --- | --- |
| Planning package | UI-REMEDIATION-PLAN-20261104 |
| Source baseline | Rider checkout, branch `docs/github-transition-governance-20261104`, HEAD `acdb131140dec58c4625132682982b7baba4a4c7` |
| Pre-existing work | Untracked app-wide audit from the preceding pass; preserved and extended |
| This task's writes | This plan, two linked prompts, audit addendum and pending-work pointers in STATUS/ROADMAP/GATE_REGISTER |
| Excluded work | Application/configuration/test changes, MAUI build/run, new captures, commits/pushes, database/cloud operations and releases |
| Evidence limit | Screenshot build/revision and crashing installed binary are unknown; source mechanisms are correlations to this baseline |
| Next checkpoint | Existing integration authority pins the actual rebuild candidate and grants exclusive implementation reservations |

The user asked for documentation and two orchestration prompts. Drafting those prompts starts no implementation agents. The broader full-roadmap prompt's future execution grants are not standing authority for this task. No change to approved palette/font/style families or clinical rules is proposed.

The supplied images were inspected locally. Do not copy them or their health/photo/meal/history values into tracked documentation, test fixtures, external AI or public PRs. The evidence references below identify the supplied captures; synthetic equivalents must be used for reproducible tests. Attachment paths are temporary and are not a repository dependency.

## Evidence and priorities

| ID | User evidence | Observed/reported problem | Priority and evidence state |
| --- | --- | --- | --- |
| PV01 | Screenshot 1, `6.13.56 PM` | Rectangular dark privacy message patch inside a rounded photo surface | P2; visually observed; no privacy leak demonstrated |
| AH01 | Screenshot 2, `6.14.29 PM` | Archive summary/filter/heading block narrower than session rows | P2; visually observed |
| NM01 | Screenshots 3/4, `6.16.05 PM` / `6.17.24 PM` | Expanded meal/time context consumes logging space; owner reports blocked scrolling. Compact context in screenshot 4 is the preferred starting point | P1; cramped/clipped composition observed, gesture failure user-reported |
| NM02 | Screenshot 5, `6.18.22 PM` | Sparse tall edit sheet, clipped fractional amounts and half-width sole Save | P1; visually observed |
| NM03 | Screenshot 6, `6.19.22 PM` | Daily Food Log lacks the main Nutrition page's micronutrient breakdown entry | P2; source and user report confirm missing parity |
| PG01 | User's Edit details / Try again account | Backend failure state remains visible behind recovery; plan creation does not complete | P1; reported, no generation-failure screenshot/API response supplied |
| CR01 | User's native stack trace | NullReferenceException in `Button.LayoutButton` → `CrossPlatformArrange` → `WrapperView.LayoutSubviews` | P0 reported blocker; exact reproduction/root cause not verified |

The original audit's twelve findings remain open. The new priority register does not replace its F01 injury-answer ambiguity, F02 failed hydration save, F03 cached cycle certainty, S01 accessibility, shared-frame work or missing-feature dispositions.

## PV01 — Photo privacy surface

**Source:** `src/MuscleCuties.UI/Pages/Profile/PhotoJourneyVaultPage.xaml:142-168` places a separately colored centered Grid inside padded `CardSecondary`. Its background follows the message's bounds, correlating with the rectangular patch. `Models/UI/Profile/PhotoVaultItem.cs:19-21` nulls the image source and hides it while unrevealed. The screenshot is a visual defect, not evidence that a private image was exposed.

**Implementation:** let the existing rounded surface own the entire hidden-state background. Center the icon/message within it without a second rectangular fill. Keep hidden, unavailable and empty distinct. Preserve reveal/reset commands, null source while hidden, collection-cell reuse and navigation privacy reset (`PhotoJourneyVaultViewModel.cs:120-130`; page code-behind `:40-55`). Do not replace safe hiding with an actual blur of a loaded private image or add another card skin.

**Acceptance:** consistent rounded surface in both appearances; no inner patch; useful localized reveal label; no transient image before reveal; hidden/revealed/unavailable behavior survives scrolling/reuse and navigating away/back. Use synthetic media only.

## AH01 — Archive width and hierarchy

**Source:** `WorkoutHistoryPage.xaml:87-89` centers a maximum-width CollectionView header to its content. Item cards at `:160-164` use a different sizing arrangement; the list has horizontal inset at `:76` without the same shared width cap on the common container.

**Implementation:** give the virtualized list/container one shared maximum width/centering/gutter contract; its header and rows fill that same available width. Apply horizontal inset once. Summary fields can stack for large text; summary, filters, section heading and session cards keep their intended shared edges. Preserve the horizontal month scroller at `:113-115`, paging, selection and history editing. Reuse the shared frame work from audit S02.

**Acceptance:** summary and session-card outer edges align; filter and heading text edges are deliberate; long EN/RU labels and accessibility text do not squeeze values or controls. Retain virtualization and no horizontal page overflow. Distinguish absent set detail for non-strength sessions from a failed detail load; do not fabricate sets to fill the layout.

## NM01 — One coherent meal editor, compact context and usable scrolling

**Correct surface:** screenshots 3/4 are `Controls/Nutrition/MealEditorModal.xaml`, not the similarly named `MealEntryModal`.

**Source:** outer rows are `Auto,Auto,Auto,*,Auto` (`:21-23`). The collapsed food-finder body has another `Auto,*` grid (`:78-80`): macro summary, four meal choices, time and two secondary actions all occupy its Auto section (`:81-199`); ingredients receive only the remaining CollectionView height (`:201-241`). Footer is pinned separately (`:447-463`). `BottomSheetSizing.Apply` caps the outer height but does not guarantee a usable inner viewport.

`IsMealChoiceLocked` also determines presentation: new/manual entry unlocks it (`NutritionViewModel.MealEntry.cs:45-63,82-94`), editing locks it (`NutritionViewModel.Meals.cs:326-331`), and suggestions depend on available meal time (`NutritionViewModel.Suggestions.cs:289`). Selecting Lunch itself is not proof of a layout bug in the meal enum. Contractual lock and whether an editor is expanded are different concepts.

**Implementation:** default to the compact meal/time context already demonstrated by screenshot 4, with an explicit Change action only when the context is editable. Introduce independent disclosure state if needed; never set a contractual lock just to make the UI smaller. Preserve meal/time drafts through food finding, suggestion selection, back, save failure and reopening.

Choose one coherent body scroll owner for the summary/context/actions/ingredients, preferably retaining a virtualized ingredient list with the preceding content in its scrolling header. Keep the modal header and primary commit footer outside that scroll owner. If an alternative layout is chosen, prove a usable ingredient viewport while context is expanded. Do not nest an unbounded list in a ScrollView, mask content under the footer, or fix the problem using another fixed spacer/height.

Review the adjacent `MealEntryModal.xaml:51-190` entry path and transfer of EntryMealType/EntryMealTime into SelectedMealType/SelectedMealTime (`MealEntry.cs:91-92`). Reuse one context representation and the shared modal system; do not create a third meal-entry design.

**Acceptance:** fresh/manual/suggested/existing meals all have a reachable first ingredient, all rows and row actions can scroll into view, context can change only when allowed, and Log meal stays reachable above keyboard/home indicator. Cover empty/one/many ingredients, short/tall supported screens, EN/RU, largest text, food finder/back and rapid repeated opening. A static screenshot does not prove the scroll gesture.

## NM02 — Useful, responsive grouped-meal editing

**Correct surface:** screenshot 5 is Food Log's inline `AnimatedModalView`, not the editor above.

**Source:** `FoodLogPage.xaml:841-856` uses `ColumnDefinitions="*,100"` and binds Entry.Text directly to float Grams (`:847-851`; `Models/UI/Nutrition/HistoryIngredientEditor.cs:5-9`). `FoodLogPage.xaml.cs:19-23` applies viewport-height sizing. The footer retains two columns (`FoodLogPage.xaml:934-953`) when existing-meal state hides Delete/Cancel (`FoodLogViewModel.cs:159-166`), leaving the only Save half-width. This correlates with the narrow numbers, large void and weak action hierarchy.

**Implementation:** use labeled ingredient/amount/unit rows with enough flexible width for the whole editable number and a stacked fallback for long names/large text. Separate locale-aware editable draft text from the original canonical numeric value. Use bounded display precision, but preserve full original values for untouched fields; never round and resave every ingredient as an incidental presentation change. Follow existing domain validation for allowed amounts, with inline errors for incomplete, invalid or nonfinite input. Handle decimal comma/point and user locale deliberately.

Use a shared content-fit mode for short forms and a bounded scroll body for long ones, within the same `AnimatedModalView` anatomy. `BottomSheetSizing.cs:36-46` already has a content-fit helper, but its assumptions must match the inner Auto/star measurement; changing one method call alone is not a proven fix. Avoid unused full-height space and retain safe-area/keyboard/focus behavior.

When Save is the sole action, let it fill the available footer width using `ButtonPrimary`. Use existing secondary roles only when a real second action exists. Keep editable context and a compact current summary/validation feedback where useful; do not add decoration or another card to fill space.

**Persistence:** preserve `_draftDate` captured from the entry (`FoodLogViewModel.cs:478,591-613`), ownership/identifiers and `_overviewUseCase.UpdateMealAsync`. Nutrient recomputation stays in its current use-case/domain/server owner. Preserve the draft on failed save; success refreshes the same date/detail state.

**Acceptance:** full readable editable values; no-edit → Save preserves ingredient quantities; deliberate edits parse correctly; invalid edits cannot commit; failed save retains input; correct historical date persists; short/long forms fit/scroll; sole Save fills the footer; keyboard, close/cancel and reopen behave consistently.

## NM03 — Daily Food Log micronutrient parity and truthful coverage

**Existing data path:** `NutritionOverviewRequest(DateOnly Date, ...)` and response micronutrients already exist (`UseCases/Features/Nutrition/Overview/Contracts/NutritionOverviewContracts.cs:3-6,32-38,178-190`). The cloud store forwards that date (`Infrastructure/Cloud/CloudNutritionAndWorkoutStores.cs:29-39`); the endpoint authenticates the user (`ServerOverviewEndpointMappings.cs:15,25-30`); PostgreSQL scopes meals/nutrient joins to that user/date (`PostgresNutritionOverviewStore.cs:104-176`). FoodLog already receives `_overviewUseCase` (`FoodLogViewModel.cs:30,144`; `MauiProgram.cs:603-615`). A new endpoint is not the starting assumption.

Main Nutrition maps the data through `Services/Nutrition/NutritionPresentationMapper.Overview.cs:40-41`, UI-local `DailyMicronutrient` / `DailyMicronutrientItem`, formatting and `NutritionViewModel.Breakdown.cs:11-50`. However, `NutritionBreakdownModal.xaml:12-14,65-72` is typed to the main NutritionViewModel, which currently loads today (`NutritionViewModel.cs:414-420`). Mounting that singleton's state on Food Log would risk showing today's data for a historical date.

**Implementation:** extract/reuse a UI-local breakdown presenter/state and nutrient-list anatomy so both entry points use the same appearance, grouping, units and modal lifecycle. Add a discoverable breakdown action to Food Log's summary and load the overview for a captured SelectedDate. The sole Nutrition presentation mapper remains the contract boundary. Do not bind domain/use-case DTOs directly to XAML or duplicate nutrient math in a ViewModel.

Apply results only if account/session/date/request generation still match; share or strengthen `_dateLoadVersion` protection (`FoodLogViewModel.cs:791-801`). Clear old details on date/account change, departure/disposal and refresh after successful mutations (`:226-244`). Preserve context on retry; show loading, empty, failure and stale states truthfully. Scope meal-specific detail to the selected meal where supported; never substitute another meal's values.

**Important coverage limit:** current micronutrients derive from catalog/user-food LoggedMealEntries (`PostgresNutritionOverviewStore.cs:180,266-284`), while macro totals also include independent FoodItemEntries (`:182-196`); nutrient joins coalesce missing values to zero (`:139-150,164-175`). UI parity alone would reproduce incomplete/zero-filled coverage for some diary sources.

Deliver two explicit acceptance levels:

1. **Presentation parity:** the same selected-date breakdown is reachable from both surfaces, with the current coverage limitation documented. This does not close nutrition data-integrity acceptance.
2. **Truthful coverage:** known zero, unavailable and partial coverage are distinguishable through the owning calculation/response contract and UI. Support covered entry types without fabricated micronutrients. Any additive DTO/schema change needs an integration reservation and compatibility checks; never silently change zero semantics or add estimates.

**Acceptance:** distinct synthetic users/dates never cross; rapid date changes cannot replace current details with stale results; edits refresh totals; labels/units/order match the main page; partial/unknown data is explicit; EN/RU, accessibility and both appearances pass on the eventual candidate.

## PG01 / CR01 — Separate native crash, recovery UI and generation diagnosis

The supplied stack contains native button layout frames, not a generator frame. `Platforms/iOS/Program.cs:9` calls UIApplication.Main and is an entry point, not evidence that the fault originated there. A user-visible fallback message likewise does not prove a local workout generator ran; the reviewed path uses the server proposal client.

### CR01: Establish the binary and investigate native layout

`MuscleCuties.UI.csproj:12-14` pins MAUI 10.0.60 and incorrectly comments that it includes fix #35284. Primary upstream evidence shows the unguarded dereference in [10.0.60 Button.iOS.cs](https://raw.githubusercontent.com/dotnet/maui/10.0.60/src/Controls/src/Core/Button/Button.iOS.cs), a null guard in [10.0.90 Button.iOS.cs](https://raw.githubusercontent.com/dotnet/maui/10.0.90/src/Controls/src/Core/Button/Button.iOS.cs), and the fix listed in the [10.0.90 release](https://github.com/dotnet/maui/releases/tag/10.0.90). This corrects the repository claim; it does not prove the crashing installation matches that pin or that an upgrade alone fixes this case.

1. Record failing source/build identity, installed app version/build and resolved Controls assembly, SDK/workload/Xcode/iOS, configuration, AOT/trimming/SourceGen and exact navigation sequence. Treat old build records as historical.
2. Use deterministic synthetic proposal responses to reproduce preview/failure → Edit details → edit → retry; add rapid back/double activation and a response completing during navigation. Compare a fake response with actual generator execution to separate native lifetime from backend work.
3. Inspect page-lifetime cancellation and handler detachment: `StrengthPlanGenerationPage.xaml.cs:17-21` schedules loading on each appearance without departure cleanup; `PageLoadExtensions.cs:98-122` defers work without a page-lifetime token.
4. Inspect close-before-navigation semantics: EditDetails (`StrengthPlanGenerationViewModel.cs:1027-1047`) awaits logical close and restores a draft before popping. Close (`:1049-1064`) clears preview; `AnimatedModalView.cs:113-129` invokes a `void` asynchronous hide (`ModalTransition.cs:63-107`). Existing generation guards help stale callbacks, but do not prove native close/handler-detach completion is awaited.
5. Prepare a controlled, compatible MAUI version candidate containing the verified fix, initially comparing 10.0.90 or a later supported compatible release. Verify actual package assets/API/toolkit compatibility; do not blindly select “latest.” Reserve project/toolchain changes through the integration authority/Platform B. Keep the version and lifetime experiments attributable.
6. Preserve shared modal focus/isolation, add page-lifetime/generation safeguards where proven necessary, and coordinate close completion before navigation. Do not add arbitrary delays, replace all native buttons, or swallow the native exception as a claimed fix.

**Acceptance:** exact candidate and reproduction sequence survive normal/rapid transitions with no crash, stale updates or duplicate navigation; unaffected buttons, Shell tabs, SourceGen, modal focus and Reduce Motion still work. Native evidence is required and remains uncollected in this planning task.

### PG01: Diagnose API/generator outcome and expose one recovery state

**Source:** generation LoadAsync has multiple preflight stages (`StrengthPlanGenerationViewModel.cs:430` onward); CreatePreview validates/persists answers before proposal creation (`:655` onward). `CloudStrengthPlanProposalStoreV020.cs:105` turns offline/server failure into generic HttpRequestException, losing useful distinctions. The underlying response has ErrorCode/status/RequestId (`MuscleCutiesBackendApiClient.cs:50`); `Services/Api/ApiFailurePresenter.cs:79` already supports structured presentation. Create/accept endpoints are `WorkoutPlanningEndpointMappings.cs:100,133`.

**Implementation:** retain a typed failure/result boundary across adapter → use case → presentation, reusing the existing failure vocabulary/presenter where possible. Capture a safe correlation ID, route template, stage, status/error category and duration; do not log request bodies, health answers, identifiers, tokens or connection strings. Diagnose preflight, required/stale safety answers, lease/conflict, infeasible/limited proposal, transport/auth/schema/server failure and actual engine exception separately.

Show one coherent foreground state: preparing, needs input, preview, failed/retry or accepted. On Edit details, close/isolate the old foreground state before navigation and preserve the draft; an obsolete failure panel must not remain interactive behind it. Try again must operate once on the current draft, with cancellation/versioning and preserved accepted plan. Use bounded/idempotent behavior for retry and ambiguous success. Do not “fix generation” by weakening safety checks or adding a silent local fallback.

**Acceptance:** synthetic success and each named failure reach the correct action/state; editing a failed/limited preview returns to the correct preferences and current draft; retry updates the current page once; no duplicate proposal/acceptance, stale overlay or lost accepted plan. For a real generator fault, attach the actual failing stage and deterministic reproduction before changing domain code.

## Implementation packages and dependency order

| Package | Lead | Dependencies / bounded outcome |
| --- | --- | --- |
| RM00 | Existing integration authority | Pin rebuild candidate, preserve unrelated work, reserve exact paths and agree typed failures/fixture cases; reconcile with active roadmap |
| RM01 | A.UI + A.API; project change reserved to I/Platform B | CR01/PG01 investigation in separate attributable slices; crash/failure recovery evidence precedes release closure |
| RM02 | A.UI | PV01/AH01 shared surface/width corrections; independent of backend diagnosis after RM00 |
| RM03 | A.UI | NM01 compact context, scroll owner and shared modal sizing/lifetime; preserve source contracts |
| RM04 | A.UI; A.API reviews persistence | NM02 draft formatting/validation/footer/date preservation; serialize FoodLog/shared sizing edits with RM03/RM05 |
| RM05 | A.API data; A.UI presentation | NM03 selected-date breakdown and truthful-coverage contract; fixture/seam agreement first, shared mapper/control reservation exclusive |
| RM06 | A.UI + A.API | Carry audit F01-F03/S01 correctness/accessibility into the same integrated candidate; no new health policy |
| RM07 | Product A | Finish shared frame and remaining intended capability disposition from the audit, including measurements/reschedule/notes/hydration; use separate feature slices |
| RM08 | Integration authority + independent reviewer | Combined source/non-UI behavior/contract evidence, then explicitly authorized native scenarios and owner acceptance; final polish only after functional closure |

Do not wait for every cosmetic task to finish before investigating the crash. Independent source/UI work can continue while a runtime/target authorization gate is unavailable. Do not add migrations, cloud spend or release work unless a concrete implementation seam requires a separately approved action.

## Two orchestration prompts and collision prevention

Use [UI/UX remediation orchestrator](orchestration/01-ui-ux-remediation-orchestrator.md) and [backend wiring/diagnostics orchestrator](orchestration/02-backend-wiring-orchestrator.md). They are **A.UI and A.API, two bounded sublanes of the existing Product A role**. They do not rename or take over the repository's **B — Platform/schema/CI** role, and do not create a second integration authority.

Reuse the active integration control record if it exists; the full-roadmap prompt identifies `docs/architecture/github-transition/execution/CONTROL.md`. Confirm its actual owner/path before treating it as live. If absent, the existing integration authority records a scoped handoff and exclusive reservations; workers do not create competing control planes or allocate schema versions. Give each lead a separate suitable worktree/branch based on a pinned candidate through the supported worktree tool. Do not reset/copy over the running rebuild.

| Seam | Default responsibility / required reservation |
| --- | --- |
| Photo/archive pages, meal-editor layouts, modal/header/lifetime, input presentation | A.UI |
| Proposal adapter/error mapping, API/use cases, PostgreSQL query/coverage, synthetic backend fixtures | A.API |
| FoodLogViewModel.cs constructor/load/save/date hooks and new breakdown state | Split only by explicit reservation: A.UI draft/input changes; A.API data/lifecycle proposal; one editor at a time for the shared file |
| NutritionBreakdownModal, UI-local breakdown state, NutritionPresentationMapper partials | A.UI consumes an agreed data contract; A.API reviews data semantics; reserve every shared file before editing |
| NutritionViewModel root/partials, EN/RU catalogs, shared modal sizing/styles/tests | One package owner per file; do not merge independently authored local styles/state words |
| MauiProgram, UI project/package graph, shared DTOs/schema, canonical DESIGN/spec/STATUS/ROADMAP | Existing integration authority exclusively, or explicit temporary reservation; Platform B handles its existing platform responsibilities |

Workers exchange concrete contract/fixture proposals and handoffs through the coordinator. Shared filesystem/worktrees are not security isolation. No lead receives release authority from this plan. Use existing session grants when they apply; the act of drafting or reading a prompt does not grant MAUI, remote Git or cloud permission.

## Verification matrix and definition of done

Use [G0–G9](../VERIFICATION_GATES.md) and existing source/contract guards. Application changes are R2 where they affect health/data/contracts; this documentation task is R0. Planned checks below are not executed results.

| Area | Required evidence on the implementation candidate |
| --- | --- |
| PV01/AH01 | Shared resource/frame checks plus synthetic hidden/reveal reuse and rendered common-edge validation |
| NM01 | Actual scroll and row-action reachability in every entry/lock/disclosure state, with keyboard and translated/large text |
| NM02 | Locale parsing, untouched-precision preservation, invalid input, failed-save retained draft, correct historical date; content-fit/long-list/footer captures |
| NM03 | Authenticated user/date isolation, covered/partial/unknown fixtures, stale-response suppression, mutation refresh, shared presentation mapping and native entry/return |
| PG01 | Offline/timeout/auth/server/schema/required-answer/limited/conflict/stale-lease cases; repeat retry, lost acceptance response and navigation failure after success |
| CR01 | Proven failing/fixed binary identity, controlled reproduction, native stack/runtime result, framework compatibility and regression transitions |
| Prior audit | Null/No/Yes injury, failed/rapid hydration writes, cached cycle Unknown/freshness, accessible custom actions, shared frame and feature disposition |

Preserve first-plan automatic acceptance only under the existing tested rule; replacements require explicit Apply/acknowledgments. Unknown injury answers stay unknown, accepted plans/history remain intact, health integration stays dormant and fasting stays excluded. No UI-local nutrient or clinical calculations.

G5 completion requires both appearances, EN/RU, supported iPhone sizes/largest text, VoiceOver, Reduce Motion, keyboard/safe areas, native back/dismiss and interrupted-network behavior on the same candidate. MAUI build/run/install/new captures require task-specific owner authorization under AGENTS; the supplied screenshots authorize analysis, not a fresh run. Complete all independent authorized implementation and non-UI checks before requesting a concrete missing verification action. Record blocked/unverified gates without calling the work release-ready.

The implementation is done only when each required issue has a current-candidate fix, source/behavior evidence, native evidence where required and combined review. A visual cleanup alone does not close backend correctness; a server success alone does not close scroll, lifecycle or accessibility defects.

## Documentation-task evidence

Baseline/status and source references were inspected; six user images and the supplied trace were analyzed. Primary MAUI issue/version evidence was checked. Documentation links, tables, whitespace and owned-file scope were validated before delivery. No application tests, MAUI execution, backend probes, deployments or data changes were performed; all implementation packages above remain pending.

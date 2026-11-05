# MISSION — UI/UX and backend remediation

Entry point for `/mission` sessions. This file is the work board, the state of record for package
progress, and the session protocol. It implements
[the remediation plan](../UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md) under the repository contract
in [AGENTS.md](../../../AGENTS.md). Package design lives in the plan; this file tracks execution.

## Objective

Close every owner-reported defect in the plan on one integrated candidate: the native
`Button.LayoutButton` crash and plan-recovery failure first, then the screenshot defects, then the
audit's correctness, accessibility and feature-disposition work — each with evidence, and none by
weakening safety, inventing data, or hiding a backend failure behind a local fallback.

The mission is complete when every required package is `DONE` on one integrated candidate, with any
scope dispositions explicitly accepted by the owner. A session can checkpoint while a named owner
action is pending. The package and the mission stay open, and independent authorized work continues.

## Non-negotiable invariants

- Automatically activate an eligible first or replacement plan from an explicit foreground generation
  intent. Disclose replacement at the initiating action. Injury-specific exercise exclusion is the
  only workout fallback and the only extra acknowledgment, through one native iOS alert.
- Missing, stale or unknown injury answers stay distinct from false. Never relax a restriction or raise
  intensity because data is absent.
- No silent local workout generation, no UI-side nutrient or clinical math, no fabricated micronutrients.
- Health synchronization stays dormant; fasting stays excluded; accepted plans and history are never
  destroyed by a failed edit.
- `MauiProgram`, the UI project/package graph, shared DTOs/schema and canonical docs belong to the
  integration authority. One editor per shared file; reserve before editing.
- Use the current user's scope to determine whether a session may implement or only review. This
  review authorized the simulator-target build and local checks and limited edits to the existing plan
  and orchestration instructions. The continuation authorizes scoped local implementation and
  self-review. The owner will execute the build, automated tests and native verification at the end.
  Protected external actions and Git publication require separate applicable authorization.

## Candidate and evidence of record

| Field | Value |
| --- | --- |
| Planning baseline | `acdb131140dec58c4625132682982b7baba4a4c7` |
| Planning package commit | `a2d55adc` |
| Independently reviewed candidate | `d0e989d549a997ba29db1e6e6a4c4cf301ed3cf2` |
| Review checkout before edits | `9a1a357aed78234f1b665bbcecac9e7c70bcaca8` (review brief and mission pointers only since candidate) |
| Implementation candidate (this pass) | HEAD still `9a1a357a`; code pin `a6f228f2951e96128fe71884b72986d38bb4ef601c527362dd2cbc9f3c2479b0` over 25 modified + 7 new `src`/`tests` files (documents excluded). Nothing committed. See [RM08 handoff](RM08-CANDIDATE-HANDOFF.md) |
| Worktree / branch | the c75928bd MuscleCuties worktree on `musclecuties-ui-ux-backend-remediation-kickoff` |
| Framework | `Microsoft.Maui.Controls` 10.0.90 (was 10.0.60) |
| Ownership record | [RM00-CANDIDATE-AND-RESERVATIONS.md](RM00-CANDIDATE-AND-RESERVATIONS.md) |
| Independent build | iOS Debug simulator target, 0 warnings / 0 errors. No native scenario exercised |
| Independent tests | Core 1055/1055, Mobile.Contracts 16/16. See [RM08 evidence](#rm08-independent-review) |

Never restate a dated result as current. Re-run before claiming.

## Package board

| ID | Outcome | Lane | State |
| --- | --- | --- | --- |
| RM00 | Candidate pin, ownership and inventory | Coordinator | **DONE** — inventory reconciled for this pass in the [handoff](RM08-CANDIDATE-HANDOFF.md). No framework change in this pass |
| RM01a | CR01 native crash and page-lifetime guard | Coordinator + A.UI | **DONE — owner-approved 2026-11-05** — stale load is fenced before wrapper state and before lease/accept |
| RM01b | PG01 automatic activation, injury acknowledgment and direct Edit details | A.API + A.UI | **DONE — owner-approved 2026-11-05** — foreground activation, up-front replacement disclosure, one native injury alert, direct Edit details and the typed failure/retry contract |
| RM02 | PV01 privacy surface, AH01 archive width | A.UI | **DONE — owner-approved 2026-11-05** — PV01 has no inner fill; AH01 uses one container width/inset |
| RM03 | Add meal entry, compact context and one scroll owner | A.UI | **DONE — owner-approved 2026-11-05** — Add meal host repair, compact context and one `CollectionView` scroll owner |
| RM04 | NM02 grouped editor, draft precision and footer | A.UI + A.API | **DONE — owner-approved 2026-11-05** — round-trip portions, row-identity save, stacked large-text rows, localized validation and full-width sole Save |
| RM05 | NM03 selected-date breakdown parity | A.UI | **DONE — owner-approved 2026-11-05** — shared UI-local presenter, one modal rebind and the Daily Food Log selected-date entry reviewed on `04fae5d2…` |
| RM05b | NM03 truthful micronutrient coverage | A.API + A.UI | **DONE — owner-approved implementation** — additive contract, main-page note and Food Log surface; disposable-store and old/new client evidence remain open verification gates |
| RM06 | Audit F01–F03, S01 accessibility carry-over | A.UI + A.API | **DONE — owner-approved 2026-11-05** — F01 tri-state answer, F02 confirmed water writes with Retry, F03 cached phase/date truthfulness, S01 accessible names |
| RM07 | Shared frame, feature disposition (measurements, reschedule, notes, hydration) | Product A | **DONE — owner-approved** — heading semantics landed; shared-frame migration and the four deferred capabilities remain recorded dispositions, not delivered features |
| RM08 | Integrated implementation review and Beta verification | Coordinator + owner | **OWNER APPROVED 2026-11-05** — candidate `04fae5d2…`; build 0/0, Core 1114/1114, contracts 16/16. Beta approval recorded; no deployment executed |

### RM03 — compact context and one scroll owner

`Controls/Nutrition/MealEditorModal.xaml` rows 1–4 of the `Auto,Auto,Auto,Auto,Auto` block hold the
meal label, four choice cells, the time picker and the two secondary actions; the ingredient
`CollectionView` receives only the remaining star height.

1. Add `IsMealContextExpanded` to `NutritionViewModel` with an explicit toggle command. Reset it in
   `ResetMealDraft()` (`NutritionViewModel.Meals.cs`) **and** in the existing-meal edit path
   (`NutritionViewModel.Meals.cs:≈332`), which sets `IsMealChoiceLocked = true` without resetting.
2. Derive the editable picker state from unlocked **and** expanded, and the compact summary state from
   locked **or** collapsed. Raise dependent notifications when either input changes.
3. Reuse `MealChoiceSummary` for both compact states. Offer Change only when unlocked and collapsed,
   and guard its command against locked context. Keep contractual lock assignments unchanged.
4. Put summary/context/actions in the `CollectionView` header unless another bounded single scroll
   owner proves equivalent reachability with expanded context. Keep the shared header and commit
   footer outside that scroll owner.

Acceptance: fresh, manual, suggested and existing meals all reach a first ingredient and every row
action; context changes only when editable; drafts survive finder/back/failure; Log meal stays above the
keyboard and home indicator; EN/RU and largest text hold.

### RM05 — NM03 presentation

Extract the breakdown presenter/state and nutrient-list anatomy out of `NutritionBreakdownModal.xaml`
(currently typed to the today-loading `NutritionViewModel`) into a UI-local presenter both surfaces use,
then add a breakdown action to Daily Food Log that loads the overview for the **captured `SelectedDate`**.
Guard with `_dateLoadVersion`; clear details on date/account change and disposal; refresh after
mutations. The mapper stays the single presentation boundary. Presentation parity alone must never be
reported as data coverage.

## Owner first-pass findings, 2026-11-05

The owner confirmed Workout Archive as done in the first pass and found the plan's volume and
activity breakdown useful. Keep both as regression coverage. Apply is the main Beta blocker,
Edit details required closing the sheet first, and Add meal made Nutrition unusable. The register
form shows apparent clipping. Inspect both the decorative Nutrition focus dot visible in screenshot
3 and the inline Profile vault shown in screenshot 4. Retain the privacy behavior. No screenshot
or personal account content is stored in the repository.

The owner's latest decision supersedes the separate Apply tap and proposed non-injury fallbacks.
**A user-started Generate or Regenerate automatically activates an eligible plan**, including
replacement after the initiating action discloses that consequence. A specific exercise excluded
because of injury is the sole permitted fallback and the only extra decision. Use one native iOS
alert with the actual adaptation and a Continue or Cancel choice. Keep the blocked exercise
excluded. For other incompatibilities, preserve the draft and active plan and go straight to the
relevant editable detail. Keep replacement authority, safety and lease checks tied to the exact
request. The owner also approved deferring cosmetic polish for this Beta attempt while fixing
blockers and task-preventing accessibility issues.
Use the [plan's PG01 slice](../UI_UX_BACKEND_REMEDIATION_PLAN_2026-11-04.md#pg01-first-pass-blocker--activate-eligible-plans-with-one-action)
and NM01 entry-point instructions for the implementation.

The seven exception attachments are identical copies of the startup signature. The separate
assembly log identifies an iPhone 16 simulator installation. Read-only inspection matched that
installed bundle to the same mixed Rider Debug artifact described below. The screenshots support
the owner's observations. Their exact source SHA and the server's proposal response remain
unconfirmed. Reproduce on the pinned, coherent candidate with synthetic data. Keep the screenshot
pass and startup package mismatch separate from the integrated review result.

Return user-facing verification as a short chat conversation once a repaired candidate exists.
Capture the owner's observed outcomes and remaining failures in the human review record.
Keep repository verification and protected Beta release gates tied to the actual build and target.

## Owner decisions and final acceptance

The owner approved scoped local implementation on 2026-11-05. The latest handoff assigns all build,
automated test and native verification execution to the owner after implementation. The coordinator
prepares reproducible commands, fixtures, and expected results. The owner will use an existing
synthetic Staging account for authenticated scenarios. Preserve the owner's Rider simulator and
verify the binary, endpoint and synthetic account before any authenticated test.

RM05b stays in scope. The owner approved a backward-compatible additive API contract with
compatibility tests. Reserve the overview contract through the integration authority, then prove
known zero, unavailable and partial coverage, existing client behavior with the additive response,
and updated client behavior when the new fields are absent. Scope this approval to the API change.
Any schema or live environment operation still needs its own applicable authorization.

The owner will review the integrated candidate independently. The first review is on the Xcode
iPhone 17 Pro Max simulator through Rider. The final review is on the owner's physical iPhone
16 Pro Max before Beta. Once the integrated candidate is ready, send the owner both items together:

- **Device checklist** with the build and environment identity, synthetic sign-in, CR01/PG01
  navigation and failure/retry sequences, photo reveal/reuse and archive alignment, meal scrolling
  and grouped edits, historical nutrient coverage, both appearances, EN/RU, large text, VoiceOver,
  Reduce Motion, keyboard/safe areas and interrupted-network behavior.
- **Review packet** with the exact baseline and candidate SHA/tree or verified local patch identity,
  scope and reservation summary, self-review findings, owner-ready commands, expected results,
  remaining gates and a place for the independent human review result. Leave binary identity and
  execution results pending until the owner runs the build and tests. Use the
  [original brief](REVIEW-BRIEF.md) as the review specification for `d0e989d5` and pin the next
  review to the actual new candidate.

### Human review record

| Field | Recorded or still needed |
| --- | --- |
| Reviewer and independence | Denis Petruk, owner and designated independent human reviewer. At sign-off confirm that the reviewer did not implement the changes under review. |
| First review | Rider iOS Debug using Xcode's iPhone 17 Pro Max simulator. Current simulator runtime is iOS 26.3. Record the actual runtime, review date and result when run. |
| Final pre-Beta review | Physical iPhone 16 Pro Max. Record actual iOS version, build/signing identity, review date and result when run. |
| Integrated candidate and build | Pending coordinator handoff. Record source SHA/tree or verified local patch identity, bundle ID, version/build, binary identity, resolved MAUI version and environment. |
| Test account | Existing synthetic Staging account. Owner sign-in is pending at the authenticated-test checkpoint. Record only that the synthetic account was verified. |
| Repair evidence | Pending. Attach each review finding's regression result and the integrated build, Core and mobile contract results. |
| Scenario results | Pending on the integrated build. Record expected/observed result and PASS, FAIL or BLOCKED per case. Attach sanitized evidence for failures. |
| Human verdict | Pending. Record the exact candidate, reviewer, date, unresolved findings and explicit decision after review. The first review and final pre-Beta approval are separate entries. |

### Startup checkpoint

The owner reported `MauiCALayerAutosizeToSuperLayerBehavior` missing during startup on 2026-11-05.
The installed iPhone 17 Pro Max bundle matches the Rider `main` Debug executable at `ff3be300`
(UUID `86DBB93E-67F1-3643-B72B-8FA0BC960D39`). The build resolves MAUI 10.0.20, but the installed
managed MAUI assemblies match 10.0.60. The native executable lacks the Objective-C class that
10.0.60 attempts to create. This bundle mismatch explains the startup exception.

The reviewed 10.0.90 app, version 1.0 build 2 (UUID `64ED083B-EBD0-3FA9-95B3-43ABA9BAE8F9`), was
verified and launched on the spare iPhone 17 simulator. It contains the native class and its
process was still running 12 seconds after launch. This was a startup smoke check without
authenticated scenarios. The owner's Rider installation was inspected read-only.

For the first review, open the pinned candidate solution in Rider, clean and rebuild its iOS
Debug output, verify the resulting bundle against the resolved MAUI package, then select the
iPhone 17 Pro Max simulator. If the previous bundle survives a normal rebuild, move only the
generated build outputs aside and rebuild with fresh output and intermediate directories.
Verify the installed bundle identity and cold start before synthetic sign-in. Preserve simulator
data and keep the runtime's native initialization error check enabled.

## Session protocol

1. Read this board, the plan and the review findings. Record the current branch, clean source SHA/tree
   and authorized scope. Reconcile RM00 against the diff and reserve each repair path with one owner.
2. Start RM01b automatic activation, injury confirmation and Edit details immediately. Coordinate
   RM01a through one owner for the shared generation ViewModel. Start RM03 Add meal in parallel on
   disjoint paths. Use the two lane prompts and the owner's latest decisions for the handoff.
3. Implement the smallest complete repair, prepare targeted synthetic regressions and self-review the
   exact diff. Follow the attempt limit below. Progress RM02–RM07 as dependencies and reservations
   allow. Keep selected-date presentation and truthful coverage independently testable. Record
   cosmetic deferrals and preserve every applicable Beta gate.
4. Mark a source-reviewed package `AWAITING OWNER VERIFICATION`. Continue all other ready work.
   Record unresolved items and reservations. Commit only with applicable authorization.
5. Integrate the completed slices into one pinned candidate, review the final diff and reconcile the
   package inventory. Deliver one owner review packet after all ready work is exhausted. The owner
   executes the tests and records independent review and Beta evidence on the candidate. Reopen any
   failed or stale package. Close the mission only when the definition of done holds.

### Self-review and three-attempt rule

- Start with the normal caller path, the applicable contract, and the smallest counterexample.
  Review the diff for functional regressions, safety, shared-file collisions and missing evidence.
  Prepare a targeted regression for the owner to run where existing tests miss the failure.
- For each issue you cannot resolve, make up to three distinct, safe attempts using the evidence
  from the preceding attempt. Record the hypothesis, inspection or change, and result of each.
  If the third attempt still fails, stop work on that issue, record its blocker and affected file
  reservations, and switch to the next independent ready task. Review the blocked list at handoff
  for issues unblocked by later work.
- After all other work is handled, return the complete unresolved list. For each feature the owner
  can verify in the simulator, give the candidate and device, prerequisites, exact taps, expected
  behavior, what to capture on failure and what decision the result resolves. Separate source,
  backend and authorization blockers so each reaches the right owner.
- Self-review is implementation evidence. Independent human review and all execution gates remain
  due on the exact candidate.

## Owner verification commands

Run these on the pinned candidate when the coordinator hands over the review packet. Include any
additional affected suites or disposable persistence checks identified in the packet.

```bash
# strict-XAML iOS build (0 warnings, 0 errors expected)
dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug \
  -p:RuntimeIdentifier=iossimulator-arm64

# backend behavior and source guards
dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj
dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj
```

The owner uses Rider and the iPhone 17 Pro Max simulator for the first pass, then a physical
iPhone 16 Pro Max for final acceptance. Verify build and endpoint before signing in to synthetic
Staging. The commands below are an alternative for a separately selected spare simulator.

```bash
xcrun simctl boot <spare-udid>; xcrun simctl bootstatus <spare-udid> -b
xcrun simctl install <spare-udid> src/MuscleCuties.UI/bin/Debug/net10.0-ios/iossimulator-arm64/MuscleCuties.UI.app
xcrun simctl launch <spare-udid> com.musclecuties.app
xcrun simctl io <spare-udid> screenshot "$PWD/artifacts/<name>.png"
```

## Implementation candidate — 2026-11-05

The RM01a/RM01b/RM03/RM04 source repairs and the RM02 AH01 width contract are complete on one
working tree, with coordinator-run build and test evidence. Every remaining package has a recorded
state and blocker. The review packet, device checklist and exact commands are in the
[RM08 candidate handoff](RM08-CANDIDATE-HANDOFF.md).

| Field | Value |
| --- | --- |
| Base commit (unchanged HEAD) | `9a1a357aed78234f1b665bbcecac9e7c70bcaca8` |
| Code pin | `a6f228f2951e96128fe71884b72986d38bb4ef601c527362dd2cbc9f3c2479b0` (25 modified + 7 new files; documentation excluded, command in the handoff) |
| Coordinator checks | iOS simulator-target build 0 warnings / 0 errors; Core 1105/1105; Mobile.Contracts 16/16 |
| Owner gates | Unrun: install/launch, authenticated simulator scenarios, EN/RU, largest text, VoiceOver, Reduce Motion, keyboard/safe areas, native scroll and sheet transitions, CR01 reproduction |
| State | **AWAITING OWNER VERIFICATION** — beta approval pending |

Landed: appearance fence before wrapper state and before lease/accept, with stale-lease cleanup;
foreground auto-activation with up-front replacement disclosure; one native injury alert naming the
excluded exercise (Cancel keeps the plan and the block); direct Edit details routing; typed unhandled
planning failures, neutral answered-server copy, bounded request reference, outcome-gated retry; Add
meal host initialization and release; round-trip portions, row-identity save projection, stacked
large-text rows, localized validation; one archive container width.

Not landed: RM05, RM05b, RM06 and RM07 feature work. RM05 needs the shared breakdown presenter
migration (a partial migration would leave the working breakdown unverifiable); RM05b needs the
reserved additive coverage contract and client compatibility evidence; RM06 needs its own
investigation; RM07 contributed a written disposition only.

**Owner verification attempt 1 (2026-11-05) — BLOCKED at launch, server-side.** The owner's build and
both suites passed, but the app never reached a working authenticated state, so R1.2–R1.5 and every
later gate are unverified. Coordinator diagnostics show a healthy launch (foreground active, no
exception, network call completes) and a live API host (unauthenticated paths answer 404), which
places the failure at an authenticated route, the account state or the Staging schema — not in this
diff. See [verification attempt 1](RM08-CANDIDATE-HANDOFF.md#verification-attempt-1-2026-11-05--blocked-at-launch-classified-backend). A follow-up repair types the dashboard overview failure, classifies it in the shared presenter and
removes unguarded server-field dereferences, so that screen now names the class instead of showing
one generic line with a retry that could never succeed.

## Definition of done

Every required package is `DONE` with current-candidate evidence and the audit's open findings have
explicit dispositions. G5 passes on one integrated candidate in both appearances, EN/RU, supported
sizes and largest text, with native back/dismiss, keyboard/safe areas, interrupted-network behavior,
VoiceOver and Reduce Motion. RM08 records independent human approval on that exact candidate.

## RM08 independent review

### Reviewer and independence

Reviewed on 2026-11-05 by Codex, an AI agent separate from the candidate's implementing session.
This is an independent AI assessment. RM08 still requires independent human approval. The owner
limited review writes to existing orchestration instructions and the plan, so this record uses the
brief's report structure inside the mission.

### Target and reproduced evidence

- Base `acdb131140dec58c4625132682982b7baba4a4c7`. Reviewed head
  `d0e989d549a997ba29db1e6e6a4c4cf301ed3cf2`, tree `71ac1d0baf120916b16288fd2ff67bfce1e3bde5`.
  The branch `musclecuties-ui-ux-backend-remediation-kickoff` started clean at
  `9a1a357aed78234f1b665bbcecac9e7c70bcaca8`. Its delta from the reviewed head is the review brief
  and two mission pointers. All source/test files matched the reviewed head at execution.
- `git log --oneline acdb1311..HEAD` returned `9a1a357a`, `d0e989d5`, `e44c31bf`, `e27a9a20` and
  `a2d55adc`. `git diff --stat acdb1311..d0e989d5` returned 23 files, 1,044 insertions and
  40 deletions. The actual-HEAD stat includes the review brief for 24 files. The candidate
  whitespace check passed. The diff excludes `docs/release-audit/`, which remains untracked in Rider.
- The iOS simulator-target build and Core test commands above exited 0 on .NET SDK 10.0.203.
  Build output was `Build succeeded.`, `0 Warning(s)`, `0 Error(s)`. Core output was
  `Failed: 0, Passed: 1055, Skipped: 0, Total: 1055`, with two xUnit1031 warnings in unchanged
  `CycleOnboardingAnchorTests.cs:44` and `:64`. The owner simulator was inspected read-only for
  its boot state. No app install, launch, device interactions or captures were performed.
- Core `--no-build --filter` for UiStyleGateTests, LocalizationCatalogTests,
  CloudStrengthPlanProposalResponseTests and ProfileWorkoutPreferencesRegenerationTests returned
  28/28. `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj`
  returned 16/16. Both had zero failures/skips.
- Isolated `dotnet run` probes using candidate client/adapter/presenter and ingredient editor code
  exited 0 and reproduced the counterexamples below. The HTTP probe used a synthetic message
  handler and stubbed unrelated dependencies. The amount probe compared float bits, serialized
  values and the candidate's repeated-ID save projection. Neither contacted a backend or database.
- `grep -n MauiVersion`, cached package metadata, iOS PE inspection and the resolved asset comparison
  confirmed 10.0.90 and the new early return. The Controls pin and seven MAUI transitive packages
  move together. The comparison showed no non-MAUI version changes.
  [10.0.60 package source](https://raw.githubusercontent.com/dotnet/maui/bf6156897c887d33dcd40592db3bcb6471916e03/src/Controls/src/Core/Button/Button.iOS.cs),
  [10.0.90 package source](https://raw.githubusercontent.com/dotnet/maui/8e2547a4707f745a27a7791495b240e756926980/src/Controls/src/Core/Button/Button.iOS.cs)

### Findings

**Defects**

1. **P1 · RM01a / C2** — `src/MuscleCuties.UI/Pages/Workout/StrengthPlanGenerationPage.xaml.cs:25`,
   `src/MuscleCuties.UI/Pages/PageLoadExtensions.cs:149` and
   `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs:412`.
   Leave before the deferred callback and the wrapper can still reset an existing load error.
   Hold a feasible first-proposal response until after departure and the earlier load can populate
   preview, start a lease and enter automatic Apply. The first current appearance passes the check.
   A probe against the resolved MVVM DLL started two direct `ExecuteAsync` calls while
   `CanExecute` was false for the second. Move the lifetime check before wrapper mutation and fence
   in-flight completion, cleanup and side effects to the current appearance. Preserve the tested
   first-plan rule when that appearance is current. Native crash reproduction remains open.
   [AsyncRelayCommand package source](https://raw.githubusercontent.com/CommunityToolkit/dotnet/638b41dad30dffabb123a39aa38eabc7e3721371/src/CommunityToolkit.Mvvm/Input/AsyncRelayCommand.cs)
2. **P1 · RM04 / C5** — `src/MuscleCuties.UI/ViewModels/Nutrition/FoodLogViewModel.cs:623`.
   Existing grouped save code matches the first editor row by food ID. Two untouched occurrences
   of one food prepared `[0.1, 0.1]` from `[0.1, 33.333332]`. The callback also reads live editor
   state after waiting on the date lock. Snapshot the validated row amounts and meal context before
   the await. Match each edit to its own row and preserve the full unchanged record.
3. **P1 · RM01b / C3–C4** —
   `src/MuscleCuties.Infrastructure/Cloud/CloudStrengthPlanProposalStoreV020.cs:109` and
   `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs:1229`.
   The actual client and store turned synthetic 401/403, 400, unknown create 409, 422 and 404 into
   `InvalidOperationException`. A 429/503 on Apply uses the generic connection catch. Actual
   `HttpClient` timeout produced `TaskCanceledException` with a `TimeoutException` cause, but the
   view model uses generic copy. Preserve structured unhandled responses and actual transport
   causes through load/create/Apply. Retain the existing actionable domain outcomes and distinguish
   caller cancellation.
4. **P1 · RM01b / C4** — `src/MuscleCuties.UI/Services/Api/ApiFailurePresenter.cs:157`,
   `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs:438` and
   `src/MuscleCuties.UI/Controls/Shared/PageLoadingOverlay.xaml:35`.
   Actual 405/408 responses, and injected typed 400/409/422, produced connection-error copy and
   `Action.None` in both languages. Load still overlays the message with generic copy and an
   unconditional Retry. Create still enables the same command for a valid idle draft. Use neutral
   localized online-failure copy and bind the single foreground recovery state's affordances and
   command guards to the notice action.
5. **P2 · RM01b / C4, G7** — `src/MuscleCuties.UI/Services/Api/ApiFailurePresenter.cs:34` and
   `src/MuscleCuties.Infrastructure/ApiClient/MuscleCutiesBackendApiClient.cs:307`.
   A synthetic 503 JSON `requestId` containing a URL rendered the URL verbatim in the reference
   field. The repository server normally emits its trace identifier. Validate the bounded opaque
   reference before display and omit invalid values. No actual user data exposure was observed.
6. **P2 · RM04 / C5** — `src/MuscleCuties.UI/Models/UI/Nutrition/HistoryIngredientEditor.cs:18`
   and `:75`. `33.333333f` opens as `33.333` and accepts `33.333 ` as a rounded edit.
   `1e-7f` opens as `0`, so typing `0` is treated as unchanged. `1,234` becomes 1.234 g in
   ru-RU and de-DE. German `1.234` is also accepted through invariant parsing. Use round-trip
   editable text, reject ambiguous grouping and localize both the row validation and Save feedback.

**Plan deviations and documentation findings**

- **P2 · RM00 / C8–C9** — `docs/design/orchestration/RM00-CANDIDATE-AND-RESERVATIONS.md:25`
  omits `HistoryIngredientEditor.cs`, `FoodLogViewModel.cs` and both changed test files from its
  path inventory. The package pin is disclosed. Its transitive MAUI changes contradict the brief's
  literal zero-drift expectation. The coordinator must reconcile the exact inventory and record
  the intended MAUI-only update before renewed reservations.
- **P2 · RM08 / C9** — `STATUS.md:65`, `docs/design/orchestration/MISSION.md:53` and
  `docs/design/orchestration/RM00-CANDIDATE-AND-RESERVATIONS.md:42` at the reviewed head overstate
  recovery or describe remaining blockers as owner-only. RM00's NM02 row still describes only the
  layout. Reconcile these historical claims when the coordinator records the new candidate.
- **P2 · RM02 / C6** — `src/MuscleCuties.UI/Pages/Workout/WorkoutHistoryPage.xaml:87` still applies
  width caps independently to header and rows. The change fills the header and keeps zero
  horizontal inner insets. The bounded title already word-wraps between the two fixed control
  columns, so the brief's literal truncation differs from the existing design contract. Measure
  common edges and trailing control reachability before deciding whether a container change is
  needed. The photo card retains its reveal recognizer and hidden image bindings at
  `src/MuscleCuties.UI/Pages/Profile/PhotoJourneyVaultPage.xaml:142`. The inner Fill only stretches
  within the padded card. Native touch and rendering still need verification.
- **P2 · RM04** — `src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml:849` and
  `src/MuscleCuties.UI/Pages/Nutrition/FoodLogPage.xaml.cs:22`. Two proportional columns and
  a full-width sole Save landed. The large-text stacked fallback and short-form content fit remain
  open under the existing layout acceptance.
- **Nit · C7/C9** — `src/MuscleCuties.UI/Resources/Styles/Styles.xaml:1388` leaves an orphan
  comment. RM00 `:52` describes two unconsumed leaf styles. Four style definitions were removed,
  including the two parent definitions that the leaf chain referenced.

**Hostile check details**

- All three changed assertions grew from exact `HttpRequestException` to exact
  `PlanningRequestFailedException` plus an enum assertion. The server Apply case now pins
  `ServerError`, offline Apply pins `OfflineRejected`, and the failed Create case pins
  `ServerError` while retaining the exact POST route and same draft assertion. The diff removes
  no test, weakens no assertion and adds no skips or unconditional pass. The new assertions can
  still pass with missing HTTP status, error code or request reference, or incorrect UI behavior.
  No ingredient regression test was added.
- `git grep -n WorkoutHistorySecondaryBody acdb1311 -- src tests` found the four definitions
  and their internal inheritance only. The same search on `d0e989d5` returned no matches.
  The style-gate file was untouched and its test passed in the focused run.
- The amount probe retained float bits and serialized values for individual untouched and exactly
  reverted `0.1`, `33.333333f`, `1000` and `1e-7` rows. Ordinary empty, zero, negative, NaN,
  infinity and overflow drafts were rejected. `12.5` works in all three probed cultures, while
  `12,5` works in ru-RU/de-DE and fails in en-US. The tiny-row zero and grouped duplicates
  contradict whole-save preservation. An isolated conversion also turned `1e-29f` into decimal
  zero. Storage fidelity remains unverified. Save uses one validation command. Keyboard completion
  has no commit binding. Dismissal and finder return stage or close the draft without writing.
  The lock's source and semantics were unchanged in the candidate.
- The cloud adapter is the bound proposal client. No silent local generation was found, and the
  inspected success path requires a server acceptance. Apply reuses the proposal ID as the
  idempotency key. The lost-response flow still needs a deterministic test. Existing EN/RU keys
  have genuinely translated values and matching format arguments, including the reference format.
  The demonstrated failures are in the status/action wiring and the accepted reference value.

### Claim table

| Claim | Verdict | Evidence |
| --- | --- | --- |
| C1 | PASS | Tagged source and cached iOS assembly confirm the guard in 10.0.90 and its absence in 10.0.60. MAUI-only dependency changes are recorded under C8. |
| C2 | FAIL | Deferred wrapper writes before the check, and a detached in-flight load can reach automatic Apply. |
| C3 | FAIL | Unhandled auth/validation/conflict responses lose metadata before presentation. No local fallback was found. |
| C4 | FAIL | Connected responses show a connection error, recovery ignores action, and an unvalidated reference is displayed. |
| C5 | FAIL | Ambiguous/rounded inputs and same-food rows fail the amount contract. |
| C6 | BLOCKED | Source review found no new touch or overflow mechanism. Rendered edges, reveal/reuse and narrow large-text controls require native evidence. |
| C7 | PASS | No candidate references for four removed keys. Unchanged style gate passed. |
| C8 | FAIL | Four runtime/test paths missing from declared inventory. MAUI version closure changed as disclosed in part. No new dependency, target, DTO, schema, MauiProgram, SQLite or health scope change found. |
| C9 | FAIL | Historical status/reservation records and the reviewed mission omit current defects or overstate recovery. |

### Gates

| Gate | State | Evidence or action |
| --- | --- | --- |
| G0 | FAIL | Clean start and exact fixed diff verified. RM00 inventory is incomplete. |
| G1 | PASS | Reviewed changes preserve layer direction and wire/schema ownership. The coordinator must record the MAUI package update. |
| G2 | NOT VERIFIED | Core suite passed. The stale-appearance first-plan case needs a focused regression and current-candidate acceptance proof. |
| G4 | FAIL | Synthetic online failure cases lose status or lead to an incorrect action. |
| G5 | BLOCKED | Source defects listed above. Signed-in native scenarios and physical-device/accessibility evidence remain outstanding. |
| G7 | FAIL | No credential/connection string/real-user identifier found in the fixed diff scan. A synthetic untrusted request reference can reach visible UI. |
| G8 | PASS | iOS simulator-target build 0 warnings/0 errors, Core 1055/1055, Mobile.Contracts 16/16. New counterexample regressions are required on the repair candidate. |
| G9 | FAIL | Reviewed candidate status and reservations need reconciliation. This review records the corrected execution state. |

### Overall verdict

**BLOCKED.** RM00, RM01a, RM01b and RM04 need local corrective work. RM02's native acceptance is
open, and RM08 needs integrated evidence and human approval. Continue through the session protocol.

### Owner actions

See the [owner decisions above](#owner-decisions-and-final-acceptance). Local repair and the RM05b
additive API contract have approval. After implementation handoff, the owner runs all tests, signs
in to synthetic Staging, performs the simulator and physical-device/accessibility checks and
records independent review.

## Owner approval — 2026-11-05 (second candidate)

The owner reported that every item performed by the owner has been approved. Recorded against the
exact source candidate; no external action was executed from this record.

| Field | Value |
| --- | --- |
| Candidate | Code pin `4582e63fea80062dce59bf9bdc3520e445feb4c2505a78b64e37ef9accd59aa2`, base `9a1a357a` |
| Reviewer and independence | Denis Petruk, owner and designated independent human reviewer |
| Decision | **APPROVED** for this candidate |
| Beta approval | Recorded as granted by the owner for this candidate; no deployment, promotion, push or migration was performed |
| Verification detail | The owner's approval supersedes the earlier `AWAITING OWNER VERIFICATION` state for RM01a, RM01b, RM02, RM03, RM04, RM06 and RM08. Per-scenario captures and the exact installed bundle identity for this run were not supplied in this record; do not restate them as collected evidence |
| Still open | RM05 breakdown parity (BLOCKED), RM05b Food Log surface and storage/client compatibility evidence (PARTIAL), RM07 full shared frame (PARTIAL) |

## Owner approval — 2026-11-05 (RM05 candidate)

The owner reviewed the RM05 candidate and reported it good. Recorded against the exact source; no
external action was executed.

| Field | Value |
| --- | --- |
| Candidate | Pre-commit content pin `04fae5d2f825146adf191f3db56ec3c87893b12525573744a034d3ff52cc333e`; base `9a1a357a` |
| Decision | **APPROVED** — RM05, RM05b presentation and RM07 dispositions accepted |
| Beta approval | Recorded for this candidate; it is committed on the mission branch; nothing has been deployed or migrated |
| Review detail | The owner reported the review as complete and good. No per-scenario captures or installed bundle identity were attached to this record, so they are not claimed as collected evidence |
| Still open | RM05b disposable-PostgreSQL rehearsal and old/new installed-client compatibility; CR01 native reproduction and remaining device gates |

Implementation work is complete. The mission is not at its Definition of Done until the open
verification gates above have current-candidate evidence.

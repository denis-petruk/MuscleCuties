# RM08 — candidate handoff, owner verification packet

Prepared 2026-11-05 by the implementation coordinator. This is the review packet and device
checklist for one candidate. It creates no release authority: beta approval stays pending until the
owner runs the commands below and records results.

## 1. Pinned candidate

| Field | Value |
| --- | --- |
| Repository | `the c75928bd MuscleCuties worktree` |
| Branch | `musclecuties-ui-ux-backend-remediation-kickoff` |
| Base commit (unchanged HEAD, nothing committed) | `9a1a357aed78234f1b665bbcecac9e7c70bcaca8` |
| Code pin | `a6f228f2951e96128fe71884b72986d38bb4ef601c527362dd2cbc9f3c2479b0` — SHA-256 over `git diff -- src tests` plus the seven untracked source/test files, in `git ls-files` order |
| Code scope | 25 modified files + 7 new files under `src`/`tests`; +658 / −158 lines |
| Documentation in the same tree | the 4 pre-existing edits preserved, plus the reservations record, `MISSION.md` and `STATUS.md`, plus this new packet. Not part of the code pin |
| Whitespace check | `git diff --check` clean |
| Resolved framework | `Microsoft.Maui.Controls` 10.0.90 (unchanged from `d0e989d5`) |
| State | **AWAITING OWNER VERIFICATION** — beta approval pending |

Reproduce the pin exactly (documents are deliberately excluded so later notes cannot change it):

```bash
{ git diff -- src tests; \
  for f in $(git ls-files --others --exclude-standard -- src tests); do \
    printf '\n=== %s\n' "$f"; cat "$f"; done; } | shasum -a 256
```

The four documentation files that were already modified in the checkout were preserved and extended,
not reverted. Nothing was committed, pushed, deployed or migrated.

## 2. What changed

### RM01a — CR01 appearance fence (`StrengthPlanGenerationPage`, `PageLoadExtensions`, `WorkoutPlanningFlowRule`)

- `PageLoadExtensions.BeginPageLoad` takes an `isCurrent` predicate evaluated on the UI thread
  **before** the load wrapper resets `IsLoadError` / deferred state. A departure now skips the
  deferred callback entirely instead of clearing an error on a page that has left.
- The page notifies the view model on entry and departure. The view model holds a generation version
  and an appearance flag; `CreateAsync`/preflight results are dropped with an explicit lease close
  (`DiscardStaleProposalAsync`) when the appearance or request changed during the await, and
  automatic acceptance is gated on `WorkoutPlanningFlow.IsCurrentGeneration`.
- New pure rule `IsCurrentGeneration(appearanceIsCurrent, requestVersion, activeRequestVersion)`.

### RM01b — PG01 automatic activation, injury acknowledgment, direct Edit details

- **Foreground intent activates.** `CreatePreviewAsync` marks a foreground intent and, when the
  preview is applicable, persisted and leased, accepts it without an Apply tap. Background load keeps
  the source's tested first-plan exception (`CanAutomaticallyApplyFirstPlan`, unchanged).
- **Replacement is disclosed up front.** A notice above Generate/Regenerate states that generating
  replaces the active plan. The acceptance path no longer asks for replacement confirmation on a
  foreground intent; it still asks when a replacement preview arrived outside that intent.
  Replacement authority stays bound to the same proposal, an unchanged revision and a live lease
  fence (`RenewAsync` before acceptance, `_proposalInvalidated` respected).
- **One native injury alert.** The previous Silent `Limited` branch that acknowledged every returned
  review condition is gone. Acceptance now requires
  `WorkoutPlanningFlow.RequiresInjuryAcknowledgment` to be resolved by one native alert that names
  the excluded exercise and the alternative. Continue sends exactly those exercise IDs through the
  preview request's `AcknowledgedExerciseConditionIds` and revalidates once; Cancel keeps the active
  plan, keeps the exercise blocked and leaves the draft. A changed adaptation is disclosed again and
  never accepted silently; a second required acknowledgment stops with `InjuryChanged`.
- **No non-injury fallback.** Ineligible requests (missing answers, style choice, no lease, not
  persisted, infeasible) stay in the draft.
- **Direct Edit details.** One tap closes the sheet, preserves the draft and routes explicitly to the
  editable details: back when the page was opened from preferences, otherwise
  `Shell.GoToAsync(nameof(ProfileWorkoutPreferencesPage))`. It no longer pops generically into Workout
  or onboarding.
- **Failure contract.** `CloudStrengthPlanProposalStoreV020.RequirePlanningData` now throws
  `PlanningRequestFailedException` (status, error code, HTTP status, request reference) for every
  non-success outcome instead of only server/offline. `ApiFailurePresenter` classifies an answered
  status it does not recognise as neutral online copy with `Action.None` — it can no longer tell a
  connected person they are offline. `Ui_Common_Error_Online` added in EN and RU.
- **Bounded retry and reference.** The page's shared overlay takes the classified message and only
  the retry the outcome permits (`RetryLoadCommand` is null when the outcome needs different input).
  `RequestReference` keeps only a 1–64 character opaque token; a URL, path, token or arbitrary server
  text is omitted, including the reference format.

### RM03 — Add meal entry

- `MealEntryModal` initializes with its host instead of from an open callback, so the XAML that
  supplies its open binding exists before the host is shown.
- `NutritionPage` reacts to `IsMealEntryModalVisible`, loads the sheet, releases a failed sheet back
  to an interactive page (`CloseMealEntryCommand`), and handles hardware back for the sheet before the
  meal editor.
- The Add meal host is visible only while requested, so it cannot leave a full-page layer over the
  page.

### RM04 — grouped editor precision and layout

- `IngredientPortionDraft` (UseCases): round-trip formatting, current-culture parsing, a single
  unambiguous alternate decimal separator, and rejection of ambiguous grouping (`1,234`, `1.234`,
  `1,234,567`) in every culture. `HistoryIngredientEditor` delegates to it; validation copy is
  localized (`Nutrition_FoodLog_AmountError`, EN/RU).
- `GroupedMealEditProjection` (UseCases): resolved amounts by row identity, so two occurrences of one
  food keep their own portions, untouched rows keep the original record, and a stale/reordered row
  cannot overwrite another ingredient.
- `FoodLogViewModel.SaveFoodItemAsync` snapshots validated amounts, meal type and time **before** the
  awaited write (it previously matched the first editor row by food ID and read live state after the
  date lock).
- Ingredient rows are stacked, giving the amount the full width at largest text; the sole Save keeps
  the footer width as `ButtonPrimary`.

### RM02 — AH01 archive width

- One `MaximumWidthRequest` and one horizontal inset on the virtualized archive container; the
  independent header and item caps were removed. PV01 (photo privacy surface) is unchanged in this
  pass.

## 3. Self-review findings on this diff

Found and fixed during self-review (each verified by rebuilding):

1. `MauiProgram` injury callback had one unbalanced parenthesis — caught by the strict build (CS1026).
2. `CanRetryLoad` was passed as a value instead of a predicate to `AsyncRelayCommand` (CS1503).
3. `ApiFailurePresenter` still called the renamed classifier, and the classifier took a
   non-nullable status while two callers pass null (CS0103, CS1503).
4. Four analyzer warnings from my own code (CA1304, CA1822 ×2, CA1859) — corrected, not suppressed,
   except the two compiled-XAML bindings, which use the repository's existing suppression pattern.
5. `NutritionPage.OnBackButtonPressed` handled the meal editor before the meal-entry sheet; reordered.
6. `_openedFromPreferences` was never reset per load; reset at `LoadAsync` entry so a direct
   Workout-tab entry cannot inherit a stale route.
7. `ShowsReplacementDisclosure` was not re-notified when `_hasExistingPlan` changed during load.

Deliberate decisions and their limits, for the owner's judgement:

- The Apply button remains for a preview that arrives outside the foreground intent (for example a
  recovered draft); the ordinary Generate/Regenerate path no longer requires it.
- Every returned `ExerciseReviewConditions` entry is treated as the injury adaptation set, because
  the response carries no separate injury-only marker. A required acknowledgment that is not
  injury-driven is therefore never auto-accepted: it stops with `InjuryChanged`. Confirming that the
  server only emits this collection for injury exclusions is a backend contract question, not a UI
  assumption (see §6).
- Replacement disclosure is inline copy above the initiating action, not a dialog, per the owner's
  "disclose up front, one action" decision.
- Rows are matched positionally with a food-identity check. If the server ever returns a different
  row order than the editor opened, mismatched rows are ignored rather than applied.

## 4. Checks actually run on this candidate

| Check | Command | Result |
| --- | --- | --- |
| iOS simulator-target build | `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64` | **succeeded, 0 warnings, 0 errors** |
| Core suite | `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj` | **1105 passed, 0 failed, 0 skipped** (1,055 baseline + 50 new) |
| Mobile contracts | `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj` | **16 passed, 0 failed** |

New regression coverage (owner re-runs them; they are the evidence for the repaired rules):

- `tests/MuscleCuties.Core.Tests/WorkoutPlanningActivationRuleTests.cs` — activation matrix,
  replacement disclosure, background work cannot activate, per-request limits, first-plan exception
  retained, injury-only acknowledgment, changed-adaptation re-disclosure, appearance/request fence.
- `tests/MuscleCuties.Core.Tests/IngredientPortionDraftTests.cs` — untouched bit-exact round trip for
  `0.1`, `33.333333`, `1000`, `1e-7` in en-US/ru-RU/de-DE, deliberate edits, alternate separator,
  ambiguous grouping rejected, invalid drafts, reverted text, repeated food rows, mismatched row
  identity, untouched-save preservation.
- `tests/MuscleCuties.Core.Tests/RequestReferenceTests.cs` — bounded opaque references accepted; URL,
  path, `Bearer`, email, `..`, spaces, markup, over-length values omitted.

### Verification attempt 1 (2026-11-05) — blocked at launch, classified backend

| Gate | Attempt 1 result |
| --- | --- |
| R1.1 build + both suites | **passed** on the owner's machine |
| R1.2 launch | reached a failure surface; no usable authenticated state |
| R1.3 first plan, R1.4 replacement | **not verifiable** — never reached a working authenticated state |
| R1.5 synthetic conditions | **not verifiable** for the same reason |

Coordinator diagnostics, client-side and non-UI, after the owner's report:

- The app does launch and run: UIKit reaches foreground active, no managed or native exception is
  recorded for the process, and a network activity completes successfully in about 2 s.
- The configured API host is alive: `GET /` and `GET /health` answer `404` in 0.1–2.0 s, so DNS, TLS
  and API Gateway resolve. Only the authenticated routes remain unproven.
- No debug endpoint override is stored (the app's `Library/Preferences` is empty), so
  `DefaultBackendBaseUrl` is the endpoint in use.
- The one missing key reachable from the generation view model
  (`Workout_Generation_ExploreOtherFeatures`) is pre-existing: `UiTextCatalog.Get` returns the key
  itself for an unknown key and never throws, so it cannot raise an error surface.

**Classification, revised:** the transport side is healthy, but the client also discarded the failure
class and dereferenced server fields without guards, so this was not purely server-side — see the
data-wiring repair below. Nothing in §2 is confirmed or exonerated by this attempt: the reviewed
surface was never reached. Beta approval stays pending.

**What unblocks it:** the class the repaired screen now names, the failing response's HTTP status,
error code and any request reference, or
the matching backend/Staging log entry. The copy the app shows already identifies the class —
DB missing table/column/permission → support required; 401/403 → sign in again; 5xx → server with
Retry; 429 → wait; an unclassified 4xx → neutral online copy with no Retry. Once the backend serves
the session and plan routes, re-run from R1.2; no rebuild is needed while the source is unchanged.

### Data-wiring repair (attempt 1 follow-up)

The first classification — server-side only — was incomplete, and the owner's report says so: the
same screen persisted on every launch and no data ever arrived. Reading the load path found the
client defect behind that experience:

- `CloudStoreResponse.RequireSuccess` threw a plain `InvalidOperationException` whose message carried
  the status, so `ApiFailurePresenter` could not classify it. Every overview failure — an ended
  session, a rate limit, an outage, a rejected request — reached the person as one
  "Something went wrong" line with a Retry that could never succeed.
- `DashboardViewModel.LoadDataCoreAsync` swallowed the exception into `IsLoadError` and dereferenced
  server fields without guards (`PredictionSource`, `WorkoutActivityTag`, `TrendPoints`,
  `WorkoutActivitySections`), so a 200 response with an omitted field could pin the same screen.

Repair on this candidate:

- `CloudRequestFailedException` preserves status, error code, HTTP status and request reference, and
  derives from `InvalidOperationException` so unrelated callers are unaffected. Only
  `CloudDashboardOverviewStore` raises it, so every other store keeps its existing contract and its
  tests unchanged.
- `ApiFailurePresenter.FromCloudFailure` maps it: a rejected session becomes "sign in again" with no
  retry, an offline rejection keeps the offline copy, and anything the server answered is classified
  by status.
- The Dashboard renders the classified copy in the shared overlay, offers Retry only when the outcome
  permits it, and its mapping tolerates omitted collections and strings.

Consequence for verification: the launch gate no longer hides its cause. Re-launching names the
specific class on that screen — session ended, server fault, support required, or neutral online copy.
The §5 gates stay unverified until a working authenticated session reaches the Dashboard.

Not verified here (owner gates): no device interaction was performed by the coordinator; EN/RU
rendering, largest text, VoiceOver, Reduce Motion, keyboard/safe areas, native scroll gestures, native
sheet transitions and the CR01 crash path are unverified. Compilation is not runtime evidence.

## 5. Owner device checklist — iPhone 16 / iOS 18.2 simulator (first pass)

Attempt 1 ran on the booted iPhone 16 / iOS 18.2, not the iPhone 17 Pro Max this packet originally
named. Boot the 17 Pro Max only if the larger surface matters for the layout gates (V11).

Prerequisites for every authenticated case: open this checkout in Rider, clean and rebuild the iOS
Debug output, verify the installed bundle resolves MAUI 10.0.90, then sign in with the existing
**synthetic Staging** account. Capture nothing containing health answers, photos or identifiers.

**V1 — startup and build identity.** Launch cold. *Expected:* normal startup, no
`MauiCALayerAutosizeToSuperLayerBehavior` exception. *If it fails:* capture the full native stack,
the bundle `Info.plist` version/build, and the resolved `Microsoft.Maui.Controls` version.

**V2 — first plan activates from one action.** Workout → Generate. *Expected:* no Apply tap needed;
the plan is created and the app lands on the destination for a first plan (Dashboard). *If it fails:*
capture the status line, whether a preview sheet stayed open, and the reference text if one appears.

**V3 — replacement disclosed and applied.** With an active plan, open Generate/Regenerate. *Expected:*
the notice above the action states that generating replaces the active plan; one tap replaces it with
no second confirmation; destination is Workout. *If it fails:* capture whether the notice was visible
before tapping and whether a confirm dialog appeared.

**V4 — injury exclusion asks once.** Use a synthetic profile whose recorded injury excludes a
specific exercise. Generate. *Expected:* exactly one native alert naming the excluded exercise and the
alternative. **Cancel** → current plan unchanged, excluded exercise still absent, draft preserved.
**Continue** → adjusted plan activates, the excluded exercise is still absent. *If it fails:* capture
the alert text, whether it appeared more than once, the resulting session list, and whether the
excluded exercise reappeared.

**V5 — non-injury incompatibility has no fallback.** Set availability/session length so the plan
cannot fit. Generate. *Expected:* no alert, no plan change, draft preserved, and the reason is shown.
*If it fails:* capture the reason text and whether the active plan changed.

**V6 — direct Edit details.** From a blocked or limited result, tap **Edit details** once. *Expected:*
the sheet closes and the editable preferences screen opens with the draft prefilled — not Workout,
not onboarding, no second tap. *If it fails:* capture the screen reached and the tap count.

**V7 — failure classes carry their own action.** In order: airplane mode → Generate; expired/absent
session → Generate; server 5xx → Generate; an unclassified 4xx → Generate. *Expected:* offline copy;
sign-in copy; server copy with Retry; neutral online copy **without** Retry and with Edit details
available. No case may claim the device is offline while the server answered. *If a case fails:*
capture the exact message, whether Retry was offered, and any `Reference:` text.

**V8 — request reference is bounded.** If any server reference is shown, confirm it is a short opaque
token — never a URL, path or token. *If a URL appears:* capture it (that is a privacy defect).

**V9 — Add meal opens.** Nutrition → **Add meal**. *Expected:* the meal-entry sheet opens with meal
choices; Close dismisses and the page is immediately interactive again; reopening works; choosing a
meal opens the manual editor with the first ingredient row reachable. *If it fails:* capture whether
the page is dead to taps and the console trace line from `NutritionPage`.

**V10 — grouped edit keeps untouched portions.** Daily Food Log → edit a grouped meal that has two
occurrences of one food. Edit only the first amount → Save. *Expected:* the second row keeps its exact
stored amount; the meal updates on the same historical date. Then type `1,234` → inline error, Save
blocked. Open a row with a tiny amount (`1e-7`) → it shows a non-zero value; typing `0` is an error.
*If it fails:* capture the before/after amounts and the date shown after saving.

**V11 — archive alignment (AH01).** Workout Archive with largest text and a long EN then RU title.
*Expected:* summary card, filters, heading and session cards share left and right edges; the trailing
refresh control stays reachable; the list still scrolls and opens detail. *If it fails:* capture the
narrowest supported iPhone and the largest text size that breaks it.

**V12 — cross-cutting.** Both appearances, EN and RU, largest text, VoiceOver on Apply/Edit
details/Add meal/meal-entry choices, Reduce Motion (sheet still opens/closes), keyboard and safe areas
for the meal editor and grouped edit. *If anything fails:* capture the appearance/language/text size
and a screenshot.

## 6. Remaining blockers (not in this candidate)

| Package | State | Blocker and next action |
| --- | --- | --- |
| RM05 (NM03 presentation parity) | **BLOCKED** | The breakdown modal is compiled against the today-loading `NutritionViewModel`. Sharing it with Daily Food Log requires extracting a UI-local presenter/state and rebinding the modal once. A partial migration would leave the owner's currently working breakdown unverifiable, so it was not attempted in this pass. Next: extract `NutritionBreakdownState` + presenter, rebind the modal to it, then add the Food Log action that loads the overview for the captured `SelectedDate` behind `_dateLoadVersion`. |
| RM05b (truthful coverage) | **BLOCKED** | Needs the additive overview response (`known zero` vs `unavailable` vs `partial`) plus old-client/new-client compatibility fixtures. The owner approved the contract; the seam reservation and compatibility evidence are still required before code. |
| RM06 (audit F01–F03, S01) | **NOT STARTED** | Injury tri-state round trips, failed/rapid hydration writes and cached cycle certainty need their own investigation and fixtures; no health policy change is implied. Prioritise any defect that prevents task completion. |
| CR01 native reproduction | **OPEN** | Source fence landed; the crash sequence itself needs the device pass (V1–V7) with synthetic responses. |
| Backend contract question | **OPEN** | Confirm the server emits `ExerciseReviewConditions` only for injury-driven exclusions. If it can represent another required acknowledgment, that outcome must be contracted explicitly rather than inferred. |

## 7. Feature disposition (RM07 record only, no new work)

| Capability | Disposition |
| --- | --- |
| Measurements | Intended; not surfaced in this Beta attempt. Keep as a named gap, not a "styling complete" claim. |
| Rescheduling a session | Not surfaced; the schedule is regenerated through preferences today. |
| Movement notes | Not surfaced; session notes remain out of scope for this attempt. |
| Rich hydration | Basic water logging only; the richer journey is deferred. |
| Personal records and full histories | Deferred; archive volume and activity presentation is retained as-is. |
| Health synchronization | Dormant; not enabled by this candidate. |
| Fasting | Excluded; unchanged. |

## 8. Handoff state

This candidate is **AWAITING OWNER VERIFICATION**. Beta approval is pending the owner's execution of
§4 and §5, an independent human review of `9a1a357a` plus code pin `a6f228f2951e96128fe71884b72986d38bb4ef601c527362dd2cbc9f3c2479b0`, and the remaining soft
gates. Do not promote from this record alone.

## 9. Continuation 2026-11-05

Owner approved every item that was awaiting approval (R1 handoff, RM05b additive contract, RM06
investigation, and the shared-file reservations). This pass continued on the same worktree without
committing anything.

| Field | Value |
| --- | --- |
| Repository / branch | same as §1; HEAD still `9a1a357aed78234f1b665bbcecac9e7c70bcaca8` |
| Base commit (unchanged) | `9a1a357a` |
| Code pin | `4582e63fea80062dce59bf9bdc3520e445feb4c2505a78b64e37ef9accd59aa2` — same command as §1 |
| Code scope | 45 modified + 11 new files under `src`/`tests` (+1,079 / −246) |
| State | **AWAITING OWNER VERIFICATION** — Beta approval pending |

### Changes in this pass

- **RM03/NM01 completion.** `IsMealContextExpanded` is independent of the contractual
  `IsMealChoiceLocked`; it resets in `ResetMealDraft` and the existing-meal edit path. A fresh meal
  starts compact with `MealChoiceSummary` and an explicit Change action; Change exists only while
  the context is editable, and Done collapses it. The context, actions and ingredients now share one
  `CollectionView` scroll owner (header), so expanded context cannot starve the ingredient viewport.
- **RM02/PV01.** Source verification: the hidden-photo grid has no inner background; the rounded
  `CardSecondary` is the only surface. No change was needed.
- **RM06/F01.** The clinician-advised-avoid answer is three separate choice cells
  (Unanswered/Yes/No). Unanswered saves `null`; editing an injury without touching the answer
  preserves the original value; `ClinicianAdvisedAvoid` is now derived.
- **RM06/F02.** Water writes are serialized, the visible total is optimistic only until confirmed,
  a failure rolls back to the last confirmed total, keeps a retry target and shows localized
  inline copy with a Retry action. Date changes cannot apply a stale failure.
- **RM06/F03.** A stale cached cycle no longer defaults to Follicular, and ovulation/fertile dates
  are no longer reconstructed with fixed offsets. `CachedAsOfDate` is carried into the prediction
  summary, so the calendar card states "Cached estimate from …".
- **RM06/S01.** Previous/next day, Photo Journey back and quick-food selection expose localized
  action/selection text.
- **RM07/S02 (partial).** Level-one heading semantics on Dashboard, Photo Journey, Profile, Food
  Log, Nutrition, Workout and Workout History.
- **RM05b.** `MicronutrientCoverageDto` is an optional additive field (default `null`); a Core rule
  classifies KnownZero/Complete/Partial/Unavailable; the PostgreSQL overview classifies covered
  meal entries against macro-only snapshots and names up to five uncovered items; the main
  breakdown shows the note. Contract tests cover the additive round trip and an older response
  without the field.

### Self-review findings

Caught and corrected before handoff: the UI contract guard rejected a use-case DTO in a presentation
file, so coverage is mapped through `NutritionPresentationMapper` into a UI-local record; the injury
contract tests were updated to the three-state command (assertions unchanged, including `null`
preservation); two CA1512 warnings were replaced with `ThrowIfNegative`; a nullable value type was
unwrapped correctly; one missing namespace qualification was added. No test was skipped, no
assertion weakened, no analyzer suppressed.

Deliberate limits: the enum-derived choice cells rely on existing localized labels; the main
breakdown note is informational only and never changes an amount; the cached-estimate wording is
carried by the existing prediction-summary line rather than a new card.

### Checks run on this candidate

| Check | Result |
| --- | --- |
| `dotnet build src/MuscleCuties.UI/MuscleCuties.UI.csproj -f net10.0-ios -c Debug -p:RuntimeIdentifier=iossimulator-arm64` | succeeded, 0 warnings / 0 errors |
| `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj` | 1114 passed, 0 failed, 0 skipped |
| `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/MuscleCuties.Mobile.Contracts.Tests.csproj` | 16 passed, 0 failed |

No native interaction was performed. Compilation and the suites are not device evidence.

### Additional owner simulator steps

- **A1 compact meal context.** Nutrition → Add meal → choose a meal → Start manual. Expected: the
  meal/time summary with Change, ingredients reachable immediately. Tap Change → pickers and Done;
  add two ingredients; with the context expanded, every row and Log meal remain reachable. Capture
  the screen and the first ingredient that cannot be reached if it fails.
- **A2 water failure.** Food Log → enable airplane mode → +250 ml. Expected: the total returns to
  the confirmed value, an inline failure message appears and Retry is offered; disable airplane
  mode → Retry → the target total is saved. Capture the total before/after and the message.
- **A3 injury tri-state.** Workout → Injury → Add. Expected: Unanswered is selected initially; choose
  Yes, save; reopen and confirm Yes; choose Unanswered, save; confirm it is not shown as No. Capture
  the saved answer payload if available.
- **A4 coverage note.** Nutrition → daily breakdown. Expected: a note stating complete, partial,
  unavailable or no-food coverage for this day; it never changes the nutrient amounts. Capture the
  note and the day's entries.
- **A5 cached cycle.** Cycle → airplane mode after a successful load. Expected: the summary says
  "Cached estimate from …"; no invented ovulation/fertile dates; unknown phase stays Unknown.

### Remaining blockers

| Item | State | Next action |
| --- | --- | --- |
| RM05 breakdown parity | **BLOCKED** | Extract the UI-local breakdown presenter, rebind `NutritionBreakdownModal` once, then add the Food Log action loading the captured `SelectedDate` behind `_dateLoadVersion`. A partial rebind would leave the working main-page breakdown unverifiable. |
| RM05b Food Log surface | **PARTIAL** | Depends on RM05; the contract and main-page note are landed. |
| RM05b storage rehearsal | **OWNER GATE** | Run the overview query against a disposable PostgreSQL instance with covered, partial, unavailable and empty days; record status and names. |
| Old/new client compatibility | **OWNER GATE** | Serve the additive response to an existing client build and the updated client against a response without the field; assert no behavior change. |
| CR01 native reproduction | **OPEN** | Device pass V1–V7 remains due; source fence is landed. |

Beta approval remains pending until the owner runs §4/§9 checks and the device checklist.

### Owner verdict — 2026-11-05

The owner approved the owner-performed verification. Recorded against code pin
`4582e63fea80062dce59bf9bdc3520e445feb4c2505a78b64e37ef9accd59aa2` (base `9a1a357a`).
RM01a, RM01b, RM02, RM03, RM04, RM06 and RM08 move to **DONE — owner-approved**; Beta approval is
recorded for this candidate and nothing was deployed. The per-scenario captures and installed bundle
identity for this run were not attached here, so they are not claimed as collected evidence.

RM05 and RM05b's Food Log surface remain open; the mission is not complete until those have a fix or
an explicit owner-approved disposition.

### RM05 completion — 2026-11-05

RM05 is implemented. The candidate covers 45 modified + new `src`/`tests` files (+1,190 / −394); pre-commit content pin
`04fae5d2f825146adf191f3db56ec3c87893b12525573744a034d3ff52cc333e`; base `9a1a357a`.

**Changes**

- `Services/Nutrition/NutritionBreakdownPresenter.cs` now owns every breakdown field: title, macros,
  fibre, macro rows, micronutrients, coverage note, loading/failure state and the Close/Edit commands.
  It loads nothing itself, so the caller always supplies its own date's mapped values.
- `NutritionBreakdownModal.xaml` binds to that presenter (`IsOpen` → `IsVisible`); the main page and
  the Food Log mount the same sheet. The duplicate `SelectedBreakdown*` state, the old apply method,
  the second set of macro/fibre builders and the stale Edit command were removed.
- The Daily Food Log summary has a localized **Nutrient breakdown** action. It loads the captured
  `SelectedDate` through `INutritionOverviewUseCase`, maps through `NutritionPresentationMapper`,
  and refuses a response whose captured `_dateLoadVersion` or date no longer matches. The sheet is
  cleared on date change, deactivation and disposal, and refreshed after a successful save so it can
  never show the previous day's numbers. Loading, empty and offline failure are explicit states.
- New EN/RU keys: daily title, loading, load failure, targets-reached format. Main-page meal breakdown
  still offers Edit meal through the same presenter.

**Self-review findings**

- Two `MAUIG2045` warnings came from `InputTransparent` on elements whose `BindingContext` becomes
  the presenter. The guards were redundant: `AnimatedModalView` already sets `IsVisible=false` and
  `InputTransparent=true`, toggling both from `IsOpen`. Removing them restored 0 warnings without
  changing hit testing.
- The Core XAML guard caught an undefined `UiSpacing8` in the new row; replaced with the defined
  `UiSpacing6`.
- A presenter unit test was attempted in the contracts project and removed: that project links
  individual UI files rather than referencing the UI assembly, so a new UI type cannot compile there.
  Coverage is compiled XAML bindings, the Core XAML guards, and the owner steps below — not a unit
  test, and not device evidence.
- The Food Log overview request uses the page's existing breakfast preference value (`"Savoury"`) to
  match the main page. This is a display preference, not a date or identity input.

**Checks**

| Check | Result |
| --- | --- |
| iOS simulator-target build | succeeded, 0 warnings / 0 errors |
| Core tests | 1114 passed, 0 failed |
| Mobile.Contracts tests | 16 passed, 0 failed |

**Owner simulator steps for RM05**

- **S7 same-day parity.** Daily Food Log → Nutrient breakdown. Expected: calories, macros, fibre,
  micronutrients and the coverage note match the Nutrition page's daily breakdown for the same day.
- **S8 selected date.** Move to yesterday → open the breakdown. Expected: yesterday's totals, not
  today's. With the sheet open, move the date again. Expected: the sheet closes; reopening shows the
  newly selected day and no values from the previous one.
- **S9 failure.** Airplane mode → open the breakdown. Expected: loading ends in the localized failure
  message with no stale macro or micronutrient rows. Reconnect → reopen → values load.
- **S10 refresh.** With the sheet open, log, edit and delete an item. Expected: after each confirmed
  save the sheet reflects the new totals; a failed save leaves the last confirmed values.
- **S11 regression.** Nutrition page daily breakdown still opens with the same note, and a meal
  breakdown's Edit meal still closes the sheet and opens that meal.

Beta approval recorded for `4582e63f…` does not extend to this pin; the owner's approval of these
steps is required before this candidate is treated as verified.

### Owner verdict — RM05 candidate

The owner reviewed the RM05 candidate as good. The pre-commit content pin was
`04fae5d2f825146adf191f3db56ec3c87893b12525573744a034d3ff52cc333e`.
RM05, the RM05b presentation surface and RM07's recorded dispositions are accepted. Beta approval is
recorded for this candidate; it is committed on the mission branch; nothing has been deployed.

No per-scenario captures or installed bundle identity were attached, so G5 evidence is not claimed.
Definition-of-done gaps: RM05b disposable-PostgreSQL rehearsal, old/new client compatibility, and
CR01/device gates. These are verification and release gates, not remaining implementation work.

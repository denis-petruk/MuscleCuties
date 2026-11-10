# Implementation plan — simulator run 2 results (2026-11-09)

Source evidence: [OWNER_SIMULATOR_QA_2026-11-09_RUN2.md](../release-audit/2026-11-09/OWNER_SIMULATOR_QA_2026-11-09_RUN2.md).
Base: `product/qa-fixes-20261109` @ `72de2ec3`. Scope: iPhone 17 simulator via Rider; iPhone SE and VoiceOver passes
are out of scope (owner, 2026-11-09). Plan only; nothing here is implemented.

Root-cause status: **Confirmed** = traced in code and consistent with the logs; **Hypothesis** = likely, verify first.

## Work packages (in order)

### WP-1 — Session lifecycle (P0: CR01, J08, F-18, F-19)

| # | Defect | Root cause | Status | Change | Files |
| --- | --- | --- | --- | --- | --- |
| 1a | Resume shows "Could not save this exercise", resets to exercise 1 (`PUT /optional-exercise` 400) | The optional Switch `Toggled` and Stepper `ValueChanged` handlers fire when the page binds data, so loading a session sends `SetExerciseOptionalAsync` for exercises the user never touched. For the already-logged Leg Curl the server answers "A logged exercise cannot be marked optional" → 400 → `ApplyFailure` shows the error card and the flow reloads | Confirmed | Only send when the value really changed by the user: compare with the item's last saved `IsOptional/OptionalSets` and skip during load (`_isApplyingLoadedState` guard); hide/disable the optional controls for logged exercises; an optional-marker failure must not reset the exercise flow (inline error on the control instead of `ApplyFailure`) | `Pages/Workout/WorkoutSessionPage.xaml.cs:115-133`, `ViewModels/Workout/WorkoutSessionViewModel.cs:270-295` |
| 1b | Resume lands on the intro (Start/Abandon) and then exercise 1, not the first unlogged exercise | Resume rebuilds activity state without restoring position | Hypothesis | On resume with logged exercises, open the exercise flow at the first unlogged/unskipped index; keep `_skippedIds` and pain-stopped ids from the server snapshot | `WorkoutSessionViewModel` load/resume path |
| 1c | Abandon activity does nothing | `AbandonPlannedSession` sets `IsAbandonConfirmationVisible`, but the confirmation `AnimatedModalView` never shows; likely the same modal-scope state as WP-2, or `IsBusy` stuck after the failed optional save | Hypothesis | Fix WP-2 first; log `IsBusy` and modal state on tap; make sure `IsBusy` is reset in every failure path of 1a | `WorkoutSessionViewModel.cs:1223-1260`, `WorkoutSessionPage.xaml:776-805` |
| 1d | Today "Start workout" for an in-progress session opens a reordered workout → `POST /sessions/start` 409 | The dashboard in-progress branch does not expose the session's planned day id; the CTA starts a new session for a different day context | Hypothesis | When a session is open, Today shows **Resume** and navigates with the open session's `planned_workout_day_id` | `PostgresDashboardOverviewStore.cs:585-603`, `DashboardViewModel` CTA |
| 1e | Plan Apply while a session is open orphans the session (F-15/R-15) | Accept does not consider open sessions | Confirmed (observed) | Block Apply with a clear reason while a session is open, or close the open session explicitly before switching | strength proposal accept path, preview footer copy |

Acceptance: 20× Train → Resume → session → Back with no error and position kept; a session can be completed (summary
shown, `completed_at` set) and abandoned (confirmation, `cancel` request, card gone).

### WP-2 — Shared modal accessibility leak (P0: Train tab invisible to accessibility; likely 1c)

| Root cause | Status | Change | Files |
| --- | --- | --- | --- |
| `ModalAccessibilityScope` is reference-counted per page (`Acquire` on open, `Release` on close). The activity chooser closes by navigating to the session page, not through Close, so `Release` never runs; the Train page siblings stay `AutomationProperties.ExcludedWithChildren = true` | Hypothesis (strong) | Track open modals as a set (idempotent acquire/release per modal instance); release on `IsVisible=false`, `Unloaded`, `HandlerChanging` and page `Disappearing`; add a debug assertion when a page appears with a non-zero scope | `Controls/Shared/AnimatedModalView.cs:295-375` and its open/close hooks |

Acceptance: after opening and leaving every shared sheet (activity chooser, swap, check-in, meal, cycle log, injury),
Maestro `inspect_screen` lists the page content. Add a UI-level test that toggles a modal and checks the property is
cleared.

### WP-3 — Onboarding fabrication (P0: E-01; P1: F-16)

| Root cause | Status | Change | Files |
| --- | --- | --- | --- |
| The VM starts with real values (`BirthDate = Today−18y`, `SelectedHeightCm = 165`, weight 65). `DatePicker.DateSelected` also fires when the binding sets the initial value (old = picker default, new = Today−18y), so `MarkBirthDateSelected` flips `_birthDateWasSelected` without user input. C-3's `Unfocused` hook adds another path | Confirmed (DB saved 2008-11-09/165/65 untouched) | Revert C-3. Make birthday, height and weight explicitly unanswered: nullable values with a placeholder, or "set" flags flipped only by user gestures (count `DateSelected` only after the page has appeared and the picker was focused). Continue blocks until all three are answered, with errors above Continue (E-04 pattern). Server: accept the profile only with explicit values (no defaults) | `ViewModels/Profile/ProfileSetupViewModel.cs:24-40,160-200,206-215`, `Pages/Onboarding/ProfileSetupPage.xaml(.cs)` |

Acceptance: untouched form → three inline errors and no DB row; entered values saved exactly; nutrition targets
appear only after real measurements.

### WP-4 — Logging integrity and history (P1: J05/G10, J09)

| # | Defect | Root cause | Status | Change | Files |
| --- | --- | --- | --- | --- | --- |
| 4a | Untouched 2nd set saved as observed (0 kg × 10, `is_observed=true`) | `BuildLogInput` sends every row in `item.Sets` once `SetsWereEdited` is true (`ObservedSetInputMapping.Build`). The 2nd row existed but was clipped off screen (WP-6) | Confirmed | Per-row confirmation: only rows the user edited or ticked are observed; untouched rows are omitted (or planned/unobserved) | `WorkoutSessionViewModel.cs:1827-1861`, `ObservedSetInputMapping`, set row model |
| 4b | Archive lists in-progress sessions as completed | Archive queries filter `!IsDeleted` only, not `CompletedAt != null` | Confirmed | Add `CompletedAt.HasValue` to the "past completed" queries and counts | `Infrastructure.PostgreSql/Workout/Archive/PostgresWorkoutArchiveStore.cs:34,66,263` |

### WP-5 — Today card truth and localization (P1: E-06 in progress, U-09 RU, F-01)

| Root cause | Status | Change | Files |
| --- | --- | --- | --- |
| The dashboard in-progress branch returns hard-coded English strings: `"In Progress"`, `"High"`, `"Active Session"`, `"Strength"`, `"{n} min"`, `"{n} sets completed"` | Confirmed | Return codes, not prose (status code, intensity code from the E-06 mapping → Moderate for strength, unit values as numbers); the client localizes via `UiTextCatalog`. Plan titles ("General strength training", "Order 1 · …") get catalog keys or a server-side localized name field. Additive DTO fields only (integration authority owns DTO changes) | `PostgresDashboardOverviewStore.cs:585-603`, `DashboardViewModel`, `WorkoutViewModel` featured card, catalogs |
| Readiness 65 / Recovery 70 before any check-in | Confirmed (observed) | No score without input: show "Check in to see readiness" until a check-in exists | readiness card VM/store |

### WP-6 — Session screen layout and C-9 follow-ups (P1: F-06, R-9)

- Move the pain report out of the bottom action grid into a shared sheet (`AnimatedModalView`) so the exercise and set
  rows keep their space; keep Switch / Stop / Cancel. Pain-context copy in the empty swap panel: "No reviewed
  alternative. Stop this exercise for today."
- Bottom bar back to one row (Swap · Notes · Skip · This hurts as icon+label or overflow) to give the scroll area room;
  make the logged-set list grow instead of clipping (fixes the hidden 2nd row behind 4a).
- First tap ignored while the celebration banner shows: dismiss the banner on any action.
- Files: `Pages/Workout/WorkoutSessionPage.xaml:614-650`, `WorkoutSessionViewModel` pain commands.

### WP-7 — First run and resilience (P1: J15, F-10, F-11)

| Item | Change | Files |
| --- | --- | --- |
| Session survives uninstall (Keychain) | On launch, if a non-sensitive Preferences install marker is missing, clear SecureStorage session/snapshot keys, then write the marker | app startup (`App`/`AppStartupPage`), `Services/Security/SecureStorageSnapshotCacheStores.cs` |
| You page fails without S3 setting | Construct the presigned-URL service lazily; the vault card shows "unavailable" instead of failing the page | `DefaultS3PresignedUrlService`, profile VM |
| Infrastructure copy shown to users | Replace "first-party PostgreSQL / AWS protection settings have not been verified" with user-facing privacy wording | profile catalog keys (EN/RU) |
| Notification prompt on first launch | Request in context (e.g. when enabling reminders) | startup permission request |

### WP-8 — Dynamic Type and RU fit (P1: J14)

- Card headers: stack icon above text (or remove the fixed icon column) at accessibility sizes; allow multi-line
  buttons; avoid mid-word breaks (`LineBreakMode=WordWrap` with sufficient width).
- React to content-size changes without relaunch (subscribe to the trait change and refresh font resources).
- Files: Today/Train cards, injury log sheet footer buttons, `Styles.xaml` button styles.

### WP-9 — P2 polish (styling audit)

System-blue checkboxes → role colour; duplicate Notes; two primary CTAs on Train (in-progress + Start); destructive
style for Delete account; Quick Log flow-intensity chips; tab bar visible under sheets; stale acknowledgment error;
"Order 1" ordinal; pace shown when unasked; native replace-plan alert → shared sheet; weekday save before Apply
(save with Apply, or label as "availability saved").

## Sequence and estimates

| Step | Content | Effort |
| --- | --- | --- |
| 1 | WP-2 modal scope (unblocks 1c and accessibility) | 0.5–1 d |
| 2 | WP-1 session lifecycle (1a first, then 1b–1e) | 2 d |
| 3 | WP-3 onboarding (revert C-3) | 1 d |
| 4 | WP-4 logging + archive | 1 d |
| 5 | WP-5 Today truth/localization (additive DTO) | 1–1.5 d |
| 6 | WP-6 session layout + C-9 follow-ups | 1 d |
| 7 | WP-7 first run/resilience | 0.5–1 d |
| 8 | WP-8 Dynamic Type/RU | 1–1.5 d |
| 9 | WP-9 polish | 1–2 d |
| 10 | Tests: unit for 1a guard, 4a mapping, 4b filter, WP-3 rules; PostgreSQL integration for archive and session complete/abandon; owner runs suites | with each WP |
| 11 | Simulator rerun via Rider (iPhone 17): full 21-row matrix incl. CR01 ×20, J08 completion, J15 fresh install, RU/dark/XXXL, Reduce Motion | 1 d |

Total ≈ 10–13 engineer-days. Owner decisions still pending from the earlier plan: ratify policy amendments A-4…A-7.
Release verdict unchanged (NO-GO) until step 11 passes and owner/AWS items close.

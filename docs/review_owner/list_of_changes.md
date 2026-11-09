# List of changes — awaiting owner review

| Field | Value |
| --- | --- |
| Branch | `product/qa-fixes-20261109` (local only, not pushed, no PR) |
| Base | `integration/beta-polish-20261109-v2` @ `f1918e138634ba39c2483bde3e1194b6d346f2a8` |
| Worktree | `.agent-worktrees/qa-20261109` |
| Date | 2026-11-09 |
| Source plan | [REMAINING_WORK_PLAN_2026-11-09.md](../beta-readiness/REMAINING_WORK_PLAN_2026-11-09.md) |
| Verification | **None run by the agent, as instructed.** Nothing was built, tested, compiled or launched after these edits; the edited XAML files were checked as well-formed XML and the shell scripts with `bash -n`. The owner runs builds, tests and simulator checks independently. |

Release status was not changed: G12 and the NO-GO verdict stand. The single edit to `RELEASE_READINESS.md` records the
owner's phone-QA deprecation (item 9).

## 1. C-1 — Caption contrast and dark-mode parity (P1, styling S-01)

- `src/MuscleCuties.UI/Controls/Workout/FeaturedWorkoutCard.xaml` (exercise preview count) and
  `src/MuscleCuties.UI/Pages/Workout/StrengthPlanGenerationPage.xaml` ("Required" tag): `TextColor="{StaticResource
  Primary}"` (#C85A87: 3.99:1 light, 3.42:1 dark) replaced with the existing accent pair
  `AppThemeBinding Light=PrimaryDark (#A6446D), Dark=SecondaryDarkText (#F0B8CF)` already used by `Styles.xaml`.
- No new token or style.
- **Check:** both captions in light and dark; contrast ≥ 4.5:1; `UiStyleGateTests`.

## 2. C-4 — Validation errors are no longer announced as headings (P2, styling S-03)

- `Pages/Onboarding/ProfileSetupPage.xaml` (2 labels) and `Pages/Onboarding/QuizPage.xaml` (1 label): removed
  `SemanticProperties.HeadingLevel="Level3"` from `ErrorLabel`s.
  - Correction to the audit: 3 error labels had it, not 7.
- Those `ErrorMessage` labels now use the existing `ScreenReaderStatusAnnouncementBehavior`, so VoiceOver announces the
  reason when it appears. Added `xmlns:sharedControls` to both pages.
- `ProfileSetupPage.xaml.cs`: announces the static wellness-acknowledgment error when Continue reveals it
  (`SemanticScreenReader.Default.Announce`).
- **Check:** VoiceOver on invalid Continue (profile setup and quiz): reason announced once, not as a heading. Related to
  the E-04 layout.

## 3. C-5 — Russian mistranslation (P3, styling S-05)

- `Services/Localization/RussianUiText.CycleWorkout.cs`: `Cycle_QuickLog_LoadingSavedEntry` changed from
  "Загрузка сохраненного входа…" ("saved login") to "Загрузка сохранённой записи…".

## 4. C-2 — Stale age error never cleared (P2, Q-02, E-01 area)

- `ViewModels/Profile/ProfileSetupViewModel.cs`: the age handler set the key `Ui_Onboarding_Error_Age` but cleared only
  when the message equalled the old English sentence.
  - It now uses a `AgeErrorKey` constant for both, so a corrected date clears the error.
  - Other errors are untouched.
- **Check:** pick an under-18 date if reachable (picker max is today−18y), then an adult date; the message disappears.

## 5. C-3 — Confirming the pre-filled birthday (P2, Q-03, E-01 area)

- `ProfileSetupViewModel.cs`: new `MarkBirthDateConfirmedFromPicker()`.
- `ProfileSetupPage.xaml`: the `DatePicker` now has `Unfocused="OnBirthDatePickerUnfocused"`, and
  `ProfileSetupPage.xaml.cs` adds that handler.
- Opening the picker and closing it with Done now counts as answering, even when the date is unchanged. Before this, a
  user turning 18 today could never confirm.
- An untouched picker is still rejected (E-01 fix preserved). The server age gate is unchanged.
- **Check:** E-01 regression:
  - an untouched picker + Continue must still show "birth date required";
  - open → Done on the default date must allow Continue;
  - `date_of_birth` must never be saved without interaction.
- **Risk:** on iOS, if `Unfocused` fires without the picker having been opened, an untouched date would count as
  confirmed. Verify on the simulator.

## 6. C-7 — T-21 analyzer warnings (P2)

- `tests/MuscleCuties.Core.Tests/BackendApiRefreshFailureTests.cs`: dropped `ConfigureAwait(false)` (xUnit1030).
- `tests/MuscleCuties.Core.Tests/CycleOnboardingAnchorTests.cs`: two tests made `async Task`; they now `await` instead
  of `.GetAwaiter().GetResult()` (xUnit1031 ×2).
- `tests/MuscleCuties.Core.Tests/TodayCheckInScoreEnergyTests.cs`: removed `[InlineData(1)]`, which duplicated
  `[InlineData(Minimum)]` (= 1) (xUnit1025).
- **Check:** `dotnet build tests/MuscleCuties.Core.Tests` shows 0 xUnit analyzer warnings; test count drops by 1, the
  duplicate row.

## 7. C-10 — S-05: seven-day post-clearance window removed (owner decision D-4, taken at recommendation)

- `src/MuscleCuties.Core/Domain/Workout/Planning/WorkoutInjuryClearanceRule.cs`:
  `OptionalCalendarDaysIncludingClearance` changed from 7 to 0, with the rationale documented on the class.
  - A cleared exercise is restored as prescribed from its clearance date, and only when current eligibility permits.
  - Unknown, active or future clearance still blocks.
  - The window remains one constant, so reverting is a one-line change.
- Effects that flow through existing code:
  - `InjuryProgressionPhaseRule` now returns `ClearedAfterWindow` from the clearance day, so `ClearedOptionalWindow`
    becomes unreachable.
  - `RequiresConservativeRestriction` releases on the clearance day.
  - Reconciler `RequiredFrom` dates equal the clearance date.
- Tests updated to the new semantics:
  - `tests/MuscleCuties.Core.Tests/WorkoutInjuryClearanceRuleTests.cs` (rewritten);
  - `tests/MuscleCuties.Core.Tests/InjuryProgressionPhaseRuleTests.cs` (first case).
  - `InjuryCanonicalReconciliationTests` (integration) references the constant and needs no edit.
- **Check:**
  - Core suite; full PostgreSQL integration suite (reconciler and preview tests may encode the old 7-day optionality
    indirectly; none were found by search, but they could not be run);
  - J04 on the simulator: a cleared exercise shows as required on the clearance day, in preview and in plan.
- **Owner:** ratify policy amendment A-7, or revert the constant to 7.

## 8. C-9 — S-04: in-session pain action (owner decision D-3, taken at recommendation)

- `ViewModels/Workout/WorkoutSessionViewModel.cs`: session-scoped `_painReportedIds`, `IsPainReportVisible` and
  `HasPainReports`, plus four commands:
  - `ReportPainCommand` opens the inline report;
  - `PainSwapCommand` opens the existing swap panel, whose candidates come from canonical eligibility (reviewed
    alternatives only);
  - `PainStopExerciseCommand` uses the existing skip path, never erasing logged sets;
  - `CancelPainReportCommand` handles a mis-tap.
  - State is cleared with the other per-session state on load.
- `Pages/Workout/WorkoutSessionPage.xaml`:
  - The bottom action bar is now two columns: Swap | Note, Skip | **This hurts**, then Next.
  - An inline `CardSecondary` pain report (title, explanation, Switch / Stop / Cancel) sits above it. It has no new
    modal, no scoring, and no load added to make up for the stopped exercise.
  - After the activity, the summary shows a reminder to add the pain to the injury log.
- Copy (EN/RU, key-identical), in `UiTextCatalog.CycleWorkout.cs` and `RussianUiText.CycleWorkout.cs`:
  - `Workout_Session_PainButton`, `…PainAccessibility`, `…PainTitle`, `…PainBody`, `…PainSwap`, `…PainStop`,
    `…PainLogReminder`;
  - Cancel reuses `Common_Cancel`.
- **Not changed (deliberate):**
  - The server still refuses injury saves while a session is open (`ActiveWorkoutPreventsInjuryException`). The pain
    report doesn't write an injury mid-session; it stops the movement and reminds the user to log afterwards.
  - Recording the pain server-side during the session would need a new session-scoped endpoint and store. That is a
    follow-up if the owner wants it.
- **Check:**
  - `LocalizationCatalogTests` (EN/RU parity);
  - J04/J08 on the simulator: "This hurts" → Switch shows only eligible swaps; Stop advances without erasing logged
    sets; the summary reminder appears; RU at the largest Dynamic Type (two-column bar) shows no clipping.
- **Owner:** ratify policy amendment A-6.

## 9. Physical-device QA deprecated; simulator wired through Rider (owner request, 2026-11-09)

- `scripts/run-ios-simulator.sh` (new):
  - resolves a **simulator only**, by name or UDID (simctl never lists physical devices), then boots it and opens
    Simulator;
  - optionally sets appearance (`MC_SIM_APPEARANCE`) and language/locale (`MC_SIM_LANGUAGE`);
  - refuses a non-loopback `MC_LOCAL_API` and warns if the API is down;
  - runs `dotnet build … -f net10.0-ios -p:RuntimeIdentifier=iossimulator-arm64 -p:_DeviceName=:v2:udid=<UDID> -t:Run`.
- `scripts/run-local-api.sh` (new):
  - runs the API on `127.0.0.1:5080` (Development) against a loopback-only PostgreSQL (`MC_LOCAL_PG`, default
    `127.0.0.1:55461/mc_local`);
  - generates throwaway JWT, receipt RSA and HMAC keys in a temp folder that is deleted on exit;
  - `--migrate` runs DbMaintenance `migrate`, `seed` and `dbcheck`;
  - OTP codes print in the terminal (Development delivery).
  - This captures the keys the QA pass had to discover by hand.
- `.run/` (new, versioned per `.gitignore` policy): Rider shell run configurations:
  - `Local API (loopback)`;
  - `iOS Simulator - iPhone 17 Pro`;
  - `iOS Simulator - iPhone 17 Pro (dark, RU)`;
  - `iOS Simulator - iPhone SE (3rd gen)`.
- **Debugger attach:** these configurations build and launch only. For breakpoints, run Rider's own `MuscleCuties.UI`
  project configuration with the same simulator selected in the device list.
- Docs:
  - `docs/beta-readiness/RELEASE_READINESS.md`, owner action 8: QA01 runs on the Rider-attached simulator; physical
    device deprecated.
  - `docs/beta-readiness/REMAINING_WORK_PLAN_2026-11-09.md`, P-1: marked deprecated.
- **Check:** run both configurations from Rider on this Mac:
  - app launches on the named simulator and reaches the local API;
  - the dark/RU configuration renders Russian in dark appearance.
- **Note:** TestFlight testers still install on real phones; only the QA acceptance gate moved.

## 10. C-8 — S-03 policy amendments (owner decisions D-1/D-2, taken at recommendation)

- `docs/release-audit/2026-11-05/WORKOUT_POLICY.md`: new section "Proposed amendments — 2026-11-09 (pending owner
  ratification)". The adopted text stays authoritative until ratified:
  - A-4: readiness reduces one optional set only for Beta; the load step is deferred until a prescription load datum
    exists. This matches the current production path, so no code change.
  - A-5: cycle estimates never change reps, sets or load by themselves; the +2 reps / extra set is withdrawn.
    `ComposedReadinessCycleDoseRule` stays test-only, as today.
  - A-6: in-session pain (item 8).
  - A-7: no post-clearance window (item 7).

## Previously produced in this session (reports, uncommitted on the same branch)

- `docs/release-audit/2026-11-09/OWNER_SIMULATOR_QA_2026-11-09.md` and `evidence-owner-qa-2026-11-09/`
- `docs/beta-readiness/SIMULATOR_TEST_REPORT_2026-11-09.md`
- `docs/beta-readiness/STYLING_AUDIT_2026-11-09.md`
- `docs/beta-readiness/REMAINING_WORK_PLAN_2026-11-09.md`

## Not done (and why)

| Item | Reason |
| --- | --- |
| C-6 native alert migration | Needs a DESIGN.md proposal approved by the owner first (design authority changes by proposal) |
| S-03 load step-down implementation | Deferred by amendment A-4; needs a load datum and an expand-only migration (B owns numbering) |
| Server-side session-scoped pain record | Out of scope for the Beta surface; listed as a follow-up in item 8 |
| Live AWS A1–A6, A11 | Needs a concrete, owner-approved change set and target; not authorized per action |
| Any build/test/simulator run | Owner runs them independently, as instructed |

## Suggested owner verification order

1. Build the UI and Core.Tests (expect 0 xUnit warnings).
2. Run the Core, Mobile Contracts and Registration suites, then the PostgreSQL integration suite.
3. From Rider: `Local API (loopback)` (with `--migrate` the first time), then `iOS Simulator - iPhone 17 Pro`. Walk
   E-01…E-07, J04 and J08 (pain action), CR01, then the dark/RU and iPhone SE configurations for J14.
4. Ratify or reject A-4…A-7. A rejected A-7 is a one-line revert (`OptionalCalendarDaysIncludingClearance = 7`) plus
   restoring the two test files from the base.

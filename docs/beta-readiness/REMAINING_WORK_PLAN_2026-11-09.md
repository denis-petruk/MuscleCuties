# Remaining work plan — 2026-11-09

Baseline `f1918e138634ba39c2483bde3e1194b6d346f2a8`. Inputs: [OWNER_SIMULATOR_QA_2026-11-09.md](../release-audit/2026-11-09/OWNER_SIMULATOR_QA_2026-11-09.md),
[SIMULATOR_TEST_REPORT_2026-11-09.md](SIMULATOR_TEST_REPORT_2026-11-09.md), [STYLING_AUDIT_2026-11-09.md](STYLING_AUDIT_2026-11-09.md),
[AUDIT.md](AUDIT.md), [RELEASE_READINESS.md](RELEASE_READINESS.md), [WORKOUT_POLICY_RESEARCH.md](../release-audit/2026-11-09/WORKOUT_POLICY_RESEARCH.md).
**Plan only — nothing here is implemented.** Effort is in engineer-days (d).

## Recommended sequence

1. **Decisions** (owner): D-1…D-4 policy, OD-5 and OD-2/3/4/6/7/8 confirmations.
2. **Code fixes**: C-1…C-7 (none depend on the decisions); then C-8…C-10 after D-1…D-3.
3. **Regression tests**: unit + PostgreSQL integration for each fix; T-21 hygiene.
4. **Simulator rerun**: build outside the sandbox (E-1), then the full 21-row matrix plus supplementary checks.
5. **PR/CI**: owner opens the PR from `-v2` (or a follow-up branch) into `main`; CI green.
6. **Internal TestFlight prerequisites**: archive/sign/upload (T-1).
7. **AWS**: A1–A6, A11 (L-1…L-3).
8. **External testers**: QA01 (P-1), App Store metadata (T-2).

## Owner decisions

| ID | Decision | Current evidence | Recommended resolution (from primary sources in WORKOUT_POLICY_RESEARCH) | Unblocks | Effort |
| --- | --- | --- | --- | --- | --- |
| D-1 | S-03 decision 4: one load step down on poor readiness vs set-only | Production reduces one optional set only; `PlannedExerciseSnapshot` has no load | **Amend the policy to set-only for Beta.** Autoregulation supports either; implementing load needs a new load datum and schema change (C-8 alt). If the owner keeps the load step, C-8 applies (L). | C-8 | owner 0.1 d |
| D-2 | S-03 decision 5: cycle reps/extra set | Not in production path | **Amend to symptoms/readiness only**; current evidence shows no phase effect on strength (PMC10076834; RCT PMC13561106). No code. | Policy text | owner 0.1 d |
| D-3 | S-04 mid-session pain surface | No in-session action; injury save blocked during a session | **Inline "This hurts" action on the exercise card**: stops the movement, records via the existing injury store, offers only reviewed substitutes (policy decision 6 already adopted). | C-9 | owner 0.1 d |
| D-4 | S-05 seven-day post-clearance window | In code, not in preview; no source | **Delete the 7-day concept**; restore only when current eligibility permits. | C-10 | owner 0.1 d |
| D-5 | OD-5, OD-2/3/4/6/7/8 confirmations | Implemented at recommendation | Confirm as implemented | T-1 | owner 0.2 d |
| D-6 | Live AWS change set (A1–A6, A11) | Repo-only evidence | Approve the concrete change set and its target account/stack in INFRA_RULES_2026-11-09 | L-1…L-3 | owner 0.5 d |

The resolutions above are the evidence-backed recommendations, adopted here as the **planning default**. They are not
clinical approvals; the owner ratifies each before C-8…C-10 start (CLAUDE.md: no invented health rules).

## Code fixes

| ID | Title | Sev | Evidence | Root cause / hypothesis | Files / areas | Approach | Tests / acceptance | Gate / owner | Deps | Risk | Effort | Decision? |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| C-1 | Bare `Primary` caption contrast | P1 | S-01: 3.99/3.42:1 | Static colour without a dark pair, on small text | `Controls/Workout/FeaturedWorkoutCard.xaml:243`, `Pages/Workout/StrengthPlanGenerationPage.xaml:736`, `UiStyleGateTests` | `AppThemeBinding` with TextAccent / SecondaryDarkText; add a gate rejecting bare `StaticResource` on `TextColor` | Gate test fails before and passes after; contrast ≥ 4.5:1 both themes; simulator light/dark capture | G12 / A | — | Low | S (0.5 d) | No |
| C-2 | Stale age error | P2 | Q-02 | Clears only when it equals the old English literal | `ViewModels/Profile/ProfileSetupViewModel.cs:168-179` | Clear when age ≥ 18 regardless of message, or compare with the key | Unit test: set under-18 then adult → `ErrorMessage` empty | E-01 / A | — | Low | S (0.25 d) | No |
| C-3 | 18th-birthday default can't be confirmed | P2 | Q-03 (hypothesis) | Selection counted only on change; default = today−18y | `ProfileSetupViewModel.cs:24,160-166`, `ProfileSetupPage.xaml.cs:29` | Leave the picker unset (placeholder) or add explicit confirm; never auto-save the default | Device check first; unit test for "untouched default is rejected; confirmed default accepted" | E-01 / A | Device repro | Low | S (0.5 d) | No |
| C-4 | Errors announced as headings | P2 | S-03 | `HeadingLevel` on `ErrorLabel` | `ProfileSetupPage.xaml`, `QuizPage.xaml`, `WorkoutSessionPage.xaml` | Remove heading level; announce on change | VoiceOver pass on simulator | G12 / A | — | Low | S (0.5 d) | No |
| C-5 | RU mistranslation | P3 | S-05 | Copy | `RussianUiText.*` (`Cycle_QuickLog_LoadingSavedEntry`) | "Загрузка сохранённой записи…" | `LocalizationCatalogTests` | J14 / A | — | Low | S (0.1 d) | No |
| C-6 | Native alert inventory | P2 | S-02 (21 calls) | Pre-dates the shared lifecycle | Profile, PhotoVault, MauiProgram, onboarding, nutrition | Classify; keep destructive/system alerts native (record the exception by DESIGN proposal); migrate content alerts | Design gate + VoiceOver focus check | Design / A | DESIGN proposal | Medium | M (1–2 d) | Owner approves DESIGN proposal |
| C-7 | T-21 analyzer warnings | P2 | 4 warnings confirmed | Test hygiene | `BackendApiRefreshFailureTests.cs:183`, `CycleOnboardingAnchorTests.cs:44,64`, `TodayCheckInScoreEnergyTests.cs:26` | Await instead of `.Result`; drop `ConfigureAwait(false)`; remove the duplicate InlineData | Build with 0 analyzer warnings; same pass counts | G01 / A | — | Low | S (0.25 d) | No |
| C-8 | S-03 implementation | P1 | D-1 | No load datum | Policy text (set-only) **or** `PlannedPrescriptionSnapshot`, `CycleReadinessPrescriptionRule`, migration 008 | Set-only: docs + tests asserting no load change. Load step: add the load datum (expand-only migration), one decrement per session, baseline preserved | Unit + PostgreSQL integration; schema ledger | G07 / A+B | D-1 | Medium (load: High) | S / L (4–6 d) | **Yes** |
| C-9 | S-04 in-session pain action | P1 | D-3 | Surface missing; injury store refuses saves during a session | `WorkoutSessionPage.xaml`, `WorkoutSessionViewModel`, `InjuryLogViewModel.cs:379`, injury store/use case | Session-scoped pain report through the existing store; stop the movement; reviewed substitutes only; no relaxation on missing data | Unit (rule), integration (store), simulator J04/J08 | G06 / A | D-3 | Medium | M (3 d) | **Yes** |
| C-10 | S-05 window removal | P2 | D-4 | Rule/preview disagree | `WorkoutInjuryClearanceRule.cs:10`, `InjuryProgressionPhaseRule.cs:14` | Delete the window; eligibility decides | Rule tests; preview parity test | G06 / A | D-4 | Medium (restores load sooner) | S (1 d) | **Yes** |

## Environment / verification

| ID | Item | Sev | Evidence | Approach | Acceptance | Owner | Effort |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E-1 | Simulator build blocked in agent sandbox | P0 (gate) | Q-01: `AF_UNIX` EPERM; MSB4216 | Run the build in a normal terminal/Rider, or allow Unix sockets for the agent sandbox; keep loopback-only API | `.app` produced from `f1918e1` or later | Owner | 0.1 d |
| E-2 | Full simulator matrix | P0 | 21 BLOCKED | iPhone SE (iOS 18.2) + iPhone 17 Pro (iOS 26.3); light/dark; EN/RU; default and largest DT; VoiceOver; Reduce Motion; CR01 ×20; supplementary E-01…U-10, G05/G08/G10/G13 | New dated record with PASS/FAIL + screenshots | A (agent) after E-1 | 1.5–2 d |
| E-3 | Re-run unit/integration suites locally | P1 | `dotnet test` blocked | Same as E-1 | Core/Contracts/Registration/integration counts recorded | A | 0.5 d |

## Live AWS (owner-authorized only)

| ID | Item | Evidence | Approach | Acceptance | Effort |
| --- | --- | --- | --- | --- | --- |
| L-1 | A1–A3: `$LATEST`, Beta designation, 14-day backups, deletion protection, restore drill | INFRA_RULES_2026-11-09 | Protected IaC plan/apply under lock | Post-apply verification + restore drill evidence | 1–2 d |
| L-2 | A4/G15: read-only `dbcheck` on Beta, adoption rehearsal on a restored snapshot; A5 aliases | Local head 7 only | db-release workflow; immutable versions + `beta` aliases | dbcheck compatible on target; alias serves pinned version | 1 d |
| L-3 | A6/G14: secrets to Secrets Manager + rotation; A11: deploy `observability.yaml`, confirm email | Plaintext env (live FAIL) | IaC + rotation runbook | No plaintext secrets in Lambda env; alarm email confirmed | 1 d |

## Physical device / TestFlight / App Store

| ID | Item | Deps | Effort |
| --- | --- | --- | --- |
| P-1 | ~~QA01 on a real iPhone~~ **Deprecated (owner, 2026-11-09):** QA01 runs on the Rider-attached simulator (`.run/`, `scripts/run-ios-simulator.sh`): EN/RU, VoiceOver, Dynamic Type, CR01 | E-2 green | 1 d |
| T-1 | Internal TestFlight: PR merged, D-5, E-01/E-02 device re-check, archive/sign (distribution profile; remove HealthKit capability from App ID), upload, internal group | E-2, PR/CI | 0.5–1 d |
| T-2 | External: App Privacy answers matching `PrivacyInfo.xcprivacy`, beta description, demo account, feedback email | L-1…L-3, P-1, C-8…C-10 | 0.5 d |

## Priority roll-up

- **P0:** E-1, E-2 (simulator gate); L-1…L-3 and P-1 for external testers.
- **P1:** C-1, C-8, C-9; E-3.
- **P2:** C-2, C-3, C-4, C-6, C-7, C-10. **P3:** C-5.

## Run 2 additions (Rider-attached simulator, fix branch `1a4ca7dd`)

Evidence: [OWNER_SIMULATOR_QA_2026-11-09_RUN2.md](../release-audit/2026-11-09/OWNER_SIMULATOR_QA_2026-11-09_RUN2.md).

| ID | Sev | Item | Hypothesis / area | Approach | Acceptance | Effort |
| --- | --- | --- | --- | --- | --- | --- |
| R-1 | P0 | E-01 still saves today−18y; height/weight defaults saved untouched | `ProfileSetupViewModel` initialises `BirthDate`, `SelectedHeightCm`, `SelectedWeightKg` to real values; `_birthDateWasSelected` flips without user input (iOS initial date push or the C-3 `Unfocused` hook) | Make DOB/height/weight unset until entered (nullable or explicit "set" flags); drop the C-3 `Unfocused` shortcut and add an explicit "Confirm date" when the picker is unchanged; server rejects a DOB equal to the request date −18y only if the client marks it untouched | Untouched form blocks with "birth date required"; DB has no row until the user answers | M (1 d) |
| R-2 | P0 | CR01: resume → Start → "Could not save this exercise", back at exercise 1, inputs missing; `PUT /workouts/optional-exercise` 400 | Resume replays skipped/pain-stopped exercises as an optional-exercise mutation the server rejects | Do not send optional-exercise for skipped/stopped items; resume at the first unlogged exercise | 20× plan→session→back with no error and position kept | M (1–2 d) |
| R-3 | P0 | Train tab content missing from the accessibility tree after the session sheet | `AnimatedModalView` isolation not released on close (hides siblings) | Restore `AccessibilityElementsHidden`/modal flags on close; add a UI test that inspects the tree after open/close | Maestro/VoiceOver sees Train content after closing any sheet | S–M |
| R-4 | P1 | Fabricated observed set (2nd set saved as 0 kg × reps, `is_observed = true`) | Next persists every prescribed set row, including ones never shown or edited | Persist only rows the user confirmed; unconfirmed rows stay planned/unobserved | DB contains only confirmed sets | M |
| R-5 | P1 | Archive lists in-progress session as completed | Archive query includes sessions without an end timestamp | Filter by completed state | In-progress sessions absent from "Past Completed" | S |
| R-6 | P1 | Today card in progress: "High", "Upcoming" + "In Progress", Start instead of Resume | Dashboard in-progress branch maps intensity from activity tag; status copy not unified | Reuse the E-06 mapping; one status; Resume CTA | Today and Train agree | S |
| R-7 | P1 | RU leftovers on Today/Train (plan titles, badges, "In Progress", "High", "min", readiness copy) | Server-authored strings / runtime map gaps | Key these strings; localize unit suffixes | RU screens show no English UI copy (exercise names excepted, per catalog decision) | M |
| R-8 | P1 | J14: mid-word breaks and clipped actions at XXXL RU; Dynamic Type not live | Fixed icon columns + single-line buttons | Allow wrap / stack icon above text at large sizes; buttons wrap; respond to content-size change | XXXL RU walk with no clipping | M |
| R-9 | P1 | C-9 follow-ups: empty swap panel says "keep it" in pain flow; card squeezes content; four-row bar clips set rows | Pain card in action bar grid | Move the pain report into a sheet or above the scroll; pain-context empty-swap copy "Stop it for today" | Pain flow readable on iPhone 17/SE; no clipped set rows | S–M |
| R-10 | P1 | Readiness/recovery 65/70 shown before any check-in, labelled Apple Health | Baseline defaults rendered as scores | Show "Check in to see readiness" with no number until data exists | No score without input | S |
| R-11 | P1 | You page fails when the S3 bucket setting is absent; infrastructure copy shown to users | Photo vault eager construction; dev/ops copy in catalog | Lazy/optional photo vault; remove infra wording | You loads without S3; copy user-facing | S |
| R-12 | P2 | Styling: system-blue checkboxes (wellness, set rows), duplicate Notes, two primary CTAs on Train, destructive action style, Quick Log single chip, tab bar under sheets, stale acknowledgment error, "Order 1" ordinal | Various XAML | Converge on role tokens and DESIGN rules | Styling re-audit | M |

Sequence update: R-1, R-2, R-3 before any further simulator pass; then R-4…R-11; then rerun the full 21-row matrix (including J06, J07, J08, J15, CR01 ×20, iPhone SE, VoiceOver, Reduce Motion), which this run did not finish.

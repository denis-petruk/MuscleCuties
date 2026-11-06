# UX02 branch handoff — activity and session lifecycle

**Package:** UX02-RUN + UX02-ACT (Product A, mobile workout & session lifecycle)
**Branch:** `product/ux02-activity-and-session`
**Base:** `origin/main` @ `d6b6cfe6`
**Head:** this document is committed above `d9b9f02d`; read `git rev-parse HEAD` for the exact tip.
**State:** READY FOR REVIEW. Branch pushed for review. No PR opened, no deploy, no database applied, no release action.

The governing scope is [IMPLEMENTATION_PLAN.md](IMPLEMENTATION_PLAN.md#ux02-run--running-save-and-abandon) and the [approved Beta workout policy](WORKOUT_POLICY.md). Stage records for the earlier proposal revisions live in [HANDOFF.md](HANDOFF.md); this file is the branch-scoped checkpoint.

## Commits on the branch

| Commit | Contents |
| --- | --- |
| `ec7b0fbd` | Timed finish with effort actuals and clean teardown: pause/resume/finish, frozen completion identity, abandon reconciliation, rest-day start guard, wrapping schedule chips, EN/RU timed copy. |
| `a271439a` | Optional session distance end to end: additive nullable `004_session_distance.sql`, finish contract/validation/idempotency hash, store and read-back, cardio-only input surfaced in the summary. |
| `d9b9f02d` | History detail shows the recorded distance, distance input length bounded; activity summary split into mutually exclusive strength and timed metric rows (the branch had put the PR and duration cards in the same column). |

## What the code does now

- **One timing authority.** `TimedActivityProgress.Describe` derives warm-up / main / cool-down, remaining time and progress from confirmed server active time plus the activity's own prescription. A section without planned movement rows runs the timed lifecycle; a section with movement rows keeps the exercise flow so distance, pace, heart-rate and power actuals are not dropped. Rest days never start.
- **Lifecycle.** `WorkoutSessionViewModel` shows the running surface on successful start/resume, pauses through the existing checkpoint route, resumes without creating a second session, and finishes through `FinishWorkoutSessionCommand` with duration + optional RPE (1–10) + optional distance (0–500 km). A failed finish keeps the draft, the confirmed time and the frozen body for a safe retry; resume is blocked past a pending completion.
- **Abandon.** Explicit confirmation, single-flight, reconciles an uncertain response before re-sending, and clears display timer, checkpoint loop, rest timer, draft and busy/overlay state before one bounded return to WorkoutPage.
- **Activities.** Walking, Cycling, Running, Swimming, Rowing, Elliptical, HIIT, Rock climbing, Yoga, Mobility and Gentle recovery all reach the shared timed surface; duration-only and prescription-free activities report elapsed time without inventing phases. Pinned by `TimedActivityProgressTests` (routing table per activity type plus phase/countdown shapes).
- **Localization.** All keys used by the Workout pages and session view model (274 checked) exist in both catalogs. `LocalizationCatalogTests` enforces XAML key existence, code-literal existence, EN/RU key parity and format-argument parity.

## Verification actually run on this branch

| Command | Result |
| --- | --- |
| `dotnet test tests/MuscleCuties.Core.Tests/` | 1219 passed, 0 failed |
| `dotnet test tests/MuscleCuties.Mobile.Contracts.Tests/` | 119 passed, 0 failed |
| `dotnet test tests/MuscleCuties.Registration.Tests/` | 44 passed, 0 failed (includes `SchemaMigrationsContractTests` for migration 004) |
| `dotnet build src/MuscleCuties.UI -f net10.0-ios -p:EnableCodeSigning=false` | succeeded, 0 warnings, 0 errors (re-run after the summary XAML fix) |
| Key parity audit `loc:LocalizedText` + `UiTextCatalog.Get/Format` vs both catalogs | 274 used keys, 0 missing in EN, 0 missing in RU |

## Post-handoff correction

An audit of the inherited tip found one lifecycle defect. `_appSuspended` was set by
`SuspendForAppBackgroundAsync` and only cleared by `StartActivityAsync`, and no foreground-resume event
exists. After backgrounding a running timed activity, **Resume** reached `if (_appSuspended) return;` in
`ResumeTimedActivityAsync` while `ResumeTimingAsync` also refused to restart the local clock, so the
paused surface kept a Resume button that could never recover; only Finish or Abandon escaped it.
`ResumeTimedActivityAsync` now clears the flag on the explicit user action, before the resume call, so a
suspension that happens during the call still wins. The ViewModel has no unit-test harness in
`tests/`, so this path is verified by build and the suites below, not by an automated regression test.

## Not verified, and why

- **No simulator or device walkthrough on this branch.** Per the repository contract, MAUI run and screenshots need task-specific owner authorization; none was given for this branch. Start, pause/resume, background/foreground, finish summary, cancel/confirm abandon, ambiguous response, HIIT gating, multi-activity day, EN/RU visuals, light/dark, large text and VoiceOver remain unobserved.
- **No live API or database.** Migration 004 was never applied; only its SQL and contract shape are checked. The finish route bounds were verified by reading `WorkoutActiveSessionUseCase` (RPE 1–10, distance 0–500 km) and by contract tests, not by a live request.
- **`MuscleCuties.Server.Tests` does not complete.** The test host stalls at discovery and produces no results; it was stopped after several minutes. Treat that suite as unrun, not green.
- **No indoor/outdoor cycling split exists in the domain.** "Outdoor cycling" therefore uses the shared cardio timed surface with optional distance; no new modality or metric field was invented.
- **No PR.** Integration into protected `main` is still the next owner-authorized step.

## Reservations and shared surfaces

`WorkoutSessionPage.xaml(.cs)`, `WorkoutSessionViewModel.cs`, `TimedActivityProgress.cs`, both `*CycleWorkout.cs` catalogs, `WorkoutPage.xaml`, `ScheduleDayItem.cs` and `WorkoutHistoryPage.xaml`/`WorkoutHistoryViewModel.cs` were touched by this package. RM-SESSION and RM-NOTICE also own the session page and overlay, so sequence them behind this branch or carry the reservation explicitly.

---

## Follow-up prompt for the next agent

Paste this into a new session that works in `/Users/denispetruk/RiderProjects/MuscleCuties`.

```text
Role: Product A mobile workout & session lifecycle engineer (continuation of UX02).
Source of truth: GitHub denis-petruk/MuscleCuties, branch
product/ux02-activity-and-session (base origin/main @ d6b6cfe6, tip after the UX02
handoff docs commit). Read AGENTS.md, then
docs/release-audit/2026-11-05/UX02_HANDOFF.md, IMPLEMENTATION_PLAN.md
(UX02-RUN, UX02-ACT, QA00) and WORKOUT_POLICY.md before editing.

State you inherit: UX02-RUN and UX02-ACT are implemented and committed on the branch.
Verified locally on that tip: Core tests 1219/1219, Mobile.Contracts 119/119,
Registration.Tests 44/44, iOS simulator build 0 warnings/0 errors, and full EN/RU key
parity for the Workout surfaces. Not verified: any simulator/device tap-through, any
live API or database exercise, and MuscleCuties.Server.Tests (the host hangs at
discovery and yields no results).

Finish the remaining job in this order, stopping at any gate you are not authorized for:

1. Inspect the branch and confirm the tip matches this handoff (git status clean,
   git log --oneline d6b6cfe6..HEAD). Do not reset, rebase or force-push. If an owner
   has authorized integration, open one reviewable PR into protected main describing
   UX02-RUN and UX02-ACT; never push to main directly.

2. Diagnose why tests/MuscleCuties.Server.Tests/ stalls at test discovery with no
   output (it never prints a result summary). Reproduce with `dotnet test
   tests/MuscleCuties.Server.Tests/ --logger "console;verbosity=normal"`, identify the
   fixture, static initializer or network dependency that blocks discovery, fix the
   cause rather than skipping tests, and report the suite's real pass/fail count. Do
   not kill processes by name pattern; stop only PIDs you started.

3. Get QA00 runtime evidence on the exact branch binary with synthetic data only:
   start a timed cardio activity, pause, resume, background/foreground, finish, then
   read the session back in history with its duration, RPE and optional distance;
   repeat for one interval activity (HIIT, including its history/safety gate) and one
   duration-only recovery activity (Mobility); exercise abandon cancel, abandon
   confirm, and an ambiguous/lost finish with retry; confirm a Rest day offers no start
   and that a multi-session day renders its chips without clipping. Check both locales,
   light/dark, large text and VoiceOver. MAUI run/screenshot work requires
   task-specific owner authorization: if it is not granted for this branch, record the
   gate as blocked instead of substituting local reasoning.

4. Only if a disposable or otherwise authorized database is available, apply migration
   004_session_distance.sql through the reviewed migration workflow, prove an older
   client payload without distanceKm still finishes, and confirm a recorded distance
   reads back. Never run dbinit to repair drift, never use Beta data, and never apply
   this to a shared environment without explicit owner authorization.

5. Update docs/release-audit/2026-11-05/UX02_HANDOFF.md with a new stage record: source
   SHA/tree, commands and real results, environment/fixture identity, screenshots,
   reviewer and owner approval status, and the exact remaining gate. Keep STATUS.md and
   ROADMAP.md untouched (canonical ownership sits with the integration authority). Do
   not claim behavior you did not observe, and do not describe a plan as verified.

Constraints: work only inside the repository; no production data, credentials in files,
destructive cleanup, or unapproved remote/cloud mutations. Preserve unrelated working
tree changes. Commit with conventional messages on the existing branch.
```

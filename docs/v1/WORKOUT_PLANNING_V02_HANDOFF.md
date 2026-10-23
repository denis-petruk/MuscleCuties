# Workout planning v0.2 — implementation handoff

Source: owner-provided **MC-PLANNING-0.2**, dated 2026-10-23. This file is the
repository handoff for another engineer. The original product document was
attached to the Codex chat and is not part of this repository.

## Product and release constraints

- The owner says to continue applying the plan and identifies the privacy-policy
  page as the place for approval documents. The repository's current
  `services/static/privacy.html` is labeled a publication draft and contains no
  workout-policy sign-off. The supplied **MC-REVIEW-0.2** also says no
  section-level formula approval is documented. Keep score display and
  recommendations disabled until the exact reviewed policy values and scope
  can be verified; do not invent reviewer identity or policy values.
- Implement C#/.NET 10 with Core domain rules, UseCases DTOs, PostgreSQL
  persistence, server endpoints, and MAUI iOS views. The repository's AGENTS.md
  prohibits SQLite in the client, despite the source document's proposed SQLite
  map. No health data may be sent to external AI or third-party services.
- Existing accepted work and completed sessions are authoritative. A proposal
  never mutates them until explicit acceptance and a successful revision compare.

## Implemented foundation in this batch

| Rule | Current behavior |
| --- | --- |
| `ExplicitSessionDurationRule` | Straight-set, round-block, timed-session, and daily totals with explicit rests, transitions, unilateral coverage, and unavailable results for incomplete timing. |
| `TodayCheckInScoreRule` | Validates the four 1–5 inputs, time zone and current local date; computes the exact raw prototype formula and bands only when the display flag and documented section approval are supplied. |
| `DailyActivityCapacityRule` | Counts distinct planned sessions, checks time windows and capacity, and returns demand-overlap review reasons without fabricating a fatigue score. |
| `ExerciseEligibilityV02Rule` | Requires reviewed catalog metadata, applies explicit restrictions/equipment/skill/exclusions, and checks reviewed substitution equivalence. |
| `PlanRevisionV02Rule` | Validates proposals, preserves completed work, freezes accepted snapshots, rejects stale revisions, and requires exact training-policy review for prescriptions. The database compare/write transaction remains to be built. |
| `TodayPlanningDecisionRule` | Gives restrictions and current symptoms precedence; a high score never increases work; lower-score suggestions require their own flag and section approval. |
| `ProgressionReviewV02Rule` | Requires a matching approved policy and consecutive complete same-exercise history; current limitations, pain, substitutions, and incomplete work prevent eligibility. It returns review/confirmation state and never changes a prescription. |

The planning score, capacity, eligibility, revision, Today, and progression
rules remain unwired to live plans. Check-in capture is now wired through MAUI,
the authenticated API, and PostgreSQL; the legacy generator remains active.
Core suite: 265 passed. Server build: zero warnings/errors. iOS simulator build:
zero warnings/errors on 2026-10-23.

The check-in slice captures optional 1–5 sleep-quality, soreness, and stress
values plus the device time-zone ID through the authenticated API, stores server
recording time and a revision number in PostgreSQL, and exposes the fields in
the localized MAUI check-in form. Partial input remains allowed and is
associated with the device time zone; this slice does not calculate or display
the v0.2 prototype score yet. Migration:
`20261023120000_AddWorkoutPlanningCheckInInputs`.

## Remaining implementation, in dependency order

1. **Versioned programming policy and review record.** Define server-owned
   `TrainingPolicy` and trusted `ReviewApproval` storage with exact ruleset,
   policy version, section IDs, reviewer evidence, exclusions, and effective
   dates. Sets, reps, effort, rest, volume, loading increments, and progression
   eligibility must come from that policy. Missing or unapproved values return
   `NeedsProgrammingReview`; no hidden fallback. Never accept a client-provided
   approval object as authority.
2. **Generator correction.** Refactor `GenerateWorkoutPlanUseCase` and
   `FourWeekPlanRule` to validate inputs and catalog completeness; use actual
   available days/minutes and outside/completed activities; allow a limited
   one-day result; select only reviewed templates; filter exercises before
   ranking; show compromises and assumptions. Separate strength-day count from
   total activity-day count. The current use case defaults to three days, 60
   minutes and 8,000 steps; the current Core generator rotates phases and uses
   neutral invented inputs. Remove unsupported fallback decisions as part of
   this migration. Historical cycle context must not automatically alter the
   prescription or fill today's check-in.
3. **Feasibility and timing.** Attach explicit duration data and provenance to
   planned blocks. Recalculate after edits. Sum multiple distinct sessions once;
   detect time collisions and overlap tags (for example climbing with grip or
   pulling work). Ask for review on unapproved workload overlap; do not infer a
   universal safe spacing threshold. Keep planned and actual duration separate.
4. **Progression and missed sessions.** Wire `ProgressionReviewV02Rule` to
   server-owned, reviewed policy and actual performance records. Compute
   qualifying target evidence from that policy, not client-supplied booleans.
   Define an explicit reviewed progression change, then submit it through the
   proposal/acceptance flow; the Core rule alone changes no load or sets.
   Mark missed work skipped or
   explicitly reschedule after capacity review; never stack catch-up workouts
   or compensate for eating.
5. **Persistence and API.** Add PostgreSQL plan/proposal/revision records and
   migrations through `PostgresDbContext`. Store input snapshot IDs, ruleset and
   policy versions, reason codes, assumptions, proposal/accepted IDs and
   acceptance time. Compare expected revision and write the accepted revision
   in one transaction; stale proposals return conflict. Existing
   `PostgresWorkoutPlanAndSessionStore` auto-generates on load and directly
   mutates scheduled days; replace that flow deliberately. Provide UseCases
   DTOs/ports and server endpoints for preview, accept, reject, skip, and
   explicit reschedule. Preserve completed work. Avoid logging raw symptoms.
6. **MAUI iOS workflow.** Add check-in capture/editing with time-zone-aware
   freshness; display session list, explicit estimates/assumptions, phase
   context, restrictions, and proposal before/after. Accept and reject are
   explicit. Treat XAML-bound UI models as runtime contracts and check all
   XAML before renaming properties or commands. Keep local caching under the
   encrypted `ILocalCacheService`; do not introduce SQLite or Preferences.
7. **Release controls and verification.** Default unapproved recommendation
   flags OFF: score display, score-based suggestions, automated progression,
   and injury-specific substitutions. Add API/use-case tests without database
   or UI boot and persistence tests for atomic stale conflict and completed
   work preservation. Test formula endpoints, local-date freshness, symptom
   precedence, phase independence, one-day capacity, multiple same-day
   sessions, demand overlap, missing policy, duplicate imports, timing
   unavailable, and rejection invariants. Run `dotnet test` and an iOS build;
   no Android, Mac Catalyst, or Windows builds unless requested.

## Exact proposed check-in formula

`Q = 100 * ((sleep - 1) + (energy - 1) + (5 - soreness) + (5 - stress)) / 16`.
Use raw `Q` for bands: `>=75` feeling ready, `>=50` consider lighter, otherwise
consider easier. This is a product heuristic, not a validated recovery measure.
Missing, fractional, out-of-range, wrong-time-zone, or stale input yields no
score. A high score never overrides restrictions or current symptoms and never
triggers progression.

## First files to inspect

- `src/MuscleCuties.Core/Domain/Workout/Planning/` and
  `src/MuscleCuties.Core/Domain/Workout/ExplicitSessionDurationRule.cs`
- `src/MuscleCuties.UseCases/Features/Workout/Planning/GenerateWorkoutPlanUseCase.cs`
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Session/PostgresWorkoutPlanAndSessionStore.cs`
- `services/MuscleCuties.Server/Workout/` and `src/MuscleCuties.UI/`
- `tests/MuscleCuties.Core.Tests/`

The owner's private GitLab project is
`git@gitlab.com:denis-petruk/musclecuties-testflight-beta.git`; the destination
branch is `test-flight-beta`. GitHub remains the separate `origin` remote.
The owner's existing Mac SSH public key was registered for authentication-only
GitLab access with a one-year expiration. The branch was pushed and its remote
head verified on 2026-10-23.

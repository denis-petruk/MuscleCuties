# Workout planning v0.2 — implementation handoff

**Continuation update — 2026-10-23:** The owner reports MC-WORKOUT-POLICY 0.1.0 approved; reviewer details and approval date were not supplied. Product-owner authorization separately enabled the v0.2 strength preview. Check-in score/recommendations, automated progression, injury-specific substitutions, and symptom triage retain separate gates. Legacy automatic generation remains blocked. The local PostgreSQL schema, canonical seed, and reviewed-catalog import were verified against the disposable local database; no deployed database was contacted. The Train no-plan state now explains missing activity prescription configuration and confirms preferences remain saved. New plan proposal/revision persistence and matching MAUI preview/acceptance UI remain incomplete. Core 342/342, Registration 34/34, Server 77/77, Mobile Contracts 15/15, and the iOS simulator build (0 warnings, 0 errors) pass. The Core suite verifies every supported activity identity survives preference persistence. Lambda deployment is deferred.

Source: owner-provided **MC-WORKOUT-POLICY 0.2.0** instructions and follow-up
template-mapping requirements, dated 2026-10-23. The original product document
was attached to the Codex chat and is not part of this repository.

## Product and release constraints

- Training policy ID: **MC-WORKOUT-POLICY**. Target version: **0.2.0**. The owner
  supplied numerical prescriptions, activity eligibility, readiness bands,
  progression/regression conditions, and hard-day limits for this task. Historical
  0.1.0 approval is reported by the owner; reviewer details and approval date
  remain unspecified and are not fabricated here.
- The readiness score is an authorized planning estimate, not a medical assessment
  or empirical recovery measure. Symptoms and restrictions take precedence. No
  clinical suitability, rehabilitation, or clearance is inferred from exercise
  names or missing data.
- Implement C#/.NET 10 with Core domain rules, UseCases DTOs, PostgreSQL
  persistence, server endpoints, and MAUI iOS views. The repository's AGENTS.md
  prohibits SQLite in the client, despite the source document's proposed SQLite
  map. No health data may be sent to external AI or third-party services.
- Existing accepted work and completed sessions are authoritative. A proposal
  never mutates them until explicit acceptance and a successful revision compare.

## Implemented foundation in this batch

### Approved catalog review integration added 2026-10-23

The supplied workbook is represented by `ReviewedExerciseCatalogV020.json`, with
independently identified policy, catalog snapshot, exercise review, template, and
directed substitution versions. Its 59 source IDs are mapped to existing catalog
records by stable seed code; no contiguous-ID assumption is made. The import
command compares identity, movement, equipment, timing, and contraindication
fields with PostgreSQL and blocks changed or missing records. The source-only
dry run matched 59 records, 160 directed pairs, and 52 template slots. The
deployed database has not been checked and no migration/import was applied.

The owner directly confirmed that the restriction assessment for all 59
exercises was completed and cleared for injury restriction matching. Each
record stores `Assessed` with the direct-instruction source. Existing
contraindication flags remain authoritative and block matching exercises.
Reviewer attribution and credentials were confirmed by the owner through direct
instruction in this chat; no separate external credential audit is claimed.

The reviewed catalog mapper distinguishes assessed-empty demand tags from
unknown tags, retains experience and timing conditions, and exposes directed
substitution pair decisions. Manual and rejected pairs cannot become automatic
equivalents. The authenticated strength preview filters by reviewed slot IDs,
equipment, exclusions, experience, conditions, and prior exercise records,
then applies v0.2 prescriptions, style limits, time trimming, and the hard-day
cap. High-intensity load eligibility requires both a recorded positive load and
user confirmation. Time feasibility requires explicitly supplied warm-up and
cooldown minutes; unilateral active time uses existing catalog metadata. The mapping is documented in
`WORKOUT_STRENGTH_REVIEWED_MAPPING_V020.md`.

This is still a preview path. The active v0.1 generator and registration/profile
regeneration paths have not been replaced, no proposal acceptance flow persists
these previews, and MAUI does not yet present them. The current machine has only
.NET SDK 9, so the new .NET 10 code and tests have not been executed here.

### Structured proposal foundation added 2026-10-23

`StructuredWorkoutPlanV02` now models a week as seven plan days, each with zero or
more independently categorized sessions and explicit blocks. Straight sets,
round-based supersets/circuits, timed catalog activities, work/recovery intervals,
distance sets, and ordered movement/pose blocks have separate representations. Swimming distance remains without
a duration until a pace is supplied. `StructuredWorkoutDurationV02Rule` returns
planned session totals separately from completed actual time, counts distinct
outside activity toward occupied daily time, and exposes timing assumptions.

`StructuredWorkoutProposalV02Rule` deterministically selects only a template
covered by a matching policy version and training-prescription review record.
It rejects missing/unapproved policy, unknown timing, unavailable exercises,
explicit restrictions, capacity failures, and outside demand overlap with
structured reasons. It places sessions into separate available windows when
provided, without counting the hours between them, and generates no extra hard
sessions just because all seven days are available. The Core suite passed
287/287 and the iOS simulator build passed with zero warnings/errors after
this foundation was added. These types are not connected to the active API, PostgreSQL plan store,
or MAUI screens. The latest implementation adds activity-specific v0.2 rules,
progression rules, versioned strength slots, a deterministic slot resolver, and
nullable reviewed catalog fields. Product-owner-authorized strength preview is
enabled independently of score display, readiness suggestions, progression, and
injury-specific substitutions. `WORKOUT_STRENGTH_TEMPLATE_MAPPING_V020.md`
records candidate IDs, equipment, policy prescriptions, and missing review
metadata. Existing catalog rows are intentionally not marked reviewed. The
active API generator and atomic proposal/revision persistence still require
integration; these pure rules are not yet a completed user-facing workflow.
The owner specified that users choose available weekdays for strength sessions
when accepting a proposal. `StrengthWeekdayAssignmentRuleV020` validates one
distinct chosen available weekday per session and maps proposal session order to
Monday-through-Sunday order. The API acceptance flow does not yet call this rule.

The onboarding discomfort slider was checked in source: left is **Severe** and
right is **None**; `QuizViewModel` saves the semantic discomfort value using
`6 - visualStep` and applies the inverse when loading. This mapping was left
unchanged, so no stored scale is silently reinterpreted.

| Rule | Current behavior |
| --- | --- |
| `ExplicitSessionDurationRule` | Straight-set, round-block, timed-session, and daily totals with explicit rests, transitions, unilateral coverage, and unavailable results for incomplete timing. |
| `TodayCheckInScoreRule` | Validates the four 1–5 inputs, time zone and current local date; computes the supplied raw score and bands. Missing, invalid, or stale values produce no score. |
| `DailyActivityCapacityRule` | Counts distinct planned sessions, checks time windows and capacity, and returns demand-overlap review reasons without fabricating a fatigue score. |
| `ExerciseEligibilityV02Rule` | Requires reviewed catalog metadata, applies explicit restrictions/equipment/skill/exclusions, and checks reviewed substitution equivalence. |
| `PlanRevisionV02Rule` | Validates proposals, preserves completed work, freezes accepted snapshots, and rejects stale revisions. The database compare/write transaction remains to be built. |
| `TodayPlanningDecisionRule` | Gives restrictions and current symptoms precedence; a high score never increases work; middle and low bands propose only the supplied optional-work choices. |
| `ProgressionReviewV02Rule` | Requires a matching approved policy and consecutive complete same-exercise history; current limitations, pain, substitutions, and incomplete work prevent eligibility. It returns review/confirmation state and never changes a prescription. |

The planning score, capacity, eligibility, revision, Today, progression,
strength-slot, and weekday-assignment rules are not yet wired into an end-to-end
persisted plan flow.
Check-in capture and score display are wired through MAUI, the authenticated API,
and PostgreSQL. As of the latest checkout on 2026-10-23, .NET SDK 10 is installed.
Core verification passes 342/342, Registration 34/34, Server 77/77, and Mobile
Contracts 15/15. The iOS simulator target builds with zero warnings and errors.
The built app was installed and launched on iPhone 17 Pro Max (iOS 26.3) by
directly installing the app bundle because the project Run target expects
unavailable iOS 26.2. Today and the check-in form were inspected without
submitting or changing account data. PostgreSQL integration-test variables were
unavailable; database-backed acceptance and deployed catalog metadata remain
unverified.

The bundled reviewed-catalog manifest checks pass 3/3. They compare the review
manifest against the canonical repository snapshot and verify changed or
missing rows block import; they do not establish that deployed PostgreSQL rows
match.

The check-in slice captures optional 1–5 sleep-quality, soreness, and stress
values plus the device time-zone ID through the authenticated API, stores server
recording time and a revision number in PostgreSQL, and exposes the fields and
score in the localized MAUI check-in form. Partial input remains allowed and is
associated with the device time zone. Migration:
`20261023120000_AddWorkoutPlanningCheckInInputs`.

## Remaining implementation, in dependency order

The owner's v0.1.0 coverage response requires separate, user-facing high
volume strength, strength high intensity, rock climbing, HIIT, cycling,
running, swimming, and yoga. General strength, walking, rowing, elliptical,
mobility, gentle recovery, rest days, and other activity must retain their own
identity. Preference selection and string persistence now retain these names;
the structured Core session model separates purpose, modality, and style.
Session cards, editing, completed-session logging, access and ability checks,
and live duration/overlap checks for every option remain to be wired.

The owner supplied concrete rules for strength, cardio, swimming, HIIT, climbing,
yoga, readiness, progression, and weekly limits. Use the values from
`WorkoutTrainingPolicyV020`; do not add further numerical rules. Activity-specific
history, reviewed catalog demand/restriction data, and timing support remain
incomplete in stored inputs and must produce explicit gaps.

1. **Versioned policy integration.** Use the supplied `WorkoutTrainingPolicyV020`
   values as authoritative configuration. Preserve historical versions and the
   owner-reported 0.1.0 approval without fabricating reviewer details or dates.
   Never accept a client-provided approval object as authority.
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

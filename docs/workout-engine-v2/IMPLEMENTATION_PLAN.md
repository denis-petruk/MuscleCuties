# MuscleCuties Workout Engine 2.0 — Implementation Plan

Status: owner-supplied recovery incorporated 2026-11-15. Product acceptance criteria below are the working specification; implementation and release are not implied. Repository: `denis-petruk/MuscleCuties`. Target: iOS .NET MAUI, C#/.NET API and backend, PostgreSQL, GitHub Actions and AWS.

## Provenance and first gate

This is the authoritative working plan, but its historical provenance is incomplete. The available referenced chat begins at the final handoff and does not expose the original design discussion or downloaded files. Gates 58–95 below were reconstructed in an earlier Work chat; their detailed original definitions and individual approvals were not independently recovered. Treat those rows as **unverified reconstructions**, not proven owner decisions. The owner supplied recovered individual definitions for gates 49–57 and grouped earlier decisions. Exact individual mappings within earlier ranges remain partly unknown; do not invent them. Current user instructions and directly evidenced decisions remain binding; unknown thresholds and disputed semantics require reconciliation before dependent implementation. No claim of full historical completeness or release readiness is justified.

**First:** on the actual checked-out GitHub branch, inspect repository instructions and working state, perform the P0 source and code audit, save and verify this plan against recoverable decisions, and commit the confirmed documentation as a focused change. Then continue implementation packages; do not require perfect historical numbering. Existing unrelated changes must remain untouched. This file's delivery does not itself authorize a reset or claim an implementation.

## Outcome and boundaries

Build one authoritative, deterministic server-side workout engine; connect actual user inputs, generation, acceptance, scheduling, execution, results and progression through MAUI, API, use cases and PostgreSQL. A failed API call must not create a local substitute plan. Maintain clean architecture, established routes/contracts and EN/RU localization unless a justified, compatible change is required. Do not send health information or secrets to external services or logs. Use synthetic test data.

A feasible first foreground plan applies automatically; replacement of an active plan uses the explicit Apply plan path after the user is told of the consequence. Generation or apply failures preserve the active plan. Withhold a strength session with no permitted strength block and explain why. One available day can produce a safe full-body plan. Missing safety inputs must never relax restrictions; report all required gaps with an action to edit setup. RIR is optional; DOMS alone does not alter dose. HIIT is deferred and excluded from new active generation and selection. Avoid inventing workout actuals or unknown starting loads.

Dev, Staging and Beta resets are within future scope only. Each destructive action requires a verified backup, demonstrated restore, exact resource preflight and separate explicit confirmation of the targets. Never touch Production. Old test-environment data need not be migrated after a separately authorized reset; until then preserve it. TestFlight and App Store Connect remain owner-only under current repository instructions.

## Requirement provenance and traceability register

**Evidence key:** H = owner-supplied recovered original decisions, 2026-11-15 (design discussions 2026-11-13–14); U = current owner request, 2026-11-15; C = referenced “Redesign Workout Planning” chat, turn `b8f00d3d-7837-449f-8fdd-e58d295129d0` (assistant summary, not original decisions); N = [MuscleCuties workout rules](https://app.notion.com/p/40f47be9496581ccb0a5f440dd8b1236), dated 2026-11-04, distinguishes preferences from research proposals; R = `AGENTS.md` and `docs/PRODUCT_WORKFLOWS.md` at baseline `a0d537b2fc5bbbc90fb76abd2e040c9c237a350c`; D = prior untracked reconstructed plan, whose gate details require source verification. U explicitly supersedes older incomplete deliverables. No status below means implemented or approved beyond the cited evidence. All entries begin **NOT VERIFIED**. During P0 replace candidate locations with exact symbols, tests, simulator scenarios and evidence links; change status only with observed results.

| ID | Requirement / acceptance intent | Evidence and uncertainty | Packages; candidate evidence and tests |
| --- | --- | --- | --- |
| MC-WE2-REQ-001 | Experience uses **20 and 130 distinct completed app-workout days**; same-day multiple sessions do not count as multiple days. Preserve real history; never manufacture completion. | H: Beginner → Intermediate after 20 distinct completed app-workout days; Intermediate → Advanced after 130. Calendar timezone, qualifying edge cases and promotion/demotion behavior still require implementation reconciliation; do not invent history. | P0/P2/P5; inspect `WorkoutExperiencePromotionRule` and `WorkoutExperiencePromotionStoreTests`; boundary, duplicate, timezone and retry cases after policy is resolved. |
| MC-WE2-REQ-002 | Support 1–7 selected days and a dynamic strength-first split based on availability, goals, recovery and feasibility. One day can yield a safe full-body plan; cardio-only days are valid. | U, N, R; exact split and infeasible-week tradeoffs unresolved. | P2/P3; generator and schedule tests for every frequency, consecutive days, one-day and constrained weeks. |
| MC-WE2-REQ-003 | Prioritize glutes only when selected, and account for direct weekly glute sets and whole-body weekly exposures. Aim for main muscle groups twice weekly, preferably three when feasible; disclose shortfalls and never fabricate coverage. | H, U, N; see recovered volume ranges and exposure criteria below; fractional indirect contributions remain an approximation. | P1/P2/P3; catalog and weekly allocation tests, conflicting priorities, constrained time/equipment. |
| MC-WE2-REQ-004 | Exercise catalog carries reviewed muscle, movement, equipment, contraindication, loading and timing metadata. Choose and replace only eligible, reviewed alternatives; support actual home equipment, bands, dumbbells and bodyweight. | U, N; no unverified catalog additions or assumed equivalence. | P1/P2/P6; `ExerciseEligibilityRuleTests`, `WorkoutCatalogCoverageTests`, replacement and equipment exclusion tests. |
| MC-WE2-REQ-005 | Progress from persisted, comparable actual working sets without requiring RIR; RIR may be recorded. No invented actuals, initial weight or forced increases. | H and U override R's omission of sessions missing RIR. Two qualifying exposures with all prescribed sets meeting the target are recovered; precise reduction details in D remain unverified. | P4/P5/P6; progression tests with and without RIR, missing observations, bodyweight and persistence. Update workflow documentation when behavior changes. |
| MC-WE2-REQ-006 | Soreness/DOMS alone must not change dose; readiness, cycle symptoms, injury and red flags receive separate safe handling. Missing safety information never relaxes restrictions. | U; N distinguishes phase from symptoms; R's 2026-11-09/10 readiness policy says remove an optional set and cycle estimate alone changes nothing. Exact red flags and return rules not recovered. `ReadinessRule.cs` currently includes soreness in its score; audit the live downstream path before claiming impact. | P0/P2/P4/P6; pairwise DOMS invariance, red-flag stop/referral, missing/stale inputs, pain exclusion, cycle and current eligibility tests. Clinical rules require validated source and owner decision. |
| MC-WE2-REQ-007 | Fit weekly volume, realistic session time, rest, transitions and safe supersets; include warmups separate from working sets. Explain safe compromises. | U; thresholds, pairing and timing parameters unresolved; D has reconstructed detail. | P2/P3; duration accounting and impossible-budget/partial-plan tests. |
| MC-WE2-REQ-008 | Support actual set logging, completion and subsequent progression, including paused or partial sessions, with idempotent synchronization and no fabricated data. | U; D adds unverified detailed gate interpretations. | P4/P5/P6; session lifecycle and PostgreSQL retry/concurrency tests; actual end-to-end UI. |
| MC-WE2-REQ-009 | Strength precedes cardio when both occur; support 1–2 independent sessions per day and a strength-session finisher; cardio-only is allowed; HIIT is deferred. | H, U, N; easy compatible post-strength finisher up to 30 minutes; intensity classification must be conservative. | P2/P3/P6; ordered multi-session, cardio-only, finisher and HIIT exclusion tests. |
| MC-WE2-REQ-010 | One deterministic canonical generator; identical normalized inputs and policy version produce identical prescription content. Show preview and apply according to repository lifecycle; safe partial plans disclose compromises. | U, R; first feasible foreground plan auto-applies; replacement uses Apply after consequence notice. No eligible strength block means withhold it. | P2/P5/P6; property, API retry, concurrent generation/apply and active-plan preservation tests. |
| MC-WE2-REQ-011 | Implement the individually recovered gates 49–57 in the table below. | H: owner-supplied recovery, with individual mapping and acceptance intent. | P1–P3; coverage and ordering tests for each gate and feasible/infeasible scenarios. |
| MC-WE2-REQ-012 | Gates 58–95: preserve every existing reconstructed row below as an investigation and acceptance checklist, not verified historical exact text. | D; only general topics and reset boundary are independently supported by U; some exact thresholds and technical semantics require original evidence. | P0–P9; attach per-row source, exact code, tests, simulator evidence and status before closing. |
| MC-WE2-REQ-013 | Preserve current records and access boundaries; reset only individually confirmed Development, Staging or Beta resources after verified backup and restore; never Production. TestFlight/App Store Connect owner-only. | U, R. Confirmation must be fresh and per target; an existing generic authorization does not suffice. | P5/P8/P9; isolated restore drill, exact resource inventory, confirmation, post-deploy checks and owner acceptance. |

### Recovered decisions and acceptance criteria

The following owner-supplied recovery is current product direction. Number ranges reflect the source and are not individual mappings. Requirements are verified only after the tests and shipped paths below have evidence. All entries: **NOT VERIFIED**.

| ID | Requirement and acceptance criterion | Provenance; package and verification |
| --- | --- | --- |
| MC-WE2-REQ-014 | Scope adults 18–64; pregnancy/postpartum programming is outside approved scope; do not falsely imply supported programming. | H; P0/P2/P6: population and safe refusal tests. |
| MC-WE2-REQ-015 | Starting direct glute sets/week: beginner/returning 6–8, intermediate 8–12, experienced 10–16; actual history overrides labels. Volume and effort are independent. For glute-selected programs target two direct meaningful glute days where feasible; report compromises. | H decisions 3–4 and unnumbered; P2/P3: range, experience, recovery, direct-vs-indirect and feasibility scenarios. |
| MC-WE2-REQ-016 | Prioritize reps/load before more sets. After approximately 3–4 weeks genuine stagnation with consistent training and adequate recovery consider one additional direct glute set, then reassess. Double progression requires two qualifying exposures with all prescribed working sets at target reps; RIR and technique are optional. | H decisions 5–9; P4/P5: qualifying/nonqualifying exposures, missing RIR, incomplete sets, stagnation/recovery and no forced increase. |
| MC-WE2-REQ-017 | General Strength targets approximately 3 RIR, High-Volume approximately 3–4 RIR; High-Intensity starts at 1 RIR and two working sets per exercise, compound 5–8 reps and 120–150 seconds rest, isolation 8–12 and 90–120 seconds. These are prescriptions, not mandatory logged inputs; safety overrides. | H decisions 10–14; P2/P4: style, exercise-type, rest and optional-logging tests. |
| MC-WE2-REQ-018 | Dynamic exercise count, no six-exercise cap; prioritize uncovered groups before goal-specific additions. A meaningful exposure generally requires at least two effective working sets in a day. Separate direct/indirect contribution; target glutes, quads, hamstrings and back twice weekly where feasible. Prefer approximately 48 hours between demanding same-muscle work without a rigid ban; redistribute before cutting stimulus. | H decisions 15–23; P2/P3: 1–7 days, successive days, exposure counting, partial plan. |
| MC-WE2-REQ-019 | Preserve meaningful priority sets in short sessions. One-set accessories are reserved for approved recovery adaptation rather than the normal standard. New local pain excludes provoking exercise and supports safe substitution; systemic red flags stop training and call for appropriate help. Reassessment before return, with 10–15% lower initial loading for first two sessions discussed as a product heuristic, not medical clearance or a universal rule. | H decisions 24–28; P2/P4/P6: pain exclusion, systemic stop, withheld alternatives, conservative return; exact symptom classification requires reconciliation. |
| MC-WE2-REQ-020 | Strength is schedule foundation; use only selected activities. Consider intensity, duration, experience and recovery. Beginners generally start with one main daily session, two only if feasible. Start with two strength days, three where feasible; one day yields full body or best safe partial. Compatible easy cardio finisher is at most 30 minutes inside strength; no more than two independent sessions by default. | H decisions 29–34 and unnumbered; P2/P3/P6: selected activities, ordering, limits, finisher and safe partial scenarios. |
| MC-WE2-REQ-021 | Generate compatible supersets where safe and time-saving; temporary equipment occupancy supports reorder or temporary replacement; permanent unavailability is persisted and reflected in future plans. Preserve distinct exercise progression histories, retain successful exercises and replace for pain, equipment, changed goal, deficient coverage or sustained lack of progress. | H decisions 40–45; P1/P3/P4/P5/P6: rest, occupancy, durable preference, history separation and stable choice. |
| MC-WE2-REQ-022 | Machine-first selection when appropriate, with controlled ROM and measurable progression; eligibility and user equipment take priority. Across two weeks aim for horizontal press, incline press and fly; overhead press, lateral raise and direct rear delts; vertical/horizontal pulls and about 3–4 suitable back variants when feasible. Include at least one direct biceps and triceps exercise weekly. | H unnumbered; P1/P2/P3: reviewed equipment, coverage, two-week diversity and infeasible disclosure. |
| MC-WE2-REQ-023 | Four cycle phases are supported; a prediction alone never changes load. Optional low-burden check-in; automatic readiness primarily reduces optional volume, not working weight. Ordinary DOMS changes neither sets, reps, weight, RIR nor selection. Missing readings remain unknown. | H unnumbered; P4/P6: phase-only and DOMS invariance, skipped check-in, no fabricated data and safe adaptations. |
| MC-WE2-REQ-024 | First feasible foreground plan, including safe ReducedVolume, automatically applies; replacement requires informed Apply Plan. Preserve active plan on failure. HIIT is deferred from new active planning but preserve historical data. Actual sets, unilateral basis, offline recovery and idempotent sync remain intact. | H, U, R; P3/P5/P6: first-plan/replace/race/failure, historical HIIT, set persistence and retry tests. |

### Recovered gates 49–57

| Gate | Acceptance criterion | Package and verification |
| --- | --- | --- |
| 49 | Cover knee extension, knee flexion and hip extension within a week when feasible; disclose gaps. | P1–P3: pattern and constrained-catalog cases. |
| 50 | At least two direct calf working sets per week when feasible; standing calf raises preferred if eligible. | P1–P3: direct counting and eligible alternative. |
| 51 | Include direct rear-delt work weekly when feasible. | P1–P3: direct coverage. |
| 52 | Dynamic core by default; static core optional. | P1–P3: selection tests. |
| 53 | Do not automatically add neck training. | P2: default exclusion. |
| 54 | No mandatory separate lower-back exercise if other patterns provide adequate work. | P1/P2: coverage and no forced addition. |
| 55 | Direct adductor work when specifically needed, not an unconditional weekly requirement. | P1/P2: conditional selection. |
| 56 | Direct hip abduction weekly in glute-focused programs when feasible. | P1–P3: selected goal and feasible alternative. |
| 57 | Exercise order is dynamic; Hip Thrust is not invariably first in glute-focused sessions. | P2/P3: competing priorities and stable deterministic ordering. |

### P0 reconciliation and implementation controls

1. Owner's recovered grouped decisions and numbered 49–57 resolve earlier gaps. Do not block clearly specified implementation on missing historical numbering. Preserve unresolved detailed original 58–95 provenance rather than labeling reconstructed specifics as exact owner approvals.
2. Count 20/130 distinct completed app-workout days and promote at the stated thresholds. Audit timezone, historical qualifications, concurrency and downgrade rules; do not invent missing actual records.
3. Override older `docs/PRODUCT_WORKFLOWS.md` wording that omits no-RIR sessions from progression. Keep an optional prescribed effort target while actual RIR and technique are optional. Preserve safe plan-acceptance path in `AGENTS.md`.
4. Audit `ReadinessRule` and its downstream path for DOMS effects, and remove any dosing influence while retaining separate injury/red-flag safety. Reconcile pain return conservatively; the 10–15% heuristic is not clearance and the unverified gate 88 timer cannot independently clear restrictions.
5. Record requirement → source → exact code → failing test → implementation commit → affected suites → simulator scenario → independent review → status. Unknown evidence is NOT VERIFIED, never PASS. Investigate the existing 1,417-pass/one-failure baseline and preserve dirty files; do not silently change assertions.

### Current code discrepancies for P0

At baseline `a0d537b2fc5bbbc90fb76abd2e040c9c237a350c`, subject to the dirty worktree: `src/MuscleCuties.Core/Domain/Workout/Planning/ObservedSetProgression.cs` excludes sessions missing RIR, and `StrengthProgressionRule.cs` consumes non-null RIR; the persisted session path calls that builder in `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Session/PostgresWorkoutPlanAndSessionStore.cs`. Inspect how prescribed set count and target reps reach progression before claiming the two-exposure rule. `DeloadTriggerRule.cs` includes soreness in recovery and `WeeklyWorkoutPlanGeneratorRule.cs` uses deload in prescription; audit check-in/readiness and persisted recovery paths for other indirect DOMS effects. These are code inspection leads, not verified end-to-end test results.

The Core baseline on this worktree was 1,417 passed and one failed: `WorkoutSessionLifecycleAndModalTests.AnimatedModalView_hooks_unloaded_and_disappearing_with_idempotent_scope`. The existing dirty modal implementation uses an ordered list while the test expects a literal HashSet declaration. Preserve the pre-existing UI changes and investigate the assertion; this result is not a green baseline and does not establish a runtime modal defect.

### Work started — 2026-11-15

- Documentation recovery was committed at `623ebb41`; its review found and corrected two internal inconsistencies. This confirms the captured scope, not historical completeness or implementation.
- `95ced899` removes soreness from the deload recovery predicate. A test-first soreness-only case failed before the change; the focused deload and canonical-recovery run passed 16/16 afterwards. The existing canonical poor-recovery fixture now reports poor sleep in both weeks; its assertion remains intact. An independent review traced the production caller. This is **partial**: other readiness/score routes can still transmit soreness into dose decisions, so MC-WE2-REQ-006 and MC-WE2-REQ-023 remain NOT VERIFIED.
- Further audit: the PostgreSQL progression reader currently groups observed rows without confirming completed sessions and a complete immutable prescription, caps rows, and uses category rep ceilings. Merely making RIR nullable could recommend increases after partial work. `PlannedPrescriptionSnapshotJson` provides historical sets/reps for eligible planned sessions; implement completed-session and complete-prescription validation before enabling no-RIR progression. Preserve missing/ambiguous histories as ineligible, with no fabricated RIR or technique. This is an implementation finding, not a verified fix.
- Cycle-readiness dosing uses a soreness-sensitive score. A soreness-independent policy must preserve pain/red-flag checks and genuine fatigue signals; review/version its calibration and approval rather than silently reusing an older approval. No app-wide DOMS PASS is claimed.
- The full Core run after the scoped change returned 1,419 passed and one failure in the pre-existing modal source-shape assertion (before the additional canonical test was added). No simulator, database, cloud or release verification is implied. The pre-existing dirty UI changes remain outside this task's commits.

### In-progress changes — 2026-11-15

These changes are committed but are not release evidence. The checkout advanced while preserving the earlier documentation and deload commits.

- `630c3dd0` implements one-day schedule, saved preferences and preview for a Full Body proposal, with safety exclusions and disclosed coverage gaps. First feasible reduced-volume activation and replacement acknowledgment were adjusted; EN/RU informational notes were added. Proposal ruleset was changed to prevent stale unaccepted proposals using earlier scheduling semantics. One-day end-to-end simulator behavior and final independent review remain open.
- `ea07bda6` connects optional-RIR progression to completed snapshot-matched sessions, requiring every prescribed set and using each snapshot's rep target. Recent partial or unverified substituted sessions block an increase. The prior two-deficit reduction is retained only where actual RIR exists. An independent review and selected PostgreSQL tests covered the substitute barrier; broader progression and scenario coverage remain required.
- On this worktree, Core tests passed 1,456/1,456; Mobile.Contracts passed 200/200; a selected local PostgreSQL integration run passed 3/3; the iOS simulator build succeeded with zero warnings/errors. These are point-in-time results against the dirty checkout, not a clean committed SHA or visual/device validation. Older tests that required two days or forbade automatic first reduced-volume plans were updated to reflect the explicitly superseding recovered decisions while retaining replacement and safety checks.
- `630c3dd0` removes unconditional adductor shortfall/priority and adductor-only glute attribution. Explicit need-specific adductor selection remains open. The Gate 56 glute-specific abductor policy and meaningful exposure counting by effective sets also remain open.
- Other soreness-sensitive readiness paths and many P1–P9 requirements remain open. Independent review has resumed after a temporary workspace credit error; unresolved findings and the final simulator flow remain open. No shared-environment reset, AWS deployment, device acceptance or TestFlight action has been performed.

## Architecture and generation

- Core owns deterministic, versioned rules; UseCases coordinate operations; Infrastructure owns persistence and catalog; Server is authoritative; MAUI presents inputs and results. Respect actual dependency and platform boundaries.
- Validate profile, safety and equipment; establish goal/history and available time; allocate weekly strength and selected cardio; choose split and muscle/movement coverage; select verified eligible exercises; prescribe work, warm-up and appropriate rest; fit duration, including transitions and unilateral work; validate restrictions and feasibility; explain compromises. Equal inputs and rule versions must yield equal prescription content, independent of generated record IDs.
- Preserve plan revisions, actual sessions and sets, and progression history; use explicit versioning and transactional/idempotent writes. The exact schema and migration strategy follow the inspected system, not a prescribed table count. Keep baseline and temporary adjustments distinguishable.
- Prefer an explainable safe partial plan when not all preferences fit; disclose missing coverage. Never claim an empty strength session is a full plan. Do not claim a clinical benefit or universal numeric prescription without evidence and approved policy.

## Packages and exit criteria

| Package | Work | Exit evidence |
| --- | --- | --- |
| P0 Baseline and conflict audit | Record branch, SHA, dirty files, instructions, architecture, test and runtime baseline; map gate decisions to current code and tests. Inspect one-day planning, optional RIR, coverage and fixed templates, first-plan acceptance, HIIT, DOMS, pain return and completion. | Traceable discrepancy and ownership matrix; no unrelated edits. |
| P1 Catalog V2 | Review existing catalog, manifest, substitutions and actual data model. Validate equipment, movement, muscle contributions, limitations, unilateral/load basis, timing, progression and safe substitutions; add evidenced gym, dumbbell, band and bodyweight coverage. The historic 59-review versus 60-seed difference is an investigation lead, not a current fact. | Active entries and alternatives validated; missing coverage surfaced; catalog tests pass. |
| P2 Canonical planner | Implement versioned input validation, weekly volume and joint coverage, split selection and deterministic exercise selection. Retire conflicting execution paths safely. | Boundary and property/scenario tests demonstrate reproducibility and safe one-day planning. |
| P3 Scheduling and optimization | Fit strength, warm-ups, supersets and supported cardio into availability and realistic duration; order strength before cardio when combined, preserve cardio-only days. | Time and scheduling cases, infeasible and partial plans verified. |
| P4 Progression and recovery | Use completed comparable history, optional RIR, plateau, deload, check-in, pain restrictions and cycle preferences as approved; preserve baseline and explain changes. | Progression, no-RIR, safety and recovery scenarios pass; no unsupported thresholds. |
| P5 Persistence and API | Implement compatible contracts, plan revisions, transactional acceptance, history, concurrency controls, idempotent actuals and safe PostgreSQL migrations. | API, authorization, race, migration and disposable PostgreSQL integration tests pass. |
| P6 MAUI experience | Wire setup, missing fields, plan preview/Apply, Day/All, session execution, activity metrics, history and progression to real APIs. Use shared design system, localization and accessibility; apply Impeccable if installed. | Observable end-to-end iOS flow, error/retry, keyboard, accessibility and EN/RU checks. |
| P7 Regression and simulator QA | Execute unit, affected suites, contracts, integration, database and iOS simulator checks on the exact candidate. | Recorded command output, SHA, simulator build and results; independent review closes blockers. |
| P8 Environment and deployment | Prepare GitHub Actions and AWS deployment, environment inventory, backups, restore drill and exact-target preflight. Only after separate confirmation execute a scoped nonproduction reset where required. | Per-environment evidence, exact artifact and deployment identity, health checks and rollback; no Production access or changes. |
| P9 Beta readiness | Resolve blockers, review end-to-end evidence, release configuration, CI and owner acceptance. | Explicit go/no-go with outstanding items and owner simulator/physical-device and distribution gates distinguished. |

## Gates 58–95: acceptance matrix

These are future checks, not current PASS claims. The rows are inherited from the previous reconstructed document, not independently verified against the missing original gate discussion. Recover source evidence or obtain owner decisions before implementing details that depend on an unverified row. Gate 95 is independently constrained by the present request.

| Gate | Required acceptance behavior |
| --- | --- |
| 58 Weekly volume | Targets reflect goal, history, tolerance and availability; muscle maxima are not mechanically added. |
| 59 Metadata | Every selected exercise has sufficiently reviewed structured metadata. |
| 60 Home alternatives | Bodyweight, dumbbells, bands and full-gym options are checked; no fictional equivalences. |
| 61 Volume optimization | Separate direct and indirect contributions and allocate weekly load jointly. |
| 62 Scenario matrix | Positive, negative and boundary generation cases pass. |
| 63 Duration | Account for warm-up, sets, rest, transitions, unilateral exercises and supersets. |
| 64 Warm-up | Provide where needed and exclude warm-up sets from working volume. |
| 65 Deload | Respond to fatigue trends rather than a fixed calendar alone. |
| 66 Return after deload | Preserve history and adapt the return of volume. |
| 67 Missed workouts | Do not require doubled work or reset progression. |
| 68 Weekly boundary | Account for rolling seven-day recovery across calendar weeks. |
| 69 Replanning | Preserve completed work; active plan changes require Apply. |
| 70 Active session | Persist sets, permit pauses, substitutions and partial completion; synchronize without duplicates. |
| 71 Optional RIR | RIR and technique rating are optional; incomplete records save and are interpreted correctly. |
| 72 Load increase | Two comparable successful sessions lead to a recommendation, not a forced increase. |
| 73 Load reduction | Sustained underperformance leads to an explained reduction recommendation. |
| 74 Plateau | Detect from history without random exercise rotation. |
| 75 Unilateral | Correct per-side load, reps, weekly volume and duration. |
| 76 Supersets | Eligible pairings, sufficient rest and justified time saving. |
| 77 First load | Never invent an unknown load; support calibration and history. |
| 78 Daily sessions | Up to two independent sessions; an eligible cardio finisher counts within strength. |
| 79 Concurrent training | Allocate strength and cardio by goal, intensity and recovery. |
| 80 HIIT deferred | Exclude HIIT from new active generation and selection; full HIIT is not a release condition. |
| 81 Cardio metrics | Support appropriate time, distance or steps and computable derived metrics. |
| 82 Cardio progression | Adapt progression without a compulsory weekly percentage. |
| 83 Cardio intensity | Support RPE and intensity without requiring sensors. |
| 84 Cycle | Support four phases; phase alone does not change load. |
| 85 Priority hierarchy | Safety, then recovery, then goal, then schedule; explain decisions. |
| 86 Optional check-in | Skipping it does not block training if required safety data are sufficient. |
| 87 Pain substitution | Exclude provoking movements; use only eligible reviewed alternatives. |
| 88 Return after pain | Reassess after 72 hours; a timer alone cannot clear a restriction. |
| 89 DOMS | Soreness does not alter weight, volume, RIR or exercise selection. |
| 90 Readiness | Adjust optional volume first; do not automatically lower weight solely from check-in. |
| 91 Check-in UX | Three simple optional measures; ask additional questions only when needed. |
| 92 Explanations | Explain automatic changes briefly without asking to confirm each one. |
| 93 Determinism | Identical inputs and rule version yield identical prescription content. |
| 94 Clean architecture | Remove conflicting old paths; no dependency on migrating old test-environment data. |
| 95 Environment reset | Reset only separately confirmed Dev/Staging/Beta targets after backup and restore drill; never touch Production. |

## Verification and release

Write a meaningful failing test before each feature, verify it fails for the missing behavior, implement, rerun it and the affected suites. Do not weaken tests to obtain green results; document justified changes when requirements supersede assertions. Cover beginners through advanced and returning users, one to seven days, consecutive days, varied goals/equipment/time, readiness and pain, missed sessions, partial plans, retry and concurrency. Include optional RIR, DOMS invariance, no phase-only load change, red flags, bodyweight zero additional load, reproducibility and exactly-once completion/synchronization.

Prove a synthetic user's real chain: registration → setup → generation → first-plan application → session → actual sets → completion → history → next recommendation; separately prove regeneration and Apply, network failures and active-plan preservation. Check both languages, iOS simulator, accessibility and actual screenshots. Report exact SHA, build, commands and observed outputs; distinguish simulator, owner physical-device and TestFlight evidence.

An independent reviewer checks behavior, tests, race conditions, safety, contracts and actual invocation paths before each logical change is confirmed. Commit confirmed logical changes without including preexisting edits. Follow current `AGENTS.md`, `CLAUDE.md`, protected-branch/PR rules and `STATUS.md` requirements. Use GitHub Actions for approved delivery; establish exact account, environment, deployed artifact, database and rollback before nonproduction operations. Green CI is not owner acceptance or proof of release. If a check or approval is unavailable, report it as blocked or not run, not passed.

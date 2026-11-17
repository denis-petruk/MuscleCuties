# MUSCLECUTIES — WORKOUT ENGINE 2.0
# MASTER ENGINEERING IMPLEMENTATION BLUEPRINT

**Version:** 1.0 — Execution Blueprint  
**Date:** November 15, 2026  
**Repository:** `denis-petruk/MuscleCuties`  
**Primary language:** C# / .NET 10  
**Mobile:** .NET MAUI, iOS only  
**Backend:** ASP.NET Core / AWS Lambda  
**Database:** PostgreSQL  
**CI/CD:** GitHub Actions  
**Target:** Verified closed-beta release candidate

**Document status:** Proposed executable specification. No implementation, build, deployment or destructive reset is claimed by this document.

---

# 0. EXECUTION CONTRACT

## 0.1 Mission

Implement MuscleCuties Workout Engine 2.0 as a complete, deterministic, evidence-informed workout-planning system.

The finished system must:

1. Generate individualized weekly strength programs.
2. Select appropriate exercises based on goals, equipment, restrictions and training history.
3. Optimize muscular volume, frequency, fatigue and session duration.
4. Support General Strength, High-Volume Strength and High-Intensity Strength.
5. Support selected ordinary cardio activities.
6. Adapt to readiness, recovery and menstrual symptoms without unjustified dose changes.
7. Handle exercise restrictions and pain appropriately.
8. Support initial-weight calibration and progressive overload.
9. Correctly log actual workouts, including partial and interrupted sessions.
10. Preserve historical performance, plan revisions and completed sessions.
11. Connect every required behavior across Core, UseCases, backend, database and MAUI.
12. Remove obsolete or conflicting workout-generation logic.
13. Pass the full required automated and simulator verification matrix.
14. Support a controlled clean deployment to Development, Staging and Beta.
15. Never touch Production as part of the destructive reset.

The work is not complete merely because the generator returns a plan.

**Completion means that an actual new user can register, configure training, receive a valid program, complete workouts, record results and receive correct future recommendations through the real application.**

## 0.2 Instructions for low-reasoning implementation agents

Each coding agent must receive one atomic task card, not this entire document as its immediate work assignment.

Every task card must specify:

- Task ID.
- Approved requirement IDs.
- Exact files to inspect.
- Exact files permitted to change.
- Existing behavior.
- Required new behavior.
- Concrete test cases and expected assertions.
- Dependencies.
- Commands to run.
- Definition of Done.
- Forbidden changes.

The execution sequence is mandatory:

**Read → inspect → add failing test → run test → implement → run tests → review → commit → update status.**

An executor must not independently invent a product rule to resolve ambiguity.

An unresolved requirement goes to the lead orchestrator. Only genuine product decisions or authorization boundaries go to the owner.

### Maximum recommended scope per atomic task

A normal task should modify:

- One primary behavior.
- One responsible production component.
- Its corresponding test files.
- Essential contract or localization files only when directly required.

A task touching multiple architectural layers must be explicitly designated an integration task.

Avoid combining database schema, algorithm selection, UI redesign and infrastructure deployment in one executor assignment.

## 0.3 Mandatory execution constraints

Follow the actual checked-out `AGENTS.md` and applicable repository instructions.

In the repository snapshot inspected for this blueprint, the operating contract requires:

- GitHub as the only repository source of truth.
- Branch-based development and PR integration.
- No direct pushes to protected `main`.
- No force-pushing or rewriting remote history.
- Preservation of unrelated worktree changes.
- Test-first development.
- No weakening or hiding existing tests.
- No secrets in logs, fixtures or prompts.
- No sensitive data in third-party AI services.
- No alternative mobile persistence store for sensitive data.
- No unauthorized database initialization.
- Updates to `STATUS.md` after completed tasks.
- No AI-tool attribution in commits or PR metadata.

The actual local file takes precedence if it differs from this inspected snapshot, subject to the owner's explicit safety boundaries.

## 0.4 Non-negotiable owner approvals

Ordinary coding, tests, permitted simulator runs, reviews, commits and PR workflows should proceed autonomously when authorized by repository policy.

The following are separate boundaries:

1. Approval of newly proposed exercise metadata that requires content review.
2. Approval of any previously unresolved safety-sensitive clinical rule.
3. Confirmation of exact resources before destructive Development, Staging or Beta resets.
4. TestFlight upload.
5. App Store Connect actions.
6. Any operation affecting Production, which is outside this project's approved scope.

A successful plan review does not authorize a database wipe.

## 0.5 Status vocabulary

Use five statuses consistently:

| Status | Meaning |
|---|---|
| PASS | Implemented and verified against current execution evidence |
| FAIL | Implemented or tested, but required behavior is incorrect |
| BLOCKED | Cannot proceed without a specifically identified dependency or approval |
| NOT VERIFIED | Implementation may exist, but required evidence is missing |
| NOT APPLICABLE | Requirement does not apply to this explicitly defined scope |

A task may be marked IMPLEMENTED internally while its acceptance requirement remains NOT VERIFIED.

Never turn NOT VERIFIED into PASS based on an old build, an agent assertion or a code review alone.

---

# 1. VERIFIED REPOSITORY BASELINE AND LOCAL RECONCILIATION

## 1.1 Available repository evidence

The following files were observed in an accessible GitHub branch. Their existence in the current local checkout must be reconfirmed.

### Core planning

- `src/MuscleCuties.Core/Domain/Workout/Planning/StrengthPlanPreviewRule.cs`
- `src/MuscleCuties.Core/Domain/Workout/Planning/WeeklyWorkoutPlanGeneratorRule.cs`
- `src/MuscleCuties.Core/Domain/Workout/Planning/StrengthTemplateCatalog.cs`
- `src/MuscleCuties.Core/Domain/Workout/Planning/StrengthTemplateResolver.cs`
- `src/MuscleCuties.Core/Domain/Workout/Planning/TenRegionMuscleCoverageRule.cs`
- `src/MuscleCuties.Core/Domain/Workout/Planning/WeeklyHypertrophyVolumeRule.cs`
- `src/MuscleCuties.Core/Domain/Workout/Planning/ObservedSetProgression.cs`
- `src/MuscleCuties.Core/Domain/Workout/Planning/ExercisePickerRule.cs`
- `src/MuscleCuties.Core/Domain/Onboarding/InitialWorkoutPlanRule.cs`

### UseCases

- `src/MuscleCuties.UseCases/Features/Workout/Planning/PreviewStrengthPlanV020UseCase.cs`
- `src/MuscleCuties.UseCases/Features/Workout/Planning/StrengthPlanPreviewV020.cs`
- `src/MuscleCuties.UseCases/Features/Workout/Planning/StrengthPlanPreviewMapperV020.cs`
- `src/MuscleCuties.UseCases/Features/Workout/Planning/WorkoutPlanningFlow.cs`
- `src/MuscleCuties.UseCases/Features/Workout/Session/WorkoutSessionActionUseCase.cs`

### Persistence

- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresStrengthPlanPreviewSourceV020.cs`
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/ReviewedExerciseCatalogV020ReviewMap.cs`
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresAcceptStrengthPlanProposalStoreV020.cs`
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Planning/PostgresWorkoutPlanReconciler.cs`
- `src/MuscleCuties.Infrastructure.PostgreSql/Workout/Session/PostgresWorkoutPlanAndSessionStore.cs`

### Server and MAUI

- `services/MuscleCuties.Server/ApiApplication.cs`
- `services/MuscleCuties.Server/Workout/Session/WorkoutPlanAndSessionEndpointMappings.cs`
- `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationViewModel.cs`
- `src/MuscleCuties.UI/ViewModels/Workout/StrengthPlanGenerationDecisions.cs`
- `src/MuscleCuties.UI/ViewModels/Workout/WorkoutSessionViewModel.cs`
- `src/MuscleCuties.UI/Pages/Workout/WorkoutSessionPage.xaml`

### Existing tests

- `tests/MuscleCuties.Core.Tests/StrengthPlanPreviewRuleTests.cs`
- `tests/MuscleCuties.Core.Tests/WorkoutPlanningSyntheticReviewTests.cs`
- `tests/MuscleCuties.Core.Tests/WorkoutPlanningPolicyComplianceTests.cs`
- `tests/MuscleCuties.Core.Tests/WeeklyWorkoutPlanGeneratorRuleTests.cs`
- `tests/MuscleCuties.Core.Tests/WeeklyHypertrophyVolumeRuleTests.cs`
- `tests/MuscleCuties.Core.Tests/StrengthProgressionConsecutiveDeficitTests.cs`
- `tests/MuscleCuties.Mobile.Contracts.Tests/ObservedSetCaptureAndProgressionContractTests.cs`
- `tests/MuscleCuties.Mobile.Contracts.Tests/StrengthPlanActivationAndApplyTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/ReviewedExerciseCatalogV020ImportTests.cs`
- `tests/MuscleCuties.Registration.Integration.Tests/AutomaticAcceptanceTransactionTests.cs`

These are verified remote paths, not permission to overwrite local changes.

## 1.2 Known discrepancies requiring resolution

The currently accessible code shows several conflicts with the approved design.

| Current implementation concern | Required replacement behavior |
|---|---|
| RIR required for qualifying progression | Optional RIR |
| Generic weekly volume bands | Per-muscle, per-experience targets |
| Session-based muscle-exposure accounting | Working-set-based accounting on distinct dates |
| Fixed template-dependent coverage | Dynamic, deficit-driven selection |
| Limited movement slots | Complete lower-body, shoulders, calves, core and goal-specific coverage |
| No complete structured glute-focus pipeline | Persisted goal-to-planning influence |
| Old HIIT scheduling logic | HIIT excluded from new generation |
| Limited warm-up representation | Explicit warm-up phases and sets |
| No complete superset prescription | Safe, time-aware superset grouping |
| No complete cardio progression | History-based cardio progression |
| Competing or obsolete generators | One canonical production authority |
| Old ReducedVolume acceptance behavior | First safe reduced-volume plan can apply automatically |
| Fixed exercise increment assumptions | Equipment-specific available increments |
| Current review snapshot tied to old versions | Version-consistent catalog review and policy activation |

The remote `StrengthPlanPreviewRule` inspected for this blueprint reports policy version `0.2.0` and ruleset `MC-PLANNING-0.2`. A previous agent reported a different ruleset revision. **The local checkout must resolve this discrepancy before version changes.**

## 1.3 Baseline procedure

**Task BASE-001**

Record:

- Absolute repository root.
- Current branch.
- HEAD SHA.
- Upstream remote.
- Worktree modifications.
- Untracked files.
- Current `AGENTS.md` requirements.
- Nested agent instructions.

Required initial command:

`git status --short --branch`

Then inspect the exact current commit and relevant history.

**Task BASE-002**

Run the existing repository's approved restore, build and test commands.

Known commands from the inspected `README.md`:

- `dotnet restore MuscleCuties.sln`
- `dotnet build MuscleCuties.sln -c Release`
- `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj -c Release`

Discover the remaining exact project paths under `tests/`.

Run database-backed tests only against disposable local PostgreSQL databases using the repository's current preparation scripts.

**Task BASE-003**

Reproduce the previously reported modal lifecycle test failure.

The earlier reported baseline of 1,417 passing and one failing test must be treated as historical information.

Record the actual failing test, exception, environment and reproduction conditions. Do not change unrelated assertions.

**Task BASE-004**

Construct a production call graph starting at the MAUI Generate action and ending at the saved accepted plan.

Identify:

1. MAUI command.
2. API client method.
3. Server route.
4. UseCase.
5. Core generation rule.
6. Catalog/source adapter.
7. Proposal persistence.
8. Plan acceptance transaction.
9. Schedule persistence.
10. Workout retrieval and session completion.

**Task BASE-005**

Search all production references to obsolete generators before deleting anything.

A file is considered truly dead only after checking production references, registration/composition, tests, serialization, reflection and possible worker entry points.

### P0 exit criteria

- Exact checkout recorded.
- Current baseline tests recorded.
- Existing defects classified.
- Production generation path mapped.
- Local changes preserved.
- No application code modified during initial inspection.

---

# 2. PRODUCT REQUIREMENTS — AUTHORITATIVE BEHAVIOR

## 2.1 Stable requirement groups

Historical gate numbers before 49 are not fully recoverable. Use stable identifiers rather than inventing missing numbers.

| Requirement family | IDs | Meaning |
|---|---|---|
| Eligibility and experience | REQ-001–009 | Supported population, 20/130 days, returning users |
| Goals and training styles | REQ-010–019 | Strength priority, glute focus, effort styles |
| Scheduling and splits | REQ-020–029 | 1–7 days, dynamic splits, recovery distribution |
| Muscle coverage and volume | REQ-030–049 | Direct sets, frequency, per-muscle targets |
| Exercise catalog | REQ-050–059 | Metadata, equipment, substitutions, variety |
| Session optimization | REQ-060–069 | Duration, warm-up, supersets, partial sessions |
| Progression | REQ-070–079 | Optional RIR, double progression, plateau |
| Recovery and safety | REQ-080–089 | Deload, pain, Check-In, cycle, DOMS |
| Cardio | REQ-090–099 | Selected activities, progression, finisher |
| Lifecycle and persistence | REQ-100–109 | Preview, Apply, logging, offline, determinism |
| Release and architecture | REQ-110–119 | One engine, CI/CD, controlled environments |

The lead must expand each requirement family into individual traceability rows. These ranges are identifiers for the new specification, not claims about historical gate numbering.

## 2.2 Training volume configuration

Implement the approved starter direct-set ranges:

| Muscle | Beginner | Intermediate | Advanced |
|---|---:|---:|---:|
| Gluteus maximus | 6–8 | 8–12 | 10–16 |
| Quadriceps | 4–6 | 6–10 | 8–12 |
| Hamstrings | 4–6 | 6–10 | 8–12 |
| Back | 6–8 | 8–12 | 10–14 |
| Chest | 4–6 | 6–8 | 8–12 |
| Lateral delts | 2–4 | 4–6 | 4–8 |
| Rear delts | 2 | 2–4 | 2–6 |
| Biceps | 2 | 2–4 | 2–6 |
| Triceps | 2 | 2–4 | 2–6 |
| Calves | 2 | 2–4 | 2–6 |
| Core | 2–4 | 4–6 | 4–8 |

Additional direct hip-abduction guidance for glute-focused users: approximately 2–4 sets weekly.

These ranges are starting guidelines. They are not unconditional hard caps and must not all be maximized simultaneously.

## 2.3 Frequency and exposure rules

When feasible:

- Glutes: two meaningful direct days weekly.
- Quadriceps: two meaningful days.
- Hamstrings: two meaningful days.
- Back: two meaningful days.
- Chest: at least one meaningful day, more when appropriate.
- Rear delts: direct weekly training.
- Biceps: at least one direct exercise weekly.
- Triceps: at least one direct exercise weekly.
- Calves: at least two direct working sets weekly.
- Core: two weekly occasions with dynamic exercise preference.
- Hip abduction: direct weekly exposure in glute-focused programs.
- Neck: no default prescription.
- Adductors: direct prescription only when needed.
- Lower-back isolation: not mandatory when adequate training is already provided.

Meaningful exposure should generally require two effective working sets on a distinct local calendar day.

For explicit direct-work requirements, indirect work cannot substitute for the direct minimum.

## 2.4 Strength styles

| Style | Effort guidance | Special behavior |
|---|---|---|
| General Strength | Approximately 3 RIR | Balanced, repeatable work |
| High-Volume Strength | Approximately 3–4 RIR | More volume where tolerable |
| High-Intensity Strength | 1 RIR | Default 2 working sets; compound 5–8 reps; isolation 8–12 reps |

High-Intensity rest guidance:

- Compound: approximately 120–150 seconds.
- Isolation: approximately 90–120 seconds.

RIR describes the target effort. Recording actual RIR is optional.

## 2.5 Essential safety hierarchy

1. Serious warning symptoms: stop activity and direct the user to appropriate medical assistance.
2. Known exercise restrictions and local pain: exclude relevant provoking exercises.
3. Readiness and recent training history: conservative permitted adjustments.
4. Training goals and priority-muscle preservation.
5. Time, equipment and scheduling preferences.
6. Cycle-phase context as information, not a loading command.

Ordinary DOMS must not independently change the prescription.

## 2.6 Versioned supersession register

The plan must explicitly supersede old behaviors:

| Old behavior | Required final behavior |
|---|---|
| RIR mandatory | RIR optional |
| One day blocks strength | Useful one-day strength plan |
| Fixed six exercises | Dynamic count |
| Fixed global volume range | Per-muscle target ranges |
| Every indirect exposure equals meaningful training | Working-set-based meaningful exposure |
| Cycle phase directly changes load | Symptoms/readiness only |
| DOMS changes prescription | DOMS invariance |
| Scheduled fixed Deload | Deload based on observed need |
| New HIIT generated | HIIT deferred |
| Hip Thrust always first | Dynamic ordering |
| First reduced-volume plan requires Apply | First safe feasible plan auto-applies |
| Existing plan silently overwritten | Explicit Apply required |
| Unknown exercise weight represented as zero | Unknown stays unknown; bodyweight added load can be zero |
| Old user data must always migrate | Clean reset specifically approved for Dev/Staging/Beta |
| Production included in reset | Production excluded |

Each row must have an explicit regression test.

---

# 3. TARGET ARCHITECTURE

## 3.1 Dependency direction

The production data flow shall be:

**MAUI UI → API client → ASP.NET Core Server → UseCases → Core planner**

Infrastructure implements UseCase ports and provides trusted catalog, profile, history and storage data.

Core must remain independent from:

- PostgreSQL.
- EF Core.
- ASP.NET Core.
- MAUI.
- AWS.
- System clocks used as hidden generation inputs.
- Randomness.
- Environment variables.

The canonical generator must operate as a pure deterministic function over explicit, validated inputs.

## 3.2 Proposed Core module structure

The following paths are proposed and may be adjusted to existing repository conventions.

`src/MuscleCuties.Core/Domain/Workout/Planning/V2/`

Suggested files:

- `WorkoutGenerationInput.cs`
- `WorkoutGenerationResult.cs`
- `WorkoutPolicyV2.cs`
- `WorkoutGenerationRule.cs`
- `MuscleAccountingRule.cs`
- `WeeklyVolumeAllocationRule.cs`
- `MeaningfulExposureRule.cs`
- `TrainingSplitSelectionRule.cs`
- `StrengthExerciseSelectionRule.cs`
- `ExerciseOrderRule.cs`
- `SessionDurationRule.cs`
- `WarmupPrescriptionRule.cs`
- `SupersetEligibilityRule.cs`
- `SessionOptimizationRule.cs`
- `CardioSchedulingRule.cs`
- `CardioProgressionRule.cs`
- `RecoveryAdjustmentRule.cs`
- `PainRestrictionRule.cs`
- `WorkoutProgressionRule.cs`
- `WorkoutGenerationTrace.cs`

Do not create redundant files when an existing well-designed component can be safely extended.

## 3.3 Canonical generation interface

The conceptual contract is:

`WorkoutGenerationResult Generate(WorkoutGenerationInput input)`

The function must not:

- Read the database.
- Generate random numbers.
- Generate persistence GUIDs.
- Call external APIs.
- Read the current clock.
- Depend on dictionary iteration order.
- Use unvalidated catalog entries.

The UseCase loads data and prepares the snapshot. Core generates the prescription. Persistence creates IDs and saves the accepted plan.

## 3.4 WorkoutGenerationInput fields

| Field | Type concept | Required behavior |
|---|---|---|
| User training profile | Trusted structured snapshot | Includes effective experience |
| Goal | Explicit goal enum | Required |
| Training focus | Balanced or goal-specific | Glute focus only when selected |
| Selected activities | Set of activity types | Never invent activities |
| Available days | Distinct weekdays, 1–7 | Required |
| Session durations | Minutes by selected activity | Required where relevant |
| Equipment | Structured equipment capabilities | Required |
| Strength style | Defined enum | Required for strength |
| Catalog snapshot | Reviewed, versioned entries | Required |
| Active restrictions | Structured records | Never relaxed by missing data |
| Prior observed sets | Completed, authenticated history | Optional |
| Prior cardio actuals | Persisted activity history | Optional |
| Existing accepted plan | Versioned snapshot | Optional |
| Daily readiness | Optional observed values | Missing values remain unknown |
| Cycle context | Phase or Unknown, symptoms | Optional, no invented phase |
| Account-local date | DateOnly | Supplied explicitly |
| Account timezone | Valid timezone | Supplied explicitly |
| Policy version | Version identifier | Required |
| Catalog version | Version identifier | Required |

No field should be duplicated merely to move data between layers.

## 3.5 WorkoutGenerationResult fields

The result must expose:

- `Status`: Feasible, Limited, InvalidInput or SafetyBlocked.
- `Sessions`: ordered session prescriptions.
- `CoverageSummary`: muscle volume and exposure.
- `WeeklyVolumeSummary`.
- `DurationSummary`.
- `LimitationCodes`.
- `AdvisoryCodes`.
- `OriginalPrescription`.
- `AdaptedPrescription`, when applicable.
- `RuleTrace`.
- `PolicyVersion`.
- `CatalogVersion`.
- `NormalizedPrescriptionHash`.

If a complete strength session is unsafe because no eligible movement remains, it must be withheld with a specific reason.

If the plan is safe but incomplete because of time or equipment, it should be Limited rather than falsely marked complete.

## 3.6 Deterministic output contract

Canonical prescription comparison must ignore persistence-generated IDs and timestamps.

The normalized content must include:

- Week and local dates.
- Selected activities.
- Exercise stable identifiers.
- Exercise order.
- Working sets.
- Repetition ranges.
- Loads or calibration requirements.
- Rest periods.
- Warm-up instructions.
- Supersets.
- Cardio prescriptions.
- Limitation and advisory codes.

All collections used in hashing must have explicit stable ordering.

**Acceptance:** 100 repeated generations with identical inputs and versions produce the same normalized content hash.

This is a regression check, not a substitute for broader property-based testing.

---

# 4. MUSCLE ACCOUNTING — IMPLEMENTATION ALGORITHM

## 4.1 Separate physical work from muscle credits

Define three distinct concepts.

**Physical working sets:** the number of sets the user actually performs.

**Direct muscle credits:** stimulus credited to directly trained muscles.

**Indirect stimulus credits:** approximate secondary involvement.

Example:

Two sets of an exercise may count as two physical sets while contributing to multiple muscles.

Do not sum muscle credits to calculate the number of physical sets the user performed.

## 4.2 Proposed muscle taxonomy

Primary volume accounting should distinguish:

1. Gluteus maximus.
2. Quadriceps.
3. Hamstrings.
4. Back.
5. Chest.
6. Lateral deltoids.
7. Rear deltoids.
8. Biceps.
9. Triceps.
10. Calves.
11. Core.

Additional functional classifications include:

- Hip abduction.
- Hip adduction.
- Anterior deltoid work.
- Knee-dominant pattern.
- Knee-flexion pattern.
- Hip-extension/hinge pattern.
- Horizontal pushing/pulling.
- Vertical pushing/pulling.

Avoid collapsing everything into one large `Legs`, `Arms` or `Shoulders` category.

## 4.3 Muscle contribution contract

Each reviewed exercise may have several muscle contributions.

Suggested fields:

- `MuscleId`.
- `ContributionKind`: Direct, Indirect or Incidental.
- `CreditPerWorkingSet`.
- `EvidenceOrReviewReference`.

Default programming guidance:

- Direct: 1.0.
- Meaningful indirect: approximately 0.5.
- Incidental: 0.

However, exact assignment is exercise-specific and must be reviewed.

Do not infer the coefficient from an exercise's name.

## 4.4 Weekly calculation

For each exercise prescription:

1. Obtain verified direct and indirect contributions.
2. Multiply each contribution by prescribed working sets.
3. Add those contributions to the appropriate muscle totals.
4. Track total physical working sets separately.
5. Group work by the user's local calendar date.
6. Compute meaningful exposure dates.
7. Compare totals to the appropriate target ranges.
8. Report unmet targets as limitations or advisories according to feasibility.

For muscles with explicit direct-work requirements, evaluate the **direct** set count and meaningful direct dates separately.

## 4.5 Meaningful training day

A muscle's meaningful direct training day exists when its approved direct credited working sets reach the applicable threshold, normally two.

Two qualifying sessions on the same calendar date count as one meaningful training day.

Indirect contributions must never independently satisfy an explicit direct-work requirement.

## 4.6 Acceptance tests

Required tests:

- One direct set is not two.
- Two direct sets on one date count as one meaningful date.
- Two direct sets on two distinct dates count as two.
- An indirect contribution alone does not satisfy direct glute training.
- Rows do not automatically satisfy rear-delt direct work.
- Compound exercises can contribute to multiple muscle accounts without increasing physical-set count.
- Unilateral exercises do not double the set count.
- Warm-up sets contribute zero working sets.
- Missing muscle metadata excludes the exercise from automatic selection.
- Weekly boundaries do not erase rolling recovery history.

---

# 5. WEEKLY VOLUME AND SPLIT OPTIMIZATION

## 5.1 Goal of the optimizer

The optimizer should find the best safe, feasible weekly program under the user's actual constraints.

Do not attempt to maximize every muscle's target simultaneously.

Use a deterministic priority hierarchy rather than an unexplained collection of random scoring weights.

## 5.2 Constraint categories

### Hard constraints

- Known safety restrictions.
- Actual equipment requirements.
- Only selected activities.
- At most two independent sessions per day.
- No unreviewed exercises.
- No prescription with zero safe strength content.
- Valid age/population scope.
- Explicit user availability.
- No invented working weights.
- No unauthorized replacement of an active plan.

### Soft constraints

- Preferred weekly volume range.
- Desired training frequency.
- Recovery spacing.
- Two-week movement variety.
- Preferred exercises.
- Equipment setup efficiency.
- Superset efficiency.
- Minimized changes to existing prescriptions.

Soft constraints may remain unmet with explicit disclosure.

## 5.3 Candidate evaluation priority

Evaluate feasible candidates lexicographically:

1. Reject unsafe candidates.
2. Minimize unmet essential coverage.
3. Minimize unmet priority-muscle direct volume.
4. Minimize session-time violations.
5. Minimize excessive overlapping fatigue.
6. Minimize unnecessary prescription changes.
7. Improve user preferences and exercise variety.
8. Resolve ties by stable exercise identifier.

Do not allow an attractive preference score to override a safety restriction.

## 5.4 Strength frequency algorithm

Inputs:

- Available weekdays.
- Selected strength activity.
- Experience.
- Training history.
- Desired session durations.
- Recovery state.
- Requested frequency, if present.

Algorithm:

1. Determine feasible strength-day candidates.
2. Do not automatically prescribe all available days.
3. Begin with appropriate experience/history guidance.
4. Test whether target coverage can fit.
5. Evaluate recovery and duration.
6. Select the best feasible frequency.
7. If only one day is available, generate a safe single-day plan rather than failing solely for frequency.

## 5.5 Split algorithm

Do not use a fixed lookup such as three days always equals Push/Pull/Legs.

For each feasible arrangement:

1. Determine weekly muscle-volume objectives.
2. Assign relevant movement patterns to sessions.
3. Preserve meaningful muscle exposures.
4. Distribute fatigue.
5. Fit available time.
6. Prefer stable and comprehensible structure.

Choose Full Body, Upper/Lower or hybrid arrangements according to the result.

## 5.6 Lower-body coverage

Ensure that the week includes the following when feasible:

- Knee-dominant work.
- Knee flexion.
- Hip extension.

All three patterns need not appear in the same session.

The engine should avoid satisfying hamstring coverage entirely through a hip hinge while omitting knee flexion from an otherwise feasible week.

## 5.7 Two-week variety

Maintain a rolling two-week context.

When feasible, include:

- Horizontal chest pressing.
- Incline chest pressing.
- Chest fly.
- Shoulder press.
- Lateral raise.
- Useful vertical and horizontal pulling variants.

Do not rotate exercises merely to fulfill a variation counter.

Stable progression takes precedence over novelty.

---

# 6. SESSION CONSTRUCTION AND DURATION

## 6.1 The optimization ladder

Apply these steps in order:

1. Create the preferred safe prescription.
2. Calculate actual duration.
3. Reorder movements where appropriate.
4. Add compatible supersets where useful.
5. Redistribute eligible work across other days.
6. Remove lower-priority accessories.
7. Reduce optional working sets only within approved rules.
8. Produce the best safe partial session.
9. Disclose unachievable coverage.

Never silently schedule a 75-minute workout into a 30-minute availability window.

## 6.2 Duration model

Total duration is the sum of:

- General warm-up.
- Exercise-specific warm-up sets.
- Working-set execution.
- Rest between efforts.
- Transitions.
- Equipment setup.
- Both sides of unilateral exercises.
- Superset structure.
- Prescribed finisher.
- Cool-down, when included.

Use the higher prescribed repetition bound for conservative timing estimates where appropriate.

### Rest accounting

For straight sets, rest normally occurs between sets, not after the final set when no further work requires it.

For supersets, count the rest structure at the group level without double-counting individual exercise rests.

Do not double-count exercise transitions already included in equipment setup.

## 6.3 Warm-up model

The general warm-up is normally guided by approximately 5–8 minutes.

Exercise-specific warm-up sets must be represented separately.

Each set requires a `SetKind`, at minimum:

- WarmUp.
- Working.

Warm-up sets:

- Do not contribute to direct working-set volume.
- Do not satisfy progression success.
- Do not count toward meaningful muscular exposure.
- Can still be logged if the UX supports that.
- Contribute to session duration.

## 6.4 Superset eligibility

Candidate pairs must be checked against:

- Target-muscle overlap.
- Training style.
- Exercise technical demand.
- Required recovery.
- Equipment compatibility.
- Setup complexity.
- Time-saving benefit.

Heavy High-Intensity compounds should normally remain straight sets.

The UI must allow users to split a superset back into regular sets without losing already logged performance.

## 6.5 Partial-plan behavior

A partial plan must retain:

- Eligible exercises.
- Actual prescribed sets.
- Unmet muscle or movement goals.
- Reason codes for omission.
- Correct predicted duration.

Do not show an incomplete plan as though it fully met all weekly coverage requirements.

---

# 7. PROGRESSION AND PERFORMANCE HISTORY

## 7.1 Source of truth

Progression must consume persisted actual working sets.

Do not use:

- Fabricated aggregate set records.
- Placeholder rows that were never edited.
- Warm-up sets as working sets.
- Duplicate session logs.
- Old machine weights for a different equipment variant.
- Unknown RIR values transformed into zero or another estimate.

## 7.2 Required observed-set information

Conceptual fields:

- Session ID.
- Stable exercise variant ID.
- Set number.
- Set kind.
- Repetitions completed.
- External load.
- Unit.
- Optional RIR.
- Optional technique assessment.
- Unilateral side data when applicable.
- Completion timestamp.
- Completion state.

A set with missing RIR can still be a valid observed working set.

A known unacceptable technique report may prevent the set from qualifying for a load-increase recommendation.

Unknown technique remains unknown; it must not be interpreted as evidence of perfect form.

## 7.3 Session comparability

Two performances are comparable when they involve:

- The same exercise variant.
- Compatible equipment and loading interpretation.
- Completed relevant working sets.
- The same applicable rep-range prescription.
- No condition that invalidates their use for the intended progression comparison.

A deliberate deload session should not be classified as an ordinary failed performance.

## 7.4 Double progression

For load increases:

1. Locate the most recent qualifying comparable performances.
2. Confirm that all required working sets reached the upper repetition bound.
3. Require two qualifying comparable sessions.
4. Select the next available realistic equipment increment.
5. Return a recommendation.
6. Preserve manual user control over the actual selected weight.

If the equipment increment is unknown, do not invent one.

Use a recommendation to calibrate or increase repetitions instead.

## 7.5 Reduction recommendation

When performance remains below the lower repetition bound in two consecutive comparable sessions:

1. Verify that the records are valid.
2. Check recovery and prescription context.
3. Recommend the next practical smaller load when appropriate.
4. Do not force the change.
5. Preserve the historical result.

One poor day is not sufficient.

## 7.6 Plateau detection

Evaluate several comparable exposures over approximately 3–4 weeks where appropriate.

Check:

- Training consistency.
- Actual completed sets.
- Weight and rep trends.
- Recovery.
- Existing volume.
- Time constraints.

For justified glute-specialization plateaus, one additional direct weekly set may be suggested.

Do not automatically replace exercises because a calendar threshold has passed.

## 7.7 Deload

The engine may suggest a Deload when multiple observations indicate cumulative fatigue or persistent underperformance.

A single poor night of sleep does not trigger a weekly Deload.

During Deload:

- Prioritize reduced optional volume.
- Preserve core exercise continuity where safe.
- Retain previous performance data.
- Do not interpret deliberate reduced performance as regression.

Return afterward should be adaptive rather than an unconditional fixed percentage reset.

## 7.8 Unilateral logging

One prescribed bilateral pair of movements counts as one exercise working set for volume accounting even if each side is logged separately.

The engine must:

- Store load per side.
- Allow distinct repetitions by side.
- Use the weaker side when evaluating progression.
- Avoid automatically prescribing extra sets for the weaker side.
- Multiply execution duration appropriately.

---

# 8. READINESS, CYCLE AND INJURY RULES

## 8.1 Optional Daily Check-In

The primary fields are:

- Energy.
- Fatigue.
- Sleep quality or duration.

The user may skip Check-In.

Additional symptoms or health-change questions are asked only when relevant.

If HealthKit values are available through the already authorized scope, they can assist prefill. They must not become required.

## 8.2 Missing information

Differentiate:

- Missing essential planning answers.
- Missing optional readiness inputs.
- Active known restrictions.
- Unknown symptom status.

Missing optional data must not cause fictitious perfect readiness.

Missing required safety information must not be silently converted into clearance.

## 8.3 Readiness dose adaptation

When low readiness is reported:

1. Preserve safety.
2. Preserve core exercises and essential work where feasible.
3. Consider removing an optional working set.
4. Do not automatically reduce working weight solely because of sleep or fatigue.
5. Do not force an entire Deload week.
6. Preserve original and adapted prescriptions.
7. Show a brief explanation.

## 8.4 DOMS invariant

For identical inputs except ordinary DOMS severity, the prescribed:

- Exercises.
- Working sets.
- Repetitions.
- RIR targets.
- Working-load recommendations.

must remain identical.

Exception: if the user separately reports new pain, injury or another safety-relevant issue, that separate input can affect the prescription.

## 8.5 Menstrual cycle

Support:

- Menstrual.
- Follicular.
- Ovulatory.
- Luteal.
- Unknown.

The predicted phase alone changes no training dose.

Actual reported symptoms and readiness may justify approved adjustments.

Unknown or estimated phase information must not be treated as confirmed observations.

## 8.6 Pain restrictions

A reported movement-provoked pain signal should:

1. Identify implicated exercises or movement patterns.
2. Apply an appropriate restriction.
3. Exclude provoking candidates.
4. Consider reviewed alternatives only where safe based on known information.
5. Preserve unrelated eligible training.
6. Explain the omitted or replaced work.

Serious warning symptoms follow a stop-workout pathway instead.

## 8.7 Reassessment

Offer reassessment approximately 72 hours after a relevant pain report.

If the problem continues, maintain the restriction and allow subsequent reassessment.

The passage of time alone does not clear it.

Where resolved symptoms and other eligibility conditions permit return, use a conservative prescription, potentially around 10–15% below the former load for the first two sessions as an approved provisional heuristic.

Do not treat this numerical guideline as medical clearance.

Clinical safety rules or exercise-specific restrictions requiring further review remain blocked until approved.

---

# 9. CARDIO AND MIXED ACTIVITY PLANNING

## 9.1 Supported active cardio

- Running.
- Walking.
- Cycling.
- Swimming.

HIIT remains deferred.

## 9.2 Activity selection

Only user-selected activities participate in the weekly schedule.

The generator may recommend a change, but should not silently schedule an unselected sport.

## 9.3 Maximum daily activity count

Up to two independent sessions per day by default.

A compatible light cardio finisher of up to 30 minutes may be attached to a strength session without counting as another independent session.

Its duration and fatigue still count.

## 9.4 Strength priority

Strength takes priority when resolving workload interference for strength-oriented goals.

This does not require strength to occur chronologically before cardio.

Morning running and evening strength may be valid when appropriate.

## 9.5 Cardio log model

Depending on activity, support:

- Duration.
- Distance.
- Steps for walking.
- Derived pace/speed when enough input exists.
- Optional RPE.
- Optional heart rate.
- Optional cycling power.

At least one meaningful available measurement is sufficient for basic logging.

Do not invent missing sensor values.

## 9.6 Cardio progression

Compare similar completed cardio sessions.

Prefer gradual increases in one primary variable at a time.

Examples:

- Running duration.
- Walking duration or distance.
- Cycling duration or distance.
- Swimming distance.

Do not impose an automatic universal 10% weekly increase.

## 9.7 Intensity

Default subjective guidance:

- Light: RPE 2–3.
- Moderate: RPE 4–5.
- Higher: RPE 6–7 for appropriate users.

No mandatory wearable is required.

## 9.8 HIIT exclusion

New HIIT generation, recommendation and activity selection must be disabled for Workout Engine 2.0.

Existing historical HIIT records can remain readable where historical data is preserved.

Do not remove shared DTO enum values or historical deserialization support without checking existing consumers.

---

# 10. PERSISTENCE, API AND ACCEPTANCE CONTRACTS

## 10.1 Database strategy decision

The inspected repository documentation contains both model-first schema descriptions and references to migration/provisioning commands.

The implementation agent must inspect actual current schema-management code before selecting an evolution strategy.

Do not assume that an EF Core migration chain exists.

Do not run `EnsureCreated`, `dbinit` or destructive initialization against a shared database without the appropriate explicit authorization.

## 10.2 Required persisted concepts

The target system must support these logical concepts, using existing schema structures where appropriate:

| Concept | Required data |
|---|---|
| User training focus | Balanced or explicitly selected specialization |
| Equipment capabilities | Available equipment and unavailable machines |
| Exercise definition | Stable ID, reviewed metadata and version |
| Muscle contribution | Direct/indirect classification |
| Plan revision | Source plan, version, status, accepted timestamp |
| Prescription | Original exercise and set assignments |
| Adaptation | Reason and adjusted assignment |
| Warm-up set | Set kind and optional actual |
| Superset | Group membership and ordering |
| Actual set | Reps, load, optional RIR/technique, side |
| Cardio actual | Activity-specific measured metrics |
| Pain restriction | Affected movement and reassessment state |
| Catalog version | Immutable review/version reference |
| Generation trace | Rule version and machine-readable reasons |
| Idempotency key | Duplicate-request protection |

Do not create duplicate storage entities where existing tables already support the required semantics.

## 10.3 Plan lifecycle

Required states:

- No accepted plan.
- Proposed.
- Feasible or Limited.
- Accepted.
- Superseded.
- Expired or Stale.
- Invalid, when applicable.

The exact names should map to existing contracts where possible.

### Initial plan

A safe feasible first plan—including a safe ReducedVolume plan—applies automatically through the existing authoritative acceptance path.

### Existing accepted plan

A regenerated proposal does not replace it until Apply Plan is explicitly selected.

### Concurrent acceptance

The system must prevent two conflicting proposals from both becoming the current accepted plan.

Use existing version, lease and transactional mechanisms.

### Stale proposal

Reject or regenerate when critical inputs change:

- Equipment.
- Safety restrictions.
- Goal or focus.
- Available days.
- Profile version.
- Catalog version.
- Policy version.
- Active accepted-plan revision.

Do not silently accept stale candidate snapshots.

## 10.4 Session completion

The completion operation must be idempotent.

Repeated requests for the same completed session must not:

- Create duplicate logs.
- Duplicate working sets.
- Increment experience twice.
- Create a second terminal completion timestamp.
- Change immutable completed-session history.

Today and Train must show the same final session state.

## 10.5 Offline synchronization

Use the repository's encrypted local cache and SecureStorage mechanisms.

No sensitive data should be moved to unencrypted Preferences or new local SQLite storage.

Pending changes need stable operation identities and reconciliation rules.

The client must distinguish:

- Not yet sent.
- Sent, response unknown.
- Confirmed saved.
- Rejected due to conflict.
- Locally abandoned before submission.

A lost API response must not cause duplicated data on retry.

---

# 11. ATOMIC IMPLEMENTATION BACKLOG

Every task below is an executable unit with a required verification outcome. File names explicitly labeled **proposed** must not be assumed to exist.

## PACKAGE A — Baseline, requirements and contracts

| Task | Work | Expected verification |
|---|---|---|
| A01 | Inspect local Git/AGENTS status and record all modifications | Baseline HEAD, branch and worktree documented |
| A02 | Run existing Core and available contract test baseline | Exact counts and failures recorded |
| A03 | Reproduce previously reported modal lifecycle failure | Exact failing case identified |
| A04 | Trace production generator call graph | Only confirmed callers listed |
| A05 | Trace preview, Apply and session-completion paths | End-to-end architecture diagram verified |
| A06 | Create authoritative requirement IDs and gate cross-references | Every recovered requirement has acceptance criteria |
| A07 | Create supersession regression register | Every superseded rule mapped to a test |
| A08 | Define Core V2 contracts and ownership | Contract tests compile and pass |
| A09 | Define versioned policy configuration | Same version produces stable configuration |
| A10 | Define API compatibility strategy | Current client DTO contracts remain valid or are explicitly versioned |

**A package exit:** the complete target contract is approved, the baseline is known and no unresolved cross-layer dependency prevents Core implementation.

## PACKAGE B — Exercise metadata and catalog foundation

| Task | Work | Expected verification |
|---|---|---|
| B01 | Audit review JSON against actual seed catalog | Every active exercise reconciled |
| B02 | Resolve reviewed 59 versus seed-manifest 60 discrepancy | No silent deletion or accidental activation |
| B03 | Add structured muscle contributions | Direct/indirect values validated |
| B04 | Add exercise-specific progression increments or increment metadata | No arbitrary machine increment assumption |
| B05 | Add equipment and setup requirements | Missing equipment excludes candidate |
| B06 | Add unilateral and load-recording semantics | Side and load rules verified |
| B07 | Add warm-up, rest and duration metadata | Missing required values reported |
| B08 | Add superset eligibility metadata | Unsafe pairings rejected |
| B09 | Add movement restriction metadata | Relevant exclusions enforced |
| B10 | Add home-equipment candidate records | Candidate completeness checked |
| B11 | Establish owner-review status for new candidates | Unreviewed content remains inactive |
| B12 | Version review snapshot with policy/template contract | Version mismatches fail closed |

**B package exit:** all existing active exercises have sufficient validated metadata, new candidates are explicitly approved or quarantined, and the planner cannot fabricate missing data.

## PACKAGE C — Muscle accounting and weekly targets

| Task | Work | Expected verification |
|---|---|---|
| C01 | Implement expanded muscle taxonomy | All required groups distinct |
| C02 | Implement physical-set counting | No duplicate total sets |
| C03 | Implement direct-muscle credits | Correct exercise-to-muscle accounting |
| C04 | Implement indirect stimulus credits | Cannot satisfy mandatory direct work |
| C05 | Implement meaningful exposure dates | Same-day sessions count as one date |
| C06 | Implement weekly target table by experience | Boundary tests for every muscle and tier |
| C07 | Implement glute-focus additional priorities | Focus changes allocation only when selected |
| C08 | Implement fatigue overlap estimation | Compound overlap accounted for |
| C09 | Replace generic tier-wide volume enforcement | New volume results match the approved table |
| C10 | Provide compatibility mapping for older DTO consumers | Existing API/client contract tests pass |

**C package exit:** correct per-muscle and per-day volume summaries for synthetic prescriptions.

## PACKAGE D — Canonical planning and dynamic split

| Task | Work | Expected verification |
|---|---|---|
| D01 | Build normalized validated generator input | Missing inputs remain explicit |
| D02 | Filter all unsafe or ineligible exercises before allocation | No excluded exercise appears |
| D03 | Implement strength-day candidate generation | 1–7 available days supported |
| D04 | Implement dynamic frequency decision | Availability does not equal frequency |
| D05 | Implement Full Body, Upper/Lower and hybrid candidate evaluation | Split responds to volume and time |
| D06 | Allocate meaningful priority-muscle exposure | Priority deficits tracked |
| D07 | Allocate knee-dominant, knee-flexion and hip-extension patterns | Three-pattern weekly tests |
| D08 | Add direct calves, rear delts and core requirements | Verified targeted exposure |
| D09 | Add conditional glute abduction and optional adductors | Correct goal-dependent selection |
| D10 | Add two-week movement variety | Stable history-aware choices |
| D11 | Add deterministic exercise ordering | Same inputs yield same order |
| D12 | Add explicit tie-break rules | Candidate order independent of collection enumeration |
| D13 | Add safe one-day and best-partial behavior | No arbitrary one-day block |
| D14 | Emit complete limitation and advisory codes | No silent requirement shortfall |

**D package exit:** the Core engine generates valid, deterministic weekly strength prescriptions with honest coverage.

## PACKAGE E — Warm-up, time and supersets

| Task | Work | Expected verification |
|---|---|---|
| E01 | Model general warm-up | Represented in prescription |
| E02 | Model exercise-specific warm-up sets | Excluded from working volume |
| E03 | Implement execution-time estimator | Rep/tempo/duration logic tested |
| E04 | Implement rest accounting | No double counting after final set |
| E05 | Implement unilateral execution duration | Both sides included |
| E06 | Implement transitions and equipment setup | No omitted setup or duplicate transitions |
| E07 | Implement compatible supersets | Eligibility tests pass |
| E08 | Implement superset-specific rest calculation | Time reduction without fabricated rest |
| E09 | Implement reordering optimization | Improves fit without violating priority |
| E10 | Implement redistribution across available days | Completed history remains untouched |
| E11 | Implement optional-volume reduction | Core work protected |
| E12 | Implement best safe partial output | All gaps disclosed and time respected |

**E package exit:** every generated session has a defensible estimated duration and correct warm-up, rest and superset structures.

## PACKAGE F — Strength progression

| Task | Work | Expected verification |
|---|---|---|
| F01 | Make actual RIR nullable end to end | Missing RIR remains null |
| F02 | Make technique assessment optional | Unknown remains unknown |
| F03 | Update progression-history builder | Valid sessions not excluded for missing RIR |
| F04 | Enforce comparable-session qualification | Different variants not mixed |
| F05 | Implement two-session increase decision | Recommendation only |
| F06 | Implement equipment-specific increments | No invented step size |
| F07 | Implement two-session reduction decision | One bad session insufficient |
| F08 | Implement initial-load calibration state | Unknown load not displayed as measured zero |
| F09 | Implement correct bodyweight added-load semantics | Genuine zero added load accepted |
| F10 | Implement unilateral performance history | Weaker-side progression honored |
| F11 | Implement plateau detection | Multiple comparable sessions required |
| F12 | Implement optional +1 glute set response | Only justified when conditions qualify |

**F package exit:** observed performance produces correct, nonforced progression advice without fabricated measurements.

## PACKAGE G — Recovery and safety

| Task | Work | Expected verification |
|---|---|---|
| G01 | Separate ordinary DOMS from injury/pain input | DOMS invariance tests pass |
| G02 | Implement optional energy/fatigue/sleep check-in | Missing optional answers allowed |
| G03 | Implement conservative readiness adjustment | Optional sets adjusted, not arbitrary load |
| G04 | Add cycle Unknown and four phase contexts | Phase-only changes preserve prescription |
| G05 | Connect actual symptoms to approved adaptation rules | Symptoms handled without blanket assumptions |
| G06 | Implement local movement restrictions | Provoking candidates removed |
| G07 | Integrate reviewed suitable substitutions | No unsafe or unreviewed replacement |
| G08 | Implement serious-warning symptom stop state | No ordinary workout generated in stop condition |
| G09 | Implement 72-hour reassessment scheduling | No timer-only automatic clearance |
| G10 | Implement conservative exercise return policy | History preserved; provisional reduction applied where appropriate |
| G11 | Implement observed-need Deload | Not triggered by one poor day |
| G12 | Implement post-Deload adaptive recovery | Prior progress not reset |
| G13 | Implement rolling seven-day recovery history | Calendar boundary does not erase fatigue |
| G14 | Implement missed-session replanning | No forced double workload |

**G package exit:** readiness, cycle and known restrictions are handled consistently, with clinical uncertainties explicitly guarded.

## PACKAGE H — Cardio

| Task | Work | Expected verification |
|---|---|---|
| H01 | Define running/walking/cycling/swimming prescriptions | Correct activity-specific fields |
| H02 | Schedule only selected activities | No unsolicited activities |
| H03 | Enforce two independent sessions daily | Third independent session rejected |
| H04 | Implement compatible finisher ≤30 minutes | Finisher stays inside strength session |
| H05 | Implement concurrent-training fatigue checks | Appropriate conflict resolution |
| H06 | Implement cardio actual logging | Incomplete but meaningful values accepted |
| H07 | Calculate pace/speed only with valid inputs | No invented derived metrics |
| H08 | Implement single-variable cardio progression | No fixed 10% rule |
| H09 | Implement RPE effort guidance | No compulsory wearable |
| H10 | Disable new HIIT generation and selection | Historical compatibility retained |

**H package exit:** selected cardio is planned, executed and progressed without interfering unnecessarily with strength goals.

## PACKAGE I — PostgreSQL, UseCases and Server integration

| Task | Work | Expected verification |
|---|---|---|
| I01 | Inspect actual schema authority and bootstrap path | Verified schema strategy documented |
| I02 | Add persisted training focus | Full profile-to-generator round trip |
| I03 | Add reviewed catalog metadata storage or mapping | Version and eligibility verified |
| I04 | Add warm-up and superset prescription representation | Save/load round trip |
| I05 | Add original/adapted prescription representation | Both versions preserved |
| I06 | Add cardio actual data contract | Save/load and unit tests pass |
| I07 | Extend preview source adapter | All required inputs loaded without guesses |
| I08 | Extend UseCase mapping to V2 Core | One canonical decision path |
| I09 | Extend preview response contract additively or through versioning | Server/mobile contract tests pass |
| I10 | Integrate transactional first-plan auto-acceptance | No extra Apply required |
| I11 | Preserve explicit replacement Apply behavior | Existing accepted plan unchanged before Apply |
| I12 | Enforce stale proposal and optimistic concurrency checks | Conflicting proposals cannot both win |
| I13 | Persist completed actual sets idempotently | Repeated save does not duplicate |
| I14 | Preserve completed sessions under replanning | Immutable performed history |
| I15 | Support offline retry reconciliation | Lost response does not duplicate |
| I16 | Update catalog/policy version validation atomically | Mismatch fails closed |
| I17 | Add structured limitation and explanation codes | API round-trip preserves meaning |
| I18 | Rehearse database evolution using disposable PostgreSQL | Fresh install and supported upgrade path verified |

**I package exit:** new engine results flow through the actual backend with correct transactions, ownership checks and persistence.

## PACKAGE J — MAUI iOS integration and UX

| Task | Work | Expected verification |
|---|---|---|
| J01 | Audit actual XAML bindings and navigation | Binding inventory complete |
| J02 | Add training-focus selection and persistence | Glute focus survives restart |
| J03 | Update workout setup missing-input UX | All missing answers listed with edit action |
| J04 | Render weekly muscle and schedule summary | Complete prescription visible |
| J05 | Render exercise sets, reps, rest and rationale | Values match server |
| J06 | Render limitation and partial-plan explanations | No false full-coverage status |
| J07 | Implement first-plan automatic activation state | No unnecessary Apply prompt |
| J08 | Implement replacement Apply consequence and confirmation | Accepted plan protected |
| J09 | Render warm-up sets separately | Not counted as working sets |
| J10 | Render superset groups and split-to-straight-sets action | No lost logs |
| J11 | Implement first-load calibration UX | Unknown is not zero |
| J12 | Make RIR and technique optional | Save without filling them |
| J13 | Implement per-side logging | Correct weight/reps and set counts |
| J14 | Implement selected cardio activity capture | Activity actuals persisted |
| J15 | Implement pain/restriction interaction | Correct safe flow |
| J16 | Implement pause/resume and partial completion | State restored after restart |
| J17 | Fix completion state agreement between Today and Train | One consistent terminal state |
| J18 | Verify EN/RU localization | No untranslated or inconsistent core copy |
| J19 | Verify VoiceOver and accessibility | Correct focus and labels |
| J20 | Run Impeccable review if installed | Findings addressed or marked unavailable |

**J package exit:** the new user-facing experience works through the real backend, not only through mocked ViewModels.

## PACKAGE K — Legacy retirement and full regression

| Task | Work | Expected verification |
|---|---|---|
| K01 | Confirm new canonical generator is the only active producer | Call graph and architecture test |
| K02 | Retire truly dead generator paths | No production references remain |
| K03 | Remove obsolete fixed-template assumptions | No hidden old coverage rules |
| K04 | Update outdated tests to approved behavior | No silent assertion weakening |
| K05 | Run full Core tests | Required suite PASS |
| K06 | Run Registration and onboarding tests | Required suite PASS |
| K07 | Run Mobile.Contracts tests | Required suite PASS |
| K08 | Run Server contract/route tests | Required suite PASS |
| K09 | Run PostgreSQL integration on disposable DBs | Required suite PASS |
| K10 | Run determinism/property tests | Required invariants PASS |
| K11 | Run complete simulator lifecycle | Real user journey PASS |
| K12 | Run EN/RU and accessibility review | No release-blocking issue |
| K13 | Check repository for obsolete developer artifacts | Clean release candidate without unrelated deletion |
| K14 | Verify GitHub Actions against exact candidate SHA | Required checks PASS |

**K package exit:** all mandatory acceptance tests and release gates are verified against the same candidate revision.

## PACKAGE L — Controlled environments and beta release

| Task | Work | Expected verification |
|---|---|---|
| L01 | Inventory Development resources | Exact account, region, IDs |
| L02 | Inventory Staging resources | Exact account, region, IDs |
| L03 | Inventory Beta resources | Exact account, region, IDs |
| L04 | Prove Production is isolated | No shared destructive target |
| L05 | Create PostgreSQL backups | Backup artifacts verified |
| L06 | Create applicable S3/user-object backups | Backup artifacts verified |
| L07 | Test restoration in isolation | Data recoverable |
| L08 | Present exact destructive action list | Separate owner approval obtained |
| L09 | Reset and deploy Development | Full smoke tests PASS |
| L10 | Reset and deploy Staging | Full integration PASS |
| L11 | Reset and deploy Beta | Full acceptance PASS |
| L12 | Verify immutable backend artifacts and Lambda aliases | Digest and deployed version agree |
| L13 | Verify rollback procedure | Recovery path demonstrated |
| L14 | Prepare TestFlight owner handoff | Owner-only actions listed |
| L15 | Publish final evidence and Beta readiness report | No critical unresolved gate |

**L package exit:** approved test environments run the verified release candidate with Production unchanged.

---

# 12. EXECUTION DEPENDENCY GRAPH

The packages are not executed simply in alphabetical order.

## Phase 1 — Baseline

A01–A07 must complete first.

No broad source changes before baseline and requirements reconciliation.

## Phase 2 — Contracts

A08–A10, B01–B05 and I01 begin as foundational design and validation tasks.

The contract and catalog metadata shape must be stabilized before engine implementation.

## Phase 3 — Catalog and accounting

Complete B's active-catalog validation alongside C.

Unreviewed new candidate content may proceed through draft/review workflows without becoming active.

## Phase 4 — Core planning

Complete D, depending on C and the reviewed catalog foundation.

D must not depend on UI or PostgreSQL.

## Phase 5 — Optimization and training history

E, F and selected independent G tasks can progress in parallel after the relevant contracts exist.

Use separate file ownership.

## Phase 6 — Adaptation and cardio

Complete G and H using stable Core contracts.

Integrate these into the canonical generation pipeline.

## Phase 7 — Persistence and application

Complete I and then J in small vertical slices.

Prefer early end-to-end verification of one representative strength plan rather than waiting until every UI screen is rewritten.

## Phase 8 — Remove old code

Only after the V2 path is fully integrated and tested, perform K01–K04.

There must be one active production generator throughout the transition. A new implementation may exist in test-only form while the older one remains active, but simultaneous competing runtime outputs are prohibited.

## Phase 9 — Full regression

Complete K05–K14.

Any failed mandatory check blocks release readiness.

## Phase 10 — Environment rollout

Complete L01–L15 under the separate destructive-operation authorization boundary.

---

# 13. REQUIREMENT-TO-TEST MATRIX FOR GATES 49–95

The following assigns one primary regression-test identifier per numbered gate. Additional cases are required in the detailed packages.

| Gate | Primary test ID | Required assertion |
|---|---|---|
| 49 | WE2-G49 | Weekly lower-body patterns covered when feasible |
| 50 | WE2-G50 | At least two direct calf sets |
| 51 | WE2-G51 | Rear-delt direct work exists |
| 52 | WE2-G52 | Two core occasions; dynamic exercises preferred |
| 53 | WE2-G53 | No automatic neck training |
| 54 | WE2-G54 | No obligatory redundant lower-back isolation |
| 55 | WE2-G55 | Adductor work conditional |
| 56 | WE2-G56 | Glute-focused abduction included |
| 57 | WE2-G57 | Exercise ordering is not fixed to Hip Thrust |
| 58 | WE2-G58 | All tier-specific muscle targets evaluated |
| 59 | WE2-G59 | Active exercise metadata complete |
| 60 | WE2-G60 | Reviewed equipment configurations produce valid options |
| 61 | WE2-G61 | Joint weekly volume optimization |
| 62 | WE2-G62 | Scenario matrix executes successfully |
| 63 | WE2-G63 | Duration includes actual components |
| 64 | WE2-G64 | Warm-up excluded from working volume |
| 65 | WE2-G65 | Deload triggered by sustained need |
| 66 | WE2-G66 | Adaptive post-Deload return |
| 67 | WE2-G67 | Missed work doesn't trigger automatic doubling |
| 68 | WE2-G68 | Rolling fatigue crosses week boundaries |
| 69 | WE2-G69 | Completed work immutable; replacement Apply required |
| 70 | WE2-G70 | Active sessions survive interruption and retry |
| 71 | WE2-G71 | Progression works without mandatory RIR |
| 72 | WE2-G72 | Two comparable successes before increase |
| 73 | WE2-G73 | Two comparable deficits before reduction |
| 74 | WE2-G74 | Plateau changes justified by history |
| 75 | WE2-G75 | Unilateral sets, sides and loads correct |
| 76 | WE2-G76 | Only compatible supersets produced |
| 77 | WE2-G77 | Unknown working load remains unknown |
| 78 | WE2-G78 | Two independent sessions plus permitted finisher |
| 79 | WE2-G79 | Concurrent fatigue considered without false chronological rule |
| 80 | WE2-G80 | No active HIIT generation |
| 81 | WE2-G81 | Cardio actuals support incomplete measurements |
| 82 | WE2-G82 | Individual cardio progression, no universal 10% |
| 83 | WE2-G83 | RPE guidance requires no sensors |
| 84 | WE2-G84 | Cycle phase alone does not modify prescription |
| 85 | WE2-G85 | Safety overrides goals and preferences |
| 86 | WE2-G86 | Optional Check-In does not create unsafe assumptions |
| 87 | WE2-G87 | Local pain excludes provoking movement |
| 88 | WE2-G88 | Reassessment and conditional conservative return |
| 89 | WE2-G89 | DOMS-only changes leave prescription invariant |
| 90 | WE2-G90 | Poor readiness removes optional work, not arbitrary load |
| 91 | WE2-G91 | Compact optional Check-In works |
| 92 | WE2-G92 | Automatic adaptations preserve original/changed rationale |
| 93 | WE2-G93 | Normalized deterministic prescription |
| 94 | WE2-G94 | No conflicting active legacy generator |
| 95 | WE2-G95 | Environment reset guardrails and Production isolation |

These test identifiers are proposed. The test files should be organized by relevant Core, integration, contract and simulator suites, not necessarily one physical file per gate.

---

# 14. END-TO-END ACCEPTANCE SCENARIOS

## Scenario S01 — One-day beginner

**Given:** Beginner, one available strength day, full gym, adequate session duration.

**Expected:**

- Valid Full Body or best safe partial plan.
- No artificial one-day safety block.
- Meaningful priority coverage.
- No HIIT.
- Complete limitation disclosure.

## Scenario S02 — Two-day beginner

**Given:** Beginner, two nonconsecutive days.

**Expected:**

- Balanced strength training.
- Appropriate initial volume.
- Meaningful exposures for important muscle groups where feasible.
- Reasonable duration.

## Scenario S03 — Three-day glute focus

**Given:** Intermediate, glute focus, full gym, three available days.

**Expected:**

- Direct glute priority.
- At least two meaningful glute days.
- Knee flexion and knee extension included when feasible.
- Direct abduction work.
- Other major muscle groups not ignored.
- No arbitrary Hip Thrust-first rule.

## Scenario S04 — Advanced, five to seven available days

**Expected:**

- Appropriate prescribed frequency, not automatically all available days.
- Managed overlap fatigue.
- Higher training volume only when supported by history and tolerance.

## Scenario S05 — Bodyweight only

**Expected:**

- No machine exercise.
- No fictitious cable anchor.
- Safe partial output if necessary.
- Explicit reviewed-catalog gaps.

## Scenario S06 — Bands and dumbbells

**Expected:**

- Correct equipment filtering.
- No invalid substitution.
- Exercise-specific progression where measurable.

## Scenario S07 — Time constrained

**Given:** Session availability shorter than preferred prescription.

**Expected:**

- Reordering and compatible supersets considered.
- Accessories reduced before important work.
- Result does not exceed time.
- Missing work disclosed.

## Scenario S08 — Poor sleep, no injury

**Expected:**

- Possible optional-set adjustment.
- No automatic arbitrary working-load reduction.
- No full-week Deload from one night.

## Scenario S09 — DOMS only

**Expected:** Identical prescription to the otherwise identical no-DOMS input.

## Scenario S10 — New local pain

**Expected:**

- Provoking movements excluded.
- Reviewed substitute when appropriate.
- Other safe training preserved.
- No claim of medical clearance.

## Scenario S11 — Serious warning symptoms

**Expected:** Workout stopped or withheld, with appropriate next-step guidance.

## Scenario S12 — Missing RIR

**Expected:**

- Actual set saved.
- RIR remains null.
- Progression history remains usable.
- No invented RIR.

## Scenario S13 — Two successful exposures

**Expected:** Recommendation for the next practical load, not compulsory modification.

## Scenario S14 — Two inadequate exposures

**Expected:** Appropriate reduction recommendation after checking comparability.

## Scenario S15 — New machine variant

**Expected:** Separate load calibration; no copied weight from another machine.

## Scenario S16 — Two sessions on one day

**Expected:** Maximum two independent sessions; meaningful muscular frequency counted by distinct dates.

## Scenario S17 — Strength plus cardio finisher

**Expected:** Finisher remains attached, ≤30 minutes, does not create third independent activity.

## Scenario S18 — Interrupted workout

**Expected:** Previously completed sets survive restart and synchronize once.

## Scenario S19 — Duplicate completion request

**Expected:** Exactly one terminal completion and one persisted set collection.

## Scenario S20 — Existing plan regenerated

**Expected:** Current accepted plan unchanged until Apply.

## Scenario S21 — Initial safe reduced-volume plan

**Expected:** Automatically accepted without the outdated extra acknowledgment.

## Scenario S22 — Repeated identical generation

**Expected:** Identical normalized prescription and stable reasoning codes.

## Scenario S23 — Policy/catalog version conflict

**Expected:** Invalid or stale proposal, not silently accepted.

## Scenario S24 — Week boundary

**Expected:** Monday training considers applicable workload from the preceding week.

## Scenario S25 — Cycle-phase-only change

**Expected:** No independent prescription change.

## Scenario S26 — Full iOS user journey

**Expected:**

Registration → onboarding → training focus → availability → plan → workout → actual sets → completion → history → progression → next plan.

The same backend-persisted state must appear consistently across application pages.

---

# 15. LOW-REASONING AGENT TASK TEMPLATE

Every coding assignment generated from this plan must use the following exact structure.

## TASK CARD

**Task ID:** `[package]-[number]`

**Objective:** One testable behavior.

**Requirement references:** Exact REQ IDs and gates.

**Prerequisites:** Completed task IDs.

**Current-state source files:** Only verified paths.

**Allowed production files:** Explicit list.

**Allowed test files:** Explicit list.

**Do not modify:** Other components, shared infrastructure, unrelated worktree changes.

### Required implementation

A numbered sequence of precise code changes, including contract signatures or fields where relevant.

### Mandatory tests

For each case provide:

- Input fixture.
- Expected output.
- Expected invariants.
- Negative cases.
- Boundary cases.

### Execution process

1. Inspect allowed source.
2. Add one targeted failing test.
3. Execute the targeted test.
4. Confirm it fails because the behavior is missing.
5. Implement the minimum required change.
6. Execute the targeted test again.
7. Run the affected suite.
8. Record actual output.
9. Request independent review.
10. Address review findings.
11. Make a focused commit.
12. Update `STATUS.md` according to repository policy.

### Stop conditions

Stop and escalate if:

- A required API or model contradicts the approved contract.
- Another agent owns a necessary file.
- Unreviewed clinical or exercise metadata would be needed.
- A destructive operation is required.
- A failing pre-existing test makes new results uninterpretable.
- The user must approve a genuinely unresolved product decision.

Do not work around these by inventing values or disabling tests.

### Required final report

- Changed files.
- Test commands and actual output.
- Acceptance assertions.
- Reviewer result.
- Commit SHA.
- What remains unverified.
- Next dependency.

---

# 16. MODEL ROUTING AND PARALLEL ORCHESTRATION

The coordinator should use the OpenAI models actually supported by the installed Codex environment.

Suggested allocation:

| Work type | Reasoning requirement |
|---|---|
| Overall architecture and conflicting requirements | High |
| Core algorithm design | Medium–High |
| Cross-layer contract changes | Medium–High |
| Narrow C# implementation | Low–Medium |
| Mechanical DTO and mapping changes | Low |
| Test-fixture construction | Low |
| Catalog identity checks | Low |
| Independent architecture review | High |
| MAUI interaction debugging | Medium–High |
| Security and destructive release review | High |

Model names and effort levels must be discovered from actual availability. Do not hardcode unavailable model IDs or invent per-agent selection functionality.

### Parallel-work rules

The orchestrator may run two or three independent agents concurrently when their file ownership does not overlap.

Examples:

- Catalog metadata audit and baseline test analysis.
- Core muscle accounting and independent frontend contract review.
- Cardio Core rules and unrelated localization review.

Avoid concurrent edits to:

- Shared DTO files.
- `StrengthPlanPreviewRule.cs`.
- `MauiProgram.cs`.
- Database schema authority.
- `ApiApplication.cs`.
- The same test class.
- `STATUS.md`.

The orchestrator integrates each accepted contribution sequentially.

### Context economy

Each subagent receives:

- The task card.
- Relevant source excerpts.
- Required contract definitions.
- Exact tests.
- Applicable repository rules.

Do not paste the entire repository or this entire blueprint into every low-reasoning agent.

Use concise state checkpoints containing:

- Branch/HEAD.
- Completed tasks.
- Open task.
- Dirty paths.
- Test evidence.
- Unresolved blockers.
- Next action.

Use the repository's permitted status mechanism rather than proliferating new dated handoff files.

---

# 17. CODE REVIEW AND COMMIT POLICY

Every confirmed logical change receives its own focused commit once the required checks pass.

Recommended commit boundaries:

- One new domain invariant.
- One contract addition with matching tests.
- One Core algorithm step with tests.
- One persistence integration with tests.
- One complete vertical UI behavior with contract tests.
- One controlled compatibility/legacy removal.
- One independently verified infrastructure change.

Do not create a giant multi-feature commit.

Do not commit unrelated dirty files.

Do not rewrite history.

Do not merge an unverified change simply to keep the orchestrator moving.

The independent reviewer must verify:

1. Approved behavior is implemented.
2. No hidden unsupported default was introduced.
3. Safety ordering remains correct.
4. All required tests exist.
5. No existing tests were weakened.
6. No unrelated file was modified.
7. No unused competing generator path was introduced.
8. The change respects architecture boundaries.
9. Localization/API/XAML compatibility is preserved where relevant.
10. The commit has actual verification evidence.

---

# 18. CI/CD VERIFICATION

The inspected repository documents these GitHub Actions workflow names:

- `ci.yml`
- `db-verify.yml`
- `backend-build.yml`
- `backend-release.yml`
- `db-release.yml`
- `mobile-release.yml`

Verify their actual local contents before editing.

## Required checks

### Build and architecture

- Full solution restore.
- Relevant project build.
- Dependency-direction tests.
- No duplicate generator authority.

### Core

- All muscle accounting tests.
- Scheduling and split tests.
- Optimization tests.
- Progression tests.
- Safety tests.
- Determinism tests.

### Contracts and services

- HTTP route parity.
- Authentication and authorization.
- Mobile DTO compatibility.
- Serialization of optional values.
- Plan versioning and acceptance.

### Database

- Fresh schema provisioning on disposable database.
- Supported schema evolution rehearsal.
- Review snapshot validation.
- Idempotency tests.
- Concurrency tests.

### MAUI

- iOS build.
- Simulator launch.
- Screen workflows.
- Workout logging.
- Session resume and completion.
- Localization.
- Accessibility.

### Deployment

- Exact package digest verification.
- Expected Lambda version and alias.
- Correct environment.
- Compatible schema.
- Successful smoke tests.
- Documented rollback.

The result of an old CI run cannot be reused as current verification evidence.

---

# 19. DEVELOPMENT, STAGING AND BETA RESET RUNBOOK

## 19.1 Approved target scope

- Development.
- Staging.
- Beta.

Production is outside the scope.

## 19.2 Preflight resource inventory

For each environment record:

- AWS account identifier.
- Region.
- PostgreSQL database/server identifier.
- S3 bucket identifiers.
- Authentication-provider dependencies.
- Lambda function names and aliases.
- Worker services.
- Secrets/credential references, without revealing secret values.
- Deployment artifact locations.
- Environment-specific URLs.
- Any unexpectedly shared resources.

An environment label alone is not sufficient identification for deletion.

## 19.3 Backup

Create and verify:

- Appropriate PostgreSQL snapshot or database backup.
- User-object and media backup where applicable.
- Relevant identity-data recovery strategy.
- Configuration needed to reconstruct the environment.

## 19.4 Restore drill

Before deletion:

1. Restore into isolated resources.
2. Verify expected structure.
3. Verify representative data.
4. Check ownership and authorization behavior.
5. Record success or failure.
6. Confirm no Production dependence.

A backup file existing on disk is not proof it can be restored.

## 19.5 Destructive approval

Present the exact resource/action table to the owner.

For every resource:

- Exact identifier.
- Environment.
- Intended operation.
- Backup reference.
- Restore verification result.
- Rebuild strategy.
- Rollback implications.

Do not execute deletion until specific authorization is obtained.

## 19.6 Rollout order

**Development → Staging → Beta.**

Each environment must pass its required smoke and integration checks before proceeding to the next.

If Staging fails, Beta remains untouched.

## 19.7 Production exclusion

No Production:

- Deletion.
- Schema change.
- Data reset.
- Catalog reseeding.
- Backend deployment.
- Alias movement.
- Secret rotation.

TestFlight and App Store Connect actions remain owner-only.

---

# 20. BETA READINESS DEFINITION OF DONE

The project is ready for Beta only when all mandatory conditions are met.

## Product

- Valid one-day through multi-day planning.
- Correct muscle volume and coverage.
- Dynamic exercise selection.
- Correct training-style prescriptions.
- No unsafe substitutions.
- Accurate enough duration estimates according to the approved verification tolerance.
- Correct optional RIR behavior.
- Correct load progression.
- Correct Deload and missed-session handling.
- DOMS invariance.
- Correct menstrual symptom handling.
- Ordinary cardio support.
- No active HIIT generation.
- Deterministic output.

## Lifecycle

- New registration works.
- Onboarding works.
- First plan automatically applies.
- Existing plan replacement requires Apply.
- Workout starts.
- Working sets save.
- Warm-up sets remain separate.
- Paused sessions resume.
- Partial completion works.
- Offline retries do not duplicate data.
- Completed sessions appear consistently on Today and Train.
- History feeds future progression.

## Technical

- One canonical production generator.
- No hidden legacy fallback.
- Catalog review versions match activated policy.
- All mandatory automated suites PASS.
- Relevant PostgreSQL integration PASS.
- Relevant MAUI simulator tests PASS.
- EN/RU and accessibility checks completed.
- Required GitHub Actions PASS.
- Release artifacts match verified candidate.
- Database schema and API are compatible.
- No critical unresolved safety or data-integrity defect.

## Environment

- Dev reset separately authorized and verified.
- Staging reset separately authorized and verified.
- Beta reset separately authorized and verified.
- Production unchanged.
- Rollback readiness established.
- Owner has exact TestFlight instructions.

---

# 21. FINAL ORCHESTRATOR REPORT FORMAT

At the end, provide one consolidated report containing:

### A. Repository state

- Branch.
- HEAD.
- Upstream.
- Final commits.
- PR status.
- Remaining dirty files.

### B. Implementation

For every package:

- Tasks completed.
- Tasks not completed.
- Files changed.
- Relevant requirements.
- Final state.

### C. Verification

For every mandatory test suite:

- Exact command.
- Actual output.
- PASS/FAIL/BLOCKED.
- Evidence.

### D. Product acceptance

- Gate 49–95 status.
- Earlier requirement status.
- Outstanding exercise-review dependencies.
- Any unresolved clinical-safety decision.

### E. Application verification

- Simulator device/runtime.
- Registration result.
- Onboarding result.
- Workout generation result.
- Actual workout completion result.
- History/progression result.
- EN/RU result.

### F. Deployment

- Development state.
- Staging state.
- Beta state.
- Artifact identifiers.
- Schema version.
- Backup/restore evidence.
- Production noninterference confirmation.
- Owner-only actions.

### G. Final verdict

Only one:

- `BETA READY`
- `NOT READY — BLOCKED`
- `NOT READY — FAILED VERIFICATION`

No partial implementation should be described as a fully completed Beta release.

---

# 22. FIRST EXECUTION BATCH

The following is the precise first batch to delegate after the owner approves the blueprint.

## Batch 1 — Documentation and baseline

**Executor 1:**

- Read the exact checkout's repository instructions.
- Record HEAD and worktree.
- Run baseline Core tests.
- Record the real baseline and pre-existing failures.
- Do not modify production source.

**Executor 2:**

- Inspect the actual production generator call graph.
- Identify every active and dead generator.
- Record precise referenced files and composition registrations.
- Produce a small code ownership inventory.

**Lead Orchestrator:**

- Reconcile findings.
- Confirm target contract.
- Identify any unresolved owner decisions.
- Update the authoritative implementation plan.
- Commit verified documentation only under repository policy.

## Batch 2 — Foundational contracts

**Executor 1:**

- Implement the reviewed exercise-contribution model with targeted tests.
- Do not modify planner selection behavior yet.

**Executor 2:**

- Implement the versioned weekly-volume target configuration with tests.
- Do not modify persistence or UI.

**Lead Orchestrator:**

- Review both sets of changes.
- Validate that Core contracts can be combined.
- Merge through the permitted PR workflow.

## Batch 3 — Canonical planning foundation

**Executor 1:**

- Implement direct/indirect muscle accounting.
- Add meaningful-date tests.

**Executor 2:**

- Design and test normalized generation input/output.
- Keep generation free of database, random and clock dependencies.

**Lead Orchestrator:**

- Review the combined interfaces.
- Establish the next exercise-selection task cards.

**Do not start broad MAUI rewrites or environment resets during these first batches.**

---

# 23. FINAL DELIVERY STANDARD

The ultimate deliverable is not simply a new class called WorkoutEngineV2.

It is a complete integrated application behavior:

**User preferences → validated training inputs → canonical weekly plan → reviewed exercises → feasible session → accepted prescription → logged performance → history → personalized progression → next plan.**

Every arrow represents a real verified contract and a required test boundary.

If a lower-reasoning agent cannot complete a task without inventing a product decision, the task must be split further or clarified by the lead.

If the catalog lacks reviewed exercise content, the code should guard the gap and produce review-ready records, but the unsupported coverage requirement must remain BLOCKED rather than falsely PASS.

If tests fail, the corresponding package remains open.

If the app builds but the simulator flow fails, the corresponding package remains open.

If the backend works but the UI is not connected, the corresponding package remains open.

If the release candidate passes locally but Beta has not been deployed or verified, Beta readiness remains unconfirmed.

**The entire project is complete only when the approved requirements, their implementation, their tests and their integration evidence agree.**

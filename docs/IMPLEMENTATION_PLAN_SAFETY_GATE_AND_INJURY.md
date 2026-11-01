# Implementation Plan — Cycle/Readiness Approval Gate + Injury Safety Questions

Status: implemented in the working tree on 2026-11-01. This document is the design record; current
activation and verification state is in `docs/HANDOFF_2026-11-01_GATE_ACTIVATION.md` and `STATUS.md`.

Owner decision 2026-11-01: keep the gate active under the provisional record and state in the product
policies that the guidance is **not clinically approved**. The Russian wording is owner-approved as
shipped; neither decision claims a clinical or native-language review.
Authority: `docs/AGENT_PROJECT_BRIEF.md` rules, owner decision recorded in
`docs/KINESIOLOGIST_POLICY_REVIEW_CHECKLIST.md`. Source baseline `8c98c78e` worktree, audit dated 2026-11-01.

Neither item is a clinical review. Both exist so the product fails closed when evidence or human
authorisation is missing, per brief §1.1 rule 2.

---

## 0. Why these two are one plan

They are separate controls that meet at exactly one seam: `hasSafetyConcern` in
`src/MuscleCuties.Infrastructure.PostgreSql/Workout/ActiveSession/PostgresCycleReadinessPrescription.cs:125-132`.

| Control | Guards | Question it answers | Current state |
| --- | --- | --- | --- |
| **A — approval gate** | The *reduction* path | "Is a human allowed to change this dose automatically?" | Gate deleted; `PlanningReviewApproval` orphaned; `CycleReadinessPolicyReviewMissing` has no producer |
| **B — injury answers** | The *withhold* path | "Do we know enough about the injury to keep planning?" | Questions absent; only symptoms and pain-change dates feed the flag |

Building B alone gives the system a richer safety trigger but no authorisation discipline. Building A
alone restores compliance but leaves the trigger starved — a user can be at pain 9/10 with a clinician
telling them not to load the joint, and nothing asks. Merged, B supplies the input that makes A's
withhold branch meaningful, and A guarantees B's consequences were authorised.

**One pipeline, two independent vetoes.** Safety always outranks policy, so a user never sees
"approval missing" when what they actually need is "stop and seek care".

---

## 1. Control A — specification

### 1.1 Precedence in `CycleReadinessPrescriptionRule.Evaluate`

Order is part of the contract and must be tested explicitly:

1. **Invalid input** (null, empty prescription, `Sets < MinimumWorkingSets`, `OptionalSets >= Sets`,
   empty `PrescriptionId`) → `SafetyReviewRequired`. Unchanged.
2. **Safety concern** (`HasNewPainOrSafetyConcern`) → `SafetyReviewRequired`. Unchanged.
3. **No usable approval** → `CycleReadinessPolicyReviewMissing`. New, returns no adjustment.
4. **Evidence evaluation** → any of the four adjustment reason codes. Unchanged logic.

Step 3 sits after step 2 deliberately: a missing approval must never mask a safety stop.

### 1.2 Approval predicate

An approval authorises an adjustment only when all hold:

- `Approval is not null`
- `Approval.RulesetVersion == TodayCheckInScoreRule.RulesetVersion` (scope: the ruleset the score came from)
- `Approval.ReviewedTrainingPolicyVersion == CycleReadinessPrescriptionRule.PolicyVersion` (`"MC-CYCLE-READINESS-0.2"`)
- `Approval.CoveredSections` contains **both** `PlanningReviewSection.TrainingPrescriptions` and
  `PlanningReviewSection.AutomatedProgression`
- `Approval.ApprovedAtUtc != default`
- `!string.IsNullOrWhiteSpace(Approval.ApprovalReference)`

Anything less yields `PolicyReviewMissing`. `PlanningReviewSection.SymptomStopLogic` and
`InjurySpecificSubstitutions` are **not** required here; they are ordinary workouts and injury
substitutions, not automatic dose changes. Record that reasoning in the rule's XML doc so the next
reader does not "fix" it.

Both required members already exist on `PlanningReviewApproval`
(`src/MuscleCuties.Core/Domain/Workout/Planning/TodayCheckInScoreRule.cs:31-36`). No new Core type.

### 1.3 Approval source

There is no approval record store and no call site supplying one (verified: `ApprovalReference`,
`CoveredSections`, `ReviewedTrainingPolicyVersion` appear only in the type declaration). Introduce a
single trusted release constant class, parallel to existing release configuration:

`src/MuscleCuties.Core/Domain/Workout/Planning/PlanningReleaseApproval.cs`

- `public static readonly PlanningReviewApproval? CycleReadinessPrescription` — `null` until the owner
  supplies a real `ApprovalReference` in Phase 0. Null is the correct shipping value: the product stays
  withheld, which is the documented state.
- Descriptive constant for the reference string; no magic literals, per brief §3.

`PostgresCycleReadinessReconciler.cs` passes `PlanningReleaseApproval.CycleReadinessPrescription` into
`ApplyAsync`, which forwards it on `CycleReadinessPrescriptionInput.Approval`.

Because the field is a `static readonly` set at build time, flipping it to `null` is a one-line revert.

### 1.4 Core surface changes

`CycleReadinessPrescriptionInput` — append trailing optional parameter:

```csharp
PlanningReviewApproval? Approval = null
```

Trailing-optional keeps all 17 existing test constructions in
`tests/MuscleCuties.Core.Tests/CycleReadinessPrescriptionRuleTests.cs` source-compatible.

`CycleReadinessPrescriptionRule` — add:

```csharp
public const string PolicyReviewMissing = "CycleReadinessPolicyReviewMissing";
```

This is the exact code the checklist cites, so no new vocabulary enters the policy record.

### 1.5 Observable consequence

Every automatic optional-set reduction stops until an approval exists. That is the intended effect and
it invalidates any test asserting a reduction without an approval fixture — a state the policy never
permitted.

---

## 2. Control B — specification

### 2.1 Answers

Two questions at injury capture, both closed-set, no free text:

| Field | Type | Values |
| --- | --- | --- |
| Worsening movements | `InjuryWorseningTrigger` (new enum) | `NotAnswered = 0`, `None = 1`, `OverheadReach = 2`, `DeepKneeFlexion = 3`, `SpinalLoading = 4`, `ImpactAndGait = 5`, `GripAndWristLoad = 6` |
| Clinician restriction | `bool?` | `null` unanswered, `false`, `true` |

Rationale for closed sets over free text: deterministic substitution selection from
`WorkoutInjurySite` + trigger, no unbounded user text entering logs or sync payloads (brief §3 "zero
sensitive traces"), no translation of user prose, and a stable column set for the model-first schema.
Value `0` (`NotAnswered`) is distinct from `None` so "didn't say" never reads as "getting better".

### 2.2 Effect

- `WorseningMovements != NotAnswered && != None` **or** `ClinicianAdvisedAvoid == true`
  → `HasNewPainOrSafetyConcern = true` for that planning day
  → `CycleReadinessSafetyRequired` outcome, no adjustment, plus stop/seek-care guidance in the UI.
- `None` / `false` → no change to planning. Answers are recorded evidence, not severity scores.
- Answers never trigger a dose *reduction*; they only withhold. Safety does not negotiate.

### 2.3 Wording (owner-supplied, Phase 0)

Keys, following the existing `Injury_Log_*` convention:

- `Injury_Log_Safety_StopAndSeekCare` — shown when either answer is affirmative.
- `Injury_Log_Safety_ClinicianRestriction` — shown when `ClinicianAdvisedAvoid == true`.
- `Injury_Log_WorseningTrigger_Label`, `Injury_Log_ClinicianAdvisedAvoid_Label`, plus one label per enum value.

Both English (`UiTextCatalog.CycleWorkout.cs`) and Russian (`RussianUiText.CycleWorkout.cs`) are
required; the catalog is bilingual and a missing key falls back, which is a silent defect. Russian
wording needs owner sign-off, not machine translation.

---

## 3. Phase 0 — decisions required before code (blocking)

| # | Decision | Recommendation |
| --- | --- | --- |
| 1 | Exact `ApprovalReference` string, `ApprovedAtUtc`, and who signs | Keep `null` (withheld) until a named person signs; the gate is then a one-line flip |
| 2 | Exact stop / seek-care wording, EN + RU | Owner text; must say stop training and seek professional care, and must not imply diagnosis |
| 3 | Is a clinician restriction a permanent block or until the injury is marked cleared? | Until cleared, re-asked at each edit; a permanent flag would strand users |
| 4 | Does `WorseningMovements` route to the same substitution engine as `WorkoutInjurySite`? | Yes — map trigger → blocked movement patterns, reuse existing substitution path; no new planner |
| 5 | Do the answers appear in history / export / deletion paths? | Include in account purge; exclude from any diagnostic log |

Phase 1 can start on decisions 1, 3, 4 with `null`/withheld defaults; wording (2) gates Phase 4 only.

---

## 3.1 Hard product rule — always applicable, always strength (owner, 2026-11-01)

Binding for both controls and for every path that alters a plan.

> A workout plan must always be applicable, and even with an injury it must still be strength training.

### Current code status (measured)

**Already correct:** rehab is *additive*, not substitution. `ExercisePickerRule.FindRehabExercises` caps at
2 exercises (`RehabExerciseLimit`), 2 sets of 12-15 at RPE 5, and `PrescribeSession` adds them
**alongside** the ordered strength slots rather than replacing them. Nothing in the injury path converts a
session into rehab-only, and `InjurySafetyProgressionRule.StatusAutoRehabBlocked` blocks automatic rehab
rather than strength.

**Not enforced:** there is no strength floor. `FilterCandidates` drops every contraindicated exercise,
`SelectForSlot` returns `null` when a slot has no eligible candidate, `InjuryPrescriptionCandidateRule`
returns `Omitted` when no safe candidate is verified, and `PrescribeSession` returns `null` outright when
`candidates.Count == 0`. No guard in `src/MuscleCuties.Core/Domain/Workout/Planning/` asserts that a
session or plan retains at least one strength exercise, and no test covers it.

### Enforcement design

- New Core rule `src/MuscleCuties.Core/Domain/Workout/Planning/StrengthPlanContinuityRule.cs`
  - `EnsureApplicable(session)` — every emitted session must contain at least one strength exercise and at
    least `MinimumWorkingSets` total sets; otherwise the session is not emitted.
  - Fallback chain, in order, each step still strength: same slot alternative → same movement pattern with
    different equipment (bodyweight counts) → same muscle group different pattern → minimum viable strength
    block (one exercise, reduced dose bounded by `WorkoutGatingRule`'s set multiplier and RPE cap).
  - Reason constants (no literals): `StrengthPreserved`,
    `StrengthReplacedByInvoiceSafeAlternative`, `SessionWithheldNoApplicableStrength`.
- Terminal case: when no strength movement is permissible at all, **withhold the session** with the injury
  review reason — never emit a rehab-only or mobility-only day. Applicability means "never ask the user to
  do something unsafe or impossible", not "always emit something".
- Rehab remains capped and additive (`RehabExerciseLimit` unchanged); it never satisfies the strength floor.
- Hook the check at the planner boundary where `PrescribeSession` results are consumed, so a `null` or
  zero-strength session surfaces as a withhold reason instead of a blank or rehab-only day.

### Tests

- Active injury with every primary-slot exercise contraindicated → a strength alternative is substituted and
  the session keeps ≥1 strength exercise.
- Active injury with no safe candidate in one slot but safe work elsewhere → session emitted, slot omitted,
  strength floor still met.
- Active injury where nothing is permissible → session withheld with
  `SessionWithheldNoApplicableStrength`, and no rehab-only session is produced.
- Empty candidate list → never yields a plan containing zero strength exercises.
- Regression: uninjured plan composition unchanged (exact exercise-set equality on the existing fixtures).

---

## 4. Phase 1 — Core (no schema, no UI)

Files:

- `src/MuscleCuties.Core/Domain/Workout/Planning/CycleReadinessPrescriptionRule.cs`
  — `PolicyReviewMissing` const, `Approval` on the input record, gate insert at precedence 3, XML doc
  explaining the two required sections and why safety precedes policy.
- `src/MuscleCuties.Core/Domain/Workout/Planning/PlanningReleaseApproval.cs` (new)
  — `static readonly PlanningReviewApproval? CycleReadinessPrescription = null;`
- `src/MuscleCuties.Core/Domain/Workout/InjuryWorseningTrigger.cs` (new enum)
- `src/MuscleCuties.Core/Domain/Workout/InjuryObservationRule.cs`
  — extend `IsValid` with `InjuryWorseningTrigger` and `bool?`; add `TryNormalizeWorseningTrigger`
  mirroring `TryNormalizeStatus`; keep the existing pain/site/date checks untouched.

Tests, `tests/MuscleCuties.Core.Tests/CycleReadinessPrescriptionRuleTests.cs` (17 existing):

- Add: null approval → `PolicyReviewMissing`; mismatched ruleset → `PolicyReviewMissing`; missing
  `TrainingPrescriptions` → `PolicyReviewMissing`; missing `AutomatedProgression` → `PolicyReviewMissing`;
  blank reference → `PolicyReviewMissing`; complete approval + low readiness → `DailyReadinessAdjustment`
  (this is the fixture earlier tests need).
- Add: safety concern **with** valid approval → still `SafetyReviewRequired` (precedence proof).
- Update: any test expecting a reduction now supplies the fixture approval.

New file `tests/MuscleCuties.Core.Tests/InjuryObservationRuleTests.cs` (or extend the existing one):

- valid trigger accepted; unknown value rejected; `null` clinician accepted; pain 0-10 and site mask
  behaviour unchanged (regression); `since > date` still rejected.

Gate: `dotnet test tests/MuscleCuties.Core.Tests` green.

---

## 5. Phase 2 — Persistence and schema (the only fingerprint-changing phase)

Model-first: the EF model is the schema authority; `security.sql` owns roles and RLS; `dbinit` creates and
seeds; `dbcheck` detects drift.

- `src/MuscleCuties.Infrastructure.PostgreSql/Models/App/AppRepoModels.cs:265-275`
  (`WorkoutInjuryLogRecord`) — add:
  ```csharp
  public string WorseningTrigger { get; set; } = "NotAnswered";
  public bool? ClinicianAdvisedAvoid { get; set; }
  ```
  Stored as its enum name, matching how `Status` is stored. Default `NotAnswered` keeps pre-existing rows
  honest instead of implying "not worsening".
- `src/MuscleCuties.Infrastructure.PostgreSql/Configurations/App/AppConfigurations.cs:403-406`
  — `WorkoutInjuryLogConfiguration` grows from a one-line `ConfigureTenant` call to a body adding
  `HasMaxLength` for `WorseningTrigger` (snake_case is automatic via `ConfigureTenant`; `bool?` needs no
  config). Do **not** add a migration — there are none by design.
- Additive columns: existing row-level policies are row predicates, not column lists, so
  `security.sql` needs no edit. Confirm during verification rather than assuming.
- Rebaseline: `dbinit` on a clean PostgreSQL 17 produces a new `model_fingerprint`. Record it in
  `STATUS.md` and hand the new `schema_fingerprint_sha256` to the owner for the external deploy policy
  file, which already requires an edit for unrelated reasons. **This phase cannot ship to an environment
  whose policy file pins the old fingerprint.**
- Stores: `PostgresWorkoutInjuryStore` and `PostgresCheckInAndInjuryStore` map the two fields on insert
  and on read; `PostgresAccountPurgeStore` already deletes by user, verify the new columns need no
  special handling (they do not — same row).

Tests: integration coverage for round-trip of both fields and for `NotAnswered` default on legacy rows,
following the existing registration-integration patterns and their `MC_*_POSTGRES` env vars.

---

## 6. Phase 3 — UseCases and Server

- `src/MuscleCuties.UseCases/Features/Workout/Injury/Contracts/WorkoutInjuryContracts.cs`
  — `SaveWorkoutInjuryInput` and `WorkoutInjuryLogDto` gain both fields as **trailing optional**
  parameters; `WorkoutInjuryImpactDto` optionally gains a `SafetyMessageKey` so the UI does not branch on
  booleans.
- `src/MuscleCuties.UseCases/Features/CheckInAndInjury/CheckInAndInjuryContracts.cs`
  — `InjuryObservationRecord` and `SaveInjuryObservationRequest` likewise.
- `WorkoutInjuryUseCase`, `CheckInAndInjuryUseCase` — pass through and validate via
  `InjuryObservationRule`; no business logic in the use case (brief §2).
- `services/MuscleCuties.Server/CheckInAndInjury/CheckInAndInjuryEndpointMappings.cs` and the workout
  injury endpoints — no new routes; only the payload widens. Reject unknown trigger values with 400
  rather than coercing.
- `src/MuscleCuties.Infrastructure/Cloud/CloudDailyCheckInAndCycleStores.cs` — same fields in the cloud
  projection so device and server agree.

Gate: Server project builds; existing tests green.

---

## 7. Phase 4 — UI and localization

- `src/MuscleCuties.UI/ViewModels/Workout/InjuryLogViewModel.cs` (506 lines)
  — add `[ObservableProperty]` for `NewWorseningTrigger` and `NewClinicianAdvisedAvoid`; include them in
  the save payload and in `BeginAdd` reset; surface `SafetyMessageKey` when either is set.
- `src/MuscleCuties.UI/Controls/Injury/InjuryLogContent.xaml` — a `Picker` bound to
  `NewWorseningTrigger` and a `Switch` bound to `NewClinicianAdvisedAvoid`, each with
  `SemanticProperties.Description`, matching the existing `LocalizedStringConverter` usage.
- `src/MuscleCuties.UI/Services/Localization/UiTextCatalog.CycleWorkout.cs` and
  `RussianUiText.CycleWorkout.cs` — the keys from §2.3, both languages, naming per `Injury_Log_*`.
- Bound properties are XAML runtime contracts: after adding, grep every `.xaml` for the new names before
  any later rename (brief §4.2).

Gate: iOS build with `-p:EnableCodeSigning=false` (the keychain has no signing identity, an environment
limitation, not a code failure) plus a manual read of the modal for layout overflow.

---

## 8. Phase 5 — Verification

| Check | Command | Expected |
| --- | --- | --- |
| Core unit | `dotnet test tests/MuscleCuties.Core.Tests` | all green, including precedence tests |
| Architecture | `dotnet test tests/MuscleCuties.Core.Tests --filter Boundary` | no outer imports added to Core |
| Server | `dotnet test tests/MuscleCuties.Server.Tests` | green |
| Schema create + seed | `DbMaintenance dbinit` on clean PG17 | `compatible`, new fingerprint, `security_statements_applied` unchanged at 300 |
| Drift | `DbMaintenance dbcheck` | compatible; `model_fingerprint_mismatch` absent |
| Integration | `MC_SCHEMA_COMPATIBILITY_POSTGRES`, `MC_REGISTRATION_WRITE_TEST_POSTGRES`, `MC_REGISTRATION_TEST_POSTGRES`, `MC_RETRY_TEST_POSTGRES`, `MC_PG09_LOAD_POSTGRES` | new round-trip tests pass; the 6 known fixture failures (`Expected "mc_pg09_disposable"`, `Actual "mc_test_registration"`) must not grow |
| iOS | `dotnet build src/MuscleCuties.UI -c Release -p:EnableCodeSigning=false` | succeeds |
| No-Android | — | do not build Android/Mac Catalyst/Windows (brief §4) |

Manual policy verification, since no test can assert authorisation reality:

1. With `PlanningReleaseApproval... = null`, a low-readiness day produces `CycleReadinessPolicyReviewMissing`
   and sets are untouched.
2. With a complete approval fixture, the same day produces `DailyReadinessOptionalSetReduced`.
3. With a valid approval **and** a worsening answer, the result is `CycleReadinessSafetyReviewRequired` and
   sets are untouched — safety wins.

---

## 9. Documentation to update in the same change

- `STATUS.md` — audit entry already records the regression; append the outcome and the new fingerprint.
- `ROADMAP.md` — close the gate item, open the injury-questions follow-up if Phase 0 wording is pending.
- `docs/AGENT_PROJECT_BRIEF.md` §3 blocker 8 and §9.4 — correct both directions: the gate existed and was
  deleted, and the two conditional questions were never implemented (the brief currently claims they are).
- `docs/KINESIOLOGIST_POLICY_REVIEW_CHECKLIST.md` — move the two safety-gap rows only after Phase 4 lands.

---

## 10. Risks

| Risk | Mitigation |
| --- | --- |
| Fingerprint change outruns the external deploy policy file | Ship Phase 2 only with the policy update; note it in the PR body |
| Reduction tests fail after the gate lands | Expected; add the approval fixture rather than loosening assertions |
| Closed-set trigger misclassifies a real injury | `NotAnswered` default plus `None`; owner review of the value list in Phase 0 |
| Russian wording drifts from English | Both catalogs edited in the same commit; owner signs the Russian text |
| Safety answers leak into logs | No logging of answers; purge already deletes the row |
| Scope creep into epic 32/33 | Not touched: no new routes, no new tables, no plan-preview changes |

## 11. Out of scope

Clinical signer, professional catalog review, readiness suggestions enablement
(`ReadinessSuggestionsEnabled` stays `false`), the separately provisioned reconciliation worker (see
`STATUS.md`), and brief blocker 5 (refresh/revoke race, never verified in this tree).

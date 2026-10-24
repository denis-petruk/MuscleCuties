# Workout Planning Clean Architecture Inspection & Gap Closure Roadmap

## 1. Clean Architecture & Invariant Inspection Summary

Dependencies point strictly inward (`Server / UI -> Infrastructure -> UseCases -> Core`) with **100% C# / .NET 10** and **Zero SQLite** (first-party PostgreSQL authority + encrypted JSON snapshots in `ILocalCacheService`).

### 1.1 Architectural Boundary Findings

| Layer | Target Area | Current State & Clean Architecture Finding | Required Remediation |
| --- | --- | --- | --- |
| **Layer 1: Core** (`src/MuscleCuties.Core`) | `Domain/Workout/Planning/*` & `Domain/Workout/VolumeBudgetRule.cs` | Zero outer-layer imports, zero ORM/JSON attributes. However, `VolumeBudgetRule.cs` contains bare numeric literals (`0.75`, `1.0`, `1.1`, `26d`, `22d`, `35`, `0.6`), violating the Zero Magic Numbers rule. | Replace all bare numeric literals in `VolumeBudgetRule.cs` with named constants and add missing pure domain rules for Sections 2.2, 6, 8, 9, 10, 11, 12, 13.5, and 14. |
| **Layer 2: UseCases** (`src/MuscleCuties.UseCases`) | `Features/Workout/Planning/*` & `Features/Workout/Contracts/*` | `StrengthPlanPreviewOutputV020.cs` uses primitive/DTO types, and `WorkoutActivityDto` exists in `Features/Workout/Contracts/`, but `StrengthPlanPreviewRequestV020`, `ActivityEligibilityInputV020`, and `ReviewActivePlanPhaseV010UseCase` still reference `MuscleCuties.Core` enums/records on boundary contracts, and UI help/threshold metadata is not exposed on UseCases contracts. | Expose UseCases-level planning metadata and DTO mappers so Layer 4 (`UI` / `Server`) never needs to import `MuscleCuties.Core.Domain.Workout.Planning` or `MuscleCuties.Core.Models.Enums.Workout`. |
| **Layer 3: Infrastructure** (`src/MuscleCuties.Infrastructure*`) | `Infrastructure.PostgreSql/Workout/Planning/*` | PostgreSQL stores (`PostgresStrengthPlanPreviewSourceV020`, `PostgresStrengthPlanProposalStoreV020`, `PostgresAcceptStrengthPlanProposalStoreV020`, `PostgresPhaseAdjustmentProposalStoreV010`) use async EF Core calls and zero SQLite. | Keep PostgreSQL as sole persistence authority; ensure any new planner outputs/traces map cleanly through UseCases DTOs. |
| **Layer 4: Mobile UI** (`src/MuscleCuties.UI`) | `ViewModels/Workout/*` | `StrengthPlanGenerationViewModel.cs`, `HiitEligibilityEvidenceCaptureViewModel.cs`, `StrengthPlanGenerationDisplayMapper.cs`, `StrengthPlanGenerationDisplayModels.cs`, `StrengthPlanGenerationInputReader.cs`, and `StrengthPlanActivityEvidenceMapper.cs` import `MuscleCuties.Core.Domain.Workout.Planning` and/or `MuscleCuties.Core.Models.Enums.Workout`. | Decouple all `ViewModels/Workout/*` files from `MuscleCuties.Core.*` by consuming `WorkoutActivityDto` and UseCases planning constants/contracts. |

---

### 1.2 Specification Coverage & Gap Matrix (Sections 0–14)

| Spec Section | Topic | Existing Core Implementation | Identified Gap | Working Block |
| --- | --- | --- | --- | --- |
| **0 & 2** | Activity History & `EffectiveExperienceTier` (1..3) | `ActivityExperienceRule` classifies `Unknown`, `Beginner`, `Established` ($\ge 12$ wks, $\ge 2$ sessions/wk), `Returning` ($\ge 28$ days). `ProfileTrainingExperiencePlanningRule` caps Beginner profile. | Missing numeric `EffectiveExperienceTier` (`1 = Beginner/New`, `2 = Intermediate/Established`, `3 = Advanced`) combining `GlobalTier` (1..3) and `ModalityTier` (`NewModality < 4` wks or `< 1` avg session/wk = 1, `Established` = 2, `Returning` = 1) with downgrade to Tier 1 when `WeeksConsistent == 0` or `AvgSessionsLast4Weeks < 1`. | `WB01` |
| **1, 3, 4 & 6** | Profile Inputs, Activity Policies, Hard-Session Caps & Weekday Spacing | `ActivityPrescriptionRule`, `MixedActivityWeekScheduleRule`, `StrengthWeekdayAssignmentRule`. | 1) `StrengthWeekdayAssignmentRule` for $f_{a_s} = 1$ currently picks index `0` ($d_1$) even when $|AvailableWeekdays| \ge 3$ instead of middle day $d_{\lfloor n/2 \rfloor}$ ($d_1$ if only 2 days), and needs explicit Section 6 spacing support. 2) `GoalPace` and `DailyCapacityMinutes` (`PreferredSessionLength`) need deterministic wiring in the unified weekly generator. | `WB02` |
| **5 & 7** | Strength Templates (`GS`, `HVS`, `SHI`), Eligibility & Time Fitting | `StrengthTemplateCatalog`, `StrengthTemplateResolver`, `ExerciseEligibilityRule`, `StrengthPlanPreviewRule`. | Implemented for strength preview; needs integration into the unified Section 14 generator with `EffectiveExperienceTier` and `StrengthStyle` (`Moderate` vs `ShorterHeavier`) rep/rest biasing. | `WB02` / `WB06` |
| **8** | Weekly Hypertrophy Direct-Set Volume $V(m)$ & Tier Caps | `StrengthPlanPreviewRule` checks only `HighVolumeInitialDirectSetsPerPrimaryMusclePerWeek = 10`. `VolumeBudgetRule` has magic numbers. | Missing `WeeklyHypertrophyVolumeRule` over major muscle groups $M = \{\text{Chest}, \text{Back}, \text{Shoulders}, \text{Quads}, \text{Glutes}, \text{Hamstrings}, \text{Arms}, \text{Calves}, \text{Core}\}$ enforcing Tier 1 ($4..8$), Tier 2 ($8..16$), and Tier 3 ($12..22$) direct-set bounds + zero magic numbers in `VolumeBudgetRule`. | `WB02` |
| **9** | Load Selection from History & 2-Week Double Progression | Deleted during legacy `V020` cleanup; no active Core rule. | Missing `StrengthProgressionRule` implementing effort defaults (Compounds $[6,12], \rho \in \{2,3\}$; Accessories $[10,20], \rho \in \{2,3\}$), single-set history load adjustment ($L_{prev} \pm \Delta L$), 2-consecutive-week double progression at $r_{high}$ with $\rho_{actual} \ge \rho_{target}$, and `EffectiveExperienceTier` concurrent muscle progression limits without simultaneous volume + load increases. | `WB03` |
| **10** | Aerobic Moderate-Equivalent Minutes ($ME_{week}$) & WHO Targets | None. | Missing `AerobicHealthTargetRule` computing $ME(\text{session}) = \text{MinutesModerate} + 2 \times \text{MinutesVigorous}$, $ME_{week} = \sum ME(\text{session})$, and checking WHO targets ($150 \le ME_{week} \le 300$ and $\ge 2$ strength days/week). | `WB04` |
| **11** | Injury Status Rules & Numeric Pain Thresholds ($0..10$) | `InjuryObservationRule` validates status/pain range; `ExerciseEligibilityRule` checks coarse contraindication flags. | Missing `InjurySafetyProgressionRule` enforcing status rules (`Acute` blocks heavy/high-impact for site; `Recovering` blocks auto-generated rehab; `Cleared` prevents instant return to previous max load) and numeric pain rules ($0..3$ mild + no next-day worsening $\to$ allow progression; $4..6$ moderate settling within hours $\to$ maintain/slightly regress; $7..10$ high-risk $\to$ block progression and stop/modify). | `WB04` |
| **12** | Recovery Hard-Day Counting & 2-Week Deload Trigger | Hard-day cap ($\le 1$/day, $\le 3$/week) exists in `MixedActivityWeekScheduleRule`; no deload rule. | Missing `DeloadTriggerRule` detecting $\ge 2$ consecutive weeks of compound underperformance + poor recovery (sleep, fatigue/energy, soreness) and prescribing deload (30–50% working set reduction for major muscles, $+1..2$ target RIR, removal of optional HIIT/long hard cardio, post-deload reassessment). | `WB05` |
| **13 & 14** | Phase Proposals, Daily Check-In Override Precedence & End-to-End Generator with Rule Trace | `PhaseAdjustmentRule` & `TodayPlanningDecisionRule` exist separately; `WorkoutPlanGenerationRule.CanGeneratePlans` is `false`. | 1) Enforce explicit precedence: `Injury/Restriction > DailyCheckInOverride > PhaseBaseline` (e.g., user feeling great on daily check-in can override high baseline discomfort $D_p$, while injury/safety always overrides phase/check-in). 2) Implement `WeeklyWorkoutPlanGeneratorRule` (Section 14 Steps 1–7) producing the complete plan + deterministic audit rule-trace and expose via Clean Architecture UseCases DTOs. | `WB05` / `WB06` |

---

## 2. Execution Plan & Working Blocks (`WB01`–`WB06`)

> **Git & Verification Protocol for Every Working Block:**
> 1. **Zero SQLite & Clean Architecture Gate:** Zero outer imports in `Core`/`UseCases`, zero `MuscleCuties.Core` imports in `UI/ViewModels/Workout/*`, zero magic numbers, async-only I/O.
> 2. **Test & Build Gate:** Run `dotnet test tests/MuscleCuties.Core.Tests/MuscleCuties.Core.Tests.csproj` (and server tests when touched) + iOS simulator build before completing each block.
> 3. **Git Author & Dual Remote Push:** Commit each completed working block with author `denis-petruk` (`git config user.name "denis-petruk"`) and push immediately to **both** GitLab (`git push gitlab HEAD`) and GitHub (`git push origin HEAD`).
> 4. **State Continuity:** Update `docs/WORKOUT_PLANNING_ROADMAP.md`, `ROADMAP.md`, `STATUS.md`, `docs/v1/ROADMAP.md`, and `docs/v1/STATUS.md` after each block.

### Block `WB01`: Clean Architecture Boundary Decoupling & `EffectiveExperienceTier` (Spec Sections 0, 1, 2)
- **Scope:**
  1. Implement `EffectiveExperienceTierRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/EffectiveExperienceTierRule.cs` supporting Section 2.2 (`BeginnerGlobal = 1`, `IntermediateGlobal = 2`, `AdvancedGlobal = 3`; `NewModality = 1`, `EstablishedModality = 2`, `ReturningModality = 1`; strong history $\implies \max(\text{ModalityTier}, \text{GlobalTier})$; weak/missing/returning history $\implies$ downgrade to Tier `1` when `WeeksConsistent == 0` or `AvgSessionsLast4Weeks < 1` or `Returning`/`Unknown`) and integrate with `ProfileTrainingExperiencePlanningRule`.
  2. Expose workout planning UI constants/metadata and `WorkoutActivityDto` helpers in `src/MuscleCuties.UseCases/Features/Workout/` so `src/MuscleCuties.UI/ViewModels/Workout/*` (`StrengthPlanGenerationViewModel`, `HiitEligibilityEvidenceCaptureViewModel`, `StrengthPlanGenerationDisplayMapper`, `StrengthPlanGenerationDisplayModels`, `StrengthPlanGenerationInputReader`, `StrengthPlanActivityEvidenceMapper`) has **zero** `using MuscleCuties.Core.*` directives.
  3. Add unit tests in `tests/MuscleCuties.Core.Tests/EffectiveExperienceTierRuleTests.cs`.
- **Status:** Completed

### Block `WB02`: Weekday Spacing, Strength Style Bias & Weekly Hypertrophy Volume Caps (Spec Sections 3, 5, 6, 7, 8)
- **Scope:**
  1. Extend `StrengthWeekdayAssignmentRule` with Section 6 deterministic weekday selection (`AssignBySpecification`: sorted ascending $[d_1, \dots, d_n]$; $f=1 \implies d_1$ when $n=2$ else middle day $d_{\lfloor n/2 \rfloor}$; $f=2 \implies$ maximal spacing $j-i$; $f>2 \implies$ evenly spread by index) while preserving existing `Assign` compatibility.
  2. Implement `WeeklyHypertrophyVolumeRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/WeeklyHypertrophyVolumeRule.cs` for the 9 major muscle groups $M = \{\text{Chest}, \text{Back}, \text{Shoulders}, \text{Quads}, \text{Glutes}, \text{Hamstrings}, \text{Arms}, \text{Calves}, \text{Core}\}$ with `EffectiveExperienceTier` direct-set bounds (Tier 1: $4..8$, Tier 2: $8..16$, Tier 3: $12..22$).
  3. Eliminate all bare magic numbers in `src/MuscleCuties.Core/Domain/Workout/VolumeBudgetRule.cs`.
  4. Add unit tests in `tests/MuscleCuties.Core.Tests/WeeklyHypertrophyVolumeRuleTests.cs` and `StrengthWeekdayAssignmentRuleTests.cs`.
- **Status:** Completed

### Block `WB03`: Deterministic Load Selection & Double Progression Engine (Spec Section 9)
- **Scope:**
  1. Implement `StrengthProgressionRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/StrengthProgressionRule.cs`:
     - Default effort prescriptions: Compounds $[6, 12], \rho \in \{2, 3\}$; Accessories $[10, 20], \rho \in \{2, 3\}$.
     - Set-by-set load calculation from previous comparable set $(r_{prev}, L_{prev}, \rho_{prev})$ with $\pm \Delta L$.
     - Two-consecutive-week double progression rule (all working sets reach $r_{high}$ with $\rho_{actual} \ge \rho_{target}$ and acceptable technique $\implies L + \Delta L$).
     - Tier-capped concurrent progressions and guard preventing simultaneous load + weekly volume increases for the same muscle group.
  2. Add unit tests in `tests/MuscleCuties.Core.Tests/StrengthProgressionRuleTests.cs`.
- **Status:** Completed

### Block `WB04`: Aerobic Moderate-Equivalent Minutes (`ME_week`) & Numeric Injury/Pain Safety Rules (Spec Sections 10, 11)
- **Scope:**
  1. Implement `AerobicHealthTargetRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/AerobicHealthTargetRule.cs` calculating $ME(\text{session}) = \text{MinutesModerate} + 2 \times \text{MinutesVigorous}$, $ME_{week}$, and evaluating WHO health targets ($150 \le ME_{week} \le 300$ and $\ge 2$ resistance training days/week).
  2. Implement `InjurySafetyProgressionRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/InjurySafetyProgressionRule.cs` enforcing `Acute` / `Recovering` / `Cleared` status rules and numeric `PainLevel` ($0..10$) thresholds ($0..3$ mild/safe, $4..6$ moderate maintain/regress, $7..10$ high-risk block/stop).
  3. Add unit tests in `tests/MuscleCuties.Core.Tests/AerobicHealthTargetRuleTests.cs` and `InjurySafetyProgressionRuleTests.cs`.
- **Status:** Completed

### Block `WB05`: Recovery Deload Trigger & Cycle/Daily Check-In Override Precedence (Spec Sections 12, 13)
- **Scope:**
  1. Implement `DeloadTriggerRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/DeloadTriggerRule.cs` triggering on $\ge 2$ consecutive weeks of compound underperformance + poor recovery, reducing major-muscle working sets by 30–50% (clamped to $\ge 1$), increasing target RIR by $1..2$, removing optional HIIT/long hard cardio, and requiring post-deload reassessment.
  2. Implement `PhaseAndCheckInPrecedenceRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/PhaseAndCheckInPrecedenceRule.cs` (and align `PhaseAdjustmentRule` / `TodayPlanningDecisionRule`) so:
     - Injury/restriction safety rules always take top precedence.
     - Daily check-in readiness can override baseline phase suggestions when the user feels ready despite high baseline $D_p$ (or downgrade when daily check-in is poor).
     - Proposals are never auto-applied without explicit `ApplyChanges` confirmation.
  3. Add unit tests in `tests/MuscleCuties.Core.Tests/DeloadTriggerRuleTests.cs` and `PhaseAndCheckInPrecedenceRuleTests.cs`.
- **Status:** Completed

### Block `WB06`: Unified Deterministic Weekly Workout Plan Generator & Rule-Trace Orchestration (Spec Section 14)
- **Scope:**
  1. Implement `WeeklyWorkoutPlanGeneratorRule` in `src/MuscleCuties.Core/Domain/Workout/Planning/WeeklyWorkoutPlanGeneratorRule.cs` orchestrating all 7 deterministic steps of Section 14:
     - Step 1: Read profile (`GoalPrimary`, `GoalPace`, `TrainingExperienceLevel`, `PreferredActivities`, `GeneralDaysPerWeek`, `PreferredSessionLength`, `AvailableEquipment`, `StrengthStyle`), `StrengthPlanningInput`, `ActivityHistoryInput`, injuries, cycle baselines, and daily check-ins.
     - Step 2: Compute `EffectiveExperienceTier` per activity.
     - Step 3: Build strength sessions (`GS`/`HVS`/`SHI`), resolve eligible exercises, verify movement coverage, and fit into `WarmUpMinutes + CoolDownMinutes + T_work` by trimming optional accessories.
     - Step 4: Pack mixed activities with hard-day caps ($\le 1$/day, $\le 3$/week), check weekly hypertrophy volume $V(m)$ against tier caps, and evaluate $ME_{week}$ & strength-day count vs. WHO targets.
     - Step 5: Apply load/double-progression rules (`StrengthProgressionRule`), injury pain gates (`InjurySafetyProgressionRule`), and deload checks (`DeloadTriggerRule`).
     - Step 6: Generate optional phase/check-in proposals (`PhaseAndCheckInPrecedenceRule`) and apply only when `ApplyChanges == true`.
     - Step 7: Output the complete plan plus structured `RuleTrace` audit log.
  2. Expose Clean Architecture UseCases interactor + DTO contracts (`GenerateDeterministicWeeklyPlanUseCase`) with zero Core type leakage to UI/Controllers.
  3. Add comprehensive end-to-end unit tests in `tests/MuscleCuties.Core.Tests/WeeklyWorkoutPlanGeneratorRuleTests.cs`, update `ROADMAP.md` / `STATUS.md` / `docs/v1/ROADMAP.md` / `docs/v1/STATUS.md`, verify `dotnet test` + iOS simulator build, and push to `gitlab` and `origin` (GitHub).
- **Status:** Completed

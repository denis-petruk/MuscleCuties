using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPlanningPolicyComplianceTests
{
    // =========================================================================
    // 2. HVS & SHI Eligibility Conditions and Reason Codes
    // =========================================================================

    [Fact]
    public void HvsEligibility_BlocksWithExactReasonCodes()
    {
        // 1. Not established history
        var notEstablished = StrengthTemplateEligibilityRule.EvaluateHvsEligibility(
            isGsEstablished: false, isHvsEstablished: false, profileLevel: TrainingExperienceLevel.Intermediate,
            isHvsReturning: false, hasConflictingInjury: false);
        Assert.True(notEstablished.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.HvsNotEstablishedHistory, notEstablished.ReasonCode);

        // 2. Profile level too low (Beginner)
        var levelTooLow = StrengthTemplateEligibilityRule.EvaluateHvsEligibility(
            isGsEstablished: true, isHvsEstablished: false, profileLevel: TrainingExperienceLevel.Beginner,
            isHvsReturning: false, hasConflictingInjury: false);
        Assert.True(levelTooLow.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.HvsProfileLevelTooLow, levelTooLow.ReasonCode);

        // 3. Returning after break
        var returning = StrengthTemplateEligibilityRule.EvaluateHvsEligibility(
            isGsEstablished: true, isHvsEstablished: true, profileLevel: TrainingExperienceLevel.Intermediate,
            isHvsReturning: true, hasConflictingInjury: false);
        Assert.True(returning.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.HvsReturningAfterBreak, returning.ReasonCode);

        // 4. Conflicting injury
        var injury = StrengthTemplateEligibilityRule.EvaluateHvsEligibility(
            isGsEstablished: true, isHvsEstablished: false, profileLevel: TrainingExperienceLevel.Intermediate,
            isHvsReturning: false, hasConflictingInjury: true);
        Assert.True(injury.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.HvsInjuryConflict, injury.ReasonCode);

        // 5. Hard day conflict
        var hardConflict = StrengthTemplateEligibilityRule.EvaluateHvsEligibility(
            isGsEstablished: true, isHvsEstablished: false, profileLevel: TrainingExperienceLevel.Intermediate,
            isHvsReturning: false, hasConflictingInjury: false, hasHardDayConflict: true);
        Assert.True(hardConflict.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.HvsHardDayConflict, hardConflict.ReasonCode);

        // 6. Eligible
        var eligible = StrengthTemplateEligibilityRule.EvaluateHvsEligibility(
            isGsEstablished: true, isHvsEstablished: false, profileLevel: TrainingExperienceLevel.Intermediate,
            isHvsReturning: false, hasConflictingInjury: false);
        Assert.True(eligible.IsAllowed);
        Assert.Equal(StrengthTemplateEligibilityRule.HvsEligible, eligible.ReasonCode);
    }

    [Fact]
    public void ShiEligibility_BlocksWithExactReasonCodes()
    {
        // 1. Insufficient exposure / not established
        var insufficient = StrengthTemplateEligibilityRule.EvaluateShiEligibility(
            isGsEstablished: false, isShiEstablished: false, profileLevel: TrainingExperienceLevel.Intermediate,
            isShiReturning: false, hasManageableLoadForAllMainExercises: true, hasConflictingInjury: false);
        Assert.True(insufficient.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.ShiInsufficientExposure, insufficient.ReasonCode);

        // 2. No manageable load history
        var noManageableLoad = StrengthTemplateEligibilityRule.EvaluateShiEligibility(
            isGsEstablished: true, isShiEstablished: false, profileLevel: TrainingExperienceLevel.Intermediate,
            isShiReturning: false, hasManageableLoadForAllMainExercises: false, hasConflictingInjury: false);
        Assert.True(noManageableLoad.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.ShiNoManageableLoadHistory, noManageableLoad.ReasonCode);

        // 3. Profile level too low
        var levelTooLow = StrengthTemplateEligibilityRule.EvaluateShiEligibility(
            isGsEstablished: true, isShiEstablished: false, profileLevel: TrainingExperienceLevel.Beginner,
            isShiReturning: false, hasManageableLoadForAllMainExercises: true, hasConflictingInjury: false);
        Assert.True(levelTooLow.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.ShiProfileLevelTooLow, levelTooLow.ReasonCode);

        // 4. Conflicting injury
        var injury = StrengthTemplateEligibilityRule.EvaluateShiEligibility(
            isGsEstablished: true, isShiEstablished: false, profileLevel: TrainingExperienceLevel.Advanced,
            isShiReturning: false, hasManageableLoadForAllMainExercises: true, hasConflictingInjury: true);
        Assert.True(injury.IsBlocked);
        Assert.Equal(StrengthTemplateEligibilityRule.ShiInjuryConflict, injury.ReasonCode);

        // 5. Eligible
        var eligible = StrengthTemplateEligibilityRule.EvaluateShiEligibility(
            isGsEstablished: true, isShiEstablished: false, profileLevel: TrainingExperienceLevel.Advanced,
            isShiReturning: false, hasManageableLoadForAllMainExercises: true, hasConflictingInjury: false);
        Assert.True(eligible.IsAllowed);
        Assert.Equal(StrengthTemplateEligibilityRule.ShiEligible, eligible.ReasonCode);
    }

    // =========================================================================
    // 3. Hard Session Caps (at most 1/day, at most 3 hard days/week, Power Yoga hard)
    // =========================================================================

    [Fact]
    public void HardSessions_AtMostOnePerDay_AndThreeDaysPerWeek()
    {
        var context = new MixedActivityWeekScheduleContext(
            SelectedActivities:
            [
                WorkoutActivityType.GeneralStrength,
                WorkoutActivityType.HighVolumeStrength,
                WorkoutActivityType.StrengthHighIntensity,
                WorkoutActivityType.RockClimbing
            ],
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            SessionDurationsMinutes: null,
            EligibilityEvidence: null);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        // Total hard days cannot exceed 3
        var daysWithHardSessions = result.Sessions
            .Where(s => s.IsHardSession)
            .Select(s => s.Day)
            .Distinct()
            .Count();

        Assert.True(daysWithHardSessions <= 3);

        // No day contains more than 1 hard session
        foreach (var day in Enum.GetValues<DayOfWeek>())
        {
            var hardSessionsOnDay = result.Sessions
                .Count(s => s.Day == day && s.IsHardSession);
            Assert.True(hardSessionsOnDay <= 1);
        }
    }

    [Fact]
    public void HardSessions_PowerYoga_ClassifiedAsHardSession()
    {
        var demandingPrescription = new ActivitySessionPrescription(
            Activity: WorkoutActivityType.Yoga,
            Purpose: SessionPurpose.Yoga,
            Modality: ActivityModality.Yoga,
            Intensity: IntendedIntensity.Vigorous,
            TrainingStyle: SessionTrainingStyle.Demanding,
            FrequencyPerWeek: 1,
            WarmupMinutes: null, MainMinutes: 30, CooldownMinutes: null, Sets: null,
            MinimumRepetitions: null, MaximumRepetitions: null, RepetitionsInReserve: null,
            RestSeconds: null, IntervalWorkCount: null, IntervalWorkSeconds: null,
            IntervalRecoveryCount: null, IntervalRecoverySeconds: null, RequiresUserConfirmation: false,
            Reasons: []);

        var gentlePrescription = demandingPrescription with
        {
            Intensity = IntendedIntensity.Easy,
            TrainingStyle = SessionTrainingStyle.Gentle
        };

        Assert.True(MixedActivityWeekScheduleRule.IsHardActivity(WorkoutActivityType.Yoga, demandingPrescription));
        Assert.False(MixedActivityWeekScheduleRule.IsHardActivity(WorkoutActivityType.Yoga, gentlePrescription));
    }

    [Fact]
    public void Hiit_Limits_AtMostTwoPerWeek_AndNeverOnSameDayAsStrengthOrClimbing()
    {
        Assert.Equal(2, ActivityPrescriptionRule.HiitMaximumSessionsPerWeek);

        var context = new MixedActivityWeekScheduleContext(
            SelectedActivities:
            [
                WorkoutActivityType.GeneralStrength,
                WorkoutActivityType.Hiit
            ],
            AvailableWeekdays: [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
            SessionDurationsMinutes: null,
            EligibilityEvidence: null);

        var result = MixedActivityWeekScheduleRule.Evaluate(context);

        var hiitDays = result.Sessions.Where(s => s.Activity == WorkoutActivityType.Hiit).Select(s => s.Day).ToHashSet();
        var strengthDays = result.Sessions.Where(s => s.Activity == WorkoutActivityType.GeneralStrength).Select(s => s.Day).ToHashSet();

        // HIIT and GS never share a day
        Assert.Empty(hiitDays.Intersect(strengthDays));
    }

    // =========================================================================
    // 4. Direct Muscle Volume (Exact Tier Caps, No warm-up credit)
    // =========================================================================

    [Fact]
    public void DirectMuscleVolume_ExactTierCaps_Section9()
    {
        // Tier 1: Target 8-12, Cap 12
        var (t1Min, t1Max, t1Cap) = WeeklyHypertrophyVolumeRule.GetTierVolumeBounds(EffectiveExperienceTier.Tier1BeginnerOrNew);
        Assert.Equal(8, t1Min);
        Assert.Equal(12, t1Max);
        Assert.Equal(12, t1Cap);

        // Tier 2: Target 12-16, Cap 16
        var (t2Min, t2Max, t2Cap) = WeeklyHypertrophyVolumeRule.GetTierVolumeBounds(EffectiveExperienceTier.Tier2IntermediateOrEstablished);
        Assert.Equal(12, t2Min);
        Assert.Equal(16, t2Max);
        Assert.Equal(16, t2Cap);

        // Tier 3: Target 16-20, Cap 22
        var (t3Min, t3Max, t3Cap) = WeeklyHypertrophyVolumeRule.GetTierVolumeBounds(EffectiveExperienceTier.Tier3Advanced);
        Assert.Equal(16, t3Min);
        Assert.Equal(20, t3Max);
        Assert.Equal(22, t3Cap);
    }

    [Fact]
    public void DirectMuscleVolume_OnlyDirectWorkingSetsCounted_NoWarmupCredit()
    {
        var exercises = new[]
        {
            new PlannedExerciseSets(1, MajorMuscleGroup.Chest, Sets: 4),
            new PlannedExerciseSets(2, MajorMuscleGroup.Chest, Sets: 4),
            new PlannedExerciseSets(3, MajorMuscleGroup.Back, Sets: 5)
        };

        var volume = WeeklyHypertrophyVolumeRule.CalculateVolume(exercises);

        Assert.Equal(8, volume[MajorMuscleGroup.Chest]);
        Assert.Equal(5, volume[MajorMuscleGroup.Back]);
        Assert.Equal(0, volume[MajorMuscleGroup.Arms]); // Indirect arms involvement receives 0 credit
    }

    // =========================================================================
    // 5. Recovery Activity Durations and Reason Codes
    // =========================================================================

    [Fact]
    public void RecoveryActivities_MissingDuration_ReturnsExplicitReasonCodes()
    {
        Assert.Equal("YOGA_DURATION_NOT_SET", ActivitySessionDurationRule.YogaDurationNotSet);
        Assert.Equal("MOBILITY_DURATION_NOT_SET", ActivitySessionDurationRule.MobilityDurationNotSet);
        Assert.Equal("GENTLE_RECOVERY_DURATION_NOT_SET", ActivitySessionDurationRule.GentleRecoveryDurationNotSet);
        Assert.Equal("OTHER_DURATION_NOT_SET", ActivitySessionDurationRule.OtherDurationNotSet);
    }

    // =========================================================================
    // 6. Injury Status and Pain Thresholds (3 vs 4, 6 vs 7, No safety bypass)
    // =========================================================================

    [Fact]
    public void Injury_PainBoundaries_3vs4_And_6vs7()
    {
        // Boundary: Pain 3 allows progression (if no next-day worsening)
        var pain3 = InjurySafetyProgressionRule.EvaluatePainProgression(painLevel: 3, hasNextDayWorsening: false);
        Assert.Equal(PainProgressionDisposition.AllowProgression, pain3.Disposition);
        Assert.True(pain3.AllowsLoadProgression);

        // Boundary: Pain 4 maintains or regresses (blocks progression even without next-day worsening)
        var pain4 = InjurySafetyProgressionRule.EvaluatePainProgression(painLevel: 4, hasNextDayWorsening: false);
        Assert.Equal(PainProgressionDisposition.MaintainOrRegress, pain4.Disposition);
        Assert.False(pain4.AllowsLoadProgression);

        // Boundary: Pain 6 maintains or regresses
        var pain6 = InjurySafetyProgressionRule.EvaluatePainProgression(painLevel: 6, hasNextDayWorsening: false);
        Assert.Equal(PainProgressionDisposition.MaintainOrRegress, pain6.Disposition);
        Assert.False(pain6.RequiresExerciseStopOrModification);

        // Boundary: Pain 7 requires exercise stop or modification
        var pain7 = InjurySafetyProgressionRule.EvaluatePainProgression(painLevel: 7, hasNextDayWorsening: false);
        Assert.Equal(PainProgressionDisposition.BlockAndModify, pain7.Disposition);
        Assert.True(pain7.RequiresExerciseStopOrModification);
    }

    [Fact]
    public void Injury_AcuteStatus_CannotBeBypassedByLowPainOrActivityLabels()
    {
        // Low pain (1/10) with Acute status still blocks heavy/high-impact movement
        var acuteDecision = InjurySafetyProgressionRule.EvaluateExerciseSafety(
            status: InjuryStatus.Acute,
            activeInjurySite: InjuryFlag.Knee,
            isHeavyMovement: true,
            isHighImpactMovement: false,
            exerciseInvolvedSite: InjuryFlag.Knee,
            requestedLoad: 80m);

        Assert.False(acuteDecision.IsAllowed);
        Assert.False(acuteDecision.IsProgressionAllowed);
        Assert.Equal(InjurySafetyProgressionRule.StatusBlockedAcute, acuteDecision.SafetyStatus);
    }

    // =========================================================================
    // 7. Aerobic Moderate-Equivalent Minutes (ME = Mod + 2 * Vig)
    // =========================================================================

    [Fact]
    public void AerobicMinutes_ModerateEquivalentArithmetic_MeEqualsModPlus2Vig()
    {
        // 30 min moderate + 15 min vigorous = 30 + 2 * 15 = 60 ME
        var sessionMe = AerobicHealthTargetRule.CalculateSessionModerateEquivalentMinutes(30m, 15m);
        Assert.Equal(60m, sessionMe);

        var weeklySessions = new[]
        {
            new AerobicSessionMinutes(MinutesModerate: 30m, MinutesVigorous: 15m), // 60 ME
            new AerobicSessionMinutes(MinutesModerate: 40m, MinutesVigorous: 25m)  // 40 + 50 = 90 ME
        };

        var totalMe = AerobicHealthTargetRule.CalculateWeeklyModerateEquivalentMinutes(weeklySessions);
        Assert.Equal(150m, totalMe);

        var eval = AerobicHealthTargetRule.Evaluate(weeklySessions, resistanceTrainingDaysPerWeek: 2);
        Assert.True(eval.MeetsAerobicTarget);
        Assert.True(eval.MeetsResistanceTarget);
        Assert.True(eval.MeetsAllTargets);
    }

    // =========================================================================
    // 8. Load Progression & Volume Guard
    // =========================================================================

    [Fact]
    public void Progression_SimultaneousVolumeAndLoadIncrease_IsBlockedForSameMuscle()
    {
        var candidate = new MuscleProgressionCandidate(
            Muscle: MajorMuscleGroup.Chest,
            ExerciseId: 101,
            CurrentLoad: 50m,
            LoadIncrement: 2.5m,
            Category: ExerciseMovementCategory.Compound,
            RecentWeeks:
            [
                new(1, [new CompletedWorkingSet(12, 50m, 3)]),
                new(2, [new CompletedWorkingSet(12, 50m, 3)])
            ],
            CurrentWeeklyVolume: 10,
            PreviousWeeklyVolume: 8); // Volume increased from 8 to 10

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier1BeginnerOrNew,
            [candidate]);

        var decision = Assert.Single(decisions);
        Assert.False(decision.LoadIncreased);
        Assert.Equal(StrengthProgressionRule.StatusBlockedByVolume, decision.ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.SimultaneousVolumeIncreaseBlocksLoadProgression, decision.ReasonCode);
    }

    // =========================================================================
    // 9. Cycle Phase Precedence Hierarchy
    // =========================================================================

    [Fact]
    public void CyclePrecedence_InjurySafetyOverridesAll_AndReadinessOverridesCycleDiscomfort()
    {
        // 1. Injury safety override
        var injuryContext = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: true,
            BaselineCycleTier: PhaseAdjustmentTier.EasierDay);

        var injuryResult = PhaseAndCheckInPrecedenceRule.Evaluate(injuryContext);
        Assert.Equal(PrecedenceLevel.InjurySafety, injuryResult.ControllingPrecedence);
        Assert.Equal(PrecedenceProposalStatus.BlockedBySafety, injuryResult.Status);

        // 2. High daily readiness overrides baseline cycle phase discomfort
        var readinessContext = new PrecedenceEvaluationContext(
            HasExplicitRestrictionConflict: false,
            HasAcuteInjuryConflict: false,
            BaselineCycleTier: PhaseAdjustmentTier.EasierDay,
            CheckInScore: new CheckInScoreResult(CheckInScoreStatus.Available, 85m, CheckInScoreBand.FeelingReady, null));

        var readinessResult = PhaseAndCheckInPrecedenceRule.Evaluate(readinessContext);
        Assert.Equal(PrecedenceLevel.DailyCheckInOverride, readinessResult.ControllingPrecedence);
        Assert.Equal(PhaseAdjustmentTier.Maintain, readinessResult.EffectiveTier);
    }

    // =========================================================================
    // 10. Session Lifecycle Preservation (Completed and InProgress preserved)
    // =========================================================================

    [Fact]
    public void SessionLifecycle_CompletedAndInProgress_CannotBeModified()
    {
        var planned = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.Planned);
        Assert.True(planned.IsAllowed);

        var inProgress = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.InProgress);
        Assert.True(inProgress.IsBlocked);
        Assert.Equal(WorkoutSessionLifecycleRule.ReasonCompletedOrInProgressSessionCannotBeModified, inProgress.ReasonCode);

        var completed = WorkoutSessionLifecycleRule.CanModifySession(WorkoutSessionLifecycleState.Completed);
        Assert.True(completed.IsBlocked);
        Assert.Equal(WorkoutSessionLifecycleRule.ReasonCompletedOrInProgressSessionCannotBeModified, completed.ReasonCode);
    }
}

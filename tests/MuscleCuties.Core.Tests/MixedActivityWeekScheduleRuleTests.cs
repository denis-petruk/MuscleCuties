using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class MixedActivityWeekScheduleRuleTests
{
    private const int FirstActivityIndex = 0;
    private const int SecondActivityIndex = 1;
    private const int FirstHistoricalWeekOffset = 1;
    private const int NoCompletedExerciseExposures = 0;
    private const int ActivitiesPerPackedDay = 2;
    private const int SelectedCardioSessionMinutes = WorkoutPreferencesRule.MinimumActivitySessionDurationMinutes;
    private const int EstablishedCardioSessionMinutes =
        ActivityPrescriptionRule.ContinuousCardioWarmupMinutes +
        ActivityPrescriptionRule.EstablishedCardioMainMinutesWithoutBaseline +
        ActivityPrescriptionRule.ContinuousCardioCooldownMinutes;
    private const int BeginnerCardioSessionMinutes =
        ActivityPrescriptionRule.ContinuousCardioWarmupMinutes +
        ActivityPrescriptionRule.BeginnerCardioMainMinutes +
        ActivityPrescriptionRule.ContinuousCardioCooldownMinutes;
    private const int ExtendedSelectedCardioSessionMinutes =
        WorkoutPreferencesRule.MinimumActivitySessionDurationMinutes +
        WorkoutPreferencesRule.ActivitySessionDurationIncrementMinutes;
    private const int BeginnerClimbingSessionMinutes = ActivityPrescriptionRule.BeginnerClimbingVisitMinutes;
    private const int StrengthSessionMinutes = ActivityPrescriptionRule.BeginnerClimbingVisitMinutes;
    private const int ExpectedThreeMixedSessions = 3;
    private const int ExpectedTwoCardioSessions = 2;
    private const int ExpectedSingleSession = 1;
    private const int ExpectedTwoCompetingSessions = 2;
    private const int ExplicitOtherActivityMinutes = 17;
    private const int InvalidMobilityPreferenceMinutes = 20;
    private static readonly int GentleYogaSessionMinutes =
        ActivitySessionDurationRule.DefaultMinutesFor(WorkoutActivityType.Yoga) ??
        throw new InvalidOperationException("Gentle Yoga must have a default duration.");

    [Fact]
    public void SpreadsSelectedActivitiesAcrossAvailableWeekdays()
    {
        var result = Evaluate(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.Running],
            [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
            Durations((WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
                (WorkoutActivityType.Running, SelectedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
                (WorkoutActivityType.Running, EvidenceFor(WorkoutActivityType.Running))));

        Assert.True(result.Sessions.Count == ExpectedTwoCardioSessions * 2,
            string.Join(" | ", result.Activities.Select(activity =>
                $"{activity.Activity}: {activity.Disposition}, {activity.TargetSessionsPerWeek}, " +
                string.Join(',', activity.Reasons))));
        Assert.Equal(ExpectedTwoCardioSessions * 2, result.Sessions.Select(session => session.Day).Distinct().Count());
        Assert.All(result.Sessions, session => Assert.Contains(session.Day,
            new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday }));
        Assert.Equal(ExpectedTwoCardioSessions,
            result.Sessions.Count(session => session.Activity == WorkoutActivityType.Running));
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled, Decision(result, WorkoutActivityType.GeneralStrength).Disposition);
    }

    [Fact]
    public void AllowsOneCyclingOrGentleYogaCompanionPerStrengthDayWithoutDailyTimeLimit()
    {
        var twoDays = new[] { DayOfWeek.Monday, DayOfWeek.Thursday };
        var activities = new[]
        {
            WorkoutActivityType.GeneralStrength,
            WorkoutActivityType.Cycling,
            WorkoutActivityType.Yoga
        };
        var result = Evaluate(
            activities,
            twoDays,
            Durations(
                (WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
                (WorkoutActivityType.Cycling, SelectedCardioSessionMinutes),
                (WorkoutActivityType.Yoga, GentleYogaSessionMinutes)),
            Evidence(
                (WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
                (WorkoutActivityType.Cycling, EvidenceFor(WorkoutActivityType.Cycling)),
                (WorkoutActivityType.Yoga, EvidenceFor(WorkoutActivityType.Yoga,
                    isDemandingYogaContent: false))),
            RequestedFrequencies(
                (WorkoutActivityType.Cycling, ExpectedTwoCardioSessions),
                (WorkoutActivityType.Yoga, ExpectedTwoCardioSessions)));

        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek,
            Decision(result, WorkoutActivityType.GeneralStrength).ScheduledSessionsPerWeek);
        Assert.Equal(ExpectedSingleSession,
            Decision(result, WorkoutActivityType.Cycling).ScheduledSessionsPerWeek);
        Assert.Equal(ExpectedSingleSession,
            Decision(result, WorkoutActivityType.Yoga).ScheduledSessionsPerWeek);
        Assert.Contains(MixedActivityWeekScheduleRule.ActivityFrequencyLimitedByAvailableWeekdays,
            Decision(result, WorkoutActivityType.Cycling).Reasons);
        Assert.Contains(MixedActivityWeekScheduleRule.ActivityFrequencyLimitedByAvailableWeekdays,
            Decision(result, WorkoutActivityType.Yoga).Reasons);
        Assert.All(result.Sessions.GroupBy(session => session.Day), day =>
        {
            Assert.Equal(twoDays.Length, day.Count());
            var strength = Assert.Single(day, session =>
                session.Activity == WorkoutActivityType.GeneralStrength);
            Assert.Contains(day, session =>
                MixedActivityWeekScheduleRule.IsSupportedSameDayStrengthCompanion(
                    strength.Activity,
                    strength.IsHardSession,
                    session.Activity,
                    session.IsHardSession));
        });
        Assert.All(result.Sessions.Where(session => session.Activity == WorkoutActivityType.GeneralStrength),
            session => Assert.Equal((decimal)StrengthSessionMinutes, session.DurationMinutes));
    }

    [Fact]
    public void DemandingYogaAndMultipleCompanionsAreNotSafeStrengthDayPairs()
    {
        Assert.False(MixedActivityWeekScheduleRule.IsSupportedSameDayStrengthCompanion(
            WorkoutActivityType.GeneralStrength,
            true,
            WorkoutActivityType.Yoga,
            true));
        Assert.False(MixedActivityWeekScheduleRule.IsSupportedSameDayStrengthCompanion(
            WorkoutActivityType.Cycling,
            false,
            WorkoutActivityType.Yoga,
            false));
        Assert.True(MixedActivityWeekScheduleRule.IsSupportedSameDayStrengthCompanion(
            WorkoutActivityType.HighVolumeStrength,
            true,
            WorkoutActivityType.Cycling,
            false));
        Assert.True(MixedActivityWeekScheduleRule.IsSupportedSameDayStrengthCompanion(
            WorkoutActivityType.StrengthHighIntensity,
            true,
            WorkoutActivityType.Yoga,
            false));
    }

    [Fact]
    public void YogaWithoutContentClassificationUsesGentleDefault()
    {
        var result = Evaluate(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.Yoga],
            [DayOfWeek.Monday, DayOfWeek.Thursday],
            Durations(
                (WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
                (WorkoutActivityType.Yoga, GentleYogaSessionMinutes)),
            Evidence(
                (WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
                (WorkoutActivityType.Yoga, EvidenceFor(WorkoutActivityType.Yoga))));

        var yogaDecision = Decision(result, WorkoutActivityType.Yoga);
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled, yogaDecision.Disposition);
        Assert.Contains(result.Sessions, session => session.Activity == WorkoutActivityType.Yoga &&
            !session.IsHardSession && session.DurationMinutes == GentleYogaSessionMinutes);
    }

    [Fact]
    public void CalculatesCyclingDurationWithoutUsingSavedSessionChoice()
    {
        var result = Evaluate(
            [WorkoutActivityType.Cycling],
            [DayOfWeek.Monday, DayOfWeek.Thursday],
            Durations((WorkoutActivityType.Cycling, ExtendedSelectedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.Cycling, EvidenceFor(WorkoutActivityType.Cycling))));
        var decision = Decision(result, WorkoutActivityType.Cycling);

        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek, result.Sessions.Count);
        Assert.All(result.Sessions, session => Assert.Equal(BeginnerCardioSessionMinutes, session.DurationMinutes));
        Assert.NotNull(decision.Prescription);
        Assert.Equal((decimal)BeginnerCardioSessionMinutes,
            (decision.Prescription!.WarmupMinutes ?? decimal.Zero) +
            (decision.Prescription.MainMinutes ?? decimal.Zero) +
            (decision.Prescription.CooldownMinutes ?? decimal.Zero));
        Assert.DoesNotContain(MixedActivityWeekScheduleRule.SelectedDurationAppliedToPrescription, decision.Reasons);
    }

    [Fact]
    public void SchedulesOnlyChosenStrengthAlternativeAndPreservesOtherSelectedActivities()
    {
        var result = Evaluate(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.HighVolumeStrength,
                WorkoutActivityType.Running],
            AllWeekdays,
            Durations((WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
                (WorkoutActivityType.HighVolumeStrength, StrengthSessionMinutes),
                (WorkoutActivityType.Running, SelectedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
                (WorkoutActivityType.HighVolumeStrength, EvidenceFor(WorkoutActivityType.HighVolumeStrength,
                    ActivityExperience.Established, isEstablishedResistanceHistory: true)),
                (WorkoutActivityType.Running, EvidenceFor(WorkoutActivityType.Running))),
            chosenStrengthActivity: WorkoutActivityType.GeneralStrength);

        Assert.Equal(ExpectedTwoCardioSessions * 2, result.Sessions.Count);
        Assert.DoesNotContain(result.Sessions,
            session => session.Activity == WorkoutActivityType.HighVolumeStrength);
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled,
            Decision(result, WorkoutActivityType.GeneralStrength).Disposition);
        Assert.Equal(MixedActivityScheduleDisposition.NotScheduled,
            Decision(result, WorkoutActivityType.HighVolumeStrength).Disposition);
        Assert.Equal(ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek,
            Decision(result, WorkoutActivityType.HighVolumeStrength).PolicySessionsPerWeek);
        Assert.Equal(ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek,
            Decision(result, WorkoutActivityType.HighVolumeStrength).TargetSessionsPerWeek);
        Assert.Contains(MixedActivityWeekScheduleRule.StrengthAlternativeNotChosen,
            Decision(result, WorkoutActivityType.HighVolumeStrength).Reasons);
        Assert.Equal(ExpectedTwoCardioSessions,
            Decision(result, WorkoutActivityType.Running).ScheduledSessionsPerWeek);
    }

    [Fact]
    public void AllowsSafeStrengthCompanionPairingWithoutComparingCombinedDurationToDailyLimit()
    {
        var result = Evaluate(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.Yoga],
            [DayOfWeek.Monday, DayOfWeek.Thursday],
            Durations((WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
                (WorkoutActivityType.Yoga, GentleYogaSessionMinutes)),
            Evidence((WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
                (WorkoutActivityType.Yoga, EvidenceFor(WorkoutActivityType.Yoga,
                    isDemandingYogaContent: false))));

        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek * ActivitiesPerPackedDay,
            result.Sessions.Count);
        Assert.Equal(ExpectedTwoCardioSessions,
            result.Sessions.Select(session => session.Day).Distinct().Count());
        Assert.All(result.Sessions.GroupBy(session => session.Day), group =>
        {
            Assert.Equal(ActivitiesPerPackedDay, group.Count());
            Assert.True(group.Sum(session => session.DurationMinutes) > StrengthSessionMinutes);
        });
    }

    [Fact]
    public void EnforcesThreeHardDayWeeklyLimitAcrossStrengthAndClimbing()
    {
        var selected = new[]
        {
            WorkoutActivityType.GeneralStrength,
            WorkoutActivityType.HighVolumeStrength,
            WorkoutActivityType.StrengthHighIntensity,
            WorkoutActivityType.RockClimbing
        };
        var durations = Durations(
            (WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
            (WorkoutActivityType.HighVolumeStrength, StrengthSessionMinutes),
            (WorkoutActivityType.StrengthHighIntensity, StrengthSessionMinutes));
        var evidence = Evidence(
            (WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
            (WorkoutActivityType.HighVolumeStrength, EvidenceFor(WorkoutActivityType.HighVolumeStrength,
                ActivityExperience.Established, isEstablishedResistanceHistory: true)),
            (WorkoutActivityType.StrengthHighIntensity, EvidenceFor(WorkoutActivityType.StrengthHighIntensity,
                ActivityExperience.Established, isEstablishedResistanceHistory: true,
                completedMainExerciseExposures: ActivityPrescriptionRule.StrengthHighIntensityRequiredExerciseExposures,
                hasKnownManageableLoad: true, userConfirmedCurrentFamiliarity: true)),
            (WorkoutActivityType.RockClimbing, EvidenceFor(WorkoutActivityType.RockClimbing,
                hasRopeClimbingCompetence: true, appropriateClimbingInstructionAvailable: true)));

        var result = Evaluate(selected, AllWeekdays, durations, evidence);

        Assert.Equal(MixedActivityWeekScheduleRule.MaximumHardDaysPerWeek, result.Sessions.Count);
        Assert.Equal(MixedActivityWeekScheduleRule.MaximumHardDaysPerWeek,
            result.Sessions.Select(session => session.Day).Distinct().Count());
        Assert.All(result.Sessions, session => Assert.True(session.IsHardSession));
        Assert.Contains(ActivityPrescriptionRule.HighIntensitySaferAlternativeReviewMissing,
            Decision(result, WorkoutActivityType.StrengthHighIntensity).Reasons);
    }

    [Fact]
    public void SchedulesOnlyOnAvailableDaysAndHonorsActivityFrequency()
    {
        var availableDays = new[] { DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Saturday };
        var result = Evaluate(
            [WorkoutActivityType.Running, WorkoutActivityType.RockClimbing],
            availableDays,
            Durations((WorkoutActivityType.Running, SelectedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.Running, EvidenceFor(WorkoutActivityType.Running)),
                (WorkoutActivityType.RockClimbing, EvidenceFor(WorkoutActivityType.RockClimbing,
                    hasRopeClimbingCompetence: true, appropriateClimbingInstructionAvailable: true))));

        Assert.All(result.Sessions, session => Assert.Contains(session.Day, availableDays));
        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek,
            Decision(result, WorkoutActivityType.Running).ScheduledSessionsPerWeek);
        Assert.Equal(ExpectedSingleSession,
            Decision(result, WorkoutActivityType.RockClimbing).ScheduledSessionsPerWeek);
        Assert.Equal(ExpectedThreeMixedSessions, result.Sessions.Count);
    }

    [Fact]
    public void UsesPrescriptionClimbingDurationWithoutRequiringAUserPreference()
    {
        var result = Evaluate(
            [WorkoutActivityType.RockClimbing],
            AllWeekdays,
            Durations(),
            Evidence((WorkoutActivityType.RockClimbing, EvidenceFor(WorkoutActivityType.RockClimbing,
                hasRopeClimbingCompetence: true, appropriateClimbingInstructionAvailable: true))));

        Assert.Single(result.Sessions);
        Assert.Equal((decimal)BeginnerClimbingSessionMinutes, result.Sessions.Single().DurationMinutes);
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled,
            Decision(result, WorkoutActivityType.RockClimbing).Disposition);
        Assert.DoesNotContain(MixedActivityWeekScheduleRule.SessionDurationMissing,
            Decision(result, WorkoutActivityType.RockClimbing).Reasons);
    }

    [Fact]
    public void AppliesHiitCapAndReplacesOneSelectedContinuousCardioSession()
    {
        const WorkoutActivityType underlyingActivity = WorkoutActivityType.Cycling;
        var result = Evaluate(
            [underlyingActivity, WorkoutActivityType.Hiit],
            AllWeekdays,
            Durations((underlyingActivity, ExtendedSelectedCardioSessionMinutes)),
            Evidence((underlyingActivity, EvidenceFor(underlyingActivity,
                ActivityExperience.Established,
                existingBaselineMinutes: ActivityPrescriptionRule.EstablishedCardioMainMinutesWithoutBaseline)),
                (WorkoutActivityType.Hiit, EvidenceFor(WorkoutActivityType.Hiit,
                    hiitEvidence: EligibleHiitEvidence()))),
            hiitUnderlyingCardioActivity: underlyingActivity);

        Assert.Equal(ExpectedSingleSession,
            Decision(result, WorkoutActivityType.Hiit).ScheduledSessionsPerWeek);
        Assert.Equal(ExpectedSingleSession, Decision(result, underlyingActivity).TargetSessionsPerWeek);
        Assert.Equal(ExpectedSingleSession,
            Decision(result, underlyingActivity).ScheduledSessionsPerWeek);
        Assert.Contains(MixedActivityWeekScheduleRule.OneContinuousSessionReplacedByHiit,
            Decision(result, underlyingActivity).Reasons);
        Assert.Equal(ExpectedTwoCompetingSessions, result.Sessions.Count);
        Assert.NotEqual(result.Sessions[FirstActivityIndex].Day, result.Sessions[SecondActivityIndex].Day);
        Assert.Equal((decimal)ActivityPrescriptionRule.HiitSessionDuration.TotalMinutes,
            result.Sessions.Single(session => session.Activity == WorkoutActivityType.Hiit).DurationMinutes);
        Assert.Equal(EstablishedCardioSessionMinutes,
            result.Sessions.Single(session => session.Activity == underlyingActivity).DurationMinutes);
    }

    [Fact]
    public void FailsClosedWhenActivitySpecificEligibilityEvidenceIsMissing()
    {
        var swimming = Evaluate(
            [WorkoutActivityType.Swimming],
            AllWeekdays,
            Durations((WorkoutActivityType.Swimming, SelectedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.Swimming, EvidenceFor(WorkoutActivityType.Swimming))));
        var climbing = Evaluate(
            [WorkoutActivityType.RockClimbing],
            AllWeekdays,
            Durations(),
            Evidence((WorkoutActivityType.RockClimbing, EvidenceFor(WorkoutActivityType.RockClimbing,
                appropriateClimbingInstructionAvailable: true))));
        var hiit = Evaluate(
            [WorkoutActivityType.Cycling, WorkoutActivityType.Hiit],
            AllWeekdays,
            Durations((WorkoutActivityType.Cycling, EstablishedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.Cycling, EvidenceFor(WorkoutActivityType.Cycling,
                ActivityExperience.Established,
                existingBaselineMinutes: ActivityPrescriptionRule.EstablishedCardioMainMinutesWithoutBaseline)),
                (WorkoutActivityType.Hiit, EvidenceFor(WorkoutActivityType.Hiit))),
            hiitUnderlyingCardioActivity: WorkoutActivityType.Cycling);

        Assert.Empty(swimming.Sessions);
        Assert.Contains(ActivityPrescriptionRuleNotEligibleReasons.SwimmingFacilityOrAbility,
            Decision(swimming, WorkoutActivityType.Swimming).Reasons);
        Assert.Contains(MixedActivityWeekScheduleRule.ClimbingCompetenceNotConfirmed,
            Decision(climbing, WorkoutActivityType.RockClimbing).Reasons);
        Assert.Collection(hiit.Sessions,
            session => Assert.Equal(WorkoutActivityType.Cycling, session.Activity),
            session => Assert.Equal(WorkoutActivityType.Cycling, session.Activity));
        Assert.Equal(MixedActivityScheduleDisposition.Blocked,
            Decision(hiit, WorkoutActivityType.Hiit).Disposition);
        Assert.Equal(ExpectedTwoCardioSessions, Decision(hiit, WorkoutActivityType.Cycling).TargetSessionsPerWeek);
    }

    [Fact]
    public void BlocksSelectedActivityWhenEligibilityEvidenceContainsDuplicateEntries()
    {
        var runningEvidence = EvidenceFor(WorkoutActivityType.Running);
        var result = Evaluate(
            [WorkoutActivityType.Running],
            AllWeekdays,
            Durations((WorkoutActivityType.Running, SelectedCardioSessionMinutes)),
            [runningEvidence, runningEvidence]);

        Assert.Equal(MixedActivityScheduleDisposition.Blocked,
            Decision(result, WorkoutActivityType.Running).Disposition);
        Assert.Contains(MixedActivityWeekScheduleRule.ActivityEligibilityEvidenceDuplicate,
            Decision(result, WorkoutActivityType.Running).Reasons);
    }

    [Fact]
    public void UsesSelectedRunningActivityAsHiitReplacementBase()
    {
        var result = Evaluate(
            [WorkoutActivityType.Running, WorkoutActivityType.Hiit],
            AllWeekdays,
            Durations((WorkoutActivityType.Running, EstablishedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.Running, EvidenceFor(WorkoutActivityType.Running,
                ActivityExperience.Established,
                existingBaselineMinutes: ActivityPrescriptionRule.EstablishedCardioMainMinutesWithoutBaseline)),
                (WorkoutActivityType.Hiit, EvidenceFor(WorkoutActivityType.Hiit,
                    hiitEvidence: EligibleHiitEvidence(WorkoutActivityType.Running)))),
            hiitUnderlyingCardioActivity: WorkoutActivityType.Running);

        Assert.Equal(MixedActivityScheduleDisposition.Scheduled,
            Decision(result, WorkoutActivityType.Hiit).Disposition);
        Assert.Equal(ExpectedSingleSession,
            Decision(result, WorkoutActivityType.Running).TargetSessionsPerWeek);
        Assert.Contains(MixedActivityWeekScheduleRule.OneContinuousSessionReplacedByHiit,
            Decision(result, WorkoutActivityType.Running).Reasons);
        Assert.Equal(ActivityModality.Running,
            Decision(result, WorkoutActivityType.Hiit).Prescription?.Modality);
    }

    [Fact]
    public void SchedulesSelectedStrengthSessionRegardlessOfDailyTotalDuration()
    {
        var result = Evaluate([WorkoutActivityType.GeneralStrength], AllWeekdays,
            Durations((WorkoutActivityType.GeneralStrength, StrengthSessionMinutes)),
            Evidence((WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength))));

        Assert.Equal(GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek, result.Sessions.Count);
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled,
            Decision(result, WorkoutActivityType.GeneralStrength).Disposition);
        Assert.All(result.Sessions, session =>
            Assert.Equal((decimal)StrengthSessionMinutes, session.DurationMinutes));
    }

    [Fact]
    public void UsesDefaultFrequencyWhenPreferenceIsMissing()
    {
        var yoga = Evaluate(
            [WorkoutActivityType.Yoga],
            AllWeekdays,
            Durations((WorkoutActivityType.Yoga, GentleYogaSessionMinutes)),
            Evidence((WorkoutActivityType.Yoga, EvidenceFor(WorkoutActivityType.Yoga,
                isDemandingYogaContent: false))));

        Assert.Equal(ActivitySessionDurationRule.RecoveryTargetMaxLimit, yoga.Sessions.Count);
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled,
            Decision(yoga, WorkoutActivityType.Yoga).Disposition);
        Assert.Equal(ActivitySessionDurationRule.RecoveryTargetMaxLimit,
            Decision(yoga, WorkoutActivityType.Yoga).TargetSessionsPerWeek);
        Assert.NotNull(Decision(yoga, WorkoutActivityType.Yoga).PolicySessionsPerWeek);
    }

    [Fact]
    public void AppliesExplicitPreferenceFrequencyAndDisplaysPolicyFrequency()
    {
        var yoga = Evaluate(
            [WorkoutActivityType.Yoga],
            AllWeekdays,
            Durations((WorkoutActivityType.Yoga, GentleYogaSessionMinutes)),
            Evidence((WorkoutActivityType.Yoga, EvidenceFor(WorkoutActivityType.Yoga,
                isDemandingYogaContent: false))),
            RequestedFrequencies((WorkoutActivityType.Yoga, ExpectedTwoCardioSessions)));

        Assert.Equal(ExpectedTwoCardioSessions, yoga.Sessions.Count);
        Assert.Equal(ExpectedTwoCardioSessions, Decision(yoga, WorkoutActivityType.Yoga).TargetSessionsPerWeek);
        Assert.NotNull(Decision(yoga, WorkoutActivityType.Yoga).PolicySessionsPerWeek);
        Assert.Equal(MixedActivityScheduleDisposition.Scheduled,
            Decision(yoga, WorkoutActivityType.Yoga).Disposition);
    }

    [Fact]
    public void OmitsOtherActivityBecausePersistedPlansDoNotSupportItsWorkoutType()
    {
        var otherPolicy = ActivitySessionDurationRule.ForUserPreference(WorkoutActivityType.Other);
        var other = Evaluate(
            [WorkoutActivityType.Other],
            AllWeekdays,
            Durations((WorkoutActivityType.Other, ExplicitOtherActivityMinutes)),
            Evidence((WorkoutActivityType.Other, EvidenceFor(WorkoutActivityType.Other))),
            RequestedFrequencies((WorkoutActivityType.Other, ExpectedSingleSession)));

        Assert.Null(otherPolicy);
        Assert.Empty(ActivitySessionDurationRule.ChoicesFor(WorkoutActivityType.Other));
        Assert.False(ActivitySessionDurationRule.IsValidSelection(
            WorkoutActivityType.Other, ExplicitOtherActivityMinutes));
        Assert.Empty(other.Sessions);
        Assert.Equal(MixedActivityScheduleDisposition.Omitted,
            Decision(other, WorkoutActivityType.Other).Disposition);
        Assert.Contains(MixedActivityWeekScheduleRule.ActivityNotSupportedByPersistedPlan,
            Decision(other, WorkoutActivityType.Other).Reasons);
    }

    [Fact]
    public void RejectsGentleMobilityDurationOutsideItsActivitySpecificRange()
    {
        var result = Evaluate(
            [WorkoutActivityType.Mobility],
            AllWeekdays,
            Durations((WorkoutActivityType.Mobility, InvalidMobilityPreferenceMinutes)),
            Evidence((WorkoutActivityType.Mobility, EvidenceFor(WorkoutActivityType.Mobility))),
            RequestedFrequencies((WorkoutActivityType.Mobility, ExpectedSingleSession)));

        Assert.Empty(result.Sessions);
        Assert.Contains(MixedActivityWeekScheduleRule.SessionDurationOutsideSavedPreferenceRange,
            Decision(result, WorkoutActivityType.Mobility).Reasons);
    }

    [Fact]
    public void RestDayDoesNotNeedADurationOrBecomeAWorkoutSession()
    {
        var rest = Evaluate(
            [WorkoutActivityType.RestDay],
            AllWeekdays,
            Durations(),
            Evidence());

        Assert.Empty(rest.Sessions);
        Assert.Equal(MixedActivityScheduleDisposition.NotScheduled,
            Decision(rest, WorkoutActivityType.RestDay).Disposition);
        Assert.Contains(MixedActivityWeekScheduleRule.RestDayIsNotAWorkoutSession,
            Decision(rest, WorkoutActivityType.RestDay).Reasons);
    }

    [Fact]
    public void SwimmingUsesBothAvailableStrengthDaysWhenItsFrequencyFits()
    {
        var result = Evaluate(
            [WorkoutActivityType.GeneralStrength, WorkoutActivityType.Swimming],
            [DayOfWeek.Monday, DayOfWeek.Thursday],
            Durations((WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
                (WorkoutActivityType.Swimming, SelectedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
                (WorkoutActivityType.Swimming, EvidenceFor(WorkoutActivityType.Swimming,
                    hasSwimmingFacilityAccess: true, canSwimIndependently: true,
                    wantsContinuousSwimming: true))),
            chosenStrengthActivity: WorkoutActivityType.GeneralStrength);

        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek,
            result.Sessions.Count(session => session.Activity == WorkoutActivityType.Swimming));
        Assert.All(result.Sessions.GroupBy(session => session.Day), day =>
            Assert.Equal(ActivitiesPerPackedDay, day.Count()));
        Assert.Empty(result.InputReasons);
    }

    [Fact]
    public void AllowsSwimmingOnlyWithConfirmedFacilityAndIndependentAbilityAndUsesItsPolicyLimit()
    {
        var swimming = Evaluate(
            [WorkoutActivityType.Swimming],
            AllWeekdays,
            Durations((WorkoutActivityType.Swimming, SelectedCardioSessionMinutes)),
            Evidence((WorkoutActivityType.Swimming, EvidenceFor(WorkoutActivityType.Swimming,
                hasSwimmingFacilityAccess: true,
                canSwimIndependently: true,
                wantsContinuousSwimming: true))));

        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek, swimming.Sessions.Count);
        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek,
            Decision(swimming, WorkoutActivityType.Swimming).TargetSessionsPerWeek);
        Assert.Equal(swimming.Sessions.Count, swimming.Sessions.Select(session => session.Day).Distinct().Count());
        var prescription = Decision(swimming, WorkoutActivityType.Swimming).Prescription;
        Assert.NotNull(prescription);
        Assert.Equal((decimal)(ActivityPrescriptionRule.SwimmingWarmupMinutes +
                               ActivityPrescriptionRule.BeginnerCardioMainMinutes +
                               ActivityPrescriptionRule.ContinuousCardioCooldownMinutes),
            (prescription!.WarmupMinutes ?? decimal.Zero) + (prescription.MainMinutes ?? decimal.Zero) +
            (prescription.CooldownMinutes ?? decimal.Zero));
    }

    [Fact]
    public void ReportsMissingDurationsAndInvalidAvailabilityAsReasons()
    {
        var missingDuration = Evaluate(
            [WorkoutActivityType.GeneralStrength], AllWeekdays, Durations(),
            Evidence((WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength))));
        var invalidAvailability = Evaluate(
            [WorkoutActivityType.GeneralStrength], [], Durations((WorkoutActivityType.GeneralStrength, StrengthSessionMinutes)),
            Evidence((WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength))));

        Assert.Contains(MixedActivityWeekScheduleRule.SessionDurationMissing,
            Decision(missingDuration, WorkoutActivityType.GeneralStrength).Reasons);
        Assert.Empty(invalidAvailability.Sessions);
        Assert.Contains(MixedActivityWeekScheduleRule.AvailableWeekdaysMustContainOneToSevenDistinctDays,
            invalidAvailability.InputReasons);
        Assert.Contains(MixedActivityWeekScheduleRule.AvailableWeekdaysMustContainOneToSevenDistinctDays,
            Decision(invalidAvailability, WorkoutActivityType.GeneralStrength).Reasons);
    }

    [Fact]
    public void ProducesTheSameScheduleRegardlessOfSelectionOrder()
    {
        var days = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
            DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var durations = Durations((WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
            (WorkoutActivityType.Running, SelectedCardioSessionMinutes),
            (WorkoutActivityType.Walking, SelectedCardioSessionMinutes));
        var evidence = Evidence(
            (WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
            (WorkoutActivityType.Running, EvidenceFor(WorkoutActivityType.Running)),
            (WorkoutActivityType.Walking, EvidenceFor(WorkoutActivityType.Walking)));

        var first = Evaluate([WorkoutActivityType.GeneralStrength, WorkoutActivityType.Running,
            WorkoutActivityType.Walking], days, durations, evidence);
        var second = Evaluate([WorkoutActivityType.Walking, WorkoutActivityType.GeneralStrength,
            WorkoutActivityType.Running], days, durations, evidence);

        Assert.Equal(first.Sessions, second.Sessions);
        Assert.Equal(first.Activities.Select(activity =>
                (activity.Activity, activity.Disposition, activity.PolicySessionsPerWeek,
                    activity.TargetSessionsPerWeek, activity.ScheduledSessionsPerWeek,
                    Reasons: string.Join(',', activity.Reasons))),
            second.Activities.Select(activity =>
                (activity.Activity, activity.Disposition, activity.PolicySessionsPerWeek,
                    activity.TargetSessionsPerWeek, activity.ScheduledSessionsPerWeek,
                    Reasons: string.Join(',', activity.Reasons))));
    }

    [Fact]
    public void CardioSessionsGenerateOwnDurationIgnoringUserSelectedDurations()
    {
        var days = AllWeekdays;
        // User picked 90m for Running and Walking, but cardio policy must generate its own duration
        var durations = Durations(
            (WorkoutActivityType.GeneralStrength, StrengthSessionMinutes),
            (WorkoutActivityType.Running, 90m),
            (WorkoutActivityType.Walking, 90m));
        var evidence = Evidence(
            (WorkoutActivityType.GeneralStrength, EvidenceFor(WorkoutActivityType.GeneralStrength)),
            (WorkoutActivityType.Running, EvidenceFor(WorkoutActivityType.Running,
                experience: ActivityExperience.Established, existingBaselineMinutes: 30)),
            (WorkoutActivityType.Walking, EvidenceFor(WorkoutActivityType.Walking,
                experience: ActivityExperience.Established, existingBaselineMinutes: 30)));

        var result = Evaluate([WorkoutActivityType.GeneralStrength, WorkoutActivityType.Running, WorkoutActivityType.Walking],
            days, durations, evidence);

        var runningSession = result.Sessions.FirstOrDefault(s => s.Activity == WorkoutActivityType.Running);
        var walkingSession = result.Sessions.FirstOrDefault(s => s.Activity == WorkoutActivityType.Walking);

        Assert.NotNull(runningSession);
        Assert.NotNull(walkingSession);
        // Established continuous cardio: 5 warmup + 30 main + 5 cooldown = 40 minutes (NOT 90)
        Assert.Equal(40m, runningSession.DurationMinutes);
        Assert.Equal(40m, walkingSession.DurationMinutes);
    }

    private static MixedActivityWeekScheduleResult Evaluate(
        IReadOnlyList<WorkoutActivityType> selectedActivities,
        IReadOnlyList<DayOfWeek> availableDays,
        IReadOnlyDictionary<WorkoutActivityType, decimal> durations,
        IReadOnlyList<ActivityPrescriptionContext> evidence,
        IReadOnlyDictionary<WorkoutActivityType, int>? requestedFrequencies = null,
        WorkoutActivityType? hiitUnderlyingCardioActivity = null,
        WorkoutActivityType? chosenStrengthActivity = null) =>
        MixedActivityWeekScheduleRule.Evaluate(new MixedActivityWeekScheduleContext(
            selectedActivities, availableDays, durations, evidence, requestedFrequencies,
            hiitUnderlyingCardioActivity, chosenStrengthActivity));

    private static ActivityPrescriptionContext EvidenceFor(
        WorkoutActivityType activity,
        ActivityExperience experience = ActivityExperience.Beginner,
        int? existingBaselineMinutes = null,
        bool isEstablishedResistanceHistory = false,
        int completedMainExerciseExposures = NoCompletedExerciseExposures,
        bool hasKnownManageableLoad = false,
        bool userConfirmedCurrentFamiliarity = false,
        HiitEligibilityEvidence? hiitEvidence = null,
        bool? hasSwimmingFacilityAccess = null,
        bool? canSwimIndependently = null,
        bool? wantsContinuousSwimming = null,
        bool? hasRopeClimbingCompetence = null,
        bool? appropriateClimbingInstructionAvailable = null,
        bool? isDemandingYogaContent = null) =>
        new(activity, experience, existingBaselineMinutes, false, true,
            isEstablishedResistanceHistory, completedMainExerciseExposures, hasKnownManageableLoad,
            userConfirmedCurrentFamiliarity, hiitEvidence, hasSwimmingFacilityAccess,
            canSwimIndependently, wantsContinuousSwimming, HasRopeClimbingCompetence: hasRopeClimbingCompetence,
            AppropriateClimbingInstructionAvailable: appropriateClimbingInstructionAvailable,
            IsDemandingYogaContent: isDemandingYogaContent);

    private static HiitEligibilityEvidence EligibleHiitEvidence(
        WorkoutActivityType underlyingActivity = WorkoutActivityType.Cycling)
    {
        var weeks = Enumerable.Range(FirstHistoricalWeekOffset, ActivityPrescriptionRule.HiitRequiredPriorWeeks)
            .Select(week => new HiitAerobicWeekEvidence(week,
                ActivityPrescriptionRule.HiitRequiredAerobicSessionsPerWeek))
            .ToArray();
        return new(true, underlyingActivity, ActivityExperience.Established, weeks, false, true);
    }

    private static IReadOnlyDictionary<WorkoutActivityType, decimal> Durations(
        params (WorkoutActivityType Activity, decimal Minutes)[] entries) =>
        entries.ToDictionary(entry => entry.Activity, entry => entry.Minutes);

    private static IReadOnlyDictionary<WorkoutActivityType, int> RequestedFrequencies(
        params (WorkoutActivityType Activity, int Sessions)[] entries) =>
        entries.ToDictionary(entry => entry.Activity, entry => entry.Sessions);

    private static IReadOnlyList<ActivityPrescriptionContext> Evidence(
        params (WorkoutActivityType Activity, ActivityPrescriptionContext Context)[] entries) =>
        entries.Select(entry => entry.Context).ToArray();

    private static MixedActivityScheduleDecision Decision(MixedActivityWeekScheduleResult result,
        WorkoutActivityType activity) => result.Activities.Single(decision => decision.Activity == activity);

    private static readonly DayOfWeek[] AllWeekdays =
    [
        DayOfWeek.Monday,
        DayOfWeek.Tuesday,
        DayOfWeek.Wednesday,
        DayOfWeek.Thursday,
        DayOfWeek.Friday,
        DayOfWeek.Saturday,
        DayOfWeek.Sunday
    ];

    private static class ActivityPrescriptionRuleNotEligibleReasons
    {
        public const string SwimmingFacilityOrAbility =
            "ConfirmFacilityAccessAndIndependentAbilityOrChooseInstructorLedActivity";
    }
}

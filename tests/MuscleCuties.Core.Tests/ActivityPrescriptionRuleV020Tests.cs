using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class ActivityPrescriptionRuleV020Tests
{
    private const int FourWeeks = WorkoutTrainingPolicyV020.HiitRequiredPriorWeeks;
    private const int AerobicSessionsPerWeek = WorkoutTrainingPolicyV020.HiitRequiredAerobicSessionsPerWeek;
    private const int ManageableAerobicSessionMinutes = WorkoutTrainingPolicyV020.HiitMinimumManageableSessionMinutes;
    private const int EstablishedExerciseExposures = WorkoutTrainingPolicyV020.StrengthHighIntensityRequiredExerciseExposures;

    [Fact]
    public void GeneralStrength_UsesBeginnerAndEstablishedRanges()
    {
        var beginner = Evaluate(WorkoutActivityType.GeneralStrength);
        var established = Evaluate(WorkoutActivityType.GeneralStrength,
            ActivityExperienceV020.Established);

        Assert.Equal(WorkoutTrainingPolicyV020.GeneralMainMinimumRepetitions,
            beginner.Sessions.Single().MinimumRepetitions);
        Assert.Equal(WorkoutTrainingPolicyV020.EstablishedMainMinimumRepetitions,
            established.Sessions.Single().MinimumRepetitions);
        Assert.Equal(WorkoutTrainingPolicyV020.InitialRepetitionsInReserve,
            beginner.Sessions.Single().RepetitionsInReserve);
    }

    [Fact]
    public void HighVolumeAndHeavyStrengthOfferGeneralStrengthUntilEligible()
    {
        var volume = Evaluate(WorkoutActivityType.HighVolumeStrength,
            ActivityExperienceV020.Beginner);
        var heavy = Evaluate(WorkoutActivityType.StrengthHighIntensity,
            ActivityExperienceV020.Established, exposures: EstablishedExerciseExposures - 1,
                knownLoad: true, familiar: true, establishedResistance: true);

        Assert.Equal(ActivityPrescriptionStatusV020.OfferGeneralStrength, volume.Status);
        Assert.Equal(ActivityPrescriptionStatusV020.OfferGeneralStrength, heavy.Status);
    }

    [Fact]
    public void EstablishedHeavyStrengthRequiresExposuresManageableLoadAndConfirmation()
    {
        var result = Evaluate(WorkoutActivityType.StrengthHighIntensity,
            ActivityExperienceV020.Established, exposures: EstablishedExerciseExposures,
            knownLoad: true, familiar: true, establishedResistance: true);

        Assert.Equal(ActivityPrescriptionStatusV020.Eligible, result.Status);
        Assert.Equal(WorkoutTrainingPolicyV020.StrengthHighIntensityMainMinimumRepetitions,
            result.Sessions.Single().MinimumRepetitions);
        Assert.Equal(WorkoutTrainingPolicyV020.StrengthHighIntensityMainRestSeconds,
            result.Sessions.Single().RestSeconds);
    }

    [Fact]
    public void CardioKeepsEstablishedBaselineAndUsesNoInventedPace()
    {
        const int ExistingBaseline = 37;
        var retained = Evaluate(WorkoutActivityType.Running, ActivityExperienceV020.Established,
            baselineMinutes: ExistingBaseline);
        var starter = Evaluate(WorkoutActivityType.Cycling, ActivityExperienceV020.Unknown);

        Assert.Equal(ExistingBaseline, retained.Sessions.Single().MainMinutes);
        Assert.Equal(WorkoutTrainingPolicyV020.BeginnerCardioMainMinutes,
            starter.Sessions.Single().MainMinutes);
        Assert.Contains("ComfortableToModerateWithoutInventedPaceOrDistance", starter.Reasons);
    }

    [Fact]
    public void SwimmingRequestsAbilityAccessAndPreferenceThenUsesTimeBasedSession()
    {
        var unknown = Evaluate(WorkoutActivityType.Swimming);
        var ready = EvaluateSwimming(access: true, independent: true, continuous: true);
        var intervals = EvaluateSwimming(access: true, independent: true, continuous: false);

        Assert.Equal(ActivityPrescriptionStatusV020.NeedsFamiliarization, unknown.Status);
        Assert.Equal(ActivityPrescriptionStatusV020.Eligible, ready.Status);
        Assert.Equal(WorkoutTrainingPolicyV020.SwimmingWarmupMinutes, ready.Sessions.Single().WarmupMinutes);
        Assert.Contains("TimeBasedWithoutInventedDistanceOrPace", ready.Reasons);
        Assert.Equal(SessionTrainingStyleV02.Intervals, intervals.Sessions.Single().TrainingStyle);
        Assert.Equal(30, intervals.Sessions.Single().IntervalWorkSeconds);
        Assert.Equal(WorkoutTrainingPolicyV020.BeginnerCardioMainMinutes,
            intervals.Sessions.Single().IntervalWorkCount);
    }

    [Fact]
    public void HiitRequiresThreeManageableTwentyMinuteSessionsInEachOfFourWeeks()
    {
        var evidence = EligibleHiitEvidence();
        var eligible = EvaluateHiit(evidence);
        var missingWeek = evidence with
        {
            PreviousFourWeeksAerobicSessions = evidence.PreviousFourWeeksAerobicSessions
                .Where(session => session.WeekOffsetFromCurrent != FourWeeks).ToArray()
        };
        var blocked = EvaluateHiit(missingWeek);

        Assert.Equal(ActivityPrescriptionStatusV020.Eligible, eligible.Status);
        Assert.Equal(TimeSpan.FromSeconds(WorkoutTrainingPolicyV020.HiitTotalSeconds),
            WorkoutTrainingPolicyV020.HiitSessionDuration);
        Assert.Equal(ActivityModalityV02.Cycling, eligible.Sessions.Single().Modality);
        Assert.Equal(ActivityPrescriptionStatusV020.BlockedByRestriction, blocked.Status);
    }

    [Fact]
    public void ClimbingRequiresInstructionForBeginnerAndYogaRequiresChosenDuration()
    {
        var climbing = EvaluateClimbing(true);
        var yogaNeedsDuration = EvaluateYoga(null);
        var yoga = EvaluateYoga(WorkoutTrainingPolicyV020.GentleYogaMinimumMinutes);

        Assert.Equal(WorkoutTrainingPolicyV020.BeginnerClimbingVisitMinutes,
            climbing.Sessions.Single().MainMinutes);
        Assert.Equal(ActivityPrescriptionStatusV020.NeedsUserDuration, yogaNeedsDuration.Status);
        Assert.Equal(ActivityPrescriptionStatusV020.Eligible, yoga.Status);
    }

    [Fact]
    public void ExplicitRestAndRestrictionAreNotTreatedAsGeneratedHardWork()
    {
        var rest = Evaluate(WorkoutActivityType.RestDay);
        var restricted = ActivityPrescriptionRuleV020.Evaluate(Context(WorkoutActivityType.Cycling) with
        {
            HasCurrentRestrictionOrSymptom = true
        });

        Assert.Empty(rest.Sessions);
        Assert.Equal(ActivityPrescriptionStatusV020.Eligible, rest.Status);
        Assert.Equal(ActivityPrescriptionStatusV020.BlockedByRestriction, restricted.Status);
    }

    private static ActivityPrescriptionResultV020 Evaluate(WorkoutActivityType activity,
        ActivityExperienceV020 experience = ActivityExperienceV020.Unknown,
        int? baselineMinutes = null,
        int exposures = 0,
        bool knownLoad = false,
        bool familiar = false,
        bool establishedResistance = false) =>
        ActivityPrescriptionRuleV020.Evaluate(Context(activity, experience) with
        {
            ExistingBaselineMinutes = baselineMinutes,
            CompletedMainExerciseExposures = exposures,
            HasKnownManageableLoad = knownLoad,
            UserConfirmedCurrentFamiliarity = familiar,
            IsEstablishedResistanceHistory = establishedResistance
        });

    private static ActivityPrescriptionContextV020 Context(WorkoutActivityType activity,
        ActivityExperienceV020 experience = ActivityExperienceV020.Unknown) =>
        new(activity, experience, null, false, true, false, 0, false, false);

    private static HiitEligibilityEvidenceV020 EligibleHiitEvidence() => new(true, true,
        Enumerable.Range(1, FourWeeks)
            .SelectMany(week => Enumerable.Range(1, AerobicSessionsPerWeek)
                .Select(_ => new HiitAerobicSessionEvidenceV020(week, ManageableAerobicSessionMinutes, true)))
            .ToArray(), false, true);

    private static ActivityPrescriptionResultV020 EvaluateHiit(
        HiitEligibilityEvidenceV020 evidence) =>
        ActivityPrescriptionRuleV020.Evaluate(Context(WorkoutActivityType.Hiit) with { HiitEvidence = evidence });

    private static ActivityPrescriptionResultV020 EvaluateSwimming(
        bool access, bool independent, bool continuous) =>
        ActivityPrescriptionRuleV020.Evaluate(Context(WorkoutActivityType.Swimming) with
        {
            HasSwimmingFacilityAccess = access,
            CanSwimIndependently = independent,
            WantsContinuousSwimming = continuous
        });

    private static ActivityPrescriptionResultV020 EvaluateClimbing(bool available) =>
        ActivityPrescriptionRuleV020.Evaluate(Context(WorkoutActivityType.RockClimbing) with
        {
            AppropriateClimbingInstructionAvailable = available
        });

    private static ActivityPrescriptionResultV020 EvaluateYoga(int? minutes) =>
        ActivityPrescriptionRuleV020.Evaluate(Context(WorkoutActivityType.Yoga) with
        {
            UserSelectedDurationMinutes = minutes,
            IsDemandingYogaContent = false
        });
}

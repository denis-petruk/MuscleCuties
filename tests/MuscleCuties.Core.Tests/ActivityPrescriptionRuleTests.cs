using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class ActivityPrescriptionRuleTests
{
    private const int FourWeeks = ActivityPrescriptionRule.HiitRequiredPriorWeeks;
    private const int AerobicSessionsPerWeek = ActivityPrescriptionRule.HiitRequiredAerobicSessionsPerWeek;
    private const int EstablishedExerciseExposures = ActivityPrescriptionRule.StrengthHighIntensityRequiredExerciseExposures;
    private const int SelectedActivityDurationMinutes = WorkoutPreferencesRule.MinimumActivitySessionDurationMinutes +
                                                       WorkoutPreferencesRule.ActivitySessionDurationIncrementMinutes;
    private const int SelectedSwimmingSessionDurationMinutes = ActivitySessionDurationRule.SwimmingDefaultMinutes;
    private const int InvalidActivityDurationMinutes = WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes +
                                                       ActivityPrescriptionRule.ContinuousCardioWarmupMinutes;

    [Fact]
    public void GeneralStrength_UsesBeginnerAndEstablishedRanges()
    {
        var beginner = Evaluate(WorkoutActivityType.GeneralStrength);
        var established = Evaluate(WorkoutActivityType.GeneralStrength,
            ActivityExperience.Established);

        Assert.Equal(ActivityPrescriptionRule.GeneralMainMinimumRepetitions,
            beginner.Sessions.Single().MinimumRepetitions);
        Assert.Equal(ActivityPrescriptionRule.EstablishedMainMinimumRepetitions,
            established.Sessions.Single().MinimumRepetitions);
        Assert.Equal(ActivityPrescriptionRule.InitialRepetitionsInReserve,
            beginner.Sessions.Single().RepetitionsInReserve);
    }

    [Fact]
    public void HighVolumeStartsConservativelyWhileHeavyStrengthStillRequiresEvidence()
    {
        var volume = Evaluate(WorkoutActivityType.HighVolumeStrength,
            ActivityExperience.Beginner);
        var heavy = Evaluate(WorkoutActivityType.StrengthHighIntensity,
            ActivityExperience.Established, exposures: EstablishedExerciseExposures - 1,
                knownLoad: true, familiar: true, establishedResistance: true);

        Assert.Equal(ActivityPrescriptionStatus.Eligible, volume.Status);
        Assert.Equal(ActivityPrescriptionRule.HighVolumeStartingMainSets, volume.Sessions.Single().Sets);
        Assert.Equal(ActivityPrescriptionRule.HighVolumeStartingRepetitionsInReserve,
            volume.Sessions.Single().RepetitionsInReserve);
        Assert.Equal(ActivityPrescriptionStatus.OfferGeneralStrength, heavy.Status);
    }

    [Fact]
    public void EstablishedHeavyStrengthRequiresExposuresManageableLoadAndConfirmation()
    {
        var result = Evaluate(WorkoutActivityType.StrengthHighIntensity,
            ActivityExperience.Established, exposures: EstablishedExerciseExposures,
            knownLoad: true, familiar: true, establishedResistance: true);

        Assert.Equal(ActivityPrescriptionStatus.Eligible, result.Status);
        Assert.Equal(ActivityPrescriptionRule.StrengthHighIntensityMainMinimumRepetitions,
            result.Sessions.Single().MinimumRepetitions);
        Assert.Equal(ActivityPrescriptionRule.StrengthHighIntensityMainRestSeconds,
            result.Sessions.Single().RestSeconds);
    }

    [Fact]
    public void CardioKeepsEstablishedBaselineAndUsesNoInventedPace()
    {
        const int ExistingBaseline = 37;
        var retained = Evaluate(WorkoutActivityType.Running, ActivityExperience.Established,
            baselineMinutes: ExistingBaseline);
        var starter = Evaluate(WorkoutActivityType.Cycling, ActivityExperience.Unknown);

        Assert.Equal(ExistingBaseline, retained.Sessions.Single().MainMinutes);
        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioMainMinutes,
            starter.Sessions.Single().MainMinutes);
        Assert.Contains("ComfortableToModerateWithoutInventedPaceOrDistance", starter.Reasons);
    }

    [Fact]
    public void CardioDurationIsCalculatedFromBaselineRatherThanStrengthSessionChoice()
    {
        const int ExistingBaselineMinutes = 37;
        var selected = ActivityPrescriptionRule.Evaluate(Context(WorkoutActivityType.Running,
            ActivityExperience.Established) with
        {
            ExistingBaselineMinutes = ExistingBaselineMinutes,
            UserSelectedDurationMinutes = SelectedActivityDurationMinutes
        });
        var invalid = ActivityPrescriptionRule.Evaluate(Context(WorkoutActivityType.Running) with
        {
            UserSelectedDurationMinutes = InvalidActivityDurationMinutes
        });

        Assert.Equal(ActivityPrescriptionStatus.Eligible, selected.Status);
        Assert.Equal(ExistingBaselineMinutes, selected.Sessions.Single().MainMinutes);
        Assert.Equal(ActivityPrescriptionStatus.Eligible, invalid.Status);
        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioMainMinutes,
            invalid.Sessions.Single().MainMinutes);
    }

    [Fact]
    public void SwimmingRequestsAbilityAccessAndPreferenceThenUsesTimeBasedSession()
    {
        var unknown = Evaluate(WorkoutActivityType.Swimming);
        var ready = EvaluateSwimming(access: true, independent: true, continuous: true);
        var intervals = EvaluateSwimming(access: true, independent: true, continuous: false);

        Assert.Equal(ActivityPrescriptionStatus.NeedsFamiliarization, unknown.Status);
        Assert.Equal(ActivityPrescriptionStatus.Eligible, ready.Status);
        Assert.Equal(ActivityPrescriptionRule.SwimmingWarmupMinutes, ready.Sessions.Single().WarmupMinutes);
        Assert.Contains("TimeBasedWithoutInventedDistanceOrPace", ready.Reasons);
        Assert.Equal(SessionTrainingStyle.Intervals, intervals.Sessions.Single().TrainingStyle);
        Assert.Equal(30, intervals.Sessions.Single().IntervalWorkSeconds);
        Assert.Equal(ActivityPrescriptionRule.BeginnerCardioMainMinutes,
            intervals.Sessions.Single().IntervalWorkCount);
    }

    [Fact]
    public void SwimmingCalculatesIntervalDurationIndependentlyOfSessionChoice()
    {
        var result = EvaluateSwimming(access: true, independent: true, continuous: false,
            selectedDurationMinutes: SelectedSwimmingSessionDurationMinutes);
        var session = result.Sessions.Single();
        var expectedMainMinutes = ActivityPrescriptionRule.BeginnerCardioMainMinutes;

        Assert.Equal(ActivityPrescriptionStatus.Eligible, result.Status);
        Assert.Equal(expectedMainMinutes, session.MainMinutes);
        Assert.Equal(expectedMainMinutes, session.IntervalWorkCount);
        Assert.Equal(ActivityPrescriptionRule.SwimmingIntervalSeconds, session.IntervalWorkSeconds);
    }

    [Fact]
    public void HiitRequiresThreeQualifyingManageableSessionsInEachPriorWeek()
    {
        var evidence = EligibleHiitEvidence();
        var eligible = EvaluateHiit(evidence);
        var missingWeek = evidence with
        {
            PreviousFourWeeksAerobicEvidence = evidence.PreviousFourWeeksAerobicEvidence
                .Where(session => session.WeekOffsetFromCurrent != FourWeeks).ToArray()
        };
        var blocked = EvaluateHiit(missingWeek);

        Assert.Equal(ActivityPrescriptionStatus.Eligible, eligible.Status);
        Assert.Equal(TimeSpan.FromSeconds(ActivityPrescriptionRule.HiitTotalSeconds),
            ActivityPrescriptionRule.HiitSessionDuration);
        Assert.Equal(ActivityModality.Cycling, eligible.Sessions.Single().Modality);
        Assert.Equal(ActivityPrescriptionStatus.BlockedByRestriction, blocked.Status);
    }

    [Theory]
    [InlineData(WorkoutActivityType.Cycling, ActivityModality.Cycling)]
    [InlineData(WorkoutActivityType.Running, ActivityModality.Running)]
    [InlineData(WorkoutActivityType.Walking, ActivityModality.Walking)]
    [InlineData(WorkoutActivityType.Rowing, ActivityModality.Rowing)]
    [InlineData(WorkoutActivityType.Elliptical, ActivityModality.Elliptical)]
    public void HiitPrescriptionRetainsTheExplicitlySelectedUnderlyingModality(
        WorkoutActivityType underlyingActivity,
        ActivityModality expectedModality)
    {
        var evidence = EligibleHiitEvidence(underlyingActivity);

        var result = EvaluateHiit(evidence);

        Assert.Equal(ActivityPrescriptionStatus.Eligible, result.Status);
        Assert.Equal(expectedModality, result.Sessions.Single().Modality);
        Assert.Contains("SelectedUnderlyingCardioActivity", result.Sessions.Single().Reasons);
    }

    [Fact]
    public void HiitEvidenceRequiresEveryPriorWeekToHaveThreeQualifyingSessionsAndUniqueWeekOffsets()
    {
        var evidence = EligibleHiitEvidence();
        var belowWeeklyThreshold = evidence with
        {
            PreviousFourWeeksAerobicEvidence = evidence.PreviousFourWeeksAerobicEvidence
                .Select(week => week.WeekOffsetFromCurrent == FourWeeks
                    ? week with { QualifyingManageableAerobicSessionCount = AerobicSessionsPerWeek - 1 }
                    : week).ToArray()
        };
        var duplicateWeek = evidence with
        {
            PreviousFourWeeksAerobicEvidence = evidence.PreviousFourWeeksAerobicEvidence
                .Append(evidence.PreviousFourWeeksAerobicEvidence[0]).ToArray()
        };

        Assert.True(HiitEligibilityEvidenceRule.IsEligible(evidence));
        Assert.False(HiitEligibilityEvidenceRule.IsEligible(belowWeeklyThreshold));
        Assert.False(HiitEligibilityEvidenceRule.IsEligible(duplicateWeek));
        Assert.False(HiitEligibilityEvidenceRule.IsEligible(evidence with
        {
            ExistingClearanceScreeningSatisfied = false
        }));
        Assert.False(HiitEligibilityEvidenceRule.IsEligible(evidence with
        {
            ExplicitOptIn = false
        }));
    }

    [Fact]
    public void ClimbingRequiresInstructionForBeginnerAndYogaRequiresChosenDuration()
    {
        var climbing = EvaluateClimbing(true);
        var yogaNeedsDuration = EvaluateYoga(null);
        var yoga = EvaluateYoga(ActivitySessionDurationRule.GentleYogaMinimumMinutes);

        Assert.Equal(ActivityPrescriptionRule.BeginnerClimbingVisitMinutes,
            climbing.Sessions.Single().MainMinutes);
        Assert.Equal(ActivityPrescriptionStatus.Eligible, yogaNeedsDuration.Status);
        Assert.Equal(ActivitySessionDurationRule.YogaDurationMinutesDefault,
            yogaNeedsDuration.Sessions.Single().MainMinutes);
        Assert.Equal(ActivityPrescriptionStatus.Eligible, yoga.Status);
    }

    [Fact]
    public void ExplicitRestAndRestrictionAreNotTreatedAsGeneratedHardWork()
    {
        var rest = Evaluate(WorkoutActivityType.RestDay);
        var restricted = ActivityPrescriptionRule.Evaluate(Context(WorkoutActivityType.Cycling) with
        {
            HasCurrentRestrictionOrSymptom = true
        });

        Assert.Empty(rest.Sessions);
        Assert.Equal(ActivityPrescriptionStatus.Eligible, rest.Status);
        Assert.Equal(ActivityPrescriptionStatus.BlockedByRestriction, restricted.Status);
    }

    private static ActivityPrescriptionResult Evaluate(WorkoutActivityType activity,
        ActivityExperience experience = ActivityExperience.Unknown,
        int? baselineMinutes = null,
        int exposures = 0,
        bool knownLoad = false,
        bool familiar = false,
        bool establishedResistance = false) =>
        ActivityPrescriptionRule.Evaluate(Context(activity, experience) with
        {
            ExistingBaselineMinutes = baselineMinutes,
            CompletedMainExerciseExposures = exposures,
            HasKnownManageableLoad = knownLoad,
            UserConfirmedCurrentFamiliarity = familiar,
            IsEstablishedResistanceHistory = establishedResistance
        });

    private static ActivityPrescriptionContext Context(WorkoutActivityType activity,
        ActivityExperience experience = ActivityExperience.Unknown) =>
        new(activity, experience, null, false, true, false, 0, false, false);

    private static HiitEligibilityEvidence EligibleHiitEvidence(
        WorkoutActivityType underlyingCardioActivity = WorkoutActivityType.Cycling) => new(true,
        underlyingCardioActivity, ActivityExperience.Established,
        Enumerable.Range(1, FourWeeks)
            .Select(week => new HiitAerobicWeekEvidence(week, AerobicSessionsPerWeek))
            .ToArray(), false, true);

    private static ActivityPrescriptionResult EvaluateHiit(
        HiitEligibilityEvidence evidence) =>
        ActivityPrescriptionRule.Evaluate(Context(WorkoutActivityType.Hiit) with { HiitEvidence = evidence });

    private static ActivityPrescriptionResult EvaluateSwimming(
        bool access, bool independent, bool continuous, int? selectedDurationMinutes = null) =>
        ActivityPrescriptionRule.Evaluate(Context(WorkoutActivityType.Swimming) with
        {
            HasSwimmingFacilityAccess = access,
            CanSwimIndependently = independent,
            WantsContinuousSwimming = continuous,
            UserSelectedDurationMinutes = selectedDurationMinutes
        });

    private static ActivityPrescriptionResult EvaluateClimbing(bool available) =>
        ActivityPrescriptionRule.Evaluate(Context(WorkoutActivityType.RockClimbing) with
        {
            AppropriateClimbingInstructionAvailable = available
        });

    private static ActivityPrescriptionResult EvaluateYoga(int? minutes) =>
        ActivityPrescriptionRule.Evaluate(Context(WorkoutActivityType.Yoga) with
        {
            UserSelectedDurationMinutes = minutes,
            IsDemandingYogaContent = false
        });
}

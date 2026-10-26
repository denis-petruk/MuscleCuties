using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ActivityPrescriptionStatus
{
    Eligible,
    OfferGeneralStrength,
    NeedsFamiliarization,
    NeedsUserDuration,
    BlockedByRestriction,
    NotSelected,
    Unsupported
}

public sealed record ActivityPrescriptionContext(
    WorkoutActivityType Activity,
    ActivityExperience Experience,
    int? ExistingBaselineMinutes,
    bool HasCurrentRestrictionOrSymptom,
    bool IsExplicitlySelected,
    bool IsEstablishedResistanceHistory,
    int CompletedMainExerciseExposures,
    bool HasKnownManageableLoad,
    bool UserConfirmedCurrentFamiliarity,
    HiitEligibilityEvidence? HiitEvidence = null,
    bool? HasSwimmingFacilityAccess = null,
    bool? CanSwimIndependently = null,
    bool? WantsContinuousSwimming = null,
    int? UserSelectedDurationMinutes = null,
    int? CatalogDurationMinutes = null,
    bool? HasRopeClimbingCompetence = null,
    bool? AppropriateClimbingInstructionAvailable = null,
    bool? IsDemandingYogaContent = null,
    bool? UserSelectedLighterWeek = null,
    string? ActivityName = null,
    IReadOnlyList<string>? ContentTags = null);

public sealed record ActivitySessionPrescription(
    WorkoutActivityType Activity,
    SessionPurpose Purpose,
    ActivityModality Modality,
    IntendedIntensity Intensity,
    SessionTrainingStyle TrainingStyle,
    int FrequencyPerWeek,
    decimal? WarmupMinutes,
    decimal? MainMinutes,
    decimal? CooldownMinutes,
    int? Sets,
    int? MinimumRepetitions,
    int? MaximumRepetitions,
    int? RepetitionsInReserve,
    int? RestSeconds,
    int? IntervalWorkCount,
    decimal? IntervalWorkSeconds,
    int? IntervalRecoveryCount,
    decimal? IntervalRecoverySeconds,
    bool RequiresUserConfirmation,
    IReadOnlyList<string> Reasons);

public sealed record ActivityPrescriptionResult(
    ActivityPrescriptionStatus Status,
    IReadOnlyList<ActivitySessionPrescription> Sessions,
    IReadOnlyList<string> Reasons);

/// <summary>Applies the activity-specific workout prescription rules.</summary>
public static class ActivityPrescriptionRule
{
    public const int GeneralStrengthSessionsPerWeek = GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek;
    public const int HighVolumeStrengthSessionsPerWeek = GeneralStrengthFrequencyResolver.MinimumStrengthSessionsPerWeek;
    public const int GeneralStrengthWorkingSets = 2;
    public const int EstablishedStrengthWorkingSetsInitially = 2;
    public const int GeneralMainMinimumRepetitions = 8;
    public const int GeneralMainMaximumRepetitions = 12;
    public const int EstablishedMainMinimumRepetitions = 6;
    public const int EstablishedMainMaximumRepetitions = 12;
    public const int AccessoryMinimumRepetitions = 10;
    public const int AccessoryMaximumRepetitions = 15;
    public const int GeneralStrengthAccessoryMinimumSets = 1;
    public const int GeneralStrengthAccessoryMaximumSets = 2;
    public const int GeneralStrengthAccessoryMinimumRepetitions = 10;
    public const int GeneralStrengthAccessoryMaximumRepetitions = 20;
    public const int GeneralStrengthAccessoryTargetRirMinimum = 2;
    public const int GeneralStrengthAccessoryTargetRirMaximum = 3;
    public const int InitialRepetitionsInReserve = 3;
    public const int GeneralMainRestSeconds = 120;
    public const int EstablishedMainRestSeconds = 150;
    public const int AccessoryRestSeconds = 90;
    public const int HighVolumeMainSets = 3;
    public const int HighVolumeAccessorySets = 2;
    public const int HighVolumeStartingMainSets = 2;
    public const int HighVolumeStartingAccessorySets = 1;
    public const int HighVolumeStartingRepetitionsInReserve = 4;
    public const string HighVolumeConservativeStartCode = "HighVolumeConservativeStart";
    public const int HighVolumeMainMinimumRepetitions = 8;
    public const int HighVolumeMainMaximumRepetitions = 12;
    public const int HighVolumeMaximumWorkingExercisesPerSession = 5;
    public const int HighVolumeInitialDirectSetsPerPrimaryMusclePerWeek = 10;
    public const int StrengthHighIntensityMainSets = 3;
    public const int StrengthHighIntensityAccessorySets = 2;
    public const int StrengthHighIntensityMainMinimumRepetitions = 4;
    public const int StrengthHighIntensityMainMaximumRepetitions = 6;
    public const int StrengthHighIntensityMainRestSeconds = 180;
    public const int StrengthHighIntensityMaximumHeavyMainMovementsPerSession = 2;
    public const int StrengthHighIntensityMaximumWorkingExercisesPerSession = 4;
    public const int StrengthHighIntensityMaximumSessionsPerWeekInitially = 2;
    public const int StrengthHighIntensityRequiredExerciseExposures = 4;

    public const int ContinuousCardioWarmupMinutes = 5;
    public const int ContinuousCardioCooldownMinutes = 5;
    public const int BeginnerCardioMainMinutes = 10;
    public const int EstablishedCardioMainMinutesWithoutBaseline = 20;
    public const int BeginnerCardioSessionsPerWeek = 2;
    public const int EstablishedCardioSessionsPerWeekWithoutBaseline = 2;
    public const int SwimmingWarmupMinutes = 5;
    public const int SwimmingIntervalSeconds = 30;
    public const int BeginnerClimbingVisitMinutes = 45;
    public const int EstablishedClimbingVisitMinutesWithoutBaseline = 60;
    public const int HiitMaximumSessionsPerWeek = 1;
    public const int HiitRequiredAerobicSessionsPerWeek = 3;
    public const int HiitRequiredPriorWeeks = 4;
    public const int HiitMinimumManageableSessionMinutes = 20;
    public const int HiitWarmupMinutes = 10;
    public const int HiitWorkIntervals = 4;
    public const int HiitWorkIntervalSeconds = 30;
    public const int HiitRecoveryIntervals = 3;
    public const int HiitRecoveryIntervalSeconds = 90;
    public const int HiitCooldownMinutes = 5;
    public const int HiitTotalSeconds = 1290;
    private const int GeneralStrengthAccessoryRestSeconds = AccessoryRestSeconds;
    private const int HighVolumeMainRestSeconds = EstablishedMainRestSeconds;
    private const int ClimbingMaximumSessionsPerWeek = 1;
    private const int ContinuousCardioFrequency = ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek;
    private const int EstablishedCardioFrequency = ActivityPrescriptionRule.EstablishedCardioSessionsPerWeekWithoutBaseline;
    private const int IntervalSecondsPerMinute = 60;
    private const int NoPrescriptionMinutes = 0;

    public static TimeSpan HiitSessionDuration => TimeSpan.FromSeconds(HiitTotalSeconds);

    public static ActivityPrescriptionResult Evaluate(ActivityPrescriptionContext? context)
    {
        if (context is null || !Enum.IsDefined(context.Activity) || !Enum.IsDefined(context.Experience) ||
            context.ExistingBaselineMinutes is < 0 || context.UserSelectedDurationMinutes is < 0 ||
            context.CatalogDurationMinutes is < 0 || context.CompletedMainExerciseExposures < 0)
            return Result(ActivityPrescriptionStatus.Unsupported, "InvalidActivityInput");
        if (!context.IsExplicitlySelected)
            return Result(ActivityPrescriptionStatus.NotSelected, "ActivityNotSelected");
        if (context.HasCurrentRestrictionOrSymptom)
            return Result(ActivityPrescriptionStatus.BlockedByRestriction, "RestrictionOrSymptomRequiresReview");

        return context.Activity switch
        {
            WorkoutActivityType.GeneralStrength => GeneralStrength(context),
            WorkoutActivityType.HighVolumeStrength => HighVolumeStrength(context),
            WorkoutActivityType.StrengthHighIntensity => HighIntensityStrength(context),
            WorkoutActivityType.Cycling or WorkoutActivityType.Running or WorkoutActivityType.Walking or
                WorkoutActivityType.Rowing or WorkoutActivityType.Elliptical => ContinuousCardio(context),
            WorkoutActivityType.Swimming => Swimming(context),
            WorkoutActivityType.Hiit => Hiit(context),
            WorkoutActivityType.RockClimbing => Climbing(context),
            WorkoutActivityType.Yoga => Yoga(context),
            WorkoutActivityType.Mobility => OptionalDuration(context,
                ActivitySessionDurationRule.GentleMobilityMinimumMinutes,
                ActivitySessionDurationRule.GentleMobilityMaximumMinutes,
                SessionPurpose.Mobility, SessionTrainingStyle.Gentle,
                ActivitySessionDurationRule.MobilityDurationMinutesDefault),
            WorkoutActivityType.GentleRecovery => OptionalDuration(context,
                ActivitySessionDurationRule.GentleRecoveryMinimumMinutes,
                ActivitySessionDurationRule.GentleRecoveryMaximumMinutes,
                SessionPurpose.Recovery, SessionTrainingStyle.Gentle,
                ActivitySessionDurationRule.GentleRecoveryDurationMinutesDefault),
            WorkoutActivityType.RestDay => new(ActivityPrescriptionStatus.Eligible, [], ["ExplicitRestDay"]),
            WorkoutActivityType.Other => OptionalDuration(context,
                ActivitySessionDurationRule.PositiveDurationMinimumMinutes,
                ActivitySessionDurationRule.MaximumSupportedDurationMinutes,
                SessionPurpose.UserActivity, SessionTrainingStyle.Unspecified),
            _ => Result(ActivityPrescriptionStatus.Unsupported, "UnsupportedActivity")
        };
    }

    private static ActivityPrescriptionResult GeneralStrength(ActivityPrescriptionContext context)
    {
        var isEstablishedExperience = context.Experience == ActivityExperience.Established;
        var isEstablishedResistance = isEstablishedExperience && context.IsEstablishedResistanceHistory;
        var frequency = isEstablishedResistance
            ? GeneralStrengthFrequencyResolver.EstablishedPolicySessionsPerWeek
            : GeneralStrengthFrequencyResolver.BaselinePolicySessionsPerWeek;
        var sets = isEstablishedExperience
            ? ActivityPrescriptionRule.EstablishedStrengthWorkingSetsInitially
            : ActivityPrescriptionRule.GeneralStrengthWorkingSets;
        var minReps = isEstablishedExperience
            ? ActivityPrescriptionRule.EstablishedMainMinimumRepetitions
            : ActivityPrescriptionRule.GeneralMainMinimumRepetitions;
        var maxReps = isEstablishedExperience
            ? ActivityPrescriptionRule.EstablishedMainMaximumRepetitions
            : ActivityPrescriptionRule.GeneralMainMaximumRepetitions;
        return Session(ActivityPrescriptionStatus.Eligible, context, SessionPurpose.Strength,
            ActivityModality.Strength, IntendedIntensity.Moderate, SessionTrainingStyle.General,
            frequency,
            sets: sets, minReps: minReps, maxReps: maxReps,
            repsInReserve: ActivityPrescriptionRule.InitialRepetitionsInReserve,
            restSeconds: isEstablishedExperience ? ActivityPrescriptionRule.EstablishedMainRestSeconds :
                ActivityPrescriptionRule.GeneralMainRestSeconds,
            reasons: ["UseEligibleCatalogMovementsAndRestrictionMappings"]);
    }

    private static ActivityPrescriptionResult HighVolumeStrength(ActivityPrescriptionContext context)
    {
        var useConservativeStart = context.Experience != ActivityExperience.Established ||
                                   !context.IsEstablishedResistanceHistory;
        return Session(ActivityPrescriptionStatus.Eligible, context, SessionPurpose.Strength,
            ActivityModality.Strength, IntendedIntensity.Moderate, SessionTrainingStyle.HighVolume,
            ActivityPrescriptionRule.HighVolumeStrengthSessionsPerWeek,
            sets: useConservativeStart
                ? ActivityPrescriptionRule.HighVolumeStartingMainSets
                : ActivityPrescriptionRule.HighVolumeMainSets,
            minReps: ActivityPrescriptionRule.HighVolumeMainMinimumRepetitions,
            maxReps: ActivityPrescriptionRule.HighVolumeMainMaximumRepetitions,
            repsInReserve: useConservativeStart
                ? ActivityPrescriptionRule.HighVolumeStartingRepetitionsInReserve
                : ActivityPrescriptionRule.InitialRepetitionsInReserve,
            restSeconds: HighVolumeMainRestSeconds,
            reasons: useConservativeStart
                ? [ActivityPrescriptionRule.HighVolumeConservativeStartCode,
                    "MaximumFiveExercisesPerSession", "InitialDirectSetLimitTenPerPrimaryMusclePerWeek"]
                : ["AccessoriesUseTwoSetsOfTenToFifteen", "MaximumFiveExercisesPerSession",
                    "InitialDirectSetLimitTenPerPrimaryMusclePerWeek"]);
    }

    private static ActivityPrescriptionResult HighIntensityStrength(ActivityPrescriptionContext context)
    {
        if (context.Experience != ActivityExperience.Established || !context.IsEstablishedResistanceHistory ||
            context.CompletedMainExerciseExposures < ActivityPrescriptionRule.StrengthHighIntensityRequiredExerciseExposures ||
            !context.HasKnownManageableLoad || !context.UserConfirmedCurrentFamiliarity)
            return Result(ActivityPrescriptionStatus.OfferGeneralStrength,
                "HeavyStrengthRequiresEstablishedHistoryFourExposuresKnownLoadAndCurrentFamiliarity");
        return Session(ActivityPrescriptionStatus.Eligible, context, SessionPurpose.Strength,
            ActivityModality.Strength, IntendedIntensity.Vigorous, SessionTrainingStyle.HighIntensity,
            ActivityPrescriptionRule.StrengthHighIntensityMaximumSessionsPerWeekInitially,
            sets: ActivityPrescriptionRule.StrengthHighIntensityMainSets,
            minReps: ActivityPrescriptionRule.StrengthHighIntensityMainMinimumRepetitions,
            maxReps: ActivityPrescriptionRule.StrengthHighIntensityMainMaximumRepetitions,
            repsInReserve: ActivityPrescriptionRule.InitialRepetitionsInReserve,
            restSeconds: ActivityPrescriptionRule.StrengthHighIntensityMainRestSeconds,
            reasons: ["MaximumTwoHeavyMainMovementsPerSession", "MaximumFourExercisesPerSession",
                "NoMaximalTestingOrFailure"]);
    }

    private static ActivityPrescriptionResult ContinuousCardio(ActivityPrescriptionContext context)
    {
        var main = context.Experience == ActivityExperience.Established
            ? context.ExistingBaselineMinutes ?? ActivityPrescriptionRule.EstablishedCardioMainMinutesWithoutBaseline
            : ActivityPrescriptionRule.BeginnerCardioMainMinutes;
        var frequency = context.Experience == ActivityExperience.Established
            ? EstablishedCardioFrequency : ContinuousCardioFrequency;
        return Session(ActivityPrescriptionStatus.Eligible, context, SessionPurpose.Conditioning,
            ToModality(context.Activity), IntendedIntensity.Moderate, SessionTrainingStyle.Continuous,
            frequency, warmup: ActivityPrescriptionRule.ContinuousCardioWarmupMinutes,
            main: main, cooldown: ActivityPrescriptionRule.ContinuousCardioCooldownMinutes,
            reasons: [context.ExistingBaselineMinutes is > 0
                ? "RetainedManageableBaseline"
                : "ComfortableToModerateWithoutInventedPaceOrDistance"]);
    }

    private static ActivityPrescriptionResult Swimming(ActivityPrescriptionContext context)
    {
        if (context.HasSwimmingFacilityAccess is not true || context.CanSwimIndependently is null)
            return Result(ActivityPrescriptionStatus.NeedsFamiliarization,
                "ConfirmFacilityAccessAndIndependentAbilityOrChooseInstructorLedActivity");
        if (context.HasSwimmingFacilityAccess is false || context.CanSwimIndependently is false)
            return Result(ActivityPrescriptionStatus.NeedsFamiliarization,
                "IndependentSwimmingNotEligibleWithoutAccessAndAbility");
        if (context.WantsContinuousSwimming is null)
            return Result(ActivityPrescriptionStatus.NeedsFamiliarization,
                "AskContinuousVersusSwimRestPreference");

        var main = context.Experience == ActivityExperience.Established
            ? context.ExistingBaselineMinutes ?? ActivityPrescriptionRule.EstablishedCardioMainMinutesWithoutBaseline
            : ActivityPrescriptionRule.BeginnerCardioMainMinutes;
        return Session(ActivityPrescriptionStatus.Eligible, context, SessionPurpose.Conditioning,
            ActivityModality.Swimming, IntendedIntensity.Moderate,
            context.WantsContinuousSwimming.Value ? SessionTrainingStyle.Continuous : SessionTrainingStyle.Intervals,
            context.Experience == ActivityExperience.Established
                ? EstablishedCardioFrequency : ContinuousCardioFrequency,
            warmup: ActivityPrescriptionRule.SwimmingWarmupMinutes, main: main,
            cooldown: ActivityPrescriptionRule.ContinuousCardioCooldownMinutes,
            intervalWorkCount: context.WantsContinuousSwimming.Value ? null : checked((int)main),
            intervalWorkSeconds: context.WantsContinuousSwimming.Value ? null : ActivityPrescriptionRule.SwimmingIntervalSeconds,
            intervalRecoveryCount: context.WantsContinuousSwimming.Value ? null : checked((int)main),
            intervalRecoverySeconds: context.WantsContinuousSwimming.Value ? null : ActivityPrescriptionRule.SwimmingIntervalSeconds,
            reasons: [context.WantsContinuousSwimming.Value
                ? "TimeBasedWithoutInventedDistanceOrPace"
                : "OptionalThirtySecondsComfortableSwimmingThirtySecondsRest"]);
    }

    private static ActivityPrescriptionResult Hiit(ActivityPrescriptionContext context)
    {
        var evidence = context.HiitEvidence;
        if (!HiitEligibilityEvidenceRule.IsEligible(evidence) ||
            evidence?.UnderlyingCardioActivity is not { } underlyingActivity)
            return Result(ActivityPrescriptionStatus.BlockedByRestriction,
                HiitEligibilityEvidenceRule.EvidenceNotEstablishedByRequiredEvidence);

        return new ActivityPrescriptionResult(ActivityPrescriptionStatus.Eligible,
            [new ActivitySessionPrescription(context.Activity, SessionPurpose.Conditioning,
                ToModality(underlyingActivity), IntendedIntensity.Vigorous, SessionTrainingStyle.Hiit,
                ActivityPrescriptionRule.HiitMaximumSessionsPerWeek, ActivityPrescriptionRule.HiitWarmupMinutes,
                null, ActivityPrescriptionRule.HiitCooldownMinutes, null, null, null, null, null,
                ActivityPrescriptionRule.HiitWorkIntervals, ActivityPrescriptionRule.HiitWorkIntervalSeconds,
                ActivityPrescriptionRule.HiitRecoveryIntervals, ActivityPrescriptionRule.HiitRecoveryIntervalSeconds,
                false, ["SelectedUnderlyingCardioActivity", "ReplacesOneCardioSession", "NotOnStrengthOrClimbingDay",
                    "NoProgression", "HardButNotAllOut"])], []);
    }

    private static ActivityPrescriptionResult Climbing(ActivityPrescriptionContext context)
    {
        if (context.HasRopeClimbingCompetence is false)
            return Result(ActivityPrescriptionStatus.NeedsFamiliarization,
                "RopeActivityRequiresCompetenceConfirmation");
        if (context.Experience is ActivityExperience.Beginner or ActivityExperience.Returning or
            ActivityExperience.Unknown)
        {
            if (context.AppropriateClimbingInstructionAvailable is not true)
                return Result(ActivityPrescriptionStatus.NeedsFamiliarization,
                    "BeginnerClimbingRequiresAppropriateInstructionOrSupervision");
            return Session(ActivityPrescriptionStatus.Eligible, context, SessionPurpose.Climbing,
                ActivityModality.Climbing, IntendedIntensity.Moderate, SessionTrainingStyle.Unspecified,
                ClimbingMaximumSessionsPerWeek, main: ActivityPrescriptionRule.BeginnerClimbingVisitMinutes,
                reasons: ["FacilityVisitIncludesWarmupAttemptsRestAndTransitions", "NoGradesOrFingerLoading"]);
        }
        var minutes = context.ExistingBaselineMinutes ??
                      ActivityPrescriptionRule.EstablishedClimbingVisitMinutesWithoutBaseline;
        return Session(ActivityPrescriptionStatus.Eligible, context, SessionPurpose.Climbing,
            ActivityModality.Climbing, IntendedIntensity.Moderate, SessionTrainingStyle.Unspecified,
            ClimbingMaximumSessionsPerWeek, main: minutes,
            reasons: [context.ExistingBaselineMinutes is > 0 ? "RetainedExistingSchedule" : "FacilityVisitDuration"]);
    }

    private static ActivityPrescriptionResult Yoga(ActivityPrescriptionContext context)
    {
        var classification = YogaClassificationRule.Classify(
            context.IsDemandingYogaContent,
            context.ActivityName,
            context.ContentTags);

        if (classification.IsInconsistent)
            return Result(ActivityPrescriptionStatus.NeedsFamiliarization,
                classification.ReasonCode ?? YogaClassificationRule.YogaClassificationInconsistentCode);

        var isDemanding = classification.IsDemanding;
        var minimum = isDemanding
            ? ActivitySessionDurationRule.DemandingYogaMinimumMinutes
            : ActivitySessionDurationRule.GentleYogaMinimumMinutes;
        var maximum = isDemanding
            ? ActivitySessionDurationRule.DemandingYogaMaximumMinutes
            : ActivitySessionDurationRule.GentleYogaMaximumMinutes;

        int duration;
        if (context.UserSelectedDurationMinutes is not null)
        {
            var userMin = context.UserSelectedDurationMinutes.Value;
            if (userMin < minimum || userMin > maximum ||
                (userMin - minimum) % ActivitySessionDurationRule.GentleYogaIncrementMinutes != 0)
            {
                return Result(ActivityPrescriptionStatus.NeedsUserDuration, "SelectedDurationOutsideAllowedRange");
            }
            duration = userMin;
        }
        else if (context.CatalogDurationMinutes is not null)
        {
            var catMin = context.CatalogDurationMinutes.Value;
            if (catMin < minimum || catMin > maximum)
            {
                return Result(ActivityPrescriptionStatus.NeedsUserDuration, "SelectedDurationOutsideAllowedRange");
            }
            duration = catMin;
        }
        else
        {
            duration = isDemanding
                ? ActivitySessionDurationRule.DemandingYogaDurationMinutesDefault
                : ActivitySessionDurationRule.YogaDurationMinutesDefault;
        }

        var style = isDemanding ? SessionTrainingStyle.Demanding : SessionTrainingStyle.Gentle;
        var intensity = isDemanding ? IntendedIntensity.Vigorous : IntendedIntensity.Easy;

        return Session(ActivityPrescriptionStatus.Eligible, context with { UserSelectedDurationMinutes = duration },
            SessionPurpose.Yoga, ActivityModality.Yoga, intensity, style, 1, main: duration, requiresConfirmation: true,
            reasons: ["UserOrCatalogSelectedDuration", "ClassifiedByActualContent"]);
    }

    private static ActivityPrescriptionResult OptionalDuration(ActivityPrescriptionContext context,
        int minimum, int maximum, SessionPurpose purpose, SessionTrainingStyle style, int? defaultMinutes = null)
    {
        int minutes;
        if (context.UserSelectedDurationMinutes is not null)
        {
            minutes = context.UserSelectedDurationMinutes.Value;
            if (!WorkoutPreferencesRule.IsValidSessionDuration(context.Activity, minutes) ||
                minutes < minimum || minutes > maximum)
            {
                return Result(ActivityPrescriptionStatus.NeedsUserDuration, "SelectedDurationOutsideAllowedRange");
            }
        }
        else if (defaultMinutes.HasValue)
        {
            minutes = defaultMinutes.Value;
        }
        else
        {
            return Result(ActivityPrescriptionStatus.NeedsUserDuration, "ChooseAnEligibleCatalogOrUserDuration");
        }

        var modality = ToModality(context.Activity);
        return Session(ActivityPrescriptionStatus.Eligible, context with { UserSelectedDurationMinutes = minutes },
            purpose, modality,
            style == SessionTrainingStyle.Demanding ? IntendedIntensity.Vigorous : IntendedIntensity.Easy,
            style, 1, main: minutes, requiresConfirmation: true,
            reasons: ["UserOrCatalogSelectedDuration", "ClassifiedByActualContent"]);
    }

    private static ActivityModality ToModality(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.StrengthHighIntensity => ActivityModality.Strength,
        WorkoutActivityType.Hiit or WorkoutActivityType.Cycling => ActivityModality.Cycling,
        WorkoutActivityType.Running => ActivityModality.Running,
        WorkoutActivityType.Walking => ActivityModality.Walking,
        WorkoutActivityType.Rowing => ActivityModality.Rowing,
        WorkoutActivityType.Elliptical => ActivityModality.Elliptical,
        WorkoutActivityType.Swimming => ActivityModality.Swimming,
        WorkoutActivityType.Yoga => ActivityModality.Yoga,
        WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery => ActivityModality.Mobility,
        WorkoutActivityType.RockClimbing => ActivityModality.Climbing,
        _ => ActivityModality.Other
    };

    private static ActivityPrescriptionResult Session(ActivityPrescriptionStatus status,
        ActivityPrescriptionContext context, SessionPurpose purpose, ActivityModality modality,
        IntendedIntensity intensity, SessionTrainingStyle style, int frequency,
        decimal? warmup = null, decimal? main = null, decimal? cooldown = null,
        int? sets = null, int? minReps = null, int? maxReps = null,
        int? repsInReserve = null, int? restSeconds = null,
        int? intervalWorkCount = null, decimal? intervalWorkSeconds = null,
        int? intervalRecoveryCount = null, decimal? intervalRecoverySeconds = null,
        bool requiresConfirmation = false, IReadOnlyList<string>? reasons = null) =>
        new(status, [new ActivitySessionPrescription(context.Activity, purpose, modality, intensity, style,
            frequency, warmup, main, cooldown, sets, minReps, maxReps, repsInReserve, restSeconds,
            intervalWorkCount, intervalWorkSeconds, intervalRecoveryCount, intervalRecoverySeconds,
            requiresConfirmation, reasons ?? [])], reasons ?? []);

    private static ActivityPrescriptionResult Result(ActivityPrescriptionStatus status, string reason) =>
        new(status, [], [reason]);
}

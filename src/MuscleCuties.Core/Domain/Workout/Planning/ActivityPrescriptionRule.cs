using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Whether an activity can be prescribed now, and the review reason when it cannot.</summary>
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

/// <summary>Everything the prescription depends on: her answers, the week's budget and the cycle phase.</summary>
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

/// <summary>What a prescribed session asks for: its frequency, minutes and any reduction.</summary>
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

/// <summary>The prescription with the notes that explain how it was reached.</summary>
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
    public const string HighIntensitySaferAlternativeReviewMissing = "HighIntensitySaferAlternativePrescriptionReviewMissing";

    public const int ContinuousCardioWarmupMinutes = 5;
    public const int ContinuousCardioCooldownMinutes = 5;
    public const int BeginnerCardioMainMinutes = 20;
    public const int EstablishedCardioMainMinutesWithoutBaseline = 30;
    public const int BeginnerCardioSessionsPerWeek = 3;
    public const int EstablishedCardioSessionsPerWeekWithoutBaseline = 4;
    public const int SwimmingWarmupMinutes = 5;
    public const int SwimmingIntervalSeconds = 30;

    /// <summary>
    /// Longest main phase a swimming session may carry. Swimming has its own user-facing duration
    /// policy (25–30 minutes, [ActivitySessionDurationRule]), so the main phase is capped there rather
    /// than inheriting the longer continuous-cardio default.
    /// </summary>
    public const int SwimmingMainMinutesCap =
        ActivitySessionDurationRule.SwimmingMaximumMinutes - SwimmingWarmupMinutes - ContinuousCardioCooldownMinutes;
    public const int BeginnerClimbingVisitMinutes = 45;
    public const int EstablishedClimbingVisitMinutesWithoutBaseline = 60;
    public const int HiitMaximumSessionsPerWeek = 2;
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
    public const string HiitDeferredInWorkoutEngineV2 = "HiitDeferredInWorkoutEngineV2";
    private const int HighVolumeMainRestSeconds = EstablishedMainRestSeconds;
    private const int ClimbingMaximumSessionsPerWeek = 1;
    private const int ContinuousCardioFrequency = ActivityPrescriptionRule.BeginnerCardioSessionsPerWeek;
    private const int EstablishedCardioFrequency = ActivityPrescriptionRule.EstablishedCardioSessionsPerWeekWithoutBaseline;

    /// <summary>The fixed HIIT session length every prescription uses.</summary>
    public static TimeSpan HiitSessionDuration => TimeSpan.FromSeconds(HiitTotalSeconds);

    /// <summary>Dispatches to the activity's prescription rule, refusing an activity she has not cleared.</summary>
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
            WorkoutActivityType.StrengthHighIntensity => HighIntensityStrength(),
            WorkoutActivityType.Cycling or WorkoutActivityType.Running or WorkoutActivityType.Walking or
                WorkoutActivityType.Rowing or WorkoutActivityType.Elliptical => ContinuousCardio(context),
            WorkoutActivityType.Swimming => Swimming(context),
            WorkoutActivityType.Hiit => Hiit(),
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

    /// <summary>Prescribes general strength: the weekly frequency and minutes her tier allows.</summary>
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

    /// <summary>Prescribes high-volume strength with more sets per session, capped by her availability.</summary>
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

    /// <summary>Prescribes high-intensity strength with heavier effort and fewer reps.</summary>
    private static ActivityPrescriptionResult HighIntensityStrength()
    {
        return Result(ActivityPrescriptionStatus.Unsupported, HighIntensitySaferAlternativeReviewMissing);
    }

    /// <summary>Prescribes continuous cardio in minutes, without intervals.</summary>
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

    /// <summary>Prescribes swimming in minutes, treating it as continuous cardio.</summary>
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

        var main = Math.Min(context.Experience == ActivityExperience.Established
            ? context.ExistingBaselineMinutes ?? ActivityPrescriptionRule.EstablishedCardioMainMinutesWithoutBaseline
            : ActivityPrescriptionRule.BeginnerCardioMainMinutes, ActivityPrescriptionRule.SwimmingMainMinutesCap);
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

    /// <summary>Prescribes HIIT only when her evidence clears it, with its fixed session length.</summary>
    private static ActivityPrescriptionResult Hiit()
    {
        // Gate 80: HIIT is deferred for new active generation/selection in Workout Engine 2.0.
        // Historical HIIT constants and DTO enum values remain intact for persisted data compatibility.
        return Result(ActivityPrescriptionStatus.Unsupported, HiitDeferredInWorkoutEngineV2);
    }

    /// <summary>Prescribes climbing sessions in minutes, treating them as demanding.</summary>
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

    /// <summary>Prescribes yoga without reducing load, since it counts as restorative.</summary>
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

    /// <summary>Prescribes the optional activity only when the remaining time allows it.</summary>
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

    /// <summary>Maps an activity onto the modality its prescription uses.</summary>
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

    /// <summary>A prescription carrying a session and the reason for it.</summary>
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

    /// <summary>A prescription carrying only a status and reason, with no session.</summary>
    private static ActivityPrescriptionResult Result(ActivityPrescriptionStatus status, string reason) =>
        new(status, [], [reason]);
}

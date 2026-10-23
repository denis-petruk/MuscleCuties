using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ActivityPrescriptionStatusV020
{
    Eligible,
    OfferGeneralStrength,
    NeedsFamiliarization,
    NeedsUserDuration,
    BlockedByRestriction,
    NotSelected,
    Unsupported
}

public sealed record HiitEligibilityEvidenceV020(
    bool ExplicitOptIn,
    bool EstablishedModalityExperience,
    IReadOnlyList<HiitAerobicSessionEvidenceV020> PreviousFourWeeksAerobicSessions,
    bool HasApplicableRestrictionOrCurrentSymptom,
    bool ExistingClearanceScreeningSatisfied);

public sealed record HiitAerobicSessionEvidenceV020(
    int WeekOffsetFromCurrent,
    int DurationMinutes,
    bool ReportedManageable);

public sealed record ActivityPrescriptionContextV020(
    WorkoutActivityType Activity,
    ActivityExperienceV020 Experience,
    int? ExistingBaselineMinutes,
    bool HasCurrentRestrictionOrSymptom,
    bool IsExplicitlySelected,
    bool IsEstablishedResistanceHistory,
    int CompletedMainExerciseExposures,
    bool HasKnownManageableLoad,
    bool UserConfirmedCurrentFamiliarity,
    HiitEligibilityEvidenceV020? HiitEvidence = null,
    bool? HasSwimmingFacilityAccess = null,
    bool? CanSwimIndependently = null,
    bool? WantsContinuousSwimming = null,
    int? UserSelectedDurationMinutes = null,
    int? CatalogDurationMinutes = null,
    bool? HasRopeClimbingCompetence = null,
    bool? AppropriateClimbingInstructionAvailable = null,
    bool? IsDemandingYogaContent = null,
    bool? UserSelectedLighterWeek = null);

public sealed record ActivitySessionPrescriptionV020(
    WorkoutActivityType Activity,
    SessionPurposeV02 Purpose,
    ActivityModalityV02 Modality,
    IntendedIntensityV02 Intensity,
    SessionTrainingStyleV02 TrainingStyle,
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

public sealed record ActivityPrescriptionResultV020(
    ActivityPrescriptionStatusV020 Status,
    IReadOnlyList<ActivitySessionPrescriptionV020> Sessions,
    IReadOnlyList<string> Reasons);

/// <summary>Maps the 0.2.0 product policy into activity-specific session prescriptions.</summary>
public static class ActivityPrescriptionRuleV020
{
    private const int GeneralStrengthAccessoryRestSeconds = WorkoutTrainingPolicyV020.AccessoryRestSeconds;
    private const int HighVolumeMainRestSeconds = WorkoutTrainingPolicyV020.EstablishedMainRestSeconds;
    private const int HighVolumeAccessorySets = 2;
    private const int StrengthHighIntensityAccessorySets = 2;
    private const int ClimbingMaximumSessionsPerWeek = 1;
    private const int ContinuousCardioFrequency = WorkoutTrainingPolicyV020.BeginnerCardioSessionsPerWeek;
    private const int EstablishedCardioFrequency = WorkoutTrainingPolicyV020.EstablishedCardioSessionsPerWeekWithoutBaseline;
    private const int IntervalSecondsPerMinute = 60;

    public static ActivityPrescriptionResultV020 Evaluate(ActivityPrescriptionContextV020? context)
    {
        if (context is null || !Enum.IsDefined(context.Activity) || !Enum.IsDefined(context.Experience) ||
            context.ExistingBaselineMinutes is < 0 || context.UserSelectedDurationMinutes is < 0 ||
            context.CatalogDurationMinutes is < 0 || context.CompletedMainExerciseExposures < 0)
            return Result(ActivityPrescriptionStatusV020.Unsupported, "InvalidActivityInput");
        if (!context.IsExplicitlySelected)
            return Result(ActivityPrescriptionStatusV020.NotSelected, "ActivityNotSelected");
        if (context.HasCurrentRestrictionOrSymptom)
            return Result(ActivityPrescriptionStatusV020.BlockedByRestriction, "RestrictionOrSymptomRequiresReview");

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
                WorkoutTrainingPolicyV020.GentleMobilityMinimumMinutes,
                WorkoutTrainingPolicyV020.GentleMobilityMaximumMinutes,
                SessionPurposeV02.Mobility, SessionTrainingStyleV02.Gentle),
            WorkoutActivityType.GentleRecovery => OptionalDuration(context,
                WorkoutTrainingPolicyV020.GentleRecoveryMinimumMinutes,
                WorkoutTrainingPolicyV020.GentleRecoveryMaximumMinutes,
                SessionPurposeV02.Recovery, SessionTrainingStyleV02.Gentle),
            WorkoutActivityType.RestDay => new(ActivityPrescriptionStatusV020.Eligible, [], ["ExplicitRestDay"]),
            WorkoutActivityType.Other => OptionalDuration(context, 1, int.MaxValue,
                SessionPurposeV02.UserActivity, SessionTrainingStyleV02.Unspecified),
            _ => Result(ActivityPrescriptionStatusV020.Unsupported, "UnsupportedActivity")
        };
    }

    private static ActivityPrescriptionResultV020 GeneralStrength(ActivityPrescriptionContextV020 context)
    {
        var established = context.Experience == ActivityExperienceV020.Established;
        var sets = established
            ? WorkoutTrainingPolicyV020.EstablishedStrengthWorkingSetsInitially
            : WorkoutTrainingPolicyV020.GeneralStrengthWorkingSets;
        var minReps = established
            ? WorkoutTrainingPolicyV020.EstablishedMainMinimumRepetitions
            : WorkoutTrainingPolicyV020.GeneralMainMinimumRepetitions;
        var maxReps = established
            ? WorkoutTrainingPolicyV020.EstablishedMainMaximumRepetitions
            : WorkoutTrainingPolicyV020.GeneralMainMaximumRepetitions;
        return Session(ActivityPrescriptionStatusV020.Eligible, context, SessionPurposeV02.Strength,
            ActivityModalityV02.Strength, IntendedIntensityV02.Moderate, SessionTrainingStyleV02.General,
            1, sets: sets, minReps: minReps, maxReps: maxReps,
            repsInReserve: WorkoutTrainingPolicyV020.InitialRepetitionsInReserve,
            restSeconds: established ? WorkoutTrainingPolicyV020.EstablishedMainRestSeconds :
                WorkoutTrainingPolicyV020.GeneralMainRestSeconds,
            reasons: ["UseEligibleCatalogMovementsAndRestrictionMappings"]);
    }

    private static ActivityPrescriptionResultV020 HighVolumeStrength(ActivityPrescriptionContextV020 context)
    {
        if (context.Experience != ActivityExperienceV020.Established || !context.IsEstablishedResistanceHistory)
            return Result(ActivityPrescriptionStatusV020.OfferGeneralStrength,
                "HighVolumeRequiresEstablishedResistanceHistory");
        return Session(ActivityPrescriptionStatusV020.Eligible, context, SessionPurposeV02.Strength,
            ActivityModalityV02.Strength, IntendedIntensityV02.Moderate, SessionTrainingStyleV02.HighVolume,
            1, sets: WorkoutTrainingPolicyV020.HighVolumeMainSets,
            minReps: WorkoutTrainingPolicyV020.HighVolumeMainMinimumRepetitions,
            maxReps: WorkoutTrainingPolicyV020.HighVolumeMainMaximumRepetitions,
            repsInReserve: WorkoutTrainingPolicyV020.InitialRepetitionsInReserve,
            restSeconds: HighVolumeMainRestSeconds,
            reasons: ["AccessoriesUseTwoSetsOfTenToFifteen", "MaximumFiveExercisesPerSession",
                "InitialDirectSetLimitTenPerPrimaryMusclePerWeek"]);
    }

    private static ActivityPrescriptionResultV020 HighIntensityStrength(ActivityPrescriptionContextV020 context)
    {
        if (context.Experience != ActivityExperienceV020.Established || !context.IsEstablishedResistanceHistory ||
            context.CompletedMainExerciseExposures < WorkoutTrainingPolicyV020.StrengthHighIntensityRequiredExerciseExposures ||
            !context.HasKnownManageableLoad || !context.UserConfirmedCurrentFamiliarity)
            return Result(ActivityPrescriptionStatusV020.OfferGeneralStrength,
                "HeavyStrengthRequiresEstablishedHistoryFourExposuresKnownLoadAndCurrentFamiliarity");
        return Session(ActivityPrescriptionStatusV020.Eligible, context, SessionPurposeV02.Strength,
            ActivityModalityV02.Strength, IntendedIntensityV02.Vigorous, SessionTrainingStyleV02.HighIntensity,
            WorkoutTrainingPolicyV020.StrengthHighIntensityMaximumSessionsPerWeekInitially,
            sets: WorkoutTrainingPolicyV020.StrengthHighIntensityMainSets,
            minReps: WorkoutTrainingPolicyV020.StrengthHighIntensityMainMinimumRepetitions,
            maxReps: WorkoutTrainingPolicyV020.StrengthHighIntensityMainMaximumRepetitions,
            repsInReserve: WorkoutTrainingPolicyV020.InitialRepetitionsInReserve,
            restSeconds: WorkoutTrainingPolicyV020.StrengthHighIntensityMainRestSeconds,
            reasons: ["MaximumTwoHeavyMainMovementsPerSession", "MaximumFourExercisesPerSession",
                "NoMaximalTestingOrFailure"]);
    }

    private static ActivityPrescriptionResultV020 ContinuousCardio(ActivityPrescriptionContextV020 context)
    {
        var main = context.Experience == ActivityExperienceV020.Established
            ? context.ExistingBaselineMinutes ?? WorkoutTrainingPolicyV020.EstablishedCardioMainMinutesWithoutBaseline
            : WorkoutTrainingPolicyV020.BeginnerCardioMainMinutes;
        var frequency = context.Experience == ActivityExperienceV020.Established
            ? EstablishedCardioFrequency : ContinuousCardioFrequency;
        return Session(ActivityPrescriptionStatusV020.Eligible, context, SessionPurposeV02.Conditioning,
            ToModality(context.Activity), IntendedIntensityV02.Moderate, SessionTrainingStyleV02.Continuous,
            frequency, warmup: WorkoutTrainingPolicyV020.ContinuousCardioWarmupMinutes,
            main: main, cooldown: WorkoutTrainingPolicyV020.ContinuousCardioCooldownMinutes,
            reasons: [context.ExistingBaselineMinutes is > 0
                ? "RetainedManageableBaseline"
                : "ComfortableToModerateWithoutInventedPaceOrDistance"]);
    }

    private static ActivityPrescriptionResultV020 Swimming(ActivityPrescriptionContextV020 context)
    {
        if (context.HasSwimmingFacilityAccess is not true || context.CanSwimIndependently is null)
            return Result(ActivityPrescriptionStatusV020.NeedsFamiliarization,
                "ConfirmFacilityAccessAndIndependentAbilityOrChooseInstructorLedActivity");
        if (context.HasSwimmingFacilityAccess is false || context.CanSwimIndependently is false)
            return Result(ActivityPrescriptionStatusV020.NeedsFamiliarization,
                "IndependentSwimmingNotEligibleWithoutAccessAndAbility");
        if (context.WantsContinuousSwimming is null)
            return Result(ActivityPrescriptionStatusV020.NeedsFamiliarization,
                "AskContinuousVersusSwimRestPreference");

        var main = context.Experience == ActivityExperienceV020.Established
            ? context.ExistingBaselineMinutes ?? WorkoutTrainingPolicyV020.EstablishedCardioMainMinutesWithoutBaseline
            : WorkoutTrainingPolicyV020.BeginnerCardioMainMinutes;
        return Session(ActivityPrescriptionStatusV020.Eligible, context, SessionPurposeV02.Conditioning,
            ActivityModalityV02.Swimming, IntendedIntensityV02.Moderate,
            context.WantsContinuousSwimming.Value ? SessionTrainingStyleV02.Continuous : SessionTrainingStyleV02.Intervals,
            context.Experience == ActivityExperienceV020.Established
                ? EstablishedCardioFrequency : ContinuousCardioFrequency,
            warmup: WorkoutTrainingPolicyV020.SwimmingWarmupMinutes, main: main,
            cooldown: WorkoutTrainingPolicyV020.ContinuousCardioCooldownMinutes,
            intervalWorkCount: context.WantsContinuousSwimming.Value ? null : checked((int)main),
            intervalWorkSeconds: context.WantsContinuousSwimming.Value ? null : 30,
            intervalRecoveryCount: context.WantsContinuousSwimming.Value ? null : checked((int)main),
            intervalRecoverySeconds: context.WantsContinuousSwimming.Value ? null : 30,
            reasons: [context.WantsContinuousSwimming.Value
                ? "TimeBasedWithoutInventedDistanceOrPace"
                : "OptionalThirtySecondsComfortableSwimmingThirtySecondsRest"]);
    }

    private static ActivityPrescriptionResultV020 Hiit(ActivityPrescriptionContextV020 context)
    {
        var evidence = context.HiitEvidence;
        if (evidence is null || !evidence.ExplicitOptIn || !evidence.EstablishedModalityExperience ||
            evidence.HasApplicableRestrictionOrCurrentSymptom || !evidence.ExistingClearanceScreeningSatisfied ||
            evidence.PreviousFourWeeksAerobicSessions is null ||
            !HasRequiredManageableAerobicHistory(evidence.PreviousFourWeeksAerobicSessions))
            return Result(ActivityPrescriptionStatusV020.BlockedByRestriction,
                "HiitEligibilityNotEstablishedByRequiredEvidence");

        return new ActivityPrescriptionResultV020(ActivityPrescriptionStatusV020.Eligible,
            [new ActivitySessionPrescriptionV020(context.Activity, SessionPurposeV02.Conditioning,
                ActivityModalityV02.Cycling, IntendedIntensityV02.Vigorous, SessionTrainingStyleV02.Hiit,
                WorkoutTrainingPolicyV020.HiitMaximumSessionsPerWeek, WorkoutTrainingPolicyV020.HiitWarmupMinutes,
                null, WorkoutTrainingPolicyV020.HiitCooldownMinutes, null, null, null, null, null,
                WorkoutTrainingPolicyV020.HiitWorkIntervals, WorkoutTrainingPolicyV020.HiitWorkIntervalSeconds,
                WorkoutTrainingPolicyV020.HiitRecoveryIntervals, WorkoutTrainingPolicyV020.HiitRecoveryIntervalSeconds,
                false, ["StationaryCyclingOnly", "ReplacesOneCardioSession", "NotOnStrengthOrClimbingDay",
                    "NoProgression", "HardButNotAllOut"])], []);
    }

    private static ActivityPrescriptionResultV020 Climbing(ActivityPrescriptionContextV020 context)
    {
        if (context.HasRopeClimbingCompetence is false)
            return Result(ActivityPrescriptionStatusV020.NeedsFamiliarization,
                "RopeActivityRequiresCompetenceConfirmation");
        if (context.Experience is ActivityExperienceV020.Beginner or ActivityExperienceV020.Returning or
            ActivityExperienceV020.Unknown)
        {
            if (context.AppropriateClimbingInstructionAvailable is not true)
                return Result(ActivityPrescriptionStatusV020.NeedsFamiliarization,
                    "BeginnerClimbingRequiresAppropriateInstructionOrSupervision");
            return Session(ActivityPrescriptionStatusV020.Eligible, context, SessionPurposeV02.Climbing,
                ActivityModalityV02.Climbing, IntendedIntensityV02.Moderate, SessionTrainingStyleV02.Unspecified,
                ClimbingMaximumSessionsPerWeek, main: WorkoutTrainingPolicyV020.BeginnerClimbingVisitMinutes,
                reasons: ["FacilityVisitIncludesWarmupAttemptsRestAndTransitions", "NoGradesOrFingerLoading"]);
        }
        var minutes = context.ExistingBaselineMinutes ??
                      WorkoutTrainingPolicyV020.EstablishedClimbingVisitMinutesWithoutBaseline;
        return Session(ActivityPrescriptionStatusV020.Eligible, context, SessionPurposeV02.Climbing,
            ActivityModalityV02.Climbing, IntendedIntensityV02.Moderate, SessionTrainingStyleV02.Unspecified,
            ClimbingMaximumSessionsPerWeek, main: minutes,
            reasons: [context.ExistingBaselineMinutes is > 0 ? "RetainedExistingSchedule" : "FacilityVisitDuration"]);
    }

    private static ActivityPrescriptionResultV020 Yoga(ActivityPrescriptionContextV020 context)
    {
        if (context.IsDemandingYogaContent is null)
            return Result(ActivityPrescriptionStatusV020.NeedsFamiliarization,
                "ClassifyYogaByContentBeforeSelectingDuration");
        var duration = context.UserSelectedDurationMinutes ?? context.CatalogDurationMinutes;
        if (duration is null)
            return Result(ActivityPrescriptionStatusV020.NeedsUserDuration, "ChooseAnEligibleCatalogOrUserDuration");
        var minimum = context.IsDemandingYogaContent == true ? 1 :
            WorkoutTrainingPolicyV020.GentleYogaMinimumMinutes;
        var maximum = context.IsDemandingYogaContent == true ? int.MaxValue :
            WorkoutTrainingPolicyV020.GentleYogaMaximumMinutes;
        return OptionalDuration(context with { UserSelectedDurationMinutes = duration }, minimum, maximum,
            SessionPurposeV02.Yoga, context.IsDemandingYogaContent == true
                ? SessionTrainingStyleV02.Demanding : SessionTrainingStyleV02.Gentle);
    }

    private static ActivityPrescriptionResultV020 OptionalDuration(ActivityPrescriptionContextV020 context,
        int minimum, int maximum, SessionPurposeV02 purpose, SessionTrainingStyleV02 style)
    {
        var minutes = context.UserSelectedDurationMinutes;
        if (minutes is null)
            return Result(ActivityPrescriptionStatusV020.NeedsUserDuration, "ChooseAnEligibleCatalogOrUserDuration");
        if (minutes < minimum || minutes > maximum)
            return Result(ActivityPrescriptionStatusV020.NeedsUserDuration, "SelectedDurationOutsideAllowedRange");
        var modality = ToModality(context.Activity);
        return Session(ActivityPrescriptionStatusV020.Eligible, context, purpose, modality,
            style == SessionTrainingStyleV02.Demanding ? IntendedIntensityV02.Vigorous : IntendedIntensityV02.Easy,
            style, 1, main: minutes, requiresConfirmation: true,
            reasons: ["UserOrCatalogSelectedDuration", "ClassifiedByActualContent"]);
    }

    private static bool HasRequiredManageableAerobicHistory(
        IReadOnlyList<HiitAerobicSessionEvidenceV020> sessions)
    {
        if (sessions.Any(session => session is null || session.WeekOffsetFromCurrent is < 1 or >
                WorkoutTrainingPolicyV020.HiitRequiredPriorWeeks || session.DurationMinutes < 0))
            return false;

        return Enumerable.Range(1, WorkoutTrainingPolicyV020.HiitRequiredPriorWeeks).All(weekOffset =>
        {
            var weekSessions = sessions.Where(session => session.WeekOffsetFromCurrent == weekOffset).ToArray();
            return weekSessions.Length >= WorkoutTrainingPolicyV020.HiitRequiredAerobicSessionsPerWeek &&
                   weekSessions.All(session =>
                       session.DurationMinutes >= WorkoutTrainingPolicyV020.HiitMinimumManageableSessionMinutes &&
                       session.ReportedManageable);
        });
    }

    private static ActivityModalityV02 ToModality(WorkoutActivityType activity) => activity switch
    {
        WorkoutActivityType.GeneralStrength or WorkoutActivityType.HighVolumeStrength or
            WorkoutActivityType.StrengthHighIntensity => ActivityModalityV02.Strength,
        WorkoutActivityType.Hiit or WorkoutActivityType.Cycling => ActivityModalityV02.Cycling,
        WorkoutActivityType.Running => ActivityModalityV02.Running,
        WorkoutActivityType.Walking => ActivityModalityV02.Walking,
        WorkoutActivityType.Rowing => ActivityModalityV02.Rowing,
        WorkoutActivityType.Elliptical => ActivityModalityV02.Elliptical,
        WorkoutActivityType.Swimming => ActivityModalityV02.Swimming,
        WorkoutActivityType.Yoga => ActivityModalityV02.Yoga,
        WorkoutActivityType.Mobility or WorkoutActivityType.GentleRecovery => ActivityModalityV02.Mobility,
        WorkoutActivityType.RockClimbing => ActivityModalityV02.Climbing,
        _ => ActivityModalityV02.Other
    };

    private static ActivityPrescriptionResultV020 Session(ActivityPrescriptionStatusV020 status,
        ActivityPrescriptionContextV020 context, SessionPurposeV02 purpose, ActivityModalityV02 modality,
        IntendedIntensityV02 intensity, SessionTrainingStyleV02 style, int frequency,
        decimal? warmup = null, decimal? main = null, decimal? cooldown = null,
        int? sets = null, int? minReps = null, int? maxReps = null,
        int? repsInReserve = null, int? restSeconds = null,
        int? intervalWorkCount = null, decimal? intervalWorkSeconds = null,
        int? intervalRecoveryCount = null, decimal? intervalRecoverySeconds = null,
        bool requiresConfirmation = false, IReadOnlyList<string>? reasons = null) =>
        new(status, [new ActivitySessionPrescriptionV020(context.Activity, purpose, modality, intensity, style,
            frequency, warmup, main, cooldown, sets, minReps, maxReps, repsInReserve, restSeconds,
            intervalWorkCount, intervalWorkSeconds, intervalRecoveryCount, intervalRecoverySeconds,
            requiresConfirmation, reasons ?? [])], reasons ?? []);

    private static ActivityPrescriptionResultV020 Result(ActivityPrescriptionStatusV020 status, string reason) =>
        new(status, [], [reason]);
}

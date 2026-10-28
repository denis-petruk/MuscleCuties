using System.Collections.ObjectModel;
using System.Globalization;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

internal static class StrengthPlanGenerationDisplayMapper
{
    private const int NoPolicyReasons = 0;
    private const decimal NoDurationMinutes = 0m;
    private const int OneSessionPerWeek = 1;
    private const string NoStatusMessage = "";
    private const string MixedActivityScheduleIncompleteCode = "MixedActivityScheduleIncomplete";
    private const string NoSafeStrengthContentRemainingCode = "NoSafeStrengthContentRemaining";

    public static MixedActivitySessionDisplay ToMixedActivitySessionDisplay(
        MixedActivityScheduleSessionV020 session)
    {
        var title = FormatLocalized("Workout_Generation_MixedSessionTitleFormat",
            FullWeekdayLabel(session.Weekday), ActivityLabel(session.ActivityTypeId));
        var effortKey = session.IsHardSession
            ? "Workout_Generation_HigherEffortSession"
            : "Workout_Generation_RegularEffortSession";
        var duration = session.DurationMinutes > NoDurationMinutes
            ? FormatLocalized("Workout_Generation_MixedSessionDurationFormat",
                session.DurationMinutes.ToString("0.#", CultureInfo.CurrentCulture))
            : UiTextCatalog.Get("Workout_Generation_EstimateUnavailable");
        var details = FormatLocalized("Workout_Generation_MixedSessionDetailsFormat", duration,
            UiTextCatalog.Get(effortKey));
        return new MixedActivitySessionDisplay(title, details)
        {
            Day = FullWeekdayLabel(session.Weekday),
            Activity = session.DayOrder is { } order
                ? FormatLocalized("Workout_Generation_OrderedActivityFormat", order.ToString(CultureInfo.CurrentCulture), ActivityLabel(session.ActivityTypeId))
                : ActivityLabel(session.ActivityTypeId),
            Duration = duration
        };
    }

    public static MixedActivityDecisionDisplay ToMixedActivityDecisionDisplay(
        MixedActivityScheduleDecisionV020 decision)
    {
        var disposition = DispositionLabel(decision.Disposition);
        var prescriptionStatus = decision.PrescriptionStatus;
        var status = string.IsNullOrWhiteSpace(prescriptionStatus) ||
                     string.Equals(prescriptionStatus, "Eligible", StringComparison.Ordinal)
            ? disposition
            : FormatLocalized("Workout_Generation_ActivityDecisionStatusFormat", disposition,
                PrescriptionStatusLabel(prescriptionStatus));

        if (decision.ScheduledSessionsPerWeek > 0 &&
            decision.EffectiveActivityTypeId.HasValue && decision.EffectiveActivityTypeId.Value != decision.ActivityTypeId)
        {
            status = UiTextCatalog.Get("Workout_Generation_Activity_ReplacedWithGeneralStrength");
        }

        var frequency = ActivityFrequencySummary(decision);
        var shouldExplain = !string.Equals(decision.Disposition, "Scheduled", StringComparison.Ordinal) ||
                            !string.IsNullOrWhiteSpace(decision.PrescriptionStatus) &&
                            !string.Equals(decision.PrescriptionStatus, "Eligible", StringComparison.Ordinal) ||
                            decision.EffectiveActivityTypeId.HasValue && decision.EffectiveActivityTypeId.Value != decision.ActivityTypeId;
        string[] reasons = shouldExplain
            ? decision.ReasonCodes
                .Where(code => !IsInformationalDiagnosticCode(code))
                .Select(code => decision.ScheduledSessionsPerWeek == 0 &&
                    code is "BeginnerProfileRequiresGeneralStrength" or
                        "HighVolumeRequiresEstablishedResistanceHistory"
                    ? UiTextCatalog.Get("Workout_Generation_Reason_HistoryInsufficient")
                    : ScheduleReasonMessage(code))
                .Distinct()
                .ToArray()
            : Array.Empty<string>();
        return new MixedActivityDecisionDisplay(
            ActivityLabel(decision.ActivityTypeId), status, frequency,
            new ObservableCollection<string>(reasons));
    }

    public static string ActivityLabel(int activityTypeId)
    {
        if (!Enum.IsDefined(typeof(WorkoutActivityDto), activityTypeId))
            return UiTextCatalog.Get("Workout_Generation_UnknownActivity");

        return ActivityLabel((WorkoutActivityDto)activityTypeId);
    }

    public static string ActivityLabel(WorkoutActivityDto activity) => activity switch
    {
        WorkoutActivityDto.GeneralStrength => UiTextCatalog.Get("Workout_Generation_GeneralStrength"),
        WorkoutActivityDto.HighVolumeStrength => UiTextCatalog.Get("Workout_Generation_HighVolumeStrength"),
        WorkoutActivityDto.StrengthHighIntensity => UiTextCatalog.Get("Workout_Generation_HighIntensityStrength"),
        WorkoutActivityDto.RockClimbing => UiTextCatalog.Get("Workout_Generation_Activity_RockClimbing"),
        WorkoutActivityDto.Hiit => UiTextCatalog.Get("Workout_Generation_Activity_Hiit"),
        WorkoutActivityDto.Cycling => UiTextCatalog.Get("Workout_Generation_Activity_Cycling"),
        WorkoutActivityDto.Running => UiTextCatalog.Get("Workout_Generation_Activity_Running"),
        WorkoutActivityDto.Swimming => UiTextCatalog.Get("Workout_Generation_Activity_Swimming"),
        WorkoutActivityDto.Yoga => UiTextCatalog.Get("Workout_Generation_Activity_Yoga"),
        WorkoutActivityDto.Walking => UiTextCatalog.Get("Workout_Generation_Activity_Walking"),
        WorkoutActivityDto.Rowing => UiTextCatalog.Get("Workout_Generation_Activity_Rowing"),
        WorkoutActivityDto.Elliptical => UiTextCatalog.Get("Workout_Generation_Activity_Elliptical"),
        WorkoutActivityDto.Mobility => UiTextCatalog.Get("Workout_Generation_Activity_Mobility"),
        WorkoutActivityDto.GentleRecovery => UiTextCatalog.Get("Workout_Generation_Activity_GentleRecovery"),
        WorkoutActivityDto.RestDay => UiTextCatalog.Get("Workout_Generation_Activity_RestDay"),
        WorkoutActivityDto.Other => UiTextCatalog.Get("Workout_Generation_Activity_Other"),
        _ => UiTextCatalog.Get("Workout_Generation_UnknownActivity")
    };

    public static string StrengthPlanDescription(WorkoutActivityDto activity) => activity switch
    {
        WorkoutActivityDto.GeneralStrength => UiTextCatalog.Get("Workout_Generation_GeneralStrengthDescription"),
        WorkoutActivityDto.HighVolumeStrength => UiTextCatalog.Get("Workout_Generation_HighVolumeStrengthDescription"),
        WorkoutActivityDto.StrengthHighIntensity => UiTextCatalog.Get("Workout_Generation_HighIntensityStrengthDescription"),
        _ => string.Empty
    };

    public static string FullWeekdayLabel(DayOfWeek weekday) => UiTextCatalog.Get(weekday switch
    {
        DayOfWeek.Monday => "Workout_Generation_FullDayMonday",
        DayOfWeek.Tuesday => "Workout_Generation_FullDayTuesday",
        DayOfWeek.Wednesday => "Workout_Generation_FullDayWednesday",
        DayOfWeek.Thursday => "Workout_Generation_FullDayThursday",
        DayOfWeek.Friday => "Workout_Generation_FullDayFriday",
        DayOfWeek.Saturday => "Workout_Generation_FullDaySaturday",
        DayOfWeek.Sunday => "Workout_Generation_FullDaySunday",
        _ => "Workout_Generation_UnknownDay"
    });

    public static string ScheduleReasonMessage(string reasonCode) => UiTextCatalog.Get(reasonCode switch
    {
        "ActivitySelectionMissing" or "NoActivitiesSelected" => "Workout_Generation_Reason_NoActivitiesSelected",
        "AvailableWeekdaysMustContainOneToSevenDistinctDays" or "AvailableWeekdayValueInvalid" =>
            "Workout_Generation_Reason_AvailableWeekdays",
        "ActivityEligibilityEvidenceMissing" or "ActivityEligibilityEvidenceDuplicate" =>
            "Workout_Generation_Reason_ActivityEvidence",
        "ActivityNotExplicitlySelected" or "ActivityNotSelected" or "ChosenStrengthActivityNotSelected" or
        "StrengthAlternativeNotChosen" =>
            "Workout_Generation_Reason_ActivityNotSelected",
        "ActivityPrescriptionNotEligible" or "UnsupportedActivity" or "InvalidActivityInput" or
        "InvalidActivityValue" or "ChosenStrengthActivityMustBeStrengthActivity" =>
            "Workout_Generation_Reason_ActivityNotEligible",
        "ActivityNotSupportedByPersistedPlan" => "Workout_Generation_Reason_ActivityNotSupportedBySavedPlan",
        "ClimbingCompetenceNotConfirmed" or "RopeActivityRequiresCompetenceConfirmation" =>
            "Workout_Generation_Reason_ClimbingCompetence",
        "YogaContentClassificationMissing" => "Workout_Generation_Reason_YogaContent",
        "YogaClassificationInconsistent" => "Workout_Generation_Reason_YogaInconsistent",
        "RestrictionOrSymptomRequiresReview" => "Workout_Generation_Reason_RestrictionReview",
        "SessionDurationMissing" or "SessionDurationMustBePositive" or "SelectedDurationOutsideAllowedRange" =>
            "Workout_Generation_Reason_SessionDurationMissing",
        "SessionDurationExceedsSupportedRange" or "SessionDurationOutsideSavedPreferenceRange" or
        "SessionDurationMustUseSavedPreferenceIncrement" or "SessionDurationDoesNotMatchPrescription" =>
            "Workout_Generation_Reason_SessionDurationInvalid",
        "SelectedDurationAppliedToPrescription" or "UserOrCatalogSelectedDuration" =>
            "Workout_Generation_Reason_SavedDurationApplied",
        "ActivityFrequencyPreferenceMissing" or "ActivityFrequencyPreferenceInvalid" =>
            "Workout_Generation_Reason_FrequencyPreference",
        "ActivityFrequencyLimitedByAvailableWeekdays" => "Workout_Generation_Reason_FrequencyLimited",
        "HardDayLimitReached" => "Workout_Generation_Reason_HardDayLimit",
        "StrengthSessionsReducedForMuscleRecovery" => "Workout_Generation_Reason_StrengthRecoverySpacing",
        "NoSafeDayAvailable" or "NotOnStrengthOrClimbingDay" => "Workout_Generation_Reason_NoSafeDay",
        "HiitUnderlyingCardioActivityUnsupported" => "Workout_Generation_Reason_HiitUnderlyingCardioUnsupported",
        "HiitUnderlyingCardioActivityMissing" or "HiitRequiresSelectedEstablishedCardio" or
        "HiitEligibilityNotEstablishedByRequiredEvidence" =>
            "Workout_Generation_Reason_HiitEligibility",
        "OneContinuousSessionReplacedByHiit" or "ReplacesOneCardioSession" =>
            "Workout_Generation_Reason_HiitReplacedCardio",
        "RestDayIsNotAWorkoutSession" or "ExplicitRestDay" => "Workout_Generation_Reason_RestDay",
        "ActivityHistoryInsufficientOrInvalid" or "ActivityHistoryInsufficient" =>
            "Workout_Generation_Reason_HistoryInsufficient",
        "EstablishedActivityAfterExtendedBreak" => "Workout_Generation_Reason_ExtendedBreak",
        "EstablishedActivityHistory" => "Workout_Generation_Reason_EstablishedHistory",
        "BelowEstablishedActivityThreshold" => "Workout_Generation_Reason_HistoryBelowThreshold",
        "BeginnerProfileRequiresGeneralStrength" =>
            "Workout_Generation_Fallback_BeginnerProfileRequiresGeneralStrength_Scheduled",
        "HighVolumeRequiresEstablishedResistanceHistory" =>
            "Workout_Generation_Fallback_HighVolumeRequiresEstablishedResistanceHistory_Scheduled",
        "ReplacedWithGeneralStrength" => "Workout_Generation_Activity_ReplacedWithGeneralStrength",
        "DirectBeginnerGeneralStrengthSettingsApplied" => "Workout_Generation_DirectBeginnerGeneralStrength",
        "HighVolumeConservativeStart" => "Workout_Generation_Reason_HighVolumeConservativeStart",
        "HeavyStrengthRequiresEstablishedHistoryFourExposuresKnownLoadAndCurrentFamiliarity" =>
            "Workout_Generation_Reason_StrengthHistory",
        "HighIntensityMainNeedsFourExposuresKnownManageableLoad" or
        "HighIntensitySelectedMainPrerequisiteMissing" =>
            "Workout_Generation_Reason_HighIntensityEvidenceMissing",
        "HighIntensitySaferAlternativePrescriptionReviewMissing" or
        "HighIntensityMainRequiresExplicitUserConfirmation" or
        "EveryRequiredHighIntensityMainNeedsFourExposuresKnownLoadAndFamiliarity" =>
            "Workout_Generation_Reason_HighIntensityExerciseEvidence",
        "MaximumTwoHeavyMainMovementsPerSession" or "MaximumFourExercisesPerSession" or
        "MaximumFiveExercisesPerSession" or "AccessoriesUseTwoSetsOfTenToFifteen" or
        "InitialDirectSetLimitTenPerPrimaryMusclePerWeek" or "NoMaximalTestingOrFailure" =>
            "Workout_Generation_Reason_TrainingLimits",
        "ConfirmFacilityAccessAndIndependentAbilityOrChooseInstructorLedActivity" or
        "IndependentSwimmingNotEligibleWithoutAccessAndAbility" or "AskContinuousVersusSwimRestPreference" =>
            "Workout_Generation_Reason_SwimAccessOrPreference",
        "TimeBasedWithoutInventedDistanceOrPace" or "ComfortableToModerateWithoutInventedPaceOrDistance" =>
            "Workout_Generation_Reason_TimeBasedActivity",
        "OptionalThirtySecondsComfortableSwimmingThirtySecondsRest" =>
            "Workout_Generation_Reason_SwimIntervalsOptional",
        "SelectedUnderlyingCardioActivity" => "Workout_Generation_Reason_HiitSelectedModality",
        "NoProgression" => "Workout_Generation_Reason_NoProgression",
        "HardButNotAllOut" => "Workout_Generation_Reason_HardNotAllOut",
        "BeginnerClimbingRequiresAppropriateInstructionOrSupervision" =>
            "Workout_Generation_Reason_ClimbingInstruction",
        "FacilityVisitIncludesWarmupAttemptsRestAndTransitions" =>
            "Workout_Generation_Reason_ClimbingVisitIncludesTransitions",
        "NoGradesOrFingerLoading" => "Workout_Generation_Reason_NoClimbingGradesOrFingerLoading",
        "RetainedManageableBaseline" or "RetainedExistingSchedule" =>
            "Workout_Generation_Reason_RetainedManageableBaseline",
        "FacilityVisitDuration" => "Workout_Generation_Reason_ClimbingVisitDuration",
        "ClassifyYogaByContentBeforeSelectingDuration" => "Workout_Generation_Reason_YogaContent",
        "ChooseAnEligibleCatalogOrUserDuration" => "Workout_Generation_Reason_ChooseActivityDuration",
        "ClassifiedByActualContent" => "Workout_Generation_Reason_YogaClassifiedByContent",
        "UseEligibleCatalogMovementsAndRestrictionMappings" => "Workout_Generation_Reason_ReviewedMovements",
        _ => "Workout_Generation_Reason_General"
    });

    public static string GetPreviewLimitationMessage(StrengthPlanPreviewResponseV020? preview)
    {
        if (preview is null)
            return NoStatusMessage;

        var reasons = preview.LimitationCodes
            .Concat(preview.Sessions.SelectMany(session => session.LimitationCodes))
            .Concat(preview.Sessions.SelectMany(session => session.Slots)
                .SelectMany(slot => slot.LimitationCodes))
            .ToHashSet(StringComparer.Ordinal);

        foreach (var scopeCode in new[] { "WorkoutPopulationBirthDateRequired",
                     "WorkoutPopulationDateUnavailable", "WorkoutPopulationAgeUnsupported",
                     "WorkoutPopulationPregnancyUnsupported" })
            if (reasons.Contains(scopeCode))
                return UiTextCatalog.Get(scopeCode);

        if (reasons.Contains("NoSafeStrengthStyleFit") &&
            (preview.MixedActivitySchedule?.Sessions.Count ?? 0) == 0)
            return UiTextCatalog.Get("Workout_Generation_Reason_NoSafeActivity");

        if (preview.AdvisoryCodes.Contains("NoSafeStrengthStyleFit") && preview.Status == "Feasible")
            return UiTextCatalog.Get("Workout_Generation_Reason_NoSafeStrengthContentRemaining");

        if (preview.Status == "Feasible" &&
            preview.FallbackReasonCode is "BeginnerProfileRequiresGeneralStrength" or
                "HighVolumeRequiresEstablishedResistanceHistory")
        {
            return UiTextCatalog.Get("Workout_Generation_Fallback_HvsToGs_Preview");
        }

        if (reasons.Contains("BeginnerProfileRequiresGeneralStrength") ||
            reasons.Contains("HighVolumeRequiresEstablishedResistanceHistory"))
            return UiTextCatalog.Get("Workout_Generation_Reason_HistoryInsufficient");

        if (reasons.Contains("StrengthSessionsExceedAvailableWeekdays"))
            return UiTextCatalog.Get("Workout_Generation_LimitationStrengthWeekdays");

        if (reasons.Contains(MixedActivityScheduleIncompleteCode) &&
            preview.MixedActivitySchedule is { } schedule)
        {
            var remainingReason = schedule.InputReasonCodes
                .Concat(schedule.Activities
                    .Where(activity => activity.Disposition is "Blocked" or "Omitted" or "PartiallyScheduled")
                    .SelectMany(activity => activity.ReasonCodes))
                .FirstOrDefault(code => !IsInformationalDiagnosticCode(code) &&
                    code is not "ActivityPrescriptionNotEligible" and not "ReplacedWithGeneralStrength");
            return remainingReason is null
                ? UiTextCatalog.Get("Workout_Generation_Reason_ScheduleIncomplete")
                : ScheduleReasonMessage(remainingReason);
        }

        if (preview.Status == "Feasible" &&
            preview.AdvisoryCodes.Contains("DirectBeginnerGeneralStrengthSettingsApplied"))
        {
            return UiTextCatalog.Get("Workout_Generation_DirectBeginnerGeneralStrength");
        }

        if (reasons.Contains(NoSafeStrengthContentRemainingCode))
        {
            return UiTextCatalog.Get("Workout_Generation_Reason_NoSafeStrengthContentRemaining");
        }

        if (reasons.Contains("HighIntensityMainNeedsFourExposuresKnownManageableLoad") ||
            reasons.Contains("HighIntensitySelectedMainPrerequisiteMissing"))
            return UiTextCatalog.Get("Workout_Generation_Reason_HighIntensityEvidenceMissing");

        if (reasons.Contains("HighIntensitySaferAlternativePrescriptionReviewMissing") ||
            reasons.Contains("HighIntensityMainRequiresExplicitUserConfirmation") ||
            reasons.Contains("EveryRequiredHighIntensityMainNeedsFourExposuresKnownLoadAndFamiliarity"))
            return UiTextCatalog.Get("Workout_Generation_Reason_HighIntensityExerciseEvidence");

        var key = reasons.Overlaps(["StrengthWarmupOrCooldownDurationMissing", "SessionDurationMissing",
                "SelectedActivityDurationInvalid",
                "RequiredMovementWorkExceedsSessionDurationOrTimingMissing"])
            ? "Workout_Generation_LimitationTiming"
            : reasons.Overlaps(["ActivityExperienceHistoryMissing", "ActivityHistoryInsufficientOrInvalid"])
                ? "Workout_Generation_LimitationExperience"
                : reasons.Overlaps(["CatalogEligibilityMetadataMissing", "TemplateSlotReviewMissing",
                    "RequiredMovementSlotUnresolved"])
                    ? "Workout_Generation_LimitationReview"
                    : "Workout_Generation_LimitationGeneral";
        return UiTextCatalog.Get(key);
    }

    public static string FormatLocalized(string key, params string[] arguments)
    {
        var formatted = UiTextCatalog.Get(key);
        for (var argumentIndex = 0; argumentIndex < arguments.Length; argumentIndex++)
            formatted = formatted.Replace($"{{{argumentIndex}}}", arguments[argumentIndex], StringComparison.Ordinal);
        return formatted;
    }

    private static string ActivityFrequencySummary(MixedActivityScheduleDecisionV020 decision)
    {
        if (decision.ScheduledSessionsPerWeek > NoPolicyReasons)
            return FrequencyValue(decision.ScheduledSessionsPerWeek);
        return decision.TargetSessionsPerWeek is > NoPolicyReasons
            ? FormatLocalized("Workout_Generation_ActivityTargetFormat",
                FrequencyValue(decision.TargetSessionsPerWeek))
            : NoStatusMessage;
    }

    private static string FrequencyValue(int? value)
    {
        if (!value.HasValue)
            return UiTextCatalog.Get("Workout_Generation_NotSpecified");

        return FormatLocalized(value.Value == OneSessionPerWeek
                ? "Workout_Generation_ActivityFrequencyOneSessionFormat"
                : "Workout_Generation_ActivityFrequencyMultipleSessionsFormat",
            value.Value.ToString(CultureInfo.CurrentCulture));
    }

    private static string FrequencySummaryValue(int? value) => value.HasValue
        ? FormatLocalized("Workout_Generation_ActivityFrequencyPerWeekFormat",
            value.Value.ToString(CultureInfo.CurrentCulture))
        : UiTextCatalog.Get("Workout_Generation_NotSpecified");

    private static string DispositionLabel(string disposition) => disposition switch
    {
        "Scheduled" => UiTextCatalog.Get("Workout_Generation_Disposition_Scheduled"),
        "PartiallyScheduled" => UiTextCatalog.Get("Workout_Generation_Disposition_Partial"),
        "Omitted" => UiTextCatalog.Get("Workout_Generation_Disposition_Omitted"),
        "Blocked" => UiTextCatalog.Get("Workout_Generation_Disposition_Blocked"),
        "NotScheduled" => UiTextCatalog.Get("Workout_Generation_Disposition_NotScheduled"),
        _ => UiTextCatalog.Get("Workout_Generation_Disposition_Review")
    };

    private static string PrescriptionStatusLabel(string status) => status switch
    {
        "Eligible" => UiTextCatalog.Get("Workout_Generation_Prescription_Eligible"),
        "OfferGeneralStrength" => UiTextCatalog.Get("Workout_Generation_Prescription_OfferGeneralStrength"),
        "NeedsFamiliarization" => UiTextCatalog.Get("Workout_Generation_Prescription_NeedsFamiliarization"),
        "NeedsUserDuration" => UiTextCatalog.Get("Workout_Generation_Prescription_NeedsUserDuration"),
        "BlockedByRestriction" => UiTextCatalog.Get("Workout_Generation_Prescription_BlockedByRestriction"),
        "NotSelected" => UiTextCatalog.Get("Workout_Generation_Prescription_NotSelected"),
        "Unsupported" => UiTextCatalog.Get("Workout_Generation_Prescription_Unsupported"),
        _ => UiTextCatalog.Get("Workout_Generation_Prescription_Review")
    };

    private static readonly HashSet<string> InformationalDiagnosticCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "NoProgression",
        "HardButNotAllOut",
        "RetainedManageableBaseline",
        "RetainedExistingSchedule",
        "ClassifiedByActualContent",
        "UserOrCatalogSelectedDuration",
        "SelectedDurationAppliedToPrescription",
        "SelectedUnderlyingCardioActivity",
        "ReplacesOneCardioSession",
        "OneContinuousSessionReplacedByHiit",
        "UseEligibleCatalogMovementsAndRestrictionMappings",
        "TimeBasedWithoutInventedDistanceOrPace",
        "ComfortableToModerateWithoutInventedPaceOrDistance",
        "FacilityVisitIncludesWarmupAttemptsRestAndTransitions",
        "FacilityVisitDuration",
        "NoGradesOrFingerLoading",
        "MaximumTwoHeavyMainMovementsPerSession",
        "MaximumFourExercisesPerSession",
        "MaximumFiveExercisesPerSession",
        "AccessoriesUseTwoSetsOfTenToFifteen",
        "InitialDirectSetLimitTenPerPrimaryMusclePerWeek",
        "NoMaximalTestingOrFailure",
        "OptionalThirtySecondsComfortableSwimmingThirtySecondsRest",
        "EstablishedActivityHistory",
        "ExplicitRestDay",
        "RestDayIsNotAWorkoutSession",
        "HighVolumeConservativeStart"
    };

    public static bool IsInformationalDiagnosticCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && InformationalDiagnosticCodes.Contains(code);
}

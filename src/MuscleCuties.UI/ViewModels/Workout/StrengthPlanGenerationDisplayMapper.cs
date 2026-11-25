using MuscleCuties.UI.Models.UI.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>Turns a plan preview into readable rows: activity and weekday labels, coverage and limitation notes,</summary>
/// and the per-session schedule the generation page shows.</summary>
internal static class StrengthPlanGenerationDisplayMapper
{
    private const int NoPolicyReasons = 0;
    private const decimal NoDurationMinutes = 0m;
    private const int OneSessionPerWeek = 1;
    private const string NoStatusMessage = "";
    private const string MixedActivityScheduleIncompleteCode = "MixedActivityScheduleIncomplete";
    private const string NoSafeStrengthContentRemainingCode = "NoSafeStrengthContentRemaining";
    private const string SessionWithheldCodePrefix = "SessionWithheld:";
    private const int OneWithheldSession = 1;
    private const string SessionWithheldOneKey = "Workout_Generation_SessionWithheldOne";
    private const string SessionWithheldManyKey = "Workout_Generation_SessionWithheldManyFormat";

    /// <summary>The session as its display model.</summary>
    public static MixedActivitySessionDisplay ToMixedActivitySessionDisplay(
        MixedActivityScheduleSessionV020 session)
    {
        var title = LocalizedFormat.Format("Workout_Generation_MixedSessionTitleFormat",
            FullWeekdayLabel(session.Weekday), ActivityLabel(session.ActivityTypeId));
        var effortKey = session.IsHardSession
            ? "Workout_Generation_HigherEffortSession"
            : "Workout_Generation_RegularEffortSession";
        var duration = session.DurationMinutes > NoDurationMinutes
            ? LocalizedFormat.Format("Workout_Generation_MixedSessionDurationFormat",
                session.DurationMinutes.ToString("0", CultureInfo.CurrentCulture))
            : UiTextCatalog.Get("Workout_Generation_EstimateUnavailable");
        var details = LocalizedFormat.Format("Workout_Generation_MixedSessionDetailsFormat", duration,
            UiTextCatalog.Get(effortKey));
        return new MixedActivitySessionDisplay(title, details)
        {
            Day = FullWeekdayLabel(session.Weekday),
            Activity = ActivityLabel(session.ActivityTypeId),
            Duration = duration,
            PhaseDetail = PhaseDetailText(session.OccurrencePrescription),
            IconGlyph = WorkoutActivityIcons.ForActivity(session.ActivityTypeId)
        };
    }

    /// <summary>
    /// The timed phase a session actually prescribes. Only supplied values are shown; an activity
    /// without a phase split stays silent instead of inventing a warm-up or interval block.
    /// </summary>
    private static string PhaseDetailText(ActivitySessionPrescriptionOutputV020? prescription)
    {
        if (prescription is null)
            return NoStatusMessage;

        var parts = new List<string>();
        if (prescription.WarmupMinutes is { } warmup)
            parts.Add(LocalizedFormat.Format("Workout_Generation_PhaseWarmupFormat",
                warmup.ToString("0.#", CultureInfo.CurrentCulture)));
        if (prescription.MainMinutes is { } main)
            parts.Add(LocalizedFormat.Format("Workout_Generation_PhaseMainFormat",
                main.ToString("0.#", CultureInfo.CurrentCulture)));
        if (prescription.CooldownMinutes is { } cooldown)
            parts.Add(LocalizedFormat.Format("Workout_Generation_PhaseCooldownFormat",
                cooldown.ToString("0.#", CultureInfo.CurrentCulture)));
        if (prescription.IntervalWorkCount is { } workCount && prescription.IntervalWorkSeconds is { } workSeconds)
            parts.Add(LocalizedFormat.Format("Workout_Generation_PhaseIntervalsFormat",
                workCount.ToString(CultureInfo.CurrentCulture),
                workSeconds.ToString("0.#", CultureInfo.CurrentCulture)));
        if (prescription.IntervalRecoveryCount is { } recoveryCount &&
            prescription.IntervalRecoverySeconds is { } recoverySeconds)
            parts.Add(LocalizedFormat.Format("Workout_Generation_PhaseRecoveryFormat",
                recoveryCount.ToString(CultureInfo.CurrentCulture),
                recoverySeconds.ToString("0.#", CultureInfo.CurrentCulture)));
        return string.Join(" · ", parts);
    }

    /// <summary>The decision as its display model.</summary>
    public static MixedActivityDecisionDisplay ToMixedActivityDecisionDisplay(
        MixedActivityScheduleDecisionV020 decision)
    {
        var disposition = DispositionLabel(decision.Disposition);
        var prescriptionStatus = decision.PrescriptionStatus;
        var status = string.IsNullOrWhiteSpace(prescriptionStatus) ||
                     string.Equals(prescriptionStatus, "Eligible", StringComparison.Ordinal)
            ? disposition
            : LocalizedFormat.Format("Workout_Generation_ActivityDecisionStatusFormat", disposition,
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

    /// <summary>The activity's name for an id that may come from a newer server build, in which case it reads as</summary>
    /// unknown rather than as an empty label.</summary>
    public static string ActivityLabel(int activityTypeId)
    {
        if (!Enum.IsDefined(typeof(WorkoutActivityDto), activityTypeId))
            return UiTextCatalog.Get("Workout_Generation_UnknownActivity");

        return ActivityLabel((WorkoutActivityDto)activityTypeId);
    }

    /// <summary>The label for an activity.</summary>
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

    /// <summary>The description shown for a strength activity.</summary>
    public static string StrengthPlanDescription(WorkoutActivityDto activity) => activity switch
    {
        WorkoutActivityDto.GeneralStrength => UiTextCatalog.Get("Workout_Generation_GeneralStrengthDescription"),
        WorkoutActivityDto.HighVolumeStrength => UiTextCatalog.Get("Workout_Generation_HighVolumeStrengthDescription"),
        WorkoutActivityDto.StrengthHighIntensity => UiTextCatalog.Get("Workout_Generation_HighIntensityStrengthDescription"),
        _ => string.Empty
    };

    /// <summary>The full label for a weekday.</summary>
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

    /// <summary>The short label for a weekday.</summary>
    public static string ShortWeekdayLabel(DayOfWeek weekday) => UiTextCatalog.Get(WeekdayLabelKey(weekday));

    /// <summary>The one short weekday resource key set; session titles and day chips both read it.</summary>
    public static string WeekdayLabelKey(DayOfWeek weekday) => weekday switch
    {
        DayOfWeek.Monday => "Workout_Generation_DayMonday",
        DayOfWeek.Tuesday => "Workout_Generation_DayTuesday",
        DayOfWeek.Wednesday => "Workout_Generation_DayWednesday",
        DayOfWeek.Thursday => "Workout_Generation_DayThursday",
        DayOfWeek.Friday => "Workout_Generation_DayFriday",
        DayOfWeek.Saturday => "Workout_Generation_DaySaturday",
        DayOfWeek.Sunday => "Workout_Generation_DaySunday",
        _ => "Workout_Generation_UnknownDay"
    };

    /// <summary>The message shown for a schedule reason code.</summary>
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
        "WeeklyCoverageMissing_UpperPull_Equipment" => "Workout_Generation_MissingUpperPullEquipment",
        "WeeklyCoverageMissing_UpperPull_BandAnchorRequired" => "Workout_Generation_MissingUpperPullBandAnchor",
        "WeeklyCoverageMissing_DirectCalves" => "Workout_Generation_MissingDirectCalves",
        "WeeklyCoverageMissing_DirectRearDelts" => "Workout_Generation_MissingDirectRearDelts",
        "WeeklyCoverageBelowTarget_DirectGluteDays" => "Workout_Generation_BelowTargetDirectGluteDays",
        "WeeklyCoverageBelowTarget_DynamicCore" => "Workout_Generation_BelowTargetDynamicCore",
        "WeeklyCoverageMissing_GluteFocusHipAbduction" => "Workout_Generation_MissingGluteFocusAbduction",
        _ => "Workout_Generation_Reason_General"
    });

    /// <summary>Summarizes weekly coverage without exposing internal reason codes.</summary>
    public static IReadOnlyList<string> CoverageNotes(StrengthPlanPreviewResponseV020? preview)
    {
        if (preview is null)
            return [];
        var codes = preview.AdvisoryCodes.Concat(preview.LimitationCodes)
            .Concat(preview.GenerationV2?.MissingGaps ?? [])
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var notes = new List<string>();
        if (codes.Contains("WeeklyCoverageMissing_UpperPull_Equipment"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingUpperPullEquipment"));
        if (codes.Contains("WeeklyCoverageMissing_UpperPull_BandAnchorRequired"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingUpperPullBandAnchor"));
        if (codes.Contains("WeeklyCoverageMissing_DirectCalves"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingDirectCalves"));
        if (codes.Contains("WeeklyCoverageMissing_DirectRearDelts"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingDirectRearDelts"));
        if (codes.Contains("WeeklyCoverageBelowTarget_DynamicCore"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_BelowTargetDynamicCore"));
        if (codes.Contains("WeeklyCoverageBelowTarget_DirectGluteDays"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_BelowTargetDirectGluteDays"));
        if (codes.Contains("WeeklyCoverageMissing_GluteFocusHipAbduction") || codes.Contains("WeeklyCoverageMissing_GluteFocusAbduction"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingGluteFocusAbduction"));
        if (codes.Contains("WeeklyMovementCoverageMissing_KneeExtension"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingKneeExtension"));
        if (codes.Contains("WeeklyMovementCoverageMissing_KneeFlexion"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingKneeFlexion"));
        if (codes.Contains("WeeklyMovementCoverageMissing_HipExtension"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MissingHipExtension"));
        if (codes.Contains("WeeklyMovementCoverageUnknown"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MovementCoverageUnknown"));
        if (preview.RequiresReducedVolumeAcknowledgment())
            notes.Add(UiTextCatalog.Get("Workout_Generation_ReducedVolumeNotice"));
        if (codes.Contains("OneDayFullBodyHasLimitedWeeklyCoverage") ||
            codes.Contains("StrengthDaysInsufficientForMinimumCoverage"))
            notes.Add(UiTextCatalog.Get("Workout_Generation_OneDayCoverageNotice"));
        if (codes.Any(code => code.StartsWith("PrimaryRegionCoverageBelowMinimum_", StringComparison.Ordinal) ||
                              code.StartsWith("LowerBodyCoverageBelowMinimum_", StringComparison.Ordinal)))
            notes.Add(UiTextCatalog.Get("Workout_Generation_CoverageGapNotice"));
        var knownRegions = new HashSet<string>(StringComparer.Ordinal)
        {
            "Chest", "LatsUpperBack", "Deltoids", "Quadriceps", "Hamstrings", "Glutes",
            "Biceps", "Triceps", "Calves", "Trunk", "Abductors", "Adductors"
        };
        var gapRegions = codes.Where(code => code.StartsWith("PrimaryRegionCoverageBelowMinimum_", StringComparison.Ordinal) ||
                                             code.StartsWith("LowerBodyCoverageBelowMinimum_", StringComparison.Ordinal))
            .Select(code => code[(code.LastIndexOf('_') + 1)..])
            .Where(knownRegions.Contains).Distinct(StringComparer.Ordinal);
        foreach (var region in gapRegions)
        {
            var key = "Workout_Generation_CoverageMuscle_" + region;
            notes.Add(UiTextCatalog.Get(key));
        }
        if (codes.Contains("ThreeExposuresUnavailableWithTwoStrengthDays") ||
            codes.Any(code => code.StartsWith("PrimaryRegionCoverageBelowPreferred_", StringComparison.Ordinal)))
            notes.Add(UiTextCatalog.Get("Workout_Generation_CoveragePreferredNotice"));
        return notes;
    }

    /// <summary>The notes shown after a plan is accepted: which regions are under-covered and whether a session was</summary>
    /// withheld, so the user knows what the plan will not do.</summary>
    public static IReadOnlyList<string> PostAcceptanceNotices(StrengthPlanPreviewResponseV020? preview)
    {
        if (preview is null)
            return [];
        var notes = CoverageNotes(preview).ToList();
        var withheld = SessionWithheldNote(preview);
        if (!string.IsNullOrWhiteSpace(withheld))
            notes.Add(withheld);
        foreach (var decision in preview.MixedActivitySchedule?.Activities ?? [])
        {
            if (decision.ActivityTypeId == (int)WorkoutActivityDto.RestDay ||
                decision.Disposition is not ("Blocked" or "Omitted" or "PartiallyScheduled"))
                continue;
            var display = ToMixedActivityDecisionDisplay(decision);
            notes.Add($"{display.Activity}: {display.Status}. {display.Frequency} {string.Join(", ", display.Reasons)}".Trim());
        }
        if (preview.RequiresReducedVolumeAcknowledgment())
            notes.Add(UiTextCatalog.Get("Workout_Generation_ReducedVolumeNotice"));
        if (preview.AdvisoryCodes.Any(code => code.StartsWith("MovementOmitted:", StringComparison.Ordinal)))
            notes.Add(UiTextCatalog.Get("Workout_Generation_MovementOmitted"));
        if (preview.AdvisoryCodes.Any(code => code.StartsWith("MovementOmittedForDuration:", StringComparison.Ordinal)))
            notes.Add(UiTextCatalog.Get("Workout_Generation_DurationOmissionNotice"));
        return notes.Distinct().ToArray();
    }

    /// <summary>One line naming why the plan is limited, combining plan-level and per-session codes without repeating</summary>
    /// the same reason twice.</summary>
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

        if (reasons.Contains(NoSafeStrengthContentRemainingCode))
        {
            return UiTextCatalog.Get("Workout_Generation_Reason_NoSafeStrengthContentRemaining");
        }

        if (reasons.Any(IsSessionWithheldCode))
            return SessionWithheldNote(preview);

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

    /// <summary>
    /// The server withholds a session whose strength block has no safe exercise left instead of
    /// sending it empty, and reports it as <c>SessionWithheld:&lt;template&gt;</c>. The preview states how
    /// many sessions were removed and why; it is silent when none were.
    /// </summary>
    public static string SessionWithheldNote(StrengthPlanPreviewResponseV020? preview)
    {
        if (preview is null)
            return NoStatusMessage;

        var withheld = preview.AdvisoryCodes
            .Concat(preview.LimitationCodes)
            .Concat(preview.Sessions.SelectMany(session => session.LimitationCodes))
            .Where(IsSessionWithheldCode)
            .Distinct(StringComparer.Ordinal)
            .Count();
        return withheld switch
        {
            NoPolicyReasons => NoStatusMessage,
            OneWithheldSession => UiTextCatalog.Get(SessionWithheldOneKey),
            _ => LocalizedFormat.Format(SessionWithheldManyKey, withheld.ToString(CultureInfo.CurrentCulture))
        };
    }

    /// <summary>True when the code says a session was withheld.</summary>
    private static bool IsSessionWithheldCode(string? code) =>
        code is not null && code.StartsWith(SessionWithheldCodePrefix, StringComparison.Ordinal);

    /// <summary>The scheduled sessions per week when the plan sets them, otherwise the target it was aiming for, marked</summary>
    /// as a target so the two are never confused.</summary>
    private static string ActivityFrequencySummary(MixedActivityScheduleDecisionV020 decision)
    {
        if (decision.ScheduledSessionsPerWeek > NoPolicyReasons)
            return FrequencyValue(decision.ScheduledSessionsPerWeek);
        return decision.TargetSessionsPerWeek is > NoPolicyReasons
            ? LocalizedFormat.Format("Workout_Generation_ActivityTargetFormat",
                FrequencyValue(decision.TargetSessionsPerWeek))
            : NoStatusMessage;
    }

    /// <summary>A session count as text, with its own singular wording and "not specified" when the plan left it open.</summary>
    private static string FrequencyValue(int? value)
    {
        if (!value.HasValue)
            return UiTextCatalog.Get("Workout_Generation_NotSpecified");

        return LocalizedFormat.Format(value.Value == OneSessionPerWeek
                ? "Workout_Generation_ActivityFrequencyOneSessionFormat"
                : "Workout_Generation_ActivityFrequencyMultipleSessionsFormat",
            value.Value.ToString(CultureInfo.CurrentCulture));
    }

    /// <summary>The frequency summary for a weekly count.</summary>
    private static string FrequencySummaryValue(int? value) => value.HasValue
        ? LocalizedFormat.Format("Workout_Generation_ActivityFrequencyPerWeekFormat",
            value.Value.ToString(CultureInfo.CurrentCulture))
        : UiTextCatalog.Get("Workout_Generation_NotSpecified");

    /// <summary>The label for a schedule disposition.</summary>
    private static string DispositionLabel(string disposition) => disposition switch
    {
        "Scheduled" => UiTextCatalog.Get("Workout_Generation_Disposition_Scheduled"),
        "PartiallyScheduled" => UiTextCatalog.Get("Workout_Generation_Disposition_Partial"),
        "Omitted" => UiTextCatalog.Get("Workout_Generation_Disposition_Omitted"),
        "Blocked" => UiTextCatalog.Get("Workout_Generation_Disposition_Blocked"),
        "NotScheduled" => UiTextCatalog.Get("Workout_Generation_Disposition_NotScheduled"),
        _ => UiTextCatalog.Get("Workout_Generation_Disposition_Review")
    };

    /// <summary>The label for a prescription status.</summary>
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

    /// <summary>True when the code only informs the user.</summary>
    public static bool IsInformationalDiagnosticCode(string? code) =>
        !string.IsNullOrWhiteSpace(code) && InformationalDiagnosticCodes.Contains(code);
}

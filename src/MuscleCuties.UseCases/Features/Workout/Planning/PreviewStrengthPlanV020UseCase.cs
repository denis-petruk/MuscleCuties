using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Loads trusted planning inputs and delegates all proposal decisions to Core.</summary>
public sealed class PreviewStrengthPlanV020UseCase(IStrengthPlanPreviewSourceV020 source)
{
    public async Task<StrengthPlanPreviewResponseV020> ExecuteAsync(Guid userId,
        StrengthPlanPreviewRequestV020 request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty || (request.DraftPreferences is { } draft &&
            (!WorkoutPreferencesRule.IsValid(draft.ActivityTypes, draft.AvailableWeekdays,
                draft.ActivitySessionDurationsMinutes, draft.RequestedSessionsPerWeek) ||
             !WorkoutPreferencesRule.IsValidEquipmentLevel(draft.EquipmentLevel))))
            return StrengthPlanPreviewMapperV020.ToResponse(StrengthPlanPreviewRule.Evaluate(null, null));

        var sourceData = await source.LoadAsync(userId, cancellationToken).ConfigureAwait(false);
        return EvaluateLoadedSource(userId, request, sourceData);
    }

    /// <summary>Evaluates the canonical preview using source data loaded by the caller.</summary>
    public static StrengthPlanPreviewResponseV020 EvaluateLoadedSource(Guid userId,
        StrengthPlanPreviewRequestV020 request, StrengthPlanPreviewSourceV020? sourceData)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.DraftPreferences is { } draft)
        {
            if (!WorkoutPreferencesRule.IsValid(draft.ActivityTypes, draft.AvailableWeekdays,
                    draft.ActivitySessionDurationsMinutes, draft.RequestedSessionsPerWeek) ||
                !WorkoutPreferencesRule.IsValidEquipmentLevel(draft.EquipmentLevel))
                return StrengthPlanPreviewMapperV020.ToResponse(StrengthPlanPreviewRule.Evaluate(null, null));
            request = request with
            {
                SelectedActivityTypes = draft.ActivityTypes.ToHashSet(),
                AvailableWeekdays = draft.AvailableWeekdays.Select(value => (DayOfWeek)value).ToHashSet(),
                ActivitySessionDurationsMinutes = draft.ActivitySessionDurationsMinutes,
                RequestedSessionsPerWeek = draft.RequestedSessionsPerWeek,
                ExcludedExerciseIds = draft.ExcludedExerciseIds?.ToHashSet()
            };
        }
        if (userId == Guid.Empty)
            return StrengthPlanPreviewMapperV020.ToResponse(
                StrengthPlanPreviewRule.Evaluate(null, null));


        if (sourceData is not null)
        {
            if (sourceData.SavedAnswers is { } savedAnswers)
                request = request with
                {
                    ActivityEligibilityEvidence = savedAnswers.ActivityEligibilityEvidence,
                    HiitEligibilityEvidence = savedAnswers.HiitEligibilityEvidence,
                    HiitUnderlyingCardioActivity = savedAnswers.HiitUnderlyingCardioActivity
                };
            var selectedActivities = request.SelectedActivityTypes?.ToHashSet() ??
                                     sourceData.SelectedActivityTypes?.ToHashSet() ?? new HashSet<int>();

            sourceData = sourceData with
            {
                SelectedActivityTypes = selectedActivities,
                AvailableWeekdays = request.AvailableWeekdays ?? sourceData.AvailableWeekdays,
                ActivitySessionDurationsMinutes = request.ActivitySessionDurationsMinutes ??
                                                  sourceData.ActivitySessionDurationsMinutes,
                RequestedSessionsPerWeek = request.RequestedSessionsPerWeek ??
                                           sourceData.RequestedSessionsPerWeek,
                Equipment = request.DraftPreferences?.EquipmentLevel is { } equipment &&
                            Enum.TryParse<Equipment>(equipment, true, out var parsedEquipment)
                    ? parsedEquipment : sourceData.Equipment,
                ExcludedExerciseIds = request.DraftPreferences?.ExcludedExerciseIds ?? sourceData.ExcludedExerciseIds
            };
        }

        if (sourceData is not null)
        {
            var selected = sourceData.SelectedActivityTypes?.Select(value => (WorkoutActivityType)value).ToArray() ?? [];
            var answers = (request.ActivityEligibilityEvidence ?? []).Select(answer => new ActivitySafetyAnswers(
                WorkoutActivityContractMapper.ToCore(answer.Activity), answer.HasSwimmingFacilityAccess,
                answer.CanSwimIndependently, answer.WantsContinuousSwimming, answer.HasRopeClimbingCompetence,
                answer.AppropriateClimbingInstructionAvailable, answer.IsDemandingYogaContent,
                answer.ActivityHistory is { } history
                    ? ActivityExperienceRule.Classify(WorkoutActivityContractMapper.ToCore(answer.Activity).ToString(),
                        new ActivityTrainingHistory(history.ActivityId, history.ConsistentParticipationWeeks,
                            history.AverageSessionsPerWeekInRecentFourWeeks, history.LastCompletedSessionDate,
                            history.IsSelfReported), request.LocalDate).Experience
                    : ActivityExperience.Unknown)).ToArray();
            var missing = ActivitySafetyAnswersRule.Missing(selected, answers);
            if (missing.Count > 0)
                return StrengthPlanPreviewMapperV020.ToResponse(StrengthPlanPreviewRule.Evaluate(null, null)) with
                {
                    Status = "RequiredAnswersMissing",
                    LimitationCodes = missing,
                    SourceAnswerVersion = sourceData.AnswerVersion
                };
        }

        var excludedExerciseIds = new HashSet<int>(request.ExcludedExerciseIds ?? new HashSet<int>());
        if (sourceData?.ExcludedExerciseIds is { } savedExclusions)
            excludedExerciseIds.UnionWith(savedExclusions);
        var choices = StrengthPlanPreviewMapperV020.ToChoices(request with
        {
            ExcludedExerciseIds = excludedExerciseIds,
            AvailableWeekdays = (sourceData?.AvailableWeekdays ?? request.AvailableWeekdays)?.ToHashSet()
        });
        var planningData = sourceData is null
            ? null
            : StrengthPlanPreviewMapperV020.ToData(sourceData);

        var selectedStrengthStyles = planningData?.SelectedActivities?
            .Where(StrengthStyleSelectionRule.IsStrengthStyle)
            .Distinct()
            .ToArray() ?? [];

        StrengthPlanPreviewResult result;
        var chosenStrengthActivity = request.Activity;
        var requiresReducedVolume = false;
        var multipleStylesIdentified = false;

        if (selectedStrengthStyles.Length > 1)
        {
            var selectionResult = StrengthStyleSelectionRule.Select(choices, planningData);
            if (selectionResult.RequiresUserChoice)
            {
                var explicitStyle = request.ExplicitStrengthStyleChoice is { } selectedStyle
                    ? WorkoutActivityContractMapper.ToCore(selectedStyle) : (WorkoutActivityType?)null;
                if (explicitStyle is null || !selectionResult.FittingUnreducedStyles.Contains(explicitStyle.Value))
                    return StrengthPlanPreviewMapperV020.ToResponse(StrengthPlanPreviewRule.Evaluate(null, null)) with
                    {
                        Status = "StrengthStyleChoiceRequired",
                        StrengthStyleChoiceRequired = true,
                        FittingStrengthStyleIds = selectionResult.FittingUnreducedStyles.Select(style => (int)style).ToArray(),
                        LimitationCodes = [StrengthStyleSelectionRule.MultipleFittingStylesIdentifiedCode]
                    };
                selectionResult = selectionResult with { ChosenStyle = explicitStyle };
            }
            if (selectionResult.ChosenStyle is { } chosenStyle)
            {
                chosenStrengthActivity = WorkoutActivityContractMapper.ToDto(chosenStyle);
                choices = choices with { Activity = chosenStyle };
                request = request with { Activity = chosenStrengthActivity };

                var eval = selectionResult.Evaluations.FirstOrDefault(e => e.Activity == chosenStyle);
                result = eval?.PreviewResult ?? StrengthPlanPreviewRule.EvaluateDirect(choices, planningData);
            }
            else
            {
                result = StrengthPlanPreviewRule.EvaluateDirect(choices, planningData);
                if (selectionResult.LimitationCode is not null)
                {
                    result = result with
                    {
                        Status = StrengthPlanPreviewStatus.Limited,
                        LimitationCodes = result.LimitationCodes
                            .Append(selectionResult.LimitationCode)
                            .Distinct(StringComparer.Ordinal)
                            .ToArray()
                    };
                }
            }

            requiresReducedVolume = selectionResult.RequiresReducedVolumeAcknowledgment;
            multipleStylesIdentified = selectionResult.RequiresUserChoice;
        }
        else
        {
            if (selectedStrengthStyles.Length == 1 &&
                choices.Activity != selectedStrengthStyles[0])
            {
                chosenStrengthActivity = WorkoutActivityContractMapper.ToDto(selectedStrengthStyles[0]);
                choices = choices with { Activity = selectedStrengthStyles[0] };
                request = request with { Activity = chosenStrengthActivity };
            }

            result = StrengthPlanPreviewRule.EvaluateWithFallback(choices, planningData);
            requiresReducedVolume = result.RequiresReducedVolumeAcknowledgment;
        }

        // A valid remainder may still be scheduled when no selected strength option is safe.
        // Invalid inputs remain failures rather than being reinterpreted as omissions.
        if (sourceData is not null && result.Status == StrengthPlanPreviewStatus.Limited &&
            selectedStrengthStyles.Length > 0 && result.Sessions.Count > 0 &&
            result.Sessions.SelectMany(session => session.Slots).Any(slot =>
                slot.IsRequired && slot.Outcome == StrengthSlotResolutionOutcome.NoSafeCandidate) &&
            result.Sessions.SelectMany(session => session.Slots).Where(slot => slot.IsRequired).All(slot =>
                slot.Outcome != StrengthSlotResolutionOutcome.MissingReviewData) &&
            result.LimitationCodes.All(code => code == "RequiredMovementSlotUnresolved"))
        {
            var context = MixedActivityScheduleMapperV020.ToContext(request, sourceData, result);
            var nonStrength = context.SelectedActivities?.Where(activity =>
                !StrengthStyleSelectionRule.IsStrengthStyle(activity)).ToArray() ?? [];
            if (nonStrength.Length > 0)
            {
                var remainder = MixedActivityWeekScheduleRule.Evaluate(context with
                {
                    SelectedActivities = nonStrength,
                    ChosenStrengthActivity = null
                });
                if (remainder.InputReasons.Count == 0 && remainder.Sessions.Count > 0)
                {
                    var omission = new MixedActivityScheduleDecisionV020((int)choices.Activity,
                        MixedActivityScheduleDisposition.Omitted.ToString(), null, null, null, 0,
                        result.LimitationCodes.Count > 0 ? result.LimitationCodes : [StrengthStyleSelectionRule.NoSafeStrengthStyleFitCode]);
                    var schedule = StrengthPlanPreviewMapperV020.ToMixedSchedule(remainder);
                    return StrengthPlanPreviewMapperV020.ToResponse(result) with
                    {
                        Status = nameof(StrengthPlanPreviewStatus.Feasible),
                        Sessions = [],
                        LimitationCodes = [],
                        AdvisoryCodes = result.AdvisoryCodes.Append(StrengthStyleSelectionRule.NoSafeStrengthStyleFitCode).ToArray(),
                        MixedActivitySchedule = schedule with { Activities = schedule.Activities.Append(omission).ToArray() },
                        SourceProfileVersion = sourceData.ProfileVersion,
                        SourceInjuryFingerprint = sourceData.InjuryFingerprint,
                        SourceActivePlanId = sourceData.ActivePlanId,
                        SourceActiveRevisionId = sourceData.ActiveRevisionId,
                        SourceHistoryFingerprint = sourceData.HistoryFingerprint,
                        SourceCatalogFingerprint = sourceData.CatalogFingerprint,
                        SourceTimeZoneVersion = sourceData.TimeZoneVersion,
                        SourceExperienceVersion = sourceData.ExperienceVersion,
                        SourceAnswerVersion = sourceData.AnswerVersion,
                        SourceMuscleGroupsByExerciseId = sourceData.MuscleGroupsByExerciseId?.ToDictionary(
                            pair => pair.Key, pair => pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray())
                    };
                }
            }
        }

        var response = StrengthPlanPreviewMapperV020.ToResponse(result);
        if (requiresReducedVolume)
        {
            response = response with { RequiresReducedVolumeAcknowledgment = true };
        }

        if (multipleStylesIdentified)
        {
            var advisoryCodes = response.AdvisoryCodes
                .Append(StrengthStyleSelectionRule.MultipleFittingStylesIdentifiedCode)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            response = response with { AdvisoryCodes = advisoryCodes };
        }

        if (sourceData is null)
            return response;

        var mixedContext = MixedActivityScheduleMapperV020.ToContext(request, sourceData, result);
        var mixedSchedule = MixedActivityWeekScheduleRule.Evaluate(mixedContext);
        var muscles = result.Sessions.Select(session =>
        {
            var groups = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var exercises = session.Slots.Where(slot => slot.ExerciseId.HasValue).ToArray();
            if (exercises.Length == 0) return (IReadOnlySet<string>?)null;
            foreach (var slot in exercises)
            {
                if (sourceData.MuscleGroupsByExerciseId is null ||
                    !sourceData.MuscleGroupsByExerciseId.TryGetValue(slot.ExerciseId!.Value, out var values) ||
                    values.Count == 0 || values.Any(string.IsNullOrWhiteSpace))
                    return null;
                groups.UnionWith(values);
            }
            return groups;
        }).ToArray();
        mixedSchedule = StrengthScheduleAssignmentRule.Reconcile(mixedSchedule, muscles,
            mixedContext.AvailableWeekdays ?? [], result.EffectiveActivity ?? WorkoutActivityContractMapper.ToCore(chosenStrengthActivity));
        return StrengthPlanPreviewMapperV020.AttachMixedSchedule(response, mixedSchedule, chosenStrengthActivity) with
        {
            SourceProfileVersion = sourceData.ProfileVersion,
            SourceInjuryFingerprint = sourceData.InjuryFingerprint,
            SourceActivePlanId = sourceData.ActivePlanId,
            SourceActiveRevisionId = sourceData.ActiveRevisionId,
            SourceHistoryFingerprint = sourceData.HistoryFingerprint,
            SourceCatalogFingerprint = sourceData.CatalogFingerprint,
            SourceTimeZoneVersion = sourceData.TimeZoneVersion,
            SourceExperienceVersion = sourceData.ExperienceVersion,
            SourceAnswerVersion = sourceData.AnswerVersion,
            SourceMuscleGroupsByExerciseId = sourceData.MuscleGroupsByExerciseId?.ToDictionary(
                pair => pair.Key, pair => pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray())
        };
    }
}

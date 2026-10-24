using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Maps persisted profile and proposal evidence into the Core weekly schedule input.</summary>
internal static class MixedActivityScheduleMapperV020
{
    private const int NoCompletedExposures = 0;
    private const int RequiredHeavyExerciseExposures =
        ActivityPrescriptionRule.StrengthHighIntensityRequiredExerciseExposures;

    public static MixedActivityWeekScheduleContext ToContext(
        StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewSourceV020 source,
        StrengthPlanPreviewResult strengthPreview)
    {
        var selectedActivities = source.SelectedActivityTypes?
            .Select(activity => (WorkoutActivityType)activity).ToArray();
        var evidenceInputs = request.ActivityEligibilityEvidence ?? [];
        var histories = BuildActivityHistories(request, evidenceInputs);
        var eligibilityEvidence = evidenceInputs
            .Where(evidence => evidence is not null && Enum.IsDefined(evidence.Activity))
            .Select(evidence => ToPrescriptionContext(WorkoutActivityContractMapper.ToCore(evidence.Activity), request, source, strengthPreview,
                evidence, histories))
            .ToList();
        if (selectedActivities is not null)
            foreach (var activity in selectedActivities.Distinct())
                if (!evidenceInputs.Any(evidence => evidence?.Activity is { } act && WorkoutActivityContractMapper.ToCore(act) == activity))
                    eligibilityEvidence.Add(ToPrescriptionContext(activity, request, source, strengthPreview,
                        null, histories));
        var requestedSessions = source.RequestedSessionsPerWeek?.ToDictionary(
            pair => (WorkoutActivityType)pair.Key, pair => pair.Value);

        return new MixedActivityWeekScheduleContext(
            selectedActivities,
            source.AvailableWeekdays?.ToArray() ?? request.AvailableWeekdays?.ToArray(),
            null,
            source.ActivitySessionDurationsMinutes?.Where(pair =>
                    Enum.IsDefined(typeof(WorkoutActivityType), pair.Key))
                .ToDictionary(pair => (WorkoutActivityType)pair.Key, pair => (decimal)pair.Value),
            eligibilityEvidence,
            requestedSessions,
            request.HiitUnderlyingCardioActivity is { } hiitActivity
                ? WorkoutActivityContractMapper.ToCore(hiitActivity)
                : null,
            WorkoutActivityContractMapper.ToCore(request.Activity));
    }

    private static Dictionary<WorkoutActivityType, ActivityTrainingHistory?> BuildActivityHistories(
        StrengthPlanPreviewRequestV020 request,
        IReadOnlyList<ActivityEligibilityInputV020> evidenceInputs)
    {
        var result = evidenceInputs
            .Where(evidence => evidence is not null && Enum.IsDefined(evidence.Activity))
            .GroupBy(evidence => WorkoutActivityContractMapper.ToCore(evidence.Activity))
            .ToDictionary(group => group.Key,
                group => ToHistory(group.First().ActivityHistory));
        result[WorkoutActivityContractMapper.ToCore(request.Activity)] = ToHistory(request.ActivityHistory);
        if (request.HiitUnderlyingCardioActivity is { } underlyingActivity &&
            request.HiitEligibilityEvidence?.UnderlyingCardioHistory is { } cardioHistory)
            result[WorkoutActivityContractMapper.ToCore(underlyingActivity)] = ToHistory(cardioHistory);
        return result;
    }

    private static ActivityPrescriptionContext ToPrescriptionContext(
        WorkoutActivityType activity,
        StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewSourceV020 source,
        StrengthPlanPreviewResult strengthPreview,
        ActivityEligibilityInputV020? evidenceInput,
        IReadOnlyDictionary<WorkoutActivityType, ActivityTrainingHistory?> histories)
    {
        var isChosenStrength = activity == WorkoutActivityContractMapper.ToCore(request.Activity);
        var activityHistory = histories.GetValueOrDefault(activity);
        var activityExperience = isChosenStrength
            ? strengthPreview.Experience
            : ActivityExperienceRule.Classify(activity.ToString(), activityHistory,
                request.LocalDate).Experience;
        var prescriptionExperience = isChosenStrength
            ? ProfileTrainingExperiencePlanningRule.Resolve(activityExperience,
                source.ProfileTrainingExperience, activityHistory).PrescriptionExperience
            : activityExperience;
        var strengthPreviewIsFeasible = isChosenStrength &&
                                         strengthPreview.Status == StrengthPlanPreviewStatus.Feasible;
        var sourceHasRestrictions = source.HasCurrentSymptoms || source.ActiveInjuryFlags != InjuryFlag.None;
        var hiitEvidence = CreateHiitEvidence(activity, request, source, histories);

        return new ActivityPrescriptionContext(
            activity,
            prescriptionExperience,
            null,
            !isChosenStrength && activity != WorkoutActivityType.Hiit && sourceHasRestrictions,
            source.SelectedActivityTypes?.Contains((int)activity) == true,
            strengthPreviewIsFeasible && activityExperience == ActivityExperience.Established,
            strengthPreviewIsFeasible && activity == WorkoutActivityType.StrengthHighIntensity
                ? RequiredHeavyExerciseExposures
                : NoCompletedExposures,
            strengthPreviewIsFeasible && activity == WorkoutActivityType.StrengthHighIntensity,
            strengthPreviewIsFeasible && activity == WorkoutActivityType.StrengthHighIntensity,
            hiitEvidence,
            evidenceInput?.HasSwimmingFacilityAccess,
            evidenceInput?.CanSwimIndependently,
            evidenceInput?.WantsContinuousSwimming,
            HasRopeClimbingCompetence: evidenceInput?.HasRopeClimbingCompetence,
            AppropriateClimbingInstructionAvailable: evidenceInput?.AppropriateClimbingInstructionAvailable,
            IsDemandingYogaContent: evidenceInput?.IsDemandingYogaContent);
    }

    private static HiitEligibilityEvidence? CreateHiitEvidence(
        WorkoutActivityType activity,
        StrengthPlanPreviewRequestV020 request,
        StrengthPlanPreviewSourceV020 source,
        IReadOnlyDictionary<WorkoutActivityType, ActivityTrainingHistory?> histories)
    {
        if (activity != WorkoutActivityType.Hiit)
            return null;

        var underlying = request.HiitUnderlyingCardioActivity is { } underlyingDto
            ? WorkoutActivityContractMapper.ToCore(underlyingDto)
            : (WorkoutActivityType?)null;
        var hasExplicitUnderlyingActivityChoice = underlying is not null;
        var underlyingExperience = underlying is { } selectedUnderlyingActivity
            ? ActivityExperienceRule.Classify(selectedUnderlyingActivity.ToString(),
                histories.GetValueOrDefault(selectedUnderlyingActivity), request.LocalDate).Experience
            : ActivityExperience.Unknown;
        var hiitInput = request.HiitEligibilityEvidence;
        HiitAerobicWeekEvidence[] weeks = [];
        if (hiitInput?.PreviousFourWeeksAerobicEvidence is { } inputWeeks &&
            inputWeeks.All(week => week is not null))
            weeks = inputWeeks.Select(week => new HiitAerobicWeekEvidence(week.WeekOffsetFromCurrent,
                week.QualifyingManageableAerobicSessionCount)).ToArray();
        var hasTrustedRestriction = source.HasCurrentSymptoms || source.ActiveInjuryFlags != InjuryFlag.None;

        return new HiitEligibilityEvidence(
            hasExplicitUnderlyingActivityChoice,
            underlying,
            underlyingExperience,
            weeks,
            hasTrustedRestriction,
            hiitInput?.ExistingClearanceScreeningSatisfied == true);
    }

    private static ActivityTrainingHistory? ToHistory(ActivityTrainingHistoryInputV020? history) =>
        history is null
            ? null
            : new ActivityTrainingHistory(history.ActivityId, history.ConsistentParticipationWeeks,
                history.AverageSessionsPerWeekInRecentFourWeeks, history.LastCompletedSessionDate,
                history.IsSelfReported);
}

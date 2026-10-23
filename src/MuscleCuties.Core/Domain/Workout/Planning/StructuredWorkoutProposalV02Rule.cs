using System.Security.Cryptography;
using System.Text;
using System.Globalization;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum StructuredProposalStatusV02
{
    Proposed,
    InvalidInput,
    NeedsProgrammingReview,
    NoEligibleTemplate,
    NeedsTimingData,
    Limited,
    NeedsDemandReview,
    Blocked
}

public enum StructuredProposalReasonV02
{
    InvalidSchedule,
    MissingPolicy,
    UnapprovedPolicy,
    NoEligibleTemplate,
    NoEligibleExercise,
    CurrentSymptomsNeedReviewedPathway,
    ExplicitRestrictionConflict,
    MissingTimingData,
    DailyTimeExceeded,
    DailyWindowConflict,
    DemandOverlapNeedsReview,
    ConflictingOutsideActivity,
    UnreviewedDemandMetadata
}

public sealed record ReviewedSessionPatternV02(
    DayOfWeek Day,
    SessionPurposeV02 Purpose,
    ActivityModalityV02 Modality,
    IntendedIntensityV02 Intensity,
    decimal? WarmupMinutes,
    decimal? CooldownMinutes,
    IReadOnlyList<SessionBlockV02> Blocks,
    IReadOnlySet<ActivityDemandTag> DemandTags,
    bool DemandTagsReviewed,
    string RuleId,
    SessionTrainingStyleV02 TrainingStyle = SessionTrainingStyleV02.Unspecified);

public sealed record ReviewedWeekTemplateV02(
    string TemplateId,
    UserGoal Goal,
    TrainingExperienceLevel Experience,
    IReadOnlyList<ReviewedSessionPatternV02> Sessions);

/// <summary>Must be loaded from trusted server configuration, never submitted as client authority.</summary>
public sealed record ReviewedWorkoutPolicyV02(
    string Version,
    string RulesetVersion,
    DateTimeOffset EffectiveFromUtc,
    DateTimeOffset? ExpiresAtUtc,
    PlanningReviewApproval Approval,
    IReadOnlyList<ReviewedWeekTemplateV02> Templates,
    IReadOnlyList<ReviewedExerciseCandidate>? ExerciseCatalog = null);

public sealed record StructuredPlanInputV02(
    Guid PlanId,
    Guid ExpectedRevisionId,
    DateOnly WeekStart,
    UserGoal Goal,
    TrainingExperienceLevel Experience,
    IReadOnlyList<DailyActivityAvailability> Availability,
    IReadOnlyList<PlannedDailyActivity> OutsideActivities,
    IReadOnlySet<ActivityModalityV02> ExcludedModalities,
    IReadOnlySet<ActivityDemandTag> BlockedDemandTags,
    bool HasCurrentPainOrConcerningSymptoms,
    DateTimeOffset NowUtc,
    ExerciseEligibilityContext? ExerciseContext = null);

public sealed record StructuredPlanProposalV02(
    StructuredProposalStatusV02 Status,
    IReadOnlyList<StructuredProposalReasonV02> Reasons,
    WeeklyPlanV02? Plan,
    string? TemplateId,
    IReadOnlyList<string> TriggeredRuleIds,
    IReadOnlyList<DayDurationV02Result> DayChecks);

/// <summary>
/// Selects reviewed templates deterministically and validates every day before returning a proposal.
/// It never accepts a proposal or changes completed work.
/// </summary>
public static class StructuredWorkoutProposalV02Rule
{
    private const int DaysInWeek = 7;
    private const int StableGuidBytes = 16;

    public static StructuredPlanProposalV02 Generate(StructuredPlanInputV02? input,
        ReviewedWorkoutPolicyV02? policy)
    {
        if (!ValidInput(input))
            return Failure(StructuredProposalStatusV02.InvalidInput,
                StructuredProposalReasonV02.InvalidSchedule);
        if (policy is null)
            return Failure(StructuredProposalStatusV02.NeedsProgrammingReview,
                StructuredProposalReasonV02.MissingPolicy);
        if (!Approved(policy, input!.NowUtc))
            return Failure(StructuredProposalStatusV02.NeedsProgrammingReview,
                StructuredProposalReasonV02.UnapprovedPolicy);
        if (input.HasCurrentPainOrConcerningSymptoms)
            return Failure(StructuredProposalStatusV02.Blocked,
                StructuredProposalReasonV02.CurrentSymptomsNeedReviewedPathway);

        var templates = policy.Templates
            .Where(template => template is not null && template.Goal == input.Goal &&
                               template.Experience == input.Experience && ValidTemplate(template))
            .OrderBy(template => template.TemplateId, StringComparer.Ordinal)
            .ToArray();
        if (templates.Length == 0)
            return Failure(StructuredProposalStatusV02.NoEligibleTemplate,
                StructuredProposalReasonV02.NoEligibleTemplate);

        var sawRestriction = false;
        var sawTiming = false;
        var sawLimited = false;
        var sawDemand = false;
        var sawWindow = false;
        var sawExercise = false;
        StructuredPlanProposalV02? windowFailure = null;
        StructuredPlanProposalV02? limitedFailure = null;
        StructuredPlanProposalV02? timingFailure = null;
        StructuredPlanProposalV02? demandFailure = null;

        foreach (var template in templates)
        {
            if (template.Sessions.Any(pattern => input.ExcludedModalities.Contains(pattern.Modality) ||
                                                 pattern.DemandTags.Overlaps(input.BlockedDemandTags)))
            {
                sawRestriction = true;
                continue;
            }
            if (!EligibleExercises(template, policy, input.ExerciseContext))
            {
                sawExercise = true;
                continue;
            }

            var (days, placementFailed) = BuildDays(input, template, policy.Version);

            var checks = days.Select(StructuredWorkoutDurationV02Rule.EvaluateDay).ToArray();
            if (checks.Any(check => check.Capacity.Reasons.Contains(DailyCapacityReason.InvalidActivity) ||
                                    check.Capacity.Reasons.Contains(DailyCapacityReason.InvalidAvailability)))
                return Failure(StructuredProposalStatusV02.InvalidInput,
                    StructuredProposalReasonV02.InvalidSchedule, checks, template.TemplateId);
            if (checks.Any(check => check.Sessions.Any(session => !session.Estimate.IsAvailable ||
                                                        session.Estimate.Minutes is <= 0) ||
                                    check.Capacity.Reasons.Contains(DailyCapacityReason.MissingDuration)))
            {
                sawTiming = true;
                timingFailure ??= Failure(StructuredProposalStatusV02.NeedsTimingData,
                    StructuredProposalReasonV02.MissingTimingData, checks, template.TemplateId);
                continue;
            }
            if (placementFailed || checks.Any(check => check.Capacity.Status == DailyCapacityStatus.Blocked))
            {
                sawWindow = true;
                windowFailure ??= Failure(StructuredProposalStatusV02.Blocked,
                    StructuredProposalReasonV02.DailyWindowConflict, checks, template.TemplateId);
                continue;
            }
            if (checks.Any(check => check.Capacity.Status == DailyCapacityStatus.Limited))
            {
                sawLimited = true;
                limitedFailure ??= Failure(StructuredProposalStatusV02.Limited,
                    StructuredProposalReasonV02.DailyTimeExceeded, checks, template.TemplateId);
                continue;
            }
            if (checks.Any(check => check.Capacity.Status == DailyCapacityStatus.NeedsReview))
            {
                sawDemand = true;
                var reason = checks.SelectMany(check => check.Capacity.Reasons)
                    .Contains(DailyCapacityReason.ConflictingDuplicate)
                    ? StructuredProposalReasonV02.ConflictingOutsideActivity
                    : checks.SelectMany(check => check.Capacity.Reasons)
                        .Contains(DailyCapacityReason.UnreviewedDemand)
                        ? StructuredProposalReasonV02.UnreviewedDemandMetadata
                        : StructuredProposalReasonV02.DemandOverlapNeedsReview;
                demandFailure ??= Failure(StructuredProposalStatusV02.NeedsDemandReview,
                    reason, checks, template.TemplateId);
                continue;
            }

            var plan = new WeeklyPlanV02(input.PlanId,
                StableProposalId(input, policy.Version, template.TemplateId),
                input.ExpectedRevisionId,
                policy.RulesetVersion, policy.Version, days,
                (policy.ExerciseCatalog ?? [])
                    .Where(candidate => candidate is not null)
                    .Select(candidate => candidate.CatalogVersion)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(version => version, StringComparer.Ordinal)
                    .ToArray());
            return new StructuredPlanProposalV02(StructuredProposalStatusV02.Proposed, [], plan,
                template.TemplateId,
                template.Sessions.Select(pattern => pattern.RuleId).Distinct(StringComparer.Ordinal).ToArray(),
                checks);
        }

        if (sawRestriction)
            return Failure(StructuredProposalStatusV02.Blocked,
                StructuredProposalReasonV02.ExplicitRestrictionConflict);
        if (sawExercise)
            return Failure(StructuredProposalStatusV02.NoEligibleTemplate,
                StructuredProposalReasonV02.NoEligibleExercise);
        if (sawWindow)
            return windowFailure!;
        if (sawLimited)
            return limitedFailure!;
        if (sawTiming)
            return timingFailure!;
        if (sawDemand)
            return demandFailure!;
        return Failure(StructuredProposalStatusV02.NoEligibleTemplate,
            StructuredProposalReasonV02.NoEligibleTemplate);
    }

    private static bool ValidInput(StructuredPlanInputV02? input)
    {
        if (input is null || input.PlanId == Guid.Empty || input.NowUtc.Offset != TimeSpan.Zero ||
            input.WeekStart.DayOfWeek != DayOfWeek.Monday ||
            !Enum.IsDefined(input.Goal) || !Enum.IsDefined(input.Experience) ||
            input.Experience == TrainingExperienceLevel.Unknown ||
            input.Availability is null || input.OutsideActivities is null ||
            input.ExcludedModalities is null || input.BlockedDemandTags is null ||
            input.Availability.Count != DaysInWeek ||
            input.Availability.Any(day => day is null || day.TotalMinutes < 0 ||
                day.Windows is null || day.Windows.Any(window => window is null || window.Start >= window.End)) ||
            input.ExerciseContext is { } exerciseContext &&
            (exerciseContext.AvailableEquipment is null ||
             exerciseContext.ExcludedExerciseIds is null ||
             exerciseContext.BlockedMovementPatterns is null ||
             exerciseContext.ExplicitlyBlockedDemandTags is null) ||
            input.ExcludedModalities.Any(modality => !Enum.IsDefined(modality)) ||
            input.BlockedDemandTags.Any(tag => !Enum.IsDefined(tag)))
            return false;

        var dates = input.Availability.Select(day => day.Date).OrderBy(date => date).ToArray();
        if (input.WeekStart > DateOnly.MaxValue.AddDays(-(DaysInWeek - 1)) ||
            dates.Where((date, index) => date != input.WeekStart.AddDays(index)).Any())
            return false;
        return input.OutsideActivities.All(activity => activity is not null &&
            dates.Contains(activity.Date));
    }

    private static bool Approved(ReviewedWorkoutPolicyV02 policy, DateTimeOffset nowUtc) =>
        !string.IsNullOrWhiteSpace(policy.Version) &&
        !string.IsNullOrWhiteSpace(policy.RulesetVersion) &&
        policy.EffectiveFromUtc.Offset == TimeSpan.Zero &&
        policy.EffectiveFromUtc <= nowUtc &&
        (!policy.ExpiresAtUtc.HasValue ||
         policy.ExpiresAtUtc.Value.Offset == TimeSpan.Zero && policy.ExpiresAtUtc > nowUtc) &&
        policy.Approval is { } approval &&
        approval.RulesetVersion == policy.RulesetVersion &&
        approval.ReviewedTrainingPolicyVersion == policy.Version &&
        approval.ApprovedAtUtc.Offset == TimeSpan.Zero &&
        approval.ApprovedAtUtc <= nowUtc &&
        !string.IsNullOrWhiteSpace(approval.ApprovalReference) &&
        approval.CoveredSections?.Contains(PlanningReviewSection.TrainingPrescriptions) == true &&
        policy.Templates is { Count: > 0 };

    private static bool ValidTemplate(ReviewedWeekTemplateV02 template) =>
        !string.IsNullOrWhiteSpace(template.TemplateId) &&
        template.Sessions is { Count: > 0 } &&
        template.Sessions.All(pattern => pattern is not null &&
            Enum.IsDefined(pattern.Day) && Enum.IsDefined(pattern.Purpose) &&
            Enum.IsDefined(pattern.Modality) && Enum.IsDefined(pattern.Intensity) &&
            pattern.Blocks is { Count: > 0 } &&
            pattern.Blocks.All(block => block is not null && Enum.IsDefined(block.Kind) &&
                (block.Kind is not (SessionBlockKindV02.Timed or SessionBlockKindV02.Intervals or
                    SessionBlockKindV02.DistanceSets) ||
                 !string.IsNullOrWhiteSpace(block.ActivityCode)) &&
                (block.Kind != SessionBlockKindV02.MovementSequence ||
                 block.Movements.Count > 0 && block.Movements.All(movement =>
                     movement is not null && !string.IsNullOrWhiteSpace(movement.MovementCode)))) &&
            pattern.DemandTags is not null &&
            pattern.DemandTagsReviewed && !string.IsNullOrWhiteSpace(pattern.RuleId) &&
            (pattern.Purpose != SessionPurposeV02.Strength ||
             pattern.Blocks.Any(block => block is not null && block.Exercises.Count > 0)));

    private static (PlanDayV02[] Days, bool PlacementFailed) BuildDays(
        StructuredPlanInputV02 input, ReviewedWeekTemplateV02 template, string policyVersion)
    {
        var days = new List<PlanDayV02>(DaysInWeek);
        var placementFailed = false;
        foreach (var availability in input.Availability.OrderBy(day => day.Date))
        {
            var outside = input.OutsideActivities
                .Where(activity => activity.Date == availability.Date).ToArray();
            var sessions = template.Sessions
                .Where(pattern => pattern.Day == availability.Date.DayOfWeek)
                .Select((pattern, index) => new PlannedSessionV02(
                    StableId(input.PlanId, input.WeekStart, $"{policyVersion}:{template.TemplateId}",
                        pattern.RuleId, availability.Date, index),
                    $"{template.TemplateId}:{pattern.RuleId}:{availability.Date:yyyy-MM-dd}:{index}",
                    availability.Date, pattern.Purpose, pattern.Modality, pattern.Intensity,
                    pattern.WarmupMinutes, pattern.CooldownMinutes, pattern.Blocks,
                    pattern.DemandTags, pattern.DemandTagsReviewed,
                    trainingStyle: pattern.TrainingStyle))
                .ToArray();

            if (availability.Windows.Count > 0)
            {
                var occupied = outside.Where(activity => activity.Start.HasValue && activity.End.HasValue &&
                                                        activity.Start < activity.End)
                    .Select(activity => (Start: activity.Start!.Value, End: activity.End!.Value))
                    .ToList();
                for (var index = 0; index < sessions.Length; index++)
                {
                    var estimate = StructuredWorkoutDurationV02Rule.EvaluateSession(sessions[index]);
                    if (!estimate.IsAvailable || estimate.Minutes is not > 0)
                        continue;
                    if (!TryPlace(availability.Windows, occupied, estimate.Minutes.Value,
                            out var start, out var end))
                    {
                        placementFailed = true;
                        continue;
                    }

                    sessions[index] = sessions[index].Schedule(start, end);
                    occupied.Add((start, end));
                }
            }

            days.Add(new PlanDayV02(availability.Date, availability, sessions, outside));
        }

        return (days.ToArray(), placementFailed);
    }

    private static bool TryPlace(IReadOnlyList<ActivityWindow> windows,
        IReadOnlyList<(TimeOnly Start, TimeOnly End)> occupied, decimal minutes,
        out TimeOnly start, out TimeOnly end)
    {
        foreach (var window in windows.OrderBy(window => window.Start))
        {
            if (minutes > (decimal)(window.End - window.Start).TotalMinutes)
                continue;
            var cursor = window.Start;
            while (cursor < window.End)
            {
                var finish = cursor.AddMinutes((double)minutes);
                if (finish <= cursor || finish > window.End)
                    break;
                var conflict = occupied.Where(interval => cursor < interval.End && interval.Start < finish)
                    .OrderBy(interval => interval.Start).FirstOrDefault();
                if (conflict == default)
                {
                    start = cursor;
                    end = finish;
                    return true;
                }

                cursor = conflict.End;
            }
        }

        start = default;
        end = default;
        return false;
    }

    private static bool EligibleExercises(ReviewedWeekTemplateV02 template,
        ReviewedWorkoutPolicyV02 policy, ExerciseEligibilityContext? context)
    {
        var prescriptions = template.Sessions.SelectMany(pattern => pattern.Blocks)
            .SelectMany(block => block.Exercises.Select(exercise => (exercise, block.Kind)))
            .ToArray();
        if (prescriptions.Length == 0)
            return true;
        if (context is null || policy.ExerciseCatalog is null)
            return false;
        var eligible = ExerciseEligibilityV02Rule.Filter(policy.ExerciseCatalog, context)
            .EligibleCandidates.GroupBy(candidate => candidate.ExerciseId)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        return prescriptions.All(item =>
            item.exercise is not null && eligible.TryGetValue(item.exercise.ExerciseId, out var candidate) &&
            candidate.SupportedTimingMethods!.Contains(item.Kind switch
            {
                SessionBlockKindV02.StraightSets => ExerciseTimingMethod.StraightSets,
                SessionBlockKindV02.Superset or SessionBlockKindV02.Circuit => ExerciseTimingMethod.Rounds,
                _ => ExerciseTimingMethod.Timed
            }));
    }

    private static Guid StableId(Guid planId, DateOnly weekStart, string templateId,
        string ruleId, DateOnly date, int index)
    {
        var source = $"{planId:N}|{weekStart:yyyy-MM-dd}|{templateId}|{ruleId}|{date:yyyy-MM-dd}|{index}";
        return HashId(source);
    }

    private static Guid StableProposalId(StructuredPlanInputV02 input, string policyVersion,
        string templateId)
    {
        var source = new StringBuilder();
        Append(source, input.PlanId.ToString("N"));
        Append(source, input.ExpectedRevisionId.ToString("N"));
        Append(source, input.WeekStart.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        Append(source, policyVersion);
        Append(source, templateId);
        Append(source, input.Goal.ToString());
        Append(source, input.Experience.ToString());
        Append(source, input.HasCurrentPainOrConcerningSymptoms.ToString());
        foreach (var day in input.Availability.OrderBy(day => day.Date))
        {
            Append(source, day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Append(source, day.TotalMinutes.ToString(CultureInfo.InvariantCulture));
            foreach (var window in day.Windows.OrderBy(window => window.Start))
            {
                Append(source, window.Start.Ticks.ToString(CultureInfo.InvariantCulture));
                Append(source, window.End.Ticks.ToString(CultureInfo.InvariantCulture));
            }
        }
        foreach (var outside in input.OutsideActivities.OrderBy(activity => activity.Date)
                     .ThenBy(activity => activity.CanonicalActivityId, StringComparer.Ordinal))
        {
            Append(source, outside.CanonicalActivityId);
            Append(source, outside.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
            Append(source, outside.PlannedMinutes?.ToString(CultureInfo.InvariantCulture) ?? "unknown");
            Append(source, outside.IsCompleted.ToString());
            Append(source, outside.IsOptional.ToString());
            Append(source, outside.DemandTagsReviewed.ToString());
            Append(source, outside.Start?.Ticks.ToString(CultureInfo.InvariantCulture) ?? "unscheduled");
            Append(source, outside.End?.Ticks.ToString(CultureInfo.InvariantCulture) ?? "unscheduled");
            foreach (var tag in (outside.DemandTags ?? new HashSet<ActivityDemandTag>()).OrderBy(tag => tag))
                Append(source, tag.ToString());
        }
        foreach (var modality in input.ExcludedModalities.OrderBy(modality => modality))
            Append(source, modality.ToString());
        foreach (var tag in input.BlockedDemandTags.OrderBy(tag => tag))
            Append(source, tag.ToString());
        if (input.ExerciseContext is { } exercise)
        {
            foreach (var equipment in exercise.AvailableEquipment.OrderBy(value => value))
                Append(source, equipment.ToString());
            foreach (var id in exercise.ExcludedExerciseIds.OrderBy(value => value))
                Append(source, id.ToString(CultureInfo.InvariantCulture));
            foreach (var pattern in exercise.BlockedMovementPatterns.OrderBy(value => value))
                Append(source, pattern.ToString());
            foreach (var tag in exercise.ExplicitlyBlockedDemandTags.OrderBy(value => value))
                Append(source, tag.ToString());
            Append(source, exercise.HasBodyZoneLimitation.ToString());
        }

        return HashId(source.ToString());
    }

    private static void Append(StringBuilder builder, string value) =>
        builder.Append(value.Length).Append(':').Append(value);

    private static Guid HashId(string source)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(source));
        return new Guid(hash.AsSpan(0, StableGuidBytes));
    }

    private static StructuredPlanProposalV02 Failure(StructuredProposalStatusV02 status,
        StructuredProposalReasonV02 reason, IReadOnlyList<DayDurationV02Result>? checks = null,
        string? templateId = null) => new(status, [reason], null, templateId, [], checks ?? []);
}

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum ActivityDemandTag
{
    Pulling,
    Grip,
    LowerBody,
    UpperBody,
    Impact,
    Conditioning,
    Mobility
}

public enum DailyCapacityStatus
{
    Feasible,
    Limited,
    NeedsReview,
    Blocked
}

public enum DailyCapacityReason
{
    InvalidAvailability,
    InvalidActivity,
    ConflictingDuplicate,
    MissingDuration,
    ExceedsAvailableMinutes,
    OutsideAvailableWindow,
    DurationExceedsScheduledWindow,
    SimultaneousActivities,
    UnreviewedDemand,
    OutsideActivityDemandOverlap
}

public sealed record ActivityWindow(TimeOnly Start, TimeOnly End);

public sealed record DailyActivityAvailability(
    DateOnly Date,
    decimal TotalMinutes,
    IReadOnlyList<ActivityWindow> Windows);

/// <summary>CanonicalActivityId links imported and manual representations of the same work.</summary>
public sealed record PlannedDailyActivity(
    string CanonicalActivityId,
    DateOnly Date,
    decimal? PlannedMinutes,
    IReadOnlySet<ActivityDemandTag>? DemandTags,
    bool DemandTagsReviewed,
    bool IsOutsideActivity,
    bool IsCompleted,
    bool IsOptional,
    TimeOnly? Start,
    TimeOnly? End);

public sealed record DailyCapacityResult(
    DailyCapacityStatus Status,
    decimal? PlannedMinutes,
    IReadOnlyList<DailyCapacityReason> Reasons,
    IReadOnlyList<PlannedDailyActivity> DistinctActivities);

/// <summary>Checks capacity and metadata without reducing unlike activities to a fatigue score.</summary>
public static class DailyActivityCapacityRule
{
    public static DailyCapacityResult Evaluate(
        DailyActivityAvailability? availability,
        IReadOnlyList<PlannedDailyActivity>? activities)
    {
        if (availability is null || availability.TotalMinutes < 0 || availability.Windows is null ||
            availability.Windows.Any(window => window is null || window.Start >= window.End))
            return Blocked(DailyCapacityReason.InvalidAvailability);
        if (activities is null || activities.Any(activity => activity is null ||
                                                       string.IsNullOrWhiteSpace(activity.CanonicalActivityId) ||
                                                       activity.Date != availability.Date ||
                                                       activity.DemandTags is null ||
                                                       activity.Start.HasValue != activity.End.HasValue ||
                                                       activity.Start.HasValue && activity.Start >= activity.End ||
                                                       activity.PlannedMinutes is < 0))
            return Blocked(DailyCapacityReason.InvalidActivity);

        var distinct = new List<PlannedDailyActivity>();
        foreach (var group in activities.GroupBy(activity => activity.CanonicalActivityId, StringComparer.Ordinal))
        {
            var first = group.First();
            if (group.Any(other => !Equivalent(first, other)))
                return new DailyCapacityResult(DailyCapacityStatus.NeedsReview, null,
                    [DailyCapacityReason.ConflictingDuplicate], []);
            distinct.Add(first);
        }

        var reasons = new HashSet<DailyCapacityReason>();
        if (distinct.Any(activity => activity.PlannedMinutes is null))
            reasons.Add(DailyCapacityReason.MissingDuration);
        if (distinct.Any(activity => !activity.DemandTagsReviewed))
            reasons.Add(DailyCapacityReason.UnreviewedDemand);

        foreach (var activity in distinct.Where(activity => activity.Start.HasValue))
        {
            if (!availability.Windows.Any(window => activity.Start >= window.Start && activity.End <= window.End))
                reasons.Add(DailyCapacityReason.OutsideAvailableWindow);
            if (activity.PlannedMinutes > (decimal)(activity.End!.Value - activity.Start!.Value).TotalMinutes)
                reasons.Add(DailyCapacityReason.DurationExceedsScheduledWindow);
        }

        for (var index = 0; index < distinct.Count; index++)
        {
            for (var otherIndex = index + 1; otherIndex < distinct.Count; otherIndex++)
            {
                var first = distinct[index];
                var second = distinct[otherIndex];
                if (first.Start.HasValue && second.Start.HasValue &&
                    first.Start < second.End && second.Start < first.End)
                    reasons.Add(DailyCapacityReason.SimultaneousActivities);
                if (first.IsOutsideActivity != second.IsOutsideActivity &&
                    first.DemandTags!.Overlaps(second.DemandTags!))
                    reasons.Add(DailyCapacityReason.OutsideActivityDemandOverlap);
            }
        }

        decimal? plannedMinutes = null;
        if (!reasons.Contains(DailyCapacityReason.MissingDuration))
        {
            try
            {
                plannedMinutes = distinct.Sum(activity => activity.PlannedMinutes!.Value);
                if (plannedMinutes > availability.TotalMinutes)
                    reasons.Add(DailyCapacityReason.ExceedsAvailableMinutes);
            }
            catch (OverflowException)
            {
                return Blocked(DailyCapacityReason.InvalidActivity);
            }
        }

        var status = reasons.Contains(DailyCapacityReason.SimultaneousActivities) ||
                     reasons.Contains(DailyCapacityReason.OutsideAvailableWindow) ||
                     reasons.Contains(DailyCapacityReason.DurationExceedsScheduledWindow)
            ? DailyCapacityStatus.Blocked
            : reasons.Contains(DailyCapacityReason.ExceedsAvailableMinutes)
                ? DailyCapacityStatus.Limited
                : reasons.Count > 0
                    ? DailyCapacityStatus.NeedsReview
                    : DailyCapacityStatus.Feasible;
        return new DailyCapacityResult(status, plannedMinutes,
            reasons.OrderBy(reason => reason).ToArray(), distinct);
    }

    private static bool Equivalent(PlannedDailyActivity first, PlannedDailyActivity second) =>
        first.Date == second.Date && first.PlannedMinutes == second.PlannedMinutes &&
        first.DemandTagsReviewed == second.DemandTagsReviewed &&
        first.IsOutsideActivity == second.IsOutsideActivity &&
        first.IsCompleted == second.IsCompleted && first.IsOptional == second.IsOptional &&
        first.Start == second.Start && first.End == second.End &&
        first.DemandTags!.SetEquals(second.DemandTags!);

    private static DailyCapacityResult Blocked(DailyCapacityReason reason) =>
        new(DailyCapacityStatus.Blocked, null, [reason], []);
}

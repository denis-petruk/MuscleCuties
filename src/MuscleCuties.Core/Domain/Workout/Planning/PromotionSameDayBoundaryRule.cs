using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>A session already on the day a promotion wants to use.</summary>
public sealed record PromotionExistingWorkout(WorkoutActivityType? Activity, int? DayOrder,
    bool Completed, bool InProgress, bool BelongsToCurrentPlan);
/// <summary>The session a promotion would add to that day.</summary>
public sealed record PromotionProposedWorkout(WorkoutActivityType? Activity, int? DayOrder);

/// <summary>Finds a proven same-day successor; null means defer to a later date.</summary>
public static class PromotionSameDayBoundaryRule
{
    /// <summary>Order of the workout that follows the replaced one on the same day.</summary>
    public static int? FindSuccessorOrder(IReadOnlyCollection<PromotionExistingWorkout> existing,
        IReadOnlyCollection<PromotionProposedWorkout> proposed)
    {
        if (existing.Count == 0 || proposed.Count != existing.Count ||
            existing.Any(item => !item.BelongsToCurrentPlan || item.Activity is null ||
                !Enum.IsDefined(item.Activity.Value) || item.DayOrder is null) ||
            proposed.Any(item => item.Activity is null || !Enum.IsDefined(item.Activity.Value) || item.DayOrder is null))
            return null;

        var expected = Enumerable.Range(WorkoutSessionOrderingRule.FirstSessionOrder, existing.Count).ToArray();
        if (!existing.Select(item => item.DayOrder!.Value).Order().SequenceEqual(expected) ||
            !proposed.Select(item => item.DayOrder!.Value).Order().SequenceEqual(expected))
            return null;

        var proposedByOrder = proposed.ToDictionary(item => item.DayOrder!.Value);
        if (existing.Any(item => proposedByOrder[item.DayOrder!.Value].Activity != item.Activity))
            return null;

        var referenced = existing.Where(item => item.Completed || item.InProgress).ToArray();
        if (referenced.Length == 0) return null;
        var lastOrder = referenced.Max(item => item.DayOrder!.Value);
        return existing.Where(item => item.DayOrder > lastOrder && !item.Completed && !item.InProgress)
            .Select(item => item.DayOrder).OrderBy(order => order).FirstOrDefault();
    }
}

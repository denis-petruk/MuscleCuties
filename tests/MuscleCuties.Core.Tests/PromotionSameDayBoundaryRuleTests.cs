using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class PromotionSameDayBoundaryRuleTests
{
    private const int FirstOrder = WorkoutSessionOrderingRule.FirstSessionOrder;
    private const int SecondOrder = 2;
    private static readonly PromotionProposedWorkout[] Proposed =
    [new(WorkoutActivityType.GeneralStrength, FirstOrder), new(WorkoutActivityType.Yoga, SecondOrder)];

    [Fact]
    public void UsesExactSuccessorAfterCompletedOrActiveWorkout()
    {
        Assert.Equal(SecondOrder, PromotionSameDayBoundaryRule.FindSuccessorOrder(
            [new(WorkoutActivityType.GeneralStrength, FirstOrder, true, false, true),
             new(WorkoutActivityType.Yoga, SecondOrder, false, false, true)], Proposed));
    }

    [Fact]
    public void ActiveLaterSessionTakesPrecedenceOverEarlierCompletion()
    {
        Assert.Null(PromotionSameDayBoundaryRule.FindSuccessorOrder(
            [new(WorkoutActivityType.GeneralStrength, FirstOrder, true, false, true),
             new(WorkoutActivityType.Yoga, SecondOrder, false, true, true)], Proposed));
    }

    [Fact]
    public void LegacyOrderDifferentPlanOrChangedActivityDefersToday()
    {
        Assert.Null(PromotionSameDayBoundaryRule.FindSuccessorOrder(
            [new(WorkoutActivityType.GeneralStrength, null, true, false, true),
             new(WorkoutActivityType.Yoga, SecondOrder, false, false, true)], Proposed));
        Assert.Null(PromotionSameDayBoundaryRule.FindSuccessorOrder(
            [new(WorkoutActivityType.GeneralStrength, FirstOrder, true, false, false),
             new(WorkoutActivityType.Yoga, SecondOrder, false, false, true)], Proposed));
        Assert.Null(PromotionSameDayBoundaryRule.FindSuccessorOrder(
            [new(WorkoutActivityType.GeneralStrength, FirstOrder, true, false, true),
             new(WorkoutActivityType.Mobility, SecondOrder, false, false, true)], Proposed));
    }
    [Fact]
    public void DuplicateOrdersAreAmbiguous()
    {
        Assert.Null(PromotionSameDayBoundaryRule.FindSuccessorOrder(
            [new(WorkoutActivityType.GeneralStrength, FirstOrder, true, false, true),
             new(WorkoutActivityType.Yoga, FirstOrder, false, false, true)], Proposed));
    }

}

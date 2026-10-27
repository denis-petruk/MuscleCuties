using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class PlanningLifecycleRuleTests
{
    [Fact]
    public void LeaseRequiresMatchingFenceAndExpiresAtDeadline()
    {
        var now = new DateTimeOffset(2026, 10, 26, 12, 0, 0, TimeSpan.Zero);
        var lease = new PlanningLease(Guid.NewGuid(), 1, now + PlanningLifecycleRule.FiveMinuteLeaseDuration);

        Assert.True(PlanningLifecycleRule.IsCurrentLease(lease, lease.LeaseId, lease.Fence, now));
        Assert.False(PlanningLifecycleRule.IsCurrentLease(lease, lease.LeaseId, lease.Fence + 1, now));
        Assert.False(PlanningLifecycleRule.IsCurrentLease(lease, lease.LeaseId, lease.Fence, lease.ExpiresAtUtc));
        Assert.Null(PlanningLifecycleRule.NextFence(long.MaxValue));
    }

    [Fact]
    public void AcceptanceRequiresAllApplicableConfirmations()
    {
        Assert.False(PlanningLifecycleRule.ConfirmationsSatisfied(true, false, true, true));
        Assert.False(PlanningLifecycleRule.ConfirmationsSatisfied(true, true, true, false));
        Assert.True(PlanningLifecycleRule.ConfirmationsSatisfied(true, true, true, true));
        Assert.True(PlanningLifecycleRule.ConfirmationsSatisfied(false, false, false, false));
    }
}

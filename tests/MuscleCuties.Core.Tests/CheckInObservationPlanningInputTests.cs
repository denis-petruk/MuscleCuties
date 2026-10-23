using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class CheckInObservationPlanningInputTests
{
    [Fact]
    public void PlanningInputs_AcceptCompleteValuesInSupportedRanges()
    {
        Assert.True(CheckInObservationRule.ArePlanningInputsValid(4, 1, 5, 3, "America/Toronto"));
    }

    [Theory]
    [InlineData(4, 0, 3, 3, "America/Toronto")]
    [InlineData(4, 3, 6, 3, "America/Toronto")]
    [InlineData(4, 3, 3, -1, "America/Toronto")]
    [InlineData(4, 3, 3, 3, "")]
    [InlineData(4, 3, 3, 3, "Unknown/Zone")]
    public void PlanningInputs_RejectOutOfRangeOrInvalidTimezone(int? energy, int? sleepQuality, int? soreness, int? stress, string? timeZoneId)
    {
        Assert.False(CheckInObservationRule.ArePlanningInputsValid(energy, sleepQuality, soreness, stress, timeZoneId));
    }

    [Fact]
    public void PlanningInputs_RejectMissingRatingsButAllowLegacyCheckInWithoutPlanningInputs()
    {
        Assert.False(CheckInObservationRule.ArePlanningInputsValid(4, 4, null, null, "America/Toronto"));
        Assert.True(CheckInObservationRule.ArePlanningInputsValid(null, null, null, null, null));
    }

    [Fact]
    public void PlanningInputs_RejectPartialCheckInWithoutTimezoneAssociation()
    {
        Assert.False(CheckInObservationRule.ArePlanningInputsValid(4, 4, null, null, null));
    }
}

using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Profile;

namespace MuscleCuties.Core.Tests;

public sealed class ProfileUpdateValidationRuleTests
{
    private const float OneUnitBelowMeasurementMinimum = 1f;

    [Fact]
    public void AcceptsRequiredMeasurementsAtConfiguredBounds()
    {
        Assert.True(ProfileUpdateValidationRule.IsValidRequiredMeasurements(
            (float)ProfileUpdateValidationRule.MinimumHeightCm,
            (float)ProfileUpdateValidationRule.MinimumWeightKg,
            CycleLengthLimits.Minimum));
        Assert.True(ProfileUpdateValidationRule.IsValidRequiredMeasurements(
            (float)ProfileUpdateValidationRule.MaximumHeightCm,
            (float)ProfileUpdateValidationRule.MaximumWeightKg,
            CycleLengthLimits.Maximum));
    }

    [Fact]
    public void RejectsNonFiniteOrOutOfRangeMeasurements()
    {
        Assert.False(ProfileUpdateValidationRule.IsValidRequiredMeasurements(
            float.NaN, (float)ProfileUpdateValidationRule.MinimumWeightKg, CycleLengthLimits.Default));
        Assert.False(ProfileUpdateValidationRule.IsValidRequiredMeasurements(
            (float)ProfileUpdateValidationRule.MinimumHeightCm, float.PositiveInfinity, CycleLengthLimits.Default));
        Assert.False(ProfileUpdateValidationRule.IsValidRequiredMeasurements(
            (float)ProfileUpdateValidationRule.MinimumHeightCm - OneUnitBelowMeasurementMinimum,
            (float)ProfileUpdateValidationRule.MinimumWeightKg,
            CycleLengthLimits.Default));
    }
}

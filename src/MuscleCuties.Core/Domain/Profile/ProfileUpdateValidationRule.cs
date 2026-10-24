using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Onboarding;

namespace MuscleCuties.Core.Domain.Profile;

public sealed record ProfileUpdateObservation(
    DateOnly? DateOfBirth,
    bool SetDateOfBirth,
    decimal? HeightCm,
    decimal? WeightKg,
    int? CycleLength);

public static class ProfileUpdateValidationRule
{
    public const decimal MinimumHeightCm = 50m;
    public const decimal MaximumHeightCm = 280m;
    public const decimal MinimumWeightKg = 20m;
    public const decimal MaximumWeightKg = 400m;

    public static bool IsValidRequiredMeasurements(float heightCm, float weightKg, int cycleLength) =>
        IsValidHeight(heightCm) &&
        IsValidWeight(weightKg) &&
        IsValidCycleLength(cycleLength);

    public static bool IsValid(ProfileUpdateObservation? update, DateOnly today)
    {
        if (update is null)
            return false;

        if (update.SetDateOfBirth && update.DateOfBirth is { } birthDate &&
            BirthDateEligibilityRule.Assess(birthDate.ToDateTime(TimeOnly.MinValue), today.ToDateTime(TimeOnly.MinValue))
                != BirthDateEligibility.Eligible)
            return false;

        if (update.HeightCm is { } height && !IsValidHeight((double)height))
            return false;
        if (update.WeightKg is { } weight && !IsValidWeight((double)weight))
            return false;
        if (update.CycleLength is { } cycleLength && !IsValidCycleLength(cycleLength))
            return false;
        return true;
    }

    private static bool IsValidHeight(double heightCm) =>
        double.IsFinite(heightCm) && heightCm >= (double)MinimumHeightCm &&
        heightCm <= (double)MaximumHeightCm;

    private static bool IsValidWeight(double weightKg) =>
        double.IsFinite(weightKg) && weightKg >= (double)MinimumWeightKg &&
        weightKg <= (double)MaximumWeightKg;

    private static bool IsValidCycleLength(int cycleLength) =>
        cycleLength >= CycleLengthLimits.Minimum && cycleLength <= CycleLengthLimits.Maximum;
}

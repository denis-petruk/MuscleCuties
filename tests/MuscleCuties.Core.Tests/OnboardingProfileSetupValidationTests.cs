using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class OnboardingProfileSetupValidationTests
{
    private const int TestTodayYear = 2026;
    private const int TestTodayMonth = 10;
    private const int TestTodayDay = 24;
    private const int MeasurementMidpointDivisor = 2;
    private const int YearsBelowMinimumAge = BirthDateEligibilityRule.MinimumEligibleAgeYears - 1;
    private static readonly DateTime Today = new(TestTodayYear, TestTodayMonth, TestTodayDay);

    [Fact]
    public void AcceptsCompleteValidProfileSetup()
    {
        var result = OnboardingValidationRule.ValidateProfileSetup(
            ValidProfile(), [(int)WorkoutActivityType.GeneralStrength], Today);

        Assert.True(result.IsValid);
        Assert.Null(result.ErrorMessage);
    }

    [Fact]
    public void RejectsProfileSetupWithoutAnActivity()
    {
        var result = OnboardingValidationRule.ValidateProfileSetup(ValidProfile(), [], Today);

        Assert.False(result.IsValid);
        Assert.Equal("Select at least one valid workout activity.", result.ErrorMessage);
    }

    [Fact]
    public void RejectsProfileSetupWithAnInvalidActivityId()
    {
        var result = OnboardingValidationRule.ValidateProfileSetup(
            ValidProfile(), [int.MaxValue], Today);

        Assert.False(result.IsValid);
        Assert.Equal("Select at least one valid workout activity.", result.ErrorMessage);
    }

    [Fact]
    public void RejectsUnderageProfileBeforeCheckingActivitySelection()
    {
        var underageProfile = ValidProfile() with
        {
            BirthDate = Today.AddYears(-YearsBelowMinimumAge)
        };

        var result = OnboardingValidationRule.ValidateProfileSetup(underageProfile, [], Today);

        Assert.False(result.IsValid);
        Assert.Equal(
            $"Please provide a valid date of birth (must be at least {BirthDateEligibilityRule.MinimumEligibleAgeYears} years old).",
            result.ErrorMessage);
    }

    private static StagedProfileObservation ValidProfile() => new(
        "Morgan",
        Today.AddYears(-BirthDateEligibilityRule.MinimumEligibleAgeYears),
        (OnboardingValidationRule.MinimumHeightCentimeters + OnboardingValidationRule.MaximumHeightCentimeters) /
        MeasurementMidpointDivisor,
        (OnboardingValidationRule.MinimumWeightKilograms + OnboardingValidationRule.MaximumWeightKilograms) /
        MeasurementMidpointDivisor,
        CycleLengthLimits.Default);
}

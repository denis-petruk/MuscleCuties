using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Domain.Onboarding;

namespace MuscleCuties.Core.Domain.Profile;

public sealed record ProfileUpdateObservation(
    DateOnly? DateOfBirth,
    bool SetDateOfBirth,
    decimal? HeightCm,
    decimal? WeightKg,
    int? CycleLength,
    int? WorkoutDaysPerWeek,
    int? SessionDurationMinutes);

public static class ProfileUpdateValidationRule
{
    public const decimal MinimumHeightCm = 50m;
    public const decimal MaximumHeightCm = 280m;
    public const decimal MinimumWeightKg = 20m;
    public const decimal MaximumWeightKg = 400m;
    public const int MinimumWorkoutDaysPerWeek = 1;
    public const int MaximumWorkoutDaysPerWeek = 7;
    public const int MinimumSessionDurationMinutes = 30;
    public const int MaximumSessionDurationMinutes = 90;
    public const int SessionDurationIncrementMinutes = 15;

    public static bool IsValid(ProfileUpdateObservation? update, DateOnly today)
    {
        if (update is null)
            return false;

        if (update.SetDateOfBirth && update.DateOfBirth is { } birthDate &&
            BirthDateEligibilityRule.Assess(birthDate.ToDateTime(TimeOnly.MinValue), today.ToDateTime(TimeOnly.MinValue))
                != BirthDateEligibility.Eligible)
            return false;

        if (update.HeightCm is { } height && height is < MinimumHeightCm or > MaximumHeightCm)
            return false;
        if (update.WeightKg is { } weight && weight is < MinimumWeightKg or > MaximumWeightKg)
            return false;
        if (update.CycleLength is { } cycleLength &&
            cycleLength is < CycleLengthLimits.Minimum or > CycleLengthLimits.Maximum)
            return false;
        if (update.WorkoutDaysPerWeek is { } days &&
            days is < MinimumWorkoutDaysPerWeek or > MaximumWorkoutDaysPerWeek)
            return false;
        return update.SessionDurationMinutes is not { } duration ||
               duration is >= MinimumSessionDurationMinutes and <= MaximumSessionDurationMinutes &&
               duration % SessionDurationIncrementMinutes == 0;
    }
}

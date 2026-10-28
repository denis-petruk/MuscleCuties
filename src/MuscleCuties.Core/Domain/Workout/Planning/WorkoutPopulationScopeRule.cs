using MuscleCuties.Core.Domain.Health;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Product scope for the current general-fitness generator; not a medical assessment.</summary>
public static class WorkoutPopulationScopeRule
{
    public const int MinimumSupportedAgeYears = 18;
    public const int MaximumSupportedAgeYears = 64;
    public const string BirthDateRequired = "WorkoutPopulationBirthDateRequired";
    public const string DateUnavailable = "WorkoutPopulationDateUnavailable";
    public const string AgeUnsupported = "WorkoutPopulationAgeUnsupported";
    public const string PregnancyUnsupported = "WorkoutPopulationPregnancyUnsupported";

    public static string? Limitation(DateOnly? birthDate, DateOnly? accountLocalDate, PregnancyStatus pregnancy)
    {
        if (pregnancy == PregnancyStatus.Yes) return PregnancyUnsupported;
        if (accountLocalDate is null || accountLocalDate == default(DateOnly)) return DateUnavailable;
        if (birthDate is null || birthDate == default(DateOnly) || birthDate > accountLocalDate) return BirthDateRequired;
        var age = accountLocalDate.Value.Year - birthDate.Value.Year;
        if (birthDate.Value > accountLocalDate.Value.AddYears(-age)) age--;
        return age is < MinimumSupportedAgeYears or > MaximumSupportedAgeYears ? AgeUnsupported : null;
    }
}

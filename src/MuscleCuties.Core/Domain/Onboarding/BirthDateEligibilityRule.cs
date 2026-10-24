namespace MuscleCuties.Core.Domain.Onboarding;

public enum BirthDateEligibility
{
    Eligible,
    MissingOrInvalid,
    Underage
}

public static class BirthDateEligibilityRule
{
    public const int MinimumEligibleAgeYears = 18;
    public const int MaximumEligibleAgeYears = 120;

    public static BirthDateEligibility Assess(DateTime birthDate, DateTime today)
    {
        var date = birthDate.Date;
        today = today.Date;
        if (date == default || date > today)
            return BirthDateEligibility.MissingOrInvalid;

        var age = today.Year - date.Year;
        if (date > today.AddYears(-age))
            age--;

        if (age < MinimumEligibleAgeYears)
            return BirthDateEligibility.Underage;

        return age > MaximumEligibleAgeYears ? BirthDateEligibility.MissingOrInvalid : BirthDateEligibility.Eligible;
    }
}

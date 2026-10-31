namespace MuscleCuties.Core.Domain.Onboarding;

public enum BirthDateEligibility
{
    Eligible,
    MissingOrInvalid,
    Underage
}

public static class BirthDateEligibilityRule
{
    private const int MinimumAge = 18;
    private const int MaximumAge = 120;

    public static BirthDateEligibility Assess(DateTime birthDate, DateTime today)
    {
        var date = birthDate.Date;
        today = today.Date;
        if (date == default || date > today)
            return BirthDateEligibility.MissingOrInvalid;

        var age = today.Year - date.Year;
        if (date > today.AddYears(-age))
            age--;

        if (age < MinimumAge)
            return BirthDateEligibility.Underage;

        return age > MaximumAge ? BirthDateEligibility.MissingOrInvalid : BirthDateEligibility.Eligible;
    }
}

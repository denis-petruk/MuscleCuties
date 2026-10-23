namespace MuscleCuties.Core.Domain.Onboarding;

public enum BirthDateEligibility
{
    Eligible,
    MissingOrInvalid,
    Underage
}

public static class BirthDateEligibilityRule
{
    public static BirthDateEligibility Assess(DateTime birthDate, DateTime today)
    {
        var date = birthDate.Date;
        today = today.Date;
        if (date == default || date > today)
            return BirthDateEligibility.MissingOrInvalid;

        var age = today.Year - date.Year;
        if (date > today.AddYears(-age))
            age--;

        if (age < 18)
            return BirthDateEligibility.Underage;

        return age > 120 ? BirthDateEligibility.MissingOrInvalid : BirthDateEligibility.Eligible;
    }
}

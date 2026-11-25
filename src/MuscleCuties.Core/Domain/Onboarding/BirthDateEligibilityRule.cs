namespace MuscleCuties.Core.Domain.Onboarding;

/// <summary>Whether her birth date allows the app to plan for her, with the reason it does not.</summary>
public enum BirthDateEligibility
{
    Eligible,
    MissingOrInvalid,
    Underage
}

/// <summary>Checks a birth date against the supported age range, refusing rather than clamping.</summary>
public static class BirthDateEligibilityRule
{
    public const int MinimumEligibleAgeYears = 18;
    public const int MaximumEligibleAgeYears = 120;

    /// <summary>The eligibility verdict for a date, treating an unreadable one as ineligible.</summary>
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

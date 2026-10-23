namespace MuscleCuties.Core.Domain.Nutrition;

public sealed record AbsorptionEntryObservation(
    double Grams,
    double FatPer100Grams,
    double NutrientPer100Grams);

public sealed record AbsorptionMealObservation(
    DateTime LoggedAt,
    IReadOnlyList<AbsorptionEntryObservation> Entries);

public static class FatSolubleAbsorptionRule
{
    public const double MinimumFatGramsInWindow = 5;
    public static readonly TimeSpan AbsorptionWindow = TimeSpan.FromHours(2);

    public static double SumAbsorbableNutrient(
        IReadOnlyList<AbsorptionMealObservation>? sourceMeals,
        IReadOnlyList<AbsorptionMealObservation>? dailyMeals)
    {
        if (sourceMeals is null || dailyMeals is null)
            return 0;

        var sum = 0d;
        foreach (var meal in sourceMeals)
        {
            if (meal is null || !HasEnoughFatNearMeal(meal, dailyMeals))
                continue;

            foreach (var entry in meal.Entries ?? [])
            {
                if (entry is null || !ValidGrams(entry.Grams) ||
                    !double.IsFinite(entry.NutrientPer100Grams) || entry.NutrientPer100Grams < 0)
                    continue;
                sum += entry.NutrientPer100Grams * entry.Grams / 100d;
                if (!double.IsFinite(sum))
                    return 0;
            }
        }

        return sum;
    }

    public static bool HasEnoughFatNearMeal(
        AbsorptionMealObservation? meal,
        IReadOnlyList<AbsorptionMealObservation>? dailyMeals)
    {
        if (meal is null || meal.LoggedAt == default || dailyMeals is null)
            return false;

        var fat = 0d;
        foreach (var candidate in dailyMeals)
        {
            if (candidate is null || candidate.LoggedAt == default ||
                Math.Abs((candidate.LoggedAt - meal.LoggedAt).TotalMinutes) > AbsorptionWindow.TotalMinutes)
                continue;
            foreach (var entry in candidate.Entries ?? [])
            {
                if (entry is null || !ValidGrams(entry.Grams) ||
                    !double.IsFinite(entry.FatPer100Grams) ||
                    entry.FatPer100Grams is < 0 or > 100)
                    continue;
                fat += entry.FatPer100Grams * entry.Grams / 100d;
                if (fat >= MinimumFatGramsInWindow)
                    return true;
            }
        }

        return false;
    }

    private static bool ValidGrams(double grams)
    {
        return double.IsFinite(grams) && grams is > 0 and <= 10_000;
    }
}

using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>One food logged for a day, with the nutrients for the amount eaten.</summary>
public sealed class DailyFoodEntry
{
    public const double MinimumPositivePortion = 0d;
    public const double MinimumNutritionValue = 0d;
    public const int MinimumBatchItems = 1;
    private const int DstFallbackShiftMinutes = 60;

    /// <summary>Takes the nutrient figures for one logged amount, clamping them at zero.</summary>
    private DailyFoodEntry(double calories, double protein, double carbs, double fat)
    {
        Calories = calories;
        Protein = protein;
        Carbs = carbs;
        Fat = fat;
    }

    /// <summary>Energy the serving contributes.</summary>
    public double Calories { get; }
    /// <summary>Protein grams the serving contributes.</summary>
    public double Protein { get; }
    /// <summary>Carbohydrate grams the serving contributes.</summary>
    public double Carbs { get; }
    /// <summary>Fat grams the serving contributes.</summary>
    public double Fat { get; }

    /// <summary>Builds a diary entry from a meal, a serving and its nutrients.</summary>
    public static DailyFoodEntry Create(MealType mealType, string name, double servingSize,
        string servingUnit, double servingsCount, double calories, double protein,
        double carbs, double fat)
    {
        if (!Enum.IsDefined(mealType) || string.IsNullOrWhiteSpace(name) ||
            string.IsNullOrWhiteSpace(servingUnit) ||
            !double.IsFinite(servingSize) || servingSize <= MinimumPositivePortion ||
            !double.IsFinite(servingsCount) || servingsCount <= MinimumPositivePortion ||
            !ValidNutrient(calories) || !ValidNutrient(protein) ||
            !ValidNutrient(carbs) || !ValidNutrient(fat))
            throw new ArgumentException("Food requires a name, positive portion and finite nutrition.");
        return new DailyFoodEntry(calories, protein, carbs, fat);
    }

    /// <summary>Refuses a batch with an empty list or a negative quantity, before anything is written.</summary>
    public static void ValidateBatch(IReadOnlyList<DailyFoodEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count < MinimumBatchItems) return;
        double calories = MinimumNutritionValue;
        double protein = MinimumNutritionValue;
        double carbs = MinimumNutritionValue;
        double fat = MinimumNutritionValue;
        foreach (var entry in entries)
        {
            calories += entry.Calories;
            protein += entry.Protein;
            carbs += entry.Carbs;
            fat += entry.Fat;
        }
        if (!double.IsFinite(calories) || !double.IsFinite(protein) ||
            !double.IsFinite(carbs) || !double.IsFinite(fat))
            throw new ArgumentException("Food totals exceed supported values.", nameof(entries));
    }

    /// <summary>Moves a source timestamp onto the target day while keeping its local time of day.</summary>
    public static DateTime CopyTimestamp(DateOnly targetDate, DateTime sourceTimestamp) =>
        targetDate.ToDateTime(TimeOnly.FromDateTime(sourceTimestamp));

    /// <summary>Chooses the local time a meal was eaten, falling back to now when none was given.</summary>
    public static DateTime ResolveConsumptionLocalTimestamp(
        DateOnly localDate,
        TimeOnly requestedLocalTime,
        TimeZoneInfo? timeZone = null)
    {
        if (localDate == default)
            throw new ArgumentException("A valid local date is required.", nameof(localDate));

        var effectiveZone = timeZone ?? TimeZoneInfo.Local;
        var unspecifiedLocal = DateTime.SpecifyKind(localDate.ToDateTime(requestedLocalTime), DateTimeKind.Unspecified);
        if (effectiveZone.IsInvalidTime(unspecifiedLocal))
        {
            var shifted = unspecifiedLocal.AddMinutes(DstFallbackShiftMinutes);
            if (DateOnly.FromDateTime(shifted) != localDate)
                shifted = localDate.ToDateTime(TimeOnly.MaxValue);
            return DateTime.SpecifyKind(shifted, DateTimeKind.Local);
        }

        return DateTime.SpecifyKind(unspecifiedLocal, DateTimeKind.Local);
    }

    /// <summary>Orders timeline items by local time, keeping the given order for ties.</summary>
    public static IReadOnlyList<T> SortTimeline<T>(
        IEnumerable<T> entries,
        Func<T, DateTime> timestampSelector,
        Func<T, MealType> mealTypeSelector,
        Func<T, Guid> idSelector)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(timestampSelector);
        ArgumentNullException.ThrowIfNull(mealTypeSelector);
        ArgumentNullException.ThrowIfNull(idSelector);

        return entries
            .OrderBy(timestampSelector)
            .ThenBy(mealTypeSelector)
            .ThenBy(idSelector)
            .ToArray();
    }

    /// <summary>Refuses a copy onto its own date, which would duplicate the day.</summary>
    public static void RequireDifferentDates(DateOnly sourceDate, DateOnly targetDate)
    {
        _ = DiaryCopyDates.Create(sourceDate, targetDate);
    }

    /// <summary>Refuses an unknown meal type rather than filing the food under a default.</summary>
    public static void RequireMealType(MealType mealType)
    {
        if (!Enum.IsDefined(mealType))
            throw new ArgumentOutOfRangeException(nameof(mealType));
    }

    /// <summary>True when a nutrient value is finite and not negative.</summary>
    private static bool ValidNutrient(double value) => double.IsFinite(value) && value >= MinimumNutritionValue;
}

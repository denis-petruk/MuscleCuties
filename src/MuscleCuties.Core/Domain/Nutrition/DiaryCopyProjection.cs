using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Domain.Nutrition;

/// <summary>One food a day copy would write, with its amount and meal.</summary>
public sealed record DiaryCopyFood(
    string Identity,
    string Name,
    MealType MealType,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    string? GroupIdentity = null);

/// <summary>The totals the copy produces, so the preview can show the effect before committing.</summary>
public sealed record DiaryCopyTotals(
    int Count,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat);

/// <summary>A read-only plan over two owner-scoped diary snapshots. Each grouped legacy food is one item.</summary>
public sealed class DiaryCopyProjection
{
    /// <summary>Holds a copy preview built from both days' entries.</summary>
    private DiaryCopyProjection(
        IReadOnlyList<DiaryCopyFood> sourceItems,
        IReadOnlyList<DiaryCopyFood> existingTargetItems,
        IReadOnlyList<DiaryCopyFood> resultingTargetItems,
        DiaryCopyTotals sourceTotals,
        DiaryCopyTotals existingTargetTotals,
        DiaryCopyTotals resultingTargetTotals)
    {
        SourceItems = sourceItems;
        ExistingTargetItems = existingTargetItems;
        ResultingTargetItems = resultingTargetItems;
        SourceTotals = sourceTotals;
        ExistingTargetTotals = existingTargetTotals;
        ResultingTargetTotals = resultingTargetTotals;
    }

    /// <summary>Entries on the day being copied from.</summary>
    public IReadOnlyList<DiaryCopyFood> SourceItems { get; }
    /// <summary>Entries the target day already holds.</summary>
    public IReadOnlyList<DiaryCopyFood> ExistingTargetItems { get; }
    /// <summary>Entries the target day holds once the copy is applied.</summary>
    public IReadOnlyList<DiaryCopyFood> ResultingTargetItems { get; }
    /// <summary>Nutrition totals of the source day.</summary>
    public DiaryCopyTotals SourceTotals { get; }
    /// <summary>Nutrition totals the target day already had.</summary>
    public DiaryCopyTotals ExistingTargetTotals { get; }
    /// <summary>Nutrition totals the target day shows after the copy.</summary>
    public DiaryCopyTotals ResultingTargetTotals { get; }

    /// <summary>Builds the preview by merging the source entries into the target day's meal.</summary>
    public static DiaryCopyProjection Create(
        IReadOnlyList<DiaryCopyFood> source,
        IReadOnlyList<DiaryCopyFood> target,
        MealType? selectedMeal,
        DiaryCopyMode mode)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(target);
        if (!Enum.IsDefined(mode))
            throw new ArgumentOutOfRangeException(nameof(mode));
        if (selectedMeal is { } mealType)
            DailyFoodEntry.RequireMealType(mealType);

        ValidateFoods(source);
        ValidateFoods(target);
        var chosenSource = source.Where(item => selectedMeal is null || item.MealType == selectedMeal).ToArray();
        var chosenTarget = target.Where(item => selectedMeal is null || item.MealType == selectedMeal).ToArray();
        var retainedTarget = mode == DiaryCopyMode.Merge
            ? target.ToArray()
            : target.Where(item => selectedMeal is not null && item.MealType != selectedMeal).ToArray();
        var resultingTarget = retainedTarget.Concat(chosenSource).ToArray();

        return new DiaryCopyProjection(
            chosenSource,
            chosenTarget,
            resultingTarget,
            Sum(chosenSource),
            Sum(chosenTarget),
            Sum(resultingTarget));
    }

    /// <summary>Refuses a projection with no foods or a non-positive amount.</summary>
    private static void ValidateFoods(IReadOnlyList<DiaryCopyFood> foods)
    {
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var food in foods)
        {
            ArgumentNullException.ThrowIfNull(food);
            if (string.IsNullOrWhiteSpace(food.Identity) || string.IsNullOrWhiteSpace(food.Name))
                throw new ArgumentException("Diary foods require an identity and name.", nameof(foods));
            if (!identities.Add(food.Identity))
                throw new ArgumentException("Diary food identities must be unique within a date.", nameof(foods));
            DailyFoodEntry.RequireMealType(food.MealType);
            if (food.Calories < decimal.Zero || food.Protein < decimal.Zero ||
                food.Carbs < decimal.Zero || food.Fat < decimal.Zero)
                throw new ArgumentException("Diary nutrients cannot be negative.", nameof(foods));
        }
    }

    /// <summary>Adds the projected foods into the totals the preview shows.</summary>
    private static DiaryCopyTotals Sum(DiaryCopyFood[] foods)
    {
        try
        {
            return new DiaryCopyTotals(
                foods.Length,
                foods.Aggregate(decimal.Zero, (total, item) => checked(total + item.Calories)),
                foods.Aggregate(decimal.Zero, (total, item) => checked(total + item.Protein)),
                foods.Aggregate(decimal.Zero, (total, item) => checked(total + item.Carbs)),
                foods.Aggregate(decimal.Zero, (total, item) => checked(total + item.Fat)));
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException("Diary totals exceed supported values.", nameof(foods), exception);
        }
    }
}

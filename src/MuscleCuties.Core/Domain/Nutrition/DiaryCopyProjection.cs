using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Domain.Nutrition;

public sealed record DiaryCopyFood(
    string Identity,
    string Name,
    MealType MealType,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat,
    string? GroupIdentity = null);

public sealed record DiaryCopyTotals(
    int Count,
    decimal Calories,
    decimal Protein,
    decimal Carbs,
    decimal Fat);

/// <summary>A read-only plan over two owner-scoped diary snapshots. Each grouped legacy food is one item.</summary>
public sealed class DiaryCopyProjection
{
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

    public IReadOnlyList<DiaryCopyFood> SourceItems { get; }
    public IReadOnlyList<DiaryCopyFood> ExistingTargetItems { get; }
    public IReadOnlyList<DiaryCopyFood> ResultingTargetItems { get; }
    public DiaryCopyTotals SourceTotals { get; }
    public DiaryCopyTotals ExistingTargetTotals { get; }
    public DiaryCopyTotals ResultingTargetTotals { get; }

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

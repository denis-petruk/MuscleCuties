namespace MuscleCuties.Core.Domain.Nutrition;

public sealed class DiaryNutritionTarget
{
    private DiaryNutritionTarget(decimal calories, decimal protein, decimal carbs, decimal fat)
    {
        Calories = calories;
        Protein = protein;
        Carbs = carbs;
        Fat = fat;
    }

    public decimal Calories { get; }
    public decimal Protein { get; }
    public decimal Carbs { get; }
    public decimal Fat { get; }

    public static DiaryNutritionTarget Create(decimal calories, decimal protein, decimal carbs, decimal fat)
    {
        if (calories < decimal.Zero || protein < decimal.Zero ||
            carbs < decimal.Zero || fat < decimal.Zero)
            throw new ArgumentOutOfRangeException(nameof(calories), "Nutrition targets cannot be negative.");
        return new DiaryNutritionTarget(calories, protein, carbs, fat);
    }
}

public sealed class DiaryWeekdayOverride
{
    private DiaryWeekdayOverride(Guid ownerId, DayOfWeek weekday, bool isEnabled, DiaryNutritionTarget target)
    {
        OwnerId = ownerId;
        Weekday = weekday;
        IsEnabled = isEnabled;
        Target = target;
    }

    public Guid OwnerId { get; }
    public DayOfWeek Weekday { get; }
    public bool IsEnabled { get; }
    public DiaryNutritionTarget Target { get; }

    public static DiaryWeekdayOverride Create(Guid ownerId, DayOfWeek weekday,
        bool isEnabled, DiaryNutritionTarget target)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("A valid owner is required.", nameof(ownerId));
        if (!Enum.IsDefined(weekday))
            throw new ArgumentOutOfRangeException(nameof(weekday));
        ArgumentNullException.ThrowIfNull(target);
        _ = DiaryNutritionTarget.Create(target.Calories, target.Protein, target.Carbs, target.Fat);
        return new DiaryWeekdayOverride(ownerId, weekday, isEnabled, target);
    }
}

public sealed class DiaryPerMealTarget
{
    private DiaryPerMealTarget(Guid ownerId, Guid categoryId, DiaryNutritionTarget target)
    {
        OwnerId = ownerId;
        CategoryId = categoryId;
        Target = target;
    }

    public Guid OwnerId { get; }
    public Guid CategoryId { get; }
    public DiaryNutritionTarget Target { get; }

    public static DiaryPerMealTarget Create(Guid ownerId, Guid categoryId, DiaryNutritionTarget target)
    {
        if (ownerId == Guid.Empty || categoryId == Guid.Empty)
            throw new ArgumentException("Owner and category identities are required.");
        ArgumentNullException.ThrowIfNull(target);
        _ = DiaryNutritionTarget.Create(target.Calories, target.Protein, target.Carbs, target.Fat);
        return new DiaryPerMealTarget(ownerId, categoryId, target);
    }
}

public enum DiaryTargetSource
{
    Unavailable,
    Calculated,
    UserWeekdayOverride
}

public sealed record DiaryEffectiveTarget(DiaryNutritionTarget? Target, DiaryTargetSource Source);

public static class DiaryTargetPolicy
{
    public static DiaryEffectiveTarget Resolve(DateOnly date, Guid ownerId,
        DiaryNutritionTarget? calculatedTarget,
        DiaryWeekdayOverride? weekdayOverride)
    {
        if (ownerId == Guid.Empty)
            throw new ArgumentException("A valid owner is required.", nameof(ownerId));
        if (weekdayOverride is not null &&
            (weekdayOverride.OwnerId != ownerId || weekdayOverride.Weekday != date.DayOfWeek))
            throw new ArgumentException("The weekday override does not match the owner and date.",
                nameof(weekdayOverride));
        if (weekdayOverride is { IsEnabled: true })
        {
            return new DiaryEffectiveTarget(weekdayOverride.Target, DiaryTargetSource.UserWeekdayOverride);
        }

        if (calculatedTarget is null)
            return new DiaryEffectiveTarget(null, DiaryTargetSource.Unavailable);
        _ = DiaryNutritionTarget.Create(calculatedTarget.Calories, calculatedTarget.Protein,
            calculatedTarget.Carbs, calculatedTarget.Fat);
        return new DiaryEffectiveTarget(calculatedTarget, DiaryTargetSource.Calculated);
    }
}

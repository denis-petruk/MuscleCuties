namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

/// <summary>A diary change, carrying the account so a listener can ignore another's edit.</summary>
public sealed class NutritionChangedEventArgs(int userId, DateOnly date) : EventArgs
{
    /// <summary>Account whose diary changed.</summary>
    public int UserId { get; } = userId;
    /// <summary>Day whose diary changed.</summary>
    public DateOnly Date { get; } = date;
}

/// <summary>Announces diary changes, which is how the day's totals stay in step.</summary>
public interface INutritionChangeNotifier
{
    event EventHandler<NutritionChangedEventArgs>? NutritionChanged;
    void NotifyNutritionChanged(int userId, DateOnly date);
}

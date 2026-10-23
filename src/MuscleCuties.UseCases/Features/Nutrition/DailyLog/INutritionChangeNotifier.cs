namespace MuscleCuties.UseCases.Features.Nutrition.DailyLog;

public sealed class NutritionChangedEventArgs(int userId, DateOnly date) : EventArgs
{
    public int UserId { get; } = userId;
    public DateOnly Date { get; } = date;
}

public interface INutritionChangeNotifier
{
    event EventHandler<NutritionChangedEventArgs>? NutritionChanged;
    void NotifyNutritionChanged(int userId, DateOnly date);
}

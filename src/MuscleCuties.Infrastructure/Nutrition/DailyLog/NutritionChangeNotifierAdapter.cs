using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using CoreNotifier = MuscleCuties.Core.Services.Nutrition.INutritionChangeNotifier;

namespace MuscleCuties.Infrastructure.Nutrition.DailyLog;

public sealed class NutritionChangeNotifierAdapter(CoreNotifier innerNotifier) : INutritionChangeNotifier, IDisposable
{
    private EventHandler<NutritionChangedEventArgs>? _handlers;

    public NutritionChangeNotifierAdapter(CoreNotifier innerNotifier, bool attachImmediately) : this(innerNotifier)
    {
        if (attachImmediately)
            innerNotifier.NutritionChanged += OnInnerNutritionChanged;
    }

    public void Dispose()
    {
        innerNotifier.NutritionChanged -= OnInnerNutritionChanged;
    }

    public event EventHandler<NutritionChangedEventArgs>? NutritionChanged
    {
        add
        {
            if (_handlers is null)
                innerNotifier.NutritionChanged += OnInnerNutritionChanged;
            _handlers += value;
        }
        remove
        {
            _handlers -= value;
            if (_handlers is null)
                innerNotifier.NutritionChanged -= OnInnerNutritionChanged;
        }
    }

    public void NotifyNutritionChanged(int userId, DateOnly date)
    {
        innerNotifier.NotifyNutritionChanged(userId, date);
    }

    private void OnInnerNutritionChanged(object? sender, Core.Services.Nutrition.NutritionChangedEventArgs e)
    {
        _handlers?.Invoke(this, new NutritionChangedEventArgs(e.UserId, e.Date));
    }
}

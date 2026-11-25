using MuscleCuties.UseCases.Features.Nutrition.DailyLog;
using CoreNotifier = MuscleCuties.Core.Services.Nutrition.INutritionChangeNotifier;

namespace MuscleCuties.Infrastructure.Nutrition.DailyLog;

/// <summary>Bridges the app's nutrition notifier onto the lower layer's interface, so a diary change reaches both.</summary>
public sealed class NutritionChangeNotifierAdapter(CoreNotifier innerNotifier) : INutritionChangeNotifier, IDisposable
{
    private EventHandler<NutritionChangedEventArgs>? _handlers;

    /// <summary>Detaches from the wrapped notifier.</summary>
    public void Dispose()
    {
        innerNotifier.NutritionChanged -= OnInnerNutritionChanged;
    }

    /// <summary>Raised for every diary change the lower layer reports.</summary>
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

    /// <summary>Forwards a change to the lower layer and then to whoever listens here.</summary>
    public void NotifyNutritionChanged(int userId, DateOnly date)
    {
        innerNotifier.NotifyNutritionChanged(userId, date);
    }

    /// <summary>Forwards the wrapped notifier's change to this adapter's subscribers.</summary>
    private void OnInnerNutritionChanged(object? sender, Core.Services.Nutrition.NutritionChangedEventArgs e)
    {
        _handlers?.Invoke(this, new NutritionChangedEventArgs(e.UserId, e.Date));
    }
}

using System.Diagnostics;

namespace MuscleCuties.Core.Services.Nutrition;

public sealed class NutritionChangedEventArgs : EventArgs
{
    public NutritionChangedEventArgs(int userId, DateOnly date)
    {
        UserId = userId;
        Date = date;
    }

    public int UserId { get; }
    public DateOnly Date { get; }
}

public interface INutritionChangeNotifier
{
    event EventHandler<NutritionChangedEventArgs>? NutritionChanged;
    void NotifyNutritionChanged(int userId, DateOnly date);
}

public class NutritionChangeNotifier(Action<Action>? dispatch = null) : INutritionChangeNotifier
{
    public event EventHandler<NutritionChangedEventArgs>? NutritionChanged;

    public void NotifyNutritionChanged(int userId, DateOnly date)
    {
        void Publish()
        {
            var handlers = NutritionChanged;
            if (handlers is null) return;
            foreach (EventHandler<NutritionChangedEventArgs> handler in handlers.GetInvocationList())
                // A subscriber must never turn an already committed write into a reported failure.
                try { handler(this, new NutritionChangedEventArgs(userId, date)); }
                catch (Exception ex) { Trace.WriteLine($"Nutrition refresh failed ({ex.GetType().Name})."); }
        }

        if (dispatch is null) Publish();
        else
            try { dispatch(Publish); }
            catch (Exception ex) { Trace.WriteLine($"Nutrition dispatch failed ({ex.GetType().Name})."); }
    }
}

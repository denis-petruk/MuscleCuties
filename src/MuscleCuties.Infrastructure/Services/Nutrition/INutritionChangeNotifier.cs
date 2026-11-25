using System.Diagnostics;

namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>A diary change: which account it was for and what changed.</summary>
public sealed class NutritionChangedEventArgs : EventArgs
{
    /// <summary>Carries the account and the change, so a listener can ignore another account's edit.</summary>
    public NutritionChangedEventArgs(int userId, DateOnly date)
    {
        UserId = userId;
        Date = date;
    }

    /// <summary>Account whose diary changed.</summary>
    public int UserId { get; }
    /// <summary>Day whose diary changed.</summary>
    public DateOnly Date { get; }
}

/// <summary>Tells the app that a diary entry changed, which is how the day's totals stay in step.</summary>
public interface INutritionChangeNotifier
{
    /// <summary>Raised for every diary change.</summary>
    event EventHandler<NutritionChangedEventArgs>? NutritionChanged;
    /// <summary>Announces a change for an account.</summary>
    void NotifyNutritionChanged(int userId, DateOnly date);
}

/// <summary>The notifier implementation both layers share.</summary>
public class NutritionChangeNotifier(Action<Action>? dispatch = null) : INutritionChangeNotifier
{
    /// <summary>Raised for every diary change.</summary>
    public event EventHandler<NutritionChangedEventArgs>? NutritionChanged;

    /// <summary>Announces that an account's diary changed on a date.</summary>
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

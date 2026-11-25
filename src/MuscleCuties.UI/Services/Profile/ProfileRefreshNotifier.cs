using MuscleCuties.Core.Services;

namespace MuscleCuties.UI.Services.Profile;

/// <summary>Tells the preloaded screens that profile data changed and which of them must reload.</summary>
public interface IProfileRefreshNotifier
{
    /// <summary>Every cached screen must reload, used when the change affects more than one tab.</summary>
    void InvalidateAll();
    /// <summary>The workout plan and the dashboard must reload, used when a change alters what today's training asks</summary>
    /// for.</summary>
    void InvalidateWorkoutAndDashboard();
    /// <summary>Only the dashboard must reload.</summary>
    void InvalidateDashboard();
}

/// <summary>Routes profile changes to the preload service, resolving it lazily so the notifier can be registered</summary>
/// before the service it depends on.</summary>
public sealed class ProfileRefreshNotifier(Lazy<IAppPreloadService> preloadService) : IProfileRefreshNotifier
{
    /// <summary>Every cached screen must reload, used when the change affects more than one tab.</summary>
    public void InvalidateAll()
    {
        preloadService.Value.InvalidateAll();
    }

    /// <summary>The workout plan and the dashboard must reload, used when a change alters what today's training asks</summary>
    /// for.</summary>
    public void InvalidateWorkoutAndDashboard()
    {
        preloadService.Value.InvalidateWorkout();
        preloadService.Value.InvalidateDashboard();
    }

    /// <summary>Only the dashboard must reload.</summary>
    public void InvalidateDashboard()
    {
        preloadService.Value.InvalidateDashboard();
    }
}

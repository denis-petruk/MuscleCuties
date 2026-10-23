using MuscleCuties.Core.Services;

namespace MuscleCuties.UI.Services.Profile;

public interface IProfileRefreshNotifier
{
    void InvalidateAll();
    void InvalidateWorkoutAndDashboard();
    void InvalidateDashboard();
}

public sealed class ProfileRefreshNotifier(Lazy<IAppPreloadService> preloadService) : IProfileRefreshNotifier
{
    public void InvalidateAll()
    {
        preloadService.Value.InvalidateAll();
    }

    public void InvalidateWorkoutAndDashboard()
    {
        preloadService.Value.InvalidateWorkout();
        preloadService.Value.InvalidateDashboard();
    }

    public void InvalidateDashboard()
    {
        preloadService.Value.InvalidateDashboard();
    }
}

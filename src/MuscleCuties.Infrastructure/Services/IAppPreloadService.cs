namespace MuscleCuties.Core.Services;

public interface IAppPreloadService
{
    DateTime LastLoadedDate { get; }
    Task PreloadAllAsync();
    Task PreloadDashboardAsync();
    Task PreloadRemainingAsync();
    Task RefreshAllAsync();
    void InvalidateAll();
    void InvalidateDashboard();
    void InvalidateWorkout();
}

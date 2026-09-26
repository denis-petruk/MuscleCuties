namespace MuscleCuties.Core.Services;

public interface IAppPreloadService
{
    Task PreloadAllAsync();
    Task PreloadDashboardAsync();
    Task PreloadRemainingAsync();
    Task RefreshAllAsync();
    void InvalidateAll();
    void InvalidateDashboard();
    void InvalidateWorkout();
    DateTime LastLoadedDate { get; }
}

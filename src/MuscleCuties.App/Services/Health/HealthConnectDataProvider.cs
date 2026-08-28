using MuscleCuties.Core.Services.Health;

#if ANDROID
using Android.Content.PM;
using Microsoft.Maui.ApplicationModel;
#endif

namespace MuscleCuties.App.Services.Health;

public sealed class HealthConnectDataProvider : IHealthDataProvider, IHealthDataProviderDiagnostics
{
    private const string HealthConnectPackageName = "com.google.android.apps.healthdata";

    public HealthDataSource Source => HealthDataSource.AppleHealth;
    public string DisplayName => "Health Connect";
    public string UnavailableMessage =>
        "Health Connect needs the Health Connect app installed, runtime permissions approved, and Play Console health permissions review before Android sync can read data.";
    public string EmptyDataMessage =>
        "Health Connect is available, but it has not returned weekly steps, sleep, resting heart rate, or HRV yet.";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
#if ANDROID
        var packageManager = Platform.AppContext.PackageManager;
        if (packageManager is null)
            return Task.FromResult(false);

        try
        {
            packageManager.GetPackageInfo(HealthConnectPackageName, PackageInfoFlags.MatchAll);
            return Task.FromResult(true);
        }
        catch (PackageManager.NameNotFoundException)
        {
            return Task.FromResult(false);
        }
#else
        return Task.FromResult(false);
#endif
    }

    public Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(
        DateTime today,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<HealthWeeklySummary?>(null);
}

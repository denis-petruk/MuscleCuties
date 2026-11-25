namespace MuscleCuties.Core.Services.Health;

/// <summary>Which health store the data comes from, so a stored state can be checked against this build.</summary>
public enum HealthDataSource
{
    AppleHealth = 0,
    HealthConnect = 1,
    Whoop = 2
}

/// <summary>The names the settings page shows for each store.</summary>
public static class HealthDataSourceExtensions
{
    /// <summary>The store's user-facing name, with a neutral fallback for an unknown source.</summary>
    public static string ToDisplayName(this HealthDataSource source)
    {
        return source switch
        {
            HealthDataSource.AppleHealth => "Apple Health",
            HealthDataSource.HealthConnect => "Health Connect",
            HealthDataSource.Whoop => "Whoop",
            _ => "Health data"
        };
    }
}

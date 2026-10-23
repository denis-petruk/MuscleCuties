namespace MuscleCuties.Core.Services.Health;

public enum HealthDataSource
{
    AppleHealth = 0,
    HealthConnect = 1,
    Whoop = 2
}

public static class HealthDataSourceExtensions
{
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

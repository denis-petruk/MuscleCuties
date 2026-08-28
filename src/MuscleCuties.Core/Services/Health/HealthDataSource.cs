namespace MuscleCuties.Core.Services.Health;

public enum HealthDataSource
{
    AppleHealth = 0,
    Whoop = 2
}

public static class HealthDataSourceExtensions
{
    public static string ToDisplayName(this HealthDataSource source) => source switch
    {
        HealthDataSource.AppleHealth => "Apple Health",
        HealthDataSource.Whoop => "Whoop",
        _ => "Health data"
    };
}

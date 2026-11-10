using Microsoft.Extensions.Configuration;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

internal static class S3StorageConfiguration
{
    private const string BucketNameSetting = "Storage:S3:BucketName";
    private const string RegionSetting = "Storage:S3:Region";

    public static bool HasBucketName(IConfiguration? configuration) =>
        !string.IsNullOrWhiteSpace(configuration?[BucketNameSetting]);

    public static string RequireBucketName(IConfiguration configuration) =>
        Require(configuration, BucketNameSetting);

    public static string RequireRegion(IConfiguration configuration) =>
        Require(configuration, RegionSetting);

    private static string Require(IConfiguration configuration, string settingName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[settingName];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{settingName} must be configured before using S3 storage.");

        return value.Trim();
    }
}

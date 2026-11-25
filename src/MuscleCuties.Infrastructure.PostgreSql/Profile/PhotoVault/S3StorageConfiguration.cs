using Microsoft.Extensions.Configuration;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>The bucket settings read once from configuration, so a missing key fails at startup rather than per request.</summary>
internal static class S3StorageConfiguration
{
    private const string BucketNameSetting = "Storage:S3:BucketName";
    private const string RegionSetting = "Storage:S3:Region";

    /// <summary>True when a bucket name is configured.</summary>
    public static bool HasBucketName(IConfiguration? configuration) =>
        !string.IsNullOrWhiteSpace(configuration?[BucketNameSetting]);

    /// <summary>The configured bucket name.</summary>
    public static string RequireBucketName(IConfiguration configuration) =>
        Require(configuration, BucketNameSetting);

    /// <summary>The configured region.</summary>
    public static string RequireRegion(IConfiguration configuration) =>
        Require(configuration, RegionSetting);

    /// <summary>Reads a required setting, failing with its name so a misconfiguration is obvious.</summary>
    private static string Require(IConfiguration configuration, string settingName)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var value = configuration[settingName];
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidOperationException($"{settingName} must be configured before using S3 storage.");

        return value.Trim();
    }
}

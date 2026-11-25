using System.Net.Http;
using Microsoft.Maui.Networking;

namespace MuscleCuties.UI.Infrastructure.Network;

/// <summary>The connectivity check and retry timings the catalogue clients share.</summary>
internal static class NetworkRetryHelper
{
    public static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(30);

    /// <summary>True when the platform reports internet access. A platform that cannot answer is treated as</summary>
    /// online, because blocking a request on an unknown state would be worse than trying it.</summary>
    public static bool HasInternetAccess()
    {
        try
        {
            return Connectivity.Current.NetworkAccess == NetworkAccess.Internet;
        }
        catch (Exception ex) when (ex is FeatureNotEnabledException or FeatureNotSupportedException)
        {
            return true;
        }
    }
}

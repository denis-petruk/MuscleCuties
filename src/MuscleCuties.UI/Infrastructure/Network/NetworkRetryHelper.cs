using System.Net.Http;
using Microsoft.Maui.Networking;

namespace MuscleCuties.UI.Infrastructure.Network;

internal static class NetworkRetryHelper
{
    public static readonly TimeSpan DefaultRateLimitDelay = TimeSpan.FromSeconds(2);
    public static readonly TimeSpan MaxRateLimitDelay = TimeSpan.FromSeconds(30);

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

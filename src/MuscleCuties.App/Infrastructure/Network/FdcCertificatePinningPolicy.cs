using System.Net.Security;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace MuscleCuties.App.Infrastructure.Network;

public static class FdcCertificatePinningPolicy
{
    private const string FdcHost = "api.nal.usda.gov";
    private const string PinEnvironmentVariable = "FDC_CERT_PIN_SHA256";

    private static readonly string[] DefaultPins =
    [
        "FqOBYMv3/p5KXaD9Nn2yJL+7h+DLDPKYoENw4kI2XMU=", // api.nal.usda.gov leaf
        "nWN7PSep5XDQdge5zK24CnCRXHr3KvzhKEGxsdqCX9E="  // intermediate CA backup
    ];

    public static bool Validate(
        object sender,
        X509Certificate? certificate,
        X509Chain? chain,
        SslPolicyErrors sslPolicyErrors)
    {
        if (sslPolicyErrors != SslPolicyErrors.None)
            return false;

        if (sender is HttpRequestMessage request &&
            !string.Equals(request.RequestUri?.Host, FdcHost, StringComparison.OrdinalIgnoreCase))
            return true;

#if DEBUG
        if (string.Equals(
                Environment.GetEnvironmentVariable("FDC_DISABLE_PIN"), "1",
                StringComparison.Ordinal))
            return true;
#endif

        if (certificate is null)
            return false;

        var pins = GetEffectivePins();
        using var cert = certificate as X509Certificate2 ?? new X509Certificate2(certificate);
        var spkiHash = Convert.ToBase64String(
            SHA256.HashData(cert.PublicKey.ExportSubjectPublicKeyInfo()));
        return pins.Contains(spkiHash);
    }

    private static IReadOnlySet<string> GetEffectivePins()
    {
        var pins = new HashSet<string>(DefaultPins, StringComparer.Ordinal);

        var envPins = Environment.GetEnvironmentVariable(PinEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(envPins))
        {
            foreach (var pin in envPins.Split(';',
                         StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                pins.Add(pin);
        }

        return pins;
    }
}

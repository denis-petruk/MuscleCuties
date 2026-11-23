using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>
/// The all-route latency gate measures real PostgreSQL work, so it skips rather than fails when no
/// database is configured. Same discovery-time pattern as the integration suite's PostgresFact.
/// </summary>
public sealed class LatencyGateFactAttribute : FactAttribute
{
    public const string ConnectionVariable = "MC_LATENCY_POSTGRES";

    public LatencyGateFactAttribute()
    {
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(ConnectionVariable))) return;
        Skip = "The all-route latency gate needs a disposable PostgreSQL fixture. Set MC_LATENCY_POSTGRES " +
               "to a connection string and rerun. The synthetic unreachable-DB fallback is not measured.";
    }
}

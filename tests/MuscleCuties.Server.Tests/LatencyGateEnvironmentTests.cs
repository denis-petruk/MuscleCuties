using Xunit;

namespace MuscleCuties.Server.Tests;

/// <summary>
/// The latency gate changed from "fail when the variable is missing" to "skip when unconfigured, assert
/// exactly as before when configured". These assertions pin both halves of that gating.
/// </summary>
public sealed class LatencyGateEnvironmentTests
{
    [Fact]
    public void Gate_is_unconfigured_and_reports_a_skip_reason_when_the_variable_is_absent()
    {
        WithConnectionVariable(null, () =>
        {
            Assert.False(LatencyGateEnvironment.IsConfigured);
            Assert.False(string.IsNullOrWhiteSpace(new LatencyGateFactAttribute().Skip));
            Assert.Equal(LatencyGateEnvironment.SkipReason, new LatencyGateFactAttribute().Skip);
            var failure = Assert.Throws<InvalidOperationException>(LatencyGateEnvironment.RequireConnection);
            Assert.Contains(LatencyGateEnvironment.ConnectionVariable, failure.Message, StringComparison.Ordinal);
        });
    }

    [Fact]
    public void Gate_is_configured_and_runs_without_a_skip_when_the_variable_is_set()
    {
        WithConnectionVariable("Host=localhost;Database=latency_gate;Username=test", () =>
        {
            Assert.True(LatencyGateEnvironment.IsConfigured);
            Assert.Null(new LatencyGateFactAttribute().Skip);
            Assert.Equal("Host=localhost;Database=latency_gate;Username=test", LatencyGateEnvironment.RequireConnection());
        });
    }

    [Theory]
    [InlineData("   ")]
    public void Blank_variable_is_still_unconfigured(string value)
    {
        WithConnectionVariable(value, () => Assert.False(LatencyGateEnvironment.IsConfigured));
    }

    private static void WithConnectionVariable(string? value, Action assertion)
    {
        var original = Environment.GetEnvironmentVariable(LatencyGateEnvironment.ConnectionVariable);
        try
        {
            Environment.SetEnvironmentVariable(LatencyGateEnvironment.ConnectionVariable, value);
            assertion();
        }
        finally
        {
            Environment.SetEnvironmentVariable(LatencyGateEnvironment.ConnectionVariable, original);
        }
    }
}

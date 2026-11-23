using MuscleCuties.UI.ViewModels.Auth;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
///     Pins the behaviour the OTP and password-recovery view models used to expose through their private
///     StartCountdown/StopCountdown forwarders: start reports the initial state synchronously, zero means
///     resend is ready, and a disposed coordinator stops reporting.
/// </summary>
public sealed class ResendCountdownCoordinatorTests
{
    private const string ReadyText = "Resend code";

    [Fact]
    public void ZeroSecondsReportsResendReadyImmediately()
    {
        var states = new List<(int Seconds, bool CanResend, string Text)>();
        using var coordinator = Create(states);

        coordinator.StartCountdown(0);

        var state = Assert.Single(states);
        Assert.Equal(0, state.Seconds);
        Assert.True(state.CanResend);
        Assert.Equal(ReadyText, state.Text);
    }

    [Fact]
    public void PositiveSecondsReportsCountdownBeforeTheFirstTick()
    {
        var states = new List<(int Seconds, bool CanResend, string Text)>();
        using var coordinator = Create(states);

        coordinator.StartCountdown(90);
        coordinator.StopCountdown();

        var state = Assert.Single(states);
        Assert.Equal(90, state.Seconds);
        Assert.False(state.CanResend);
        Assert.Equal("Resend in 90s", state.Text);
    }

    [Fact]
    public void NegativeSecondsIsTreatedAsReadyRatherThanThrowing()
    {
        var states = new List<(int Seconds, bool CanResend, string Text)>();
        using var coordinator = Create(states);

        coordinator.StartCountdown(-5);

        var state = Assert.Single(states);
        Assert.Equal(0, state.Seconds);
        Assert.True(state.CanResend);
        Assert.Equal(ReadyText, state.Text);
    }

    [Fact]
    public void RestartingDoesNotReportTwiceForTheSameStart()
    {
        var states = new List<(int Seconds, bool CanResend, string Text)>();
        using var coordinator = Create(states);

        coordinator.StartCountdown(30);
        coordinator.StartCountdown(30);
        coordinator.StopCountdown();

        Assert.Equal(2, states.Count);
        Assert.All(states, state => Assert.Equal(30, state.Seconds));
    }

    [Fact]
    public void DisposedCoordinatorIgnoresFurtherStarts()
    {
        var states = new List<(int Seconds, bool CanResend, string Text)>();
        var coordinator = Create(states);
        coordinator.Dispose();

        coordinator.StartCountdown(60);

        Assert.Empty(states);
    }

    [Fact]
    public void StoppingWithoutStartingAndDisposingTwiceAreHarmless()
    {
        var states = new List<(int Seconds, bool CanResend, string Text)>();
        var coordinator = Create(states);

        coordinator.StopCountdown();
        coordinator.Dispose();
        coordinator.Dispose();

        Assert.Empty(states);
    }

    private static ResendCountdownCoordinator Create(List<(int Seconds, bool CanResend, string Text)> states) =>
        new((seconds, canResend, text) => states.Add((seconds, canResend, text)),
            ReadyText,
            seconds => $"Resend in {seconds}s");
}

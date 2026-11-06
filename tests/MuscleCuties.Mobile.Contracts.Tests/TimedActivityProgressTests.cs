using MuscleCuties.UI.Services.Workout;
using Xunit;
using Surface = MuscleCuties.UI.Services.Workout.TimedActivityProgress.WorkoutActivitySurface;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
/// The timed running surface is shared by every non-strength activity. These checks pin the surface
/// routing per activity type and the phase/countdown derivation for each activity's prescription shape,
/// plus the zero-time and negative-time edges, so no activity invents phases it was not prescribed.
/// </summary>
public sealed class TimedActivityProgressTests
{
    private const decimal WarmupMinutes = 5m;
    private const decimal MainMinutes = 20m;
    private const decimal CooldownMinutes = 5m;
    private const int SecondsPerMinute = 60;

    /// <summary>
    /// Emitted section tag per activity type, mirroring the planner: cardio types persist as
    /// <c>WorkoutType.Cardio</c> (CARDIO), yoga/mobility/gentle recovery as <c>WorkoutType.Recovery</c>
    /// (RECOVERY), and rock climbing as <c>WorkoutType.Strength</c> (STRENGTH) even though it receives no
    /// movement rows because only true strength activities get exercises. The <c>hasRows</c> flag mirrors
    /// that planner invariant: non-strength activities never carry movement rows.
    /// </summary>
    public static TheoryData<string, string, bool, Surface> ActivityRouting => new()
    {
        { "Walking", "CARDIO", false, Surface.TimedActivity },
        { "Cycling", "CARDIO", false, Surface.TimedActivity },
        { "Running", "CARDIO", false, Surface.TimedActivity },
        { "Swimming", "CARDIO", false, Surface.TimedActivity },
        { "Rowing", "CARDIO", false, Surface.TimedActivity },
        { "Elliptical", "CARDIO", false, Surface.TimedActivity },
        { "HIIT", "CARDIO", false, Surface.TimedActivity },
        // Climbing is planned as a strength-type day but receives no movement rows; it must still be timed
        // rather than landing on an empty exercise flow.
        { "RockClimbing", "STRENGTH", false, Surface.TimedActivity },
        { "Yoga", "RECOVERY", false, Surface.TimedActivity },
        { "Mobility", "RECOVERY", false, Surface.TimedActivity },
        { "GentleRecovery", "RECOVERY", false, Surface.TimedActivity },
        { "HighVolumeStrength", "STRENGTH", true, Surface.ExerciseFlow },
        { "StrengthHighIntensity", "STRENGTH", true, Surface.ExerciseFlow },
        { "GeneralStrength", "STRENGTH", true, Surface.ExerciseFlow },
        { "RestDay", "REST", false, Surface.RestDay },
        { "RestDayDefensiveRows", "REST", true, Surface.RestDay },
        // A mixed strength day can carry a cardio or recovery movement block. Those rows keep the exercise
        // flow because they record distance, pace, heart-rate and power actuals.
        { "MixedDayCardioBlock", "CARDIO", true, Surface.ExerciseFlow },
        { "MixedDayRecoveryBlock", "RECOVERY", true, Surface.ExerciseFlow },
        // Unroutable data is never stranded on an empty exercise flow.
        { "UnknownTagWithRows", "OTHER", true, Surface.ExerciseFlow },
        { "UnknownTagWithoutRows", "OTHER", false, Surface.TimedActivity },
        { "EmptyTagWithRows", "", true, Surface.ExerciseFlow },
        { "EmptyTagWithoutRows", "", false, Surface.TimedActivity }
    };

    [Theory]
    [MemberData(nameof(ActivityRouting))]
    public void EachActivityTypeResolvesToItsPinnedSurface(
        string activity, string tag, bool hasRows, Surface expected)
    {
        Assert.Equal(expected, TimedActivityProgress.DecideSurface(tag, hasRows));
        Assert.NotEmpty(activity);
    }

    [Fact]
    public void RestTagIsCaseInsensitiveAndWhitespaceSafe()
    {
        Assert.True(TimedActivityProgress.IsRestTag("REST"));
        Assert.True(TimedActivityProgress.IsRestTag("rest"));
        Assert.False(TimedActivityProgress.IsRestTag(""));
        Assert.False(TimedActivityProgress.IsRestTag("   "));
        Assert.False(TimedActivityProgress.IsRestTag(null));
        Assert.False(TimedActivityProgress.IsRestTag("CARDIO"));
    }

    public static TheoryData<string, decimal?, decimal?, decimal?> TimedActivityPrescriptions => new()
    {
        // Continuous cardio: five-minute warm-up and cool-down around the main piece.
        { "Walking", WarmupMinutes, MainMinutes, CooldownMinutes },
        { "Cycling", WarmupMinutes, MainMinutes, CooldownMinutes },
        { "Running", WarmupMinutes, MainMinutes, CooldownMinutes },
        { "Swimming", WarmupMinutes, MainMinutes, CooldownMinutes },
        { "Rowing", WarmupMinutes, MainMinutes, CooldownMinutes },
        { "Elliptical", WarmupMinutes, MainMinutes, CooldownMinutes },
        // HIIT prescribes a longer warm-up around its intervals.
        { "HIIT", 10m, MainMinutes, CooldownMinutes },
        // Climbing prescribes a longer main block.
        { "RockClimbing", WarmupMinutes, 30m, CooldownMinutes },
        // Recovery-type activities prescribe a main block only.
        { "Yoga", null, MainMinutes, null },
        { "Mobility", null, 10m, null },
        { "GentleRecovery", null, 10m, null }
    };

    [Theory]
    [MemberData(nameof(TimedActivityPrescriptions))]
    public void EachTimedActivityDerivesItsOwnPhasesAndCountdown(
        string activity, decimal? warmupMinutes, decimal? mainMinutes, decimal? cooldownMinutes)
    {
        var totalSeconds = ToSeconds(warmupMinutes) + ToSeconds(mainMinutes) + ToSeconds(cooldownMinutes);
        var startPhase = warmupMinutes is > 0
            ? TimedActivityProgress.WarmupPhaseKey
            : TimedActivityProgress.MainPhaseKey;
        var endPhase = cooldownMinutes is > 0
            ? TimedActivityProgress.CooldownPhaseKey
            : TimedActivityProgress.MainPhaseKey;

        var atStart = TimedActivityProgress.Describe(0, warmupMinutes, mainMinutes, cooldownMinutes);
        Assert.Equal(startPhase, atStart.PhaseKey);
        Assert.Equal(totalSeconds, atStart.TotalSeconds);
        Assert.Equal(totalSeconds, atStart.RemainingSeconds);

        var midway = TimedActivityProgress.Describe(
            ToSeconds(warmupMinutes) + ToSeconds(mainMinutes) / 2, warmupMinutes, mainMinutes, cooldownMinutes);
        Assert.Equal(TimedActivityProgress.MainPhaseKey, midway.PhaseKey);

        var atEnd = TimedActivityProgress.Describe(totalSeconds - 1, warmupMinutes, mainMinutes, cooldownMinutes);
        Assert.Equal(endPhase, atEnd.PhaseKey);
        Assert.Equal(1, atEnd.RemainingSeconds);

        var past = TimedActivityProgress.Describe(totalSeconds, warmupMinutes, mainMinutes, cooldownMinutes);
        Assert.Equal(endPhase, past.PhaseKey);
        Assert.Equal(0, past.RemainingSeconds);
        Assert.Equal(1, past.Progress);
        Assert.NotEmpty(activity);
    }

    [Fact]
    public void MissingPrescriptionReportsElapsedTimeOnly()
    {
        var state = TimedActivityProgress.Describe(90, null, null, null);

        Assert.False(state.HasPhase);
        Assert.False(state.HasCountdown);
        Assert.Equal(90, state.ElapsedSeconds);
        Assert.Equal(0, state.Progress);
    }

    [Fact]
    public void ZeroMinutePrescriptionStaysDurationOnly()
    {
        var state = TimedActivityProgress.Describe(30, 0m, 0m, 0m);

        Assert.False(state.HasPhase);
        Assert.False(state.HasCountdown);
        Assert.Equal(30, state.ElapsedSeconds);
    }

    [Fact]
    public void ElapsedPastThePrescriptionStopsAtZeroRemaining()
    {
        var state = TimedActivityProgress.Describe(
            1800 + 120, WarmupMinutes, MainMinutes, CooldownMinutes);

        Assert.Equal(TimedActivityProgress.CooldownPhaseKey, state.PhaseKey);
        Assert.Equal(0, state.RemainingSeconds);
        Assert.Equal(1, state.Progress);
    }

    [Fact]
    public void DurationOnlyActivityKeepsMainPhaseWithoutInventingWarmup()
    {
        var state = TimedActivityProgress.Describe(60, null, MainMinutes, null);

        Assert.Equal(TimedActivityProgress.MainPhaseKey, state.PhaseKey);
        Assert.Equal((int)(MainMinutes * SecondsPerMinute), state.TotalSeconds);
        Assert.Equal((int)(MainMinutes * SecondsPerMinute) - 60, state.RemainingSeconds);
    }

    [Fact]
    public void NegativeElapsedTimeIsTreatedAsTheStart()
    {
        var state = TimedActivityProgress.Describe(-5, WarmupMinutes, MainMinutes, CooldownMinutes);

        Assert.Equal(0, state.ElapsedSeconds);
        Assert.Equal(1800, state.RemainingSeconds);
    }

    [Theory]
    [InlineData(0d, null)]
    [InlineData(1d, 1)]
    [InlineData(7d, 7)]
    [InlineData(7.4d, 7)]
    [InlineData(7.6d, 8)]
    [InlineData(10d, 10)]
    [InlineData(11d, null)]
    [InlineData(-1d, null)]
    public void OptionalEffortNormalizesWithoutInventingAValue(double value, int? expected)
    {
        Assert.Equal(expected, TimedActivityProgress.NormalizeRpe(value));
    }

    [Fact]
    public void UnratedEffortStaysNull()
    {
        Assert.Null(TimedActivityProgress.NormalizeRpe(null));
    }

    [Theory]
    [InlineData("Walking", "CARDIO")]
    [InlineData("Cycling", "CARDIO")]
    [InlineData("Mobility", "RECOVERY")]
    public void BetaApprovedDurationOnlyActivitiesUseTheTimedSurface(string activity, string tag)
    {
        // The three approved Beta additions are duration-only: they reach the shared timed
        // lifecycle, and without a prescription they report elapsed time instead of inventing phases.
        Assert.Equal(Surface.TimedActivity, TimedActivityProgress.DecideSurface(tag, false));

        var state = TimedActivityProgress.Describe(45, null, null, null);
        Assert.False(state.HasPhase);
        Assert.False(state.HasCountdown);
        Assert.Equal(45, state.ElapsedSeconds);
        Assert.NotEmpty(activity);
    }

    [Theory]
    [InlineData("CARDIO", true)]
    [InlineData("cardio", true)]
    [InlineData("RECOVERY", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void DistanceIsOfferedOnlyForCardioTimedActivities(string? tag, bool expected)
    {
        Assert.Equal(expected, TimedActivityProgress.SupportsDistance(tag));
    }

    [Theory]
    [InlineData(0d, null)]
    [InlineData(5.234d, 5.23)]
    [InlineData(0.01d, 0.01)]
    [InlineData(500d, 500d)]
    [InlineData(500.01d, null)]
    public void OptionalDistanceNormalizesWithinBounds(double value, double? expected)
    {
        var normalized = TimedActivityProgress.NormalizeDistance((decimal)value);
        Assert.Equal(expected is null ? null : (decimal?)expected, normalized);
    }

    private static int ToSeconds(decimal? minutes) =>
        minutes is > 0 ? (int)(minutes.Value * SecondsPerMinute) : 0;
}

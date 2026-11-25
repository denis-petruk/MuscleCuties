namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Calculates check-in history averages without treating absent observations as zero.</summary>
public static class CheckInCoverageRule
{
    /// <summary>Mean of the recorded sleep samples, or nothing when none were recorded.</summary>
    public static decimal? AverageSleep(IEnumerable<decimal?> samples) => Average(samples);

    /// <summary>Averages the days that recorded steps, so a missing day never reads as a zero-step day.</summary>
    public static int? AverageSteps(IEnumerable<int?> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var present = samples.Where(sample => sample.HasValue).Select(sample => sample!.Value).ToArray();
        return present.Length == 0 ? null : (int)Math.Round(present.Average(), MidpointRounding.AwayFromZero);
    }

    /// <summary>Averages the recorded samples only, returning null when there are none.</summary>
    private static decimal? Average(IEnumerable<decimal?> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var present = samples.Where(sample => sample.HasValue).Select(sample => sample!.Value).ToArray();
        return present.Length == 0 ? null : present.Average();
    }
}

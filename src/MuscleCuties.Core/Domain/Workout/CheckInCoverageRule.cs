namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Calculates check-in history averages without treating absent observations as zero.</summary>
public static class CheckInCoverageRule
{
    public static decimal? AverageSleep(IEnumerable<decimal?> samples) => Average(samples);

    public static int? AverageSteps(IEnumerable<int?> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var present = samples.Where(sample => sample.HasValue).Select(sample => sample!.Value).ToArray();
        return present.Length == 0 ? null : (int)Math.Round(present.Average(), MidpointRounding.AwayFromZero);
    }

    private static decimal? Average(IEnumerable<decimal?> samples)
    {
        ArgumentNullException.ThrowIfNull(samples);
        var present = samples.Where(sample => sample.HasValue).Select(sample => sample!.Value).ToArray();
        return present.Length == 0 ? null : present.Average();
    }
}

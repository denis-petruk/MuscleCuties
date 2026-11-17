namespace MuscleCuties.Infrastructure.Services.Workout;

/// <summary>
/// Session rows keep an absolute set number for persistence, but people and automation count warm-up
/// and working sets separately. Treating the absolute number as the label made the first working set
/// "Working set 2" whenever a warm-up existed, and left no WorkingSet1 row at all.
/// </summary>
public static class WorkoutSetNumbering
{
    /// <summary>Absolute number plus the per-kind ordinal for each row, in row order.</summary>
    public static IReadOnlyList<(int AbsoluteNumber, int DisplayNumber)> Number(IReadOnlyList<bool> warmupFlags)
    {
        ArgumentNullException.ThrowIfNull(warmupFlags);

        var warmups = 0;
        var working = 0;
        var numbers = new (int AbsoluteNumber, int DisplayNumber)[warmupFlags.Count];
        for (var index = 0; index < warmupFlags.Count; index++)
        {
            var display = warmupFlags[index] ? ++warmups : ++working;
            numbers[index] = (index + 1, display);
        }

        return numbers;
    }
}

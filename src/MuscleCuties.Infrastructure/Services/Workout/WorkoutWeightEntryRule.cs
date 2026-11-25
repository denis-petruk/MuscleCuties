using System.Globalization;

namespace MuscleCuties.Infrastructure.Services.Workout;

/// <summary>One set row as the session screen sees it: a typed load, its role and whether the user touched it.</summary>
public readonly record struct WorkoutEnteredSet(string? WeightText, bool IsWarmup, bool WasEdited);

/// <summary>
/// Gate 77: the load a strength exercise actually carries. Validation and payload construction must
/// agree, and a calibration exercise is satisfied by a load the user typed into any set row, not only
/// into a working set: clearing unobserved rows leaves the aggregate blank, which used to block
/// logging with "Add a valid weight in kg." even though the screen held a valid number.
/// </summary>
public static class WorkoutWeightEntryRule
{
    public const double MinimumWeightKg = 0d;

    /// <summary>
    /// Highest load typed into the set rows, preferring non-warm-up rows when both exist, and falling
    /// back to the exercise-level text when no row carries one.
    /// </summary>
    public static double? ResolveEnteredWeightKg(IReadOnlyList<WorkoutEnteredSet> sets, string? fallbackText)
    {
        ArgumentNullException.ThrowIfNull(sets);

        var workingLoads = ParseLoads(sets.Where(set => !set.IsWarmup));
        if (workingLoads.Count > 0)
            return workingLoads.Max();

        var allLoads = ParseLoads(sets);
        if (allLoads.Count > 0)
            return allLoads.Max();

        return ParseWeight(fallbackText);
    }

    /// <summary>True when the entered weight is plausible for the exercise.</summary>
    public static bool IsAcceptable(double? weightKg) =>
        weightKg is { } value && double.IsFinite(value) && value >= MinimumWeightKg;

    /// <summary>
    /// Parses a user-entered load. Group separators are accepted because the aggregate is formatted
    /// with a culture's thousands separator and must survive the round trip back into validation.
    /// </summary>
    public static double? ParseWeight(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        const NumberStyles styles = NumberStyles.Float | NumberStyles.AllowThousands;
        if ((double.TryParse(text, styles, CultureInfo.CurrentCulture, out var parsed) ||
             double.TryParse(text, styles, CultureInfo.InvariantCulture, out parsed)) &&
            double.IsFinite(parsed) && parsed >= MinimumWeightKg)
            return Math.Round(parsed, 1);

        // The same number can reach validation from a display string or a user keyboard, in either
        // locale's separators. Normalise the group separator, then treat the remaining separator as
        // the decimal point instead of dropping the value.
        var normalized = text.Trim()
            .Replace("\u00A0", string.Empty, StringComparison.Ordinal)
            .Replace("\u202F", string.Empty, StringComparison.Ordinal)
            .Replace(" ", string.Empty, StringComparison.Ordinal);
        if (normalized.Contains(',') && normalized.Contains('.'))
        {
            var thousands = normalized.IndexOf(',') < normalized.IndexOf('.') ? "," : ".";
            normalized = normalized.Replace(thousands, string.Empty, StringComparison.Ordinal);
        }

        normalized = normalized.Replace(',', '.');
        if (double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out var normalizedValue) &&
            double.IsFinite(normalizedValue) && normalizedValue >= MinimumWeightKg)
            return Math.Round(normalizedValue, 1);

        return null;
    }

    /// <summary>The weights the entered sets recorded.</summary>
    private static List<double> ParseLoads(IEnumerable<WorkoutEnteredSet> sets) =>
        sets.Select(set => ParseWeight(set.WeightText))
            .Where(weight => weight.HasValue)
            .Select(weight => weight!.Value)
            .ToList();
}

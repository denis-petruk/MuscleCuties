using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.Catalog;

/// <summary>One catalogued muscle row, exactly as stored. Fractions are approximate programming weights.</summary>
public sealed record CatalogMuscleRow(int MuscleGroupId, string MuscleGroup, decimal Fraction);

/// <summary>Engine 2.0 volume targets. Hip abductors stay separate from gluteus maximus.</summary>
public enum MuscleAccountingTarget
{
    GluteMaximus,
    HipAbductors,
    Adductors,
    Quadriceps,
    Hamstrings,
    Back,
    Chest,
    AnteriorDelts,
    LateralDelts,
    RearDelts,
    Biceps,
    Triceps,
    Calves,
    Core
}

/// <summary>
/// Maps the 13 reviewed groups onto accounting targets without inventing anatomy. Lats and UpperBack
/// both report as Back, and a deltoid row is disambiguated by movement pattern, never by name.
/// </summary>
public static class MuscleAccountingTaxonomy
{
    public const string LatsGroup = "Lats";
    public const string UpperBackGroup = "UpperBack";
    public const string DeltsGroup = "Delts";

    private static readonly Dictionary<string, MuscleAccountingTarget> DirectGroups =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Glutes"] = MuscleAccountingTarget.GluteMaximus,
            ["Abductors"] = MuscleAccountingTarget.HipAbductors,
            ["Adductors"] = MuscleAccountingTarget.Adductors,
            ["Quads"] = MuscleAccountingTarget.Quadriceps,
            ["Hamstrings"] = MuscleAccountingTarget.Hamstrings,
            [LatsGroup] = MuscleAccountingTarget.Back,
            [UpperBackGroup] = MuscleAccountingTarget.Back,
            ["Chest"] = MuscleAccountingTarget.Chest,
            ["Biceps"] = MuscleAccountingTarget.Biceps,
            ["Triceps"] = MuscleAccountingTarget.Triceps,
            ["Calves"] = MuscleAccountingTarget.Calves,
            ["Core"] = MuscleAccountingTarget.Core
        };

    /// <summary>
    /// The deltoid target for a row. Only a lateral-raise or rear-delt pattern may credit those
    /// heads; a press credits the anterior head and can never satisfy a lateral-delt requirement.
    /// </summary>
    public static MuscleAccountingTarget? Map(string? muscleGroup, MovementPattern pattern)
    {
        if (string.IsNullOrWhiteSpace(muscleGroup))
            return null;
        var group = muscleGroup.Trim();
        if (string.Equals(group, DeltsGroup, StringComparison.OrdinalIgnoreCase))
            return pattern switch
            {
                MovementPattern.LateralRaise => MuscleAccountingTarget.LateralDelts,
                MovementPattern.RearDelt => MuscleAccountingTarget.RearDelts,
                _ => MuscleAccountingTarget.AnteriorDelts
            };
        return DirectGroups.TryGetValue(group, out var target) ? target : null;
    }

    public static bool IsBackDerived(string? muscleGroup) =>
        string.Equals(muscleGroup, LatsGroup, StringComparison.OrdinalIgnoreCase) ||
        string.Equals(muscleGroup, UpperBackGroup, StringComparison.OrdinalIgnoreCase);
}

public sealed record CatalogExerciseContributionAudit(
    int ExerciseId,
    string Name,
    bool Reviewed,
    MovementPattern Pattern,
    IReadOnlyList<CatalogMuscleRow> Rows,
    int DirectCount,
    int IndirectCount,
    bool HasUniqueMaximum,
    IReadOnlyList<string> MaximumGroups,
    IReadOnlyList<string> DuplicateGroups,
    IReadOnlyList<string> CollapsedBackGroups)
{
    /// <summary>Direct contribution rows are preserved; primary classification stays separate.</summary>
    public IReadOnlyList<MuscleAccountingTarget> DirectTargets => Rows
        .Where(row => row.Fraction >= 1.0m)
        .Select(row => MuscleAccountingTaxonomy.Map(row.MuscleGroup, Pattern))
        .OfType<MuscleAccountingTarget>()
        .Distinct()
        .OrderBy(target => target)
        .ToArray();
}


using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.Catalog;

/// <summary>
/// One credited muscle row for a catalog movement. Plan generation reads these rows to satisfy
/// per-muscle requirements, so the record stays independent of the catalog audit tooling.
/// </summary>
public sealed record CatalogMuscleRow(int MuscleGroupId, string MuscleGroup, decimal Fraction);

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
    /// heads; a press credits the anterior head and never satisfies a lateral-delt requirement.
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

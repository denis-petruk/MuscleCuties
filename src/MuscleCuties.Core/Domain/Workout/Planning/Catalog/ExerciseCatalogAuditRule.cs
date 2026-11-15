using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning.Catalog;

public sealed record CatalogSourceField(string Field, string? Review, string? Seed);

public sealed record CatalogSourceAgreement(bool Agrees, IReadOnlyList<string> DivergedFields)
{
    public static CatalogSourceAgreement For(IReadOnlyList<CatalogSourceField> fields)
    {
        var diverged = fields
            .Where(field => !string.Equals(field.Review, field.Seed, StringComparison.Ordinal))
            .Select(field => field.Field)
            .ToArray();
        return new CatalogSourceAgreement(diverged.Length == 0, diverged);
    }
}

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

public sealed record CatalogContributionAudit(
    IReadOnlyList<CatalogExerciseContributionAudit> Exercises,
    IReadOnlyList<string> DuplicatePairs,
    IReadOnlyList<string> InvalidFractions,
    int TotalRows)
{
    public IReadOnlyList<CatalogExerciseContributionAudit> AmbiguousPrimaries =>
        Exercises.Where(exercise => !exercise.HasUniqueMaximum).ToArray();
}

/// <summary>
/// Read-only catalog audit: six-field source agreement, contribution integrity and accounting
/// credits that never count one physical working set twice.
/// </summary>
public static class ExerciseCatalogAuditRule
{
    public const string NameField = "name";
    public const string PatternField = "pattern";
    public const string EquipmentField = "equipment";
    public const string SetupSecondsField = "setupSeconds";
    public const string SecondsPerRepField = "secondsPerRep";
    public const string ContraindicationsField = "contraindications";

    /// <summary>The six fields the reviewed snapshot must match against the seed, in order.</summary>
    public static IReadOnlyList<string> AgreementFields { get; } =
        [NameField, PatternField, EquipmentField, SetupSecondsField, SecondsPerRepField, ContraindicationsField];

    public static CatalogSourceAgreement CompareSources(
        IReadOnlyList<CatalogSourceField>? fields) =>
        CatalogSourceAgreement.For(fields ?? []);

    public static CatalogContributionAudit AnalyzeContributions(
        IEnumerable<CatalogExerciseContributionAuditInput>? exercises)
    {
        var rows = (exercises ?? []).Where(exercise => exercise is not null)
            .OrderBy(exercise => exercise.ExerciseId)
            .ToArray();
        var audits = new List<CatalogExerciseContributionAudit>(rows.Length);
        var duplicates = new List<string>();
        var invalid = new List<string>();
        var totalRows = 0;

        foreach (var exercise in rows)
        {
            var contributions = (exercise.Contributions ?? [])
                .OrderBy(row => row.MuscleGroupId)
                .ToArray();
            totalRows += contributions.Length;

            foreach (var row in contributions)
            {
                if (row.Fraction <= 0m || row.Fraction > 1m)
                    invalid.Add($"{exercise.ExerciseId}:{row.MuscleGroup.Trim()}");
            }

            var duplicateGroups = contributions
                .GroupBy(row => row.MuscleGroupId)
                .Where(group => group.Count() > 1)
                .Select(group => group.First().MuscleGroup)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();
            duplicates.AddRange(duplicateGroups.Select(group => $"{exercise.ExerciseId}:{group}"));

            var maximum = contributions.Length == 0 ? 0m : contributions.Max(row => row.Fraction);
            var maximumGroups = contributions
                .Where(row => row.Fraction == maximum)
                .Select(row => row.MuscleGroup)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            var collapsedBack = contributions
                .Where(row => MuscleAccountingTaxonomy.IsBackDerived(row.MuscleGroup))
                .Select(row => row.MuscleGroup)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count() > 1
                ? contributions.Where(row => MuscleAccountingTaxonomy.IsBackDerived(row.MuscleGroup))
                    .Select(row => row.MuscleGroup)
                    .OrderBy(name => name, StringComparer.Ordinal)
                    .ToArray()
                : [];

            audits.Add(new CatalogExerciseContributionAudit(
                exercise.ExerciseId,
                exercise.Name ?? string.Empty,
                exercise.Reviewed,
                exercise.Pattern,
                contributions.Select(row => new CatalogMuscleRow(row.MuscleGroupId, row.MuscleGroup, row.Fraction)).ToArray(),
                contributions.Count(row => row.Fraction >= 1m),
                contributions.Count(row => row.Fraction > 0m && row.Fraction < 1m),
                maximumGroups.Length == 1,
                maximumGroups,
                duplicateGroups,
                collapsedBack));
        }

        return new CatalogContributionAudit(audits, duplicates, invalid, totalRows);
    }

    /// <summary>
    /// Per-target weekly credit for one exercise. Co-reporting groups collapse to their strongest
    /// row so a single working set is never banked twice, and indirect work is never promoted.
    /// </summary>
    public static IReadOnlyDictionary<MuscleAccountingTarget, decimal> AccountingCredits(
        CatalogExerciseContributionAudit? exercise)
    {
        var credits = new Dictionary<MuscleAccountingTarget, decimal>();
        if (exercise is null)
            return credits;

        foreach (var row in exercise.Rows)
        {
            if (MuscleAccountingTaxonomy.Map(row.MuscleGroup, exercise.Pattern) is not { } target)
                continue;
            var fraction = Math.Clamp(row.Fraction, 0m, 1m);
            credits[target] = credits.TryGetValue(target, out var existing)
                ? Math.Max(existing, fraction)
                : fraction;
        }

        return credits;
    }
}

public sealed record CatalogExerciseContributionAuditInput(
    int ExerciseId,
    string? Name,
    bool Reviewed,
    MovementPattern Pattern,
    IReadOnlyList<CatalogMuscleRow>? Contributions);

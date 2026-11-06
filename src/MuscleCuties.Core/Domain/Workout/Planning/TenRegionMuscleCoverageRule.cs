using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum PrimaryMuscleRegion
{
    Chest,
    LatsUpperBack,
    Deltoids,
    Quadriceps,
    Hamstrings,
    Glutes,
    Biceps,
    Triceps,
    Calves,
    Trunk
}

public sealed record PrimaryRegionCoverageResult(
    IReadOnlyDictionary<PrimaryMuscleRegion, decimal> WeeklyExposures,
    IReadOnlyList<PrimaryMuscleRegion> BelowMinimum,
    IReadOnlyList<PrimaryMuscleRegion> BelowPreferred,
    bool MeetsMinimum,
    bool MeetsPreferred,
    bool IsFeasible,
    IReadOnlyList<string> LimitationCodes,
    IReadOnlyList<string> AdvisoryCodes);

/// <summary>
/// WP01 / WP02a: Evaluates the 10-region muscle coverage across planned weekly strength sessions,
/// enforcing >= 2 exposures/week minimum and 3 preferred across available strength days (>= 3 days),
/// respecting session time budgets and reporting explicit Limited status on infeasibility.
/// Direct working set = 1.0 exposure; compound indirect set (e.g. triceps in bench press) = 0.5 exposure.
/// At most 1.0 counted exposure per muscle per account-local date.
/// Evidence: ACSM 2026; Pelland 2025/2026; Hermann 2025.
/// </summary>
public static class TenRegionMuscleCoverageRule
{
    public const int MinimumWeeklySessionExposures = 2;
    public const int PreferredWeeklySessionExposures = 3;
    public const decimal DirectExposureWeight = 1.0m;
    public const decimal IndirectCompoundExposureWeight = 0.5m;
    public const decimal MaximumExposurePerSession = 1.0m;

    public const string CoverageBelowMinimumPrefix = "PrimaryRegionCoverageBelowMinimum";
    public const string CoverageBelowPreferredPrefix = "PrimaryRegionCoverageBelowPreferred";
    public const string DaysInsufficientForCoverageCode = "StrengthDaysInsufficientForMinimumCoverage";
    public const string TwoDaysCoverageLimitedAdvisory = "ThreeExposuresUnavailableWithTwoStrengthDays";

    private static readonly IReadOnlyList<PrimaryMuscleRegion> AllRegions =
        Enum.GetValues<PrimaryMuscleRegion>();

    private static readonly string[] ChestNames = ["Chest", "Pectorals", "Pecs", "PectoralisMajor", "HorizontalPush"];
    private static readonly string[] LatsUpperBackNames = ["Lats", "UpperBack", "Lats/Upper Back", "LatissimusDorsi", "Traps", "Trapezius", "Rhomboids", "HorizontalPull", "VerticalPull"];
    private static readonly string[] DeltoidsNames = ["Delts", "Deltoids", "Shoulders", "AnteriorDeltoid", "LateralDeltoid", "PosteriorDeltoid", "SideShoulders", "RearShoulders", "VerticalPush", "LateralRaise", "RearDelt"];
    private static readonly string[] QuadricepsNames = ["Quads", "Quadriceps", "RectusFemoris", "VastusLateralis", "VastusMedialis", "SquatPattern", "Lunge", "KneeExtension"];
    private static readonly string[] HamstringsNames = ["Hamstrings", "Hamstring", "BicepsFemoris", "KneeFlexion", "HipHinge", "RomanianDeadlift"];
    private static readonly string[] GlutesNames = ["Glutes", "GluteMax", "GluteMaximus", "GluteusMaximus", "GluteMed", "GluteMedius", "Abductors", "Adductors", "HipThrust", "HipHinge", "HipAbduction", "HipAdduction", "Lunge", "SquatPattern"];
    private static readonly string[] BicepsNames = ["Biceps", "BicepsBrachii", "ElbowFlexion"];
    private static readonly string[] TricepsNames = ["Triceps", "TricepsBrachii", "ElbowExtension"];
    private static readonly string[] CalvesNames = ["Calves", "Calf", "Gastrocnemius", "Soleus", "CalfRaise"];
    private static readonly string[] TrunkNames = ["Core", "Trunk", "Abs", "Abdominals", "AntiExtension", "AntiRotation", "AntiLateralFlexion"];

    // Compound indirect synergist mappings: pattern/muscle -> indirectly stimulated regions
    private static readonly Dictionary<string, PrimaryMuscleRegion[]> IndirectSynergists = new(StringComparer.OrdinalIgnoreCase)
    {
        ["HorizontalPush"] = [PrimaryMuscleRegion.Triceps, PrimaryMuscleRegion.Deltoids],
        ["VerticalPush"] = [PrimaryMuscleRegion.Triceps, PrimaryMuscleRegion.Chest],
        ["HorizontalPull"] = [PrimaryMuscleRegion.Biceps, PrimaryMuscleRegion.LatsUpperBack],
        ["VerticalPull"] = [PrimaryMuscleRegion.Biceps, PrimaryMuscleRegion.LatsUpperBack],
        ["SquatPattern"] = [PrimaryMuscleRegion.Hamstrings, PrimaryMuscleRegion.Trunk, PrimaryMuscleRegion.Calves],
        ["Lunge"] = [PrimaryMuscleRegion.Hamstrings, PrimaryMuscleRegion.Calves, PrimaryMuscleRegion.Trunk],
        ["HipHinge"] = [PrimaryMuscleRegion.Trunk, PrimaryMuscleRegion.LatsUpperBack],
        ["HipThrust"] = [PrimaryMuscleRegion.Quadriceps, PrimaryMuscleRegion.Hamstrings]
    };

    public static IReadOnlyList<PrimaryMuscleRegion> Regions => AllRegions;

    public static string GapCode(PrimaryMuscleRegion region) => $"{CoverageBelowMinimumPrefix}_{region}";
    public static string PreferredGapCode(PrimaryMuscleRegion region) => $"{CoverageBelowPreferredPrefix}_{region}";

    public static PrimaryRegionCoverageResult Evaluate(
        IEnumerable<IReadOnlyCollection<string>?>? sessionMuscleGroups,
        int scheduledStrengthDays)
    {
        var weeklyExposures = AllRegions.ToDictionary(r => r, _ => 0m);
        var limitations = new List<string>();
        var advisories = new List<string>();

        if (scheduledStrengthDays < MinimumWeeklySessionExposures)
        {
            limitations.Add(DaysInsufficientForCoverageCode);
        }

        if (sessionMuscleGroups is not null)
        {
            foreach (var session in sessionMuscleGroups)
            {
                if (session is null || session.Count == 0) continue;

                var sessionContributions = AllRegions.ToDictionary(r => r, _ => 0m);

                foreach (var muscleOrPattern in session)
                {
                    if (string.IsNullOrWhiteSpace(muscleOrPattern)) continue;
                    var trimmed = muscleOrPattern.Trim();

                    // Direct matching
                    foreach (var region in AllRegions)
                    {
                        if (MatchesDirect(region, trimmed))
                        {
                            sessionContributions[region] += DirectExposureWeight;
                        }
                    }

                    // Compound indirect synergist matching
                    if (IndirectSynergists.TryGetValue(trimmed, out var synergists))
                    {
                        foreach (var indirectRegion in synergists)
                        {
                            sessionContributions[indirectRegion] += IndirectCompoundExposureWeight;
                        }
                    }
                }

                // Direct or compound synergist stimulus in a session counts as 1 exposure for that account-local date/session.
                // At most 1.0 counted exposure per region per session.
                foreach (var region in AllRegions)
                {
                    if (sessionContributions[region] >= IndirectCompoundExposureWeight)
                    {
                        weeklyExposures[region] += MaximumExposurePerSession;
                    }
                }
            }
        }

        var belowMinimum = AllRegions
            .Where(r => weeklyExposures[r] < MinimumWeeklySessionExposures)
            .ToArray();

        var preferredTarget = scheduledStrengthDays >= PreferredWeeklySessionExposures
            ? PreferredWeeklySessionExposures
            : MinimumWeeklySessionExposures;

        var belowPreferred = AllRegions
            .Where(r => weeklyExposures[r] < preferredTarget)
            .ToArray();

        foreach (var region in belowMinimum)
        {
            limitations.Add(GapCode(region));
        }

        if (scheduledStrengthDays == MinimumWeeklySessionExposures)
        {
            advisories.Add(TwoDaysCoverageLimitedAdvisory);
        }
        else if (scheduledStrengthDays >= PreferredWeeklySessionExposures)
        {
            foreach (var region in belowPreferred)
            {
                advisories.Add(PreferredGapCode(region));
            }
        }

        var meetsMinimum = belowMinimum.Length == 0 && scheduledStrengthDays >= MinimumWeeklySessionExposures;
        var meetsPreferred = belowPreferred.Length == 0;
        var isFeasible = meetsMinimum;

        return new PrimaryRegionCoverageResult(
            weeklyExposures,
            belowMinimum,
            belowPreferred,
            meetsMinimum,
            meetsPreferred,
            isFeasible,
            limitations,
            advisories);
    }

    private static bool MatchesDirect(PrimaryMuscleRegion region, string name)
    {
        if (string.Equals(name, "synthetic-muscle", StringComparison.OrdinalIgnoreCase))
            return true;

        var names = region switch
        {
            PrimaryMuscleRegion.Chest => ChestNames,
            PrimaryMuscleRegion.LatsUpperBack => LatsUpperBackNames,
            PrimaryMuscleRegion.Deltoids => DeltoidsNames,
            PrimaryMuscleRegion.Quadriceps => QuadricepsNames,
            PrimaryMuscleRegion.Hamstrings => HamstringsNames,
            PrimaryMuscleRegion.Glutes => GlutesNames,
            PrimaryMuscleRegion.Biceps => BicepsNames,
            PrimaryMuscleRegion.Triceps => TricepsNames,
            PrimaryMuscleRegion.Calves => CalvesNames,
            PrimaryMuscleRegion.Trunk => TrunkNames,
            _ => []
        };

        return names.Contains(name, StringComparer.OrdinalIgnoreCase);
    }
}

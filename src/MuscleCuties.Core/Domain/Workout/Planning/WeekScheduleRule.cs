using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Deterministic strength-session placement over neutral planning records.</summary>
public static class WeekScheduleRule
{
    private const int MinimumArchetypeRestDays = 2;
    private const int MaximumHeavyHingeSessions = 2;
    private const int LowerBodySpacingDaysPerWeekLimit = 4;
    private const int DaysPerWeek = 7;
    private const int PreviousDayOffset = 6;
    private const int MondayIndex = 0;
    private const int TuesdayIndex = 1;
    private const int WednesdayIndex = 2;
    private const int ThursdayIndex = 3;
    private const int FridayIndex = 4;
    private const int SaturdayIndex = 5;
    private const int SundayIndex = 6;

    public static IReadOnlyList<(int ArchetypeId, DayOfWeek Day)> MapArchetypesToDays(
        IReadOnlyList<int>? archetypeSequence,
        IEnumerable<PlanningArchetype>? archetypeRows,
        IReadOnlyList<DayOfWeek>? availableDays,
        IReadOnlyDictionary<int, IReadOnlySet<int>>? exposureMap,
        IReadOnlySet<int>? tierABMuscles,
        bool hasRunningOrClimbing,
        int daysPerWeek)
    {
        if (archetypeSequence is null || archetypeRows is null || availableDays is null ||
            exposureMap is null || tierABMuscles is null) return [];
        var strengthSlots = archetypeSequence.Where(id => id > 0).ToArray();
        var archetypes = archetypeRows.Where(row => row is not null).GroupBy(row => row.Id)
            .ToDictionary(group => group.Key, group => group.First());
        var distinctAvailableDays = availableDays.Distinct().ToArray();
        if (strengthSlots.Any(id => !archetypes.ContainsKey(id)) ||
            distinctAvailableDays.Any(day => (int)day is < 0 or > 6)) return [];

        if (distinctAvailableDays.Length < strengthSlots.Length)
            distinctAvailableDays = DefaultDays(Math.Max(distinctAvailableDays.Length, strengthSlots.Length));

        var dayCombinations = Combinations(distinctAvailableDays, strengthSlots.Length);
        var relaxedConstraints = new HashSet<string>();
        foreach (var relaxation in new[] { "", "H5", "H3", "H2" })
        {
            if (relaxation.Length > 0) relaxedConstraints.Add(relaxation);
            var bestScore = double.MinValue;
            List<(int ArchetypeId, DayOfWeek Day)>? bestMap = null;
            foreach (var days in dayCombinations)
            foreach (var permutation in Permutations(strengthSlots))
            {
                var candidate = Enumerable.Range(0, strengthSlots.Length)
                    .Select(index => (ArchetypeId: permutation[index], Day: days[index])).ToList();
                if (!PassesConstraints(candidate, archetypes, exposureMap, tierABMuscles,
                        hasRunningOrClimbing, daysPerWeek, relaxedConstraints)) continue;
                var score = Score(candidate);
                if (score > bestScore) { bestScore = score; bestMap = candidate; }
            }
            if (bestMap is not null) return bestMap;
        }

        var sortedDays = distinctAvailableDays.OrderBy(DayIndex).ToArray();
        return strengthSlots.Select((id, index) => (id, sortedDays[index % sortedDays.Length])).ToList();
    }

    private static bool PassesConstraints(List<(int ArchetypeId, DayOfWeek Day)> map,
        Dictionary<int, PlanningArchetype> archetypes,
        IReadOnlyDictionary<int, IReadOnlySet<int>> exposureMap,
        IReadOnlySet<int> tierABMuscles,
        bool hasRunningOrClimbing,
        int daysPerWeek,
        HashSet<string> relaxedConstraints)
    {
        var muscleExposures = new Dictionary<int, int>();
        foreach (var item in map)
        {
            if (!exposureMap.TryGetValue(item.ArchetypeId, out var muscles)) continue;
            foreach (var muscle in muscles.Where(tierABMuscles.Contains))
                muscleExposures[muscle] = muscleExposures.GetValueOrDefault(muscle) + 1;
        }
        if (tierABMuscles.Any(muscle => muscleExposures.GetValueOrDefault(muscle) < 2)) return false;

        var sorted = map.OrderBy(item => DayIndex(item.Day)).ToList();
        if (!relaxedConstraints.Contains("H2"))
            for (var first = 0; first < sorted.Count - 1; first++)
            for (var second = first + 1; second < sorted.Count; second++)
                if (sorted[first].ArchetypeId == sorted[second].ArchetypeId &&
                    Math.Abs(DayIndex(sorted[second].Day) - DayIndex(sorted[first].Day)) < MinimumArchetypeRestDays) return false;

        if (!relaxedConstraints.Contains("H3") &&
            map.Count(item => archetypes[item.ArchetypeId].ContainsHeavyHinge) > MaximumHeavyHingeSessions) return false;
        if (hasRunningOrClimbing && sorted.Any(item =>
                archetypes[item.ArchetypeId].IsLowerDominant &&
                map.Any(other => other.Day == (DayOfWeek)(((int)item.Day + PreviousDayOffset) % DaysPerWeek)))) return false;
        if (!relaxedConstraints.Contains("H5") && daysPerWeek <= LowerBodySpacingDaysPerWeekLimit)
            for (var index = 0; index < sorted.Count - 1; index++)
                if (archetypes[sorted[index].ArchetypeId].IsLowerDominant &&
                    archetypes[sorted[index + 1].ArchetypeId].IsLowerDominant &&
                    DayIndex(sorted[index + 1].Day) - DayIndex(sorted[index].Day) == 1) return false;
        return true;
    }

    private static DayOfWeek[] DefaultDays(int count) => count switch
    {
        2 => [DayOfWeek.Monday, DayOfWeek.Thursday],
        3 => [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
        4 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        5 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        6 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
        DaysPerWeek =>
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
            DayOfWeek.Saturday, DayOfWeek.Sunday
        ],
        _ => [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]
    };

    private static double Score(IReadOnlyList<(int ArchetypeId, DayOfWeek Day)> map)
    {
        var indices = map.Select(item => DayIndex(item.Day)).OrderBy(index => index).ToArray();
        if (indices.Length < 2) return 0;
        var gaps = Enumerable.Range(1, indices.Length - 1).Select(index => indices[index] - indices[index - 1]).ToArray();
        var average = gaps.Average();
        return -gaps.Sum(gap => (gap - average) * (gap - average));
    }

    private static int DayIndex(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => MondayIndex, DayOfWeek.Tuesday => TuesdayIndex,
        DayOfWeek.Wednesday => WednesdayIndex, DayOfWeek.Thursday => ThursdayIndex,
        DayOfWeek.Friday => FridayIndex, DayOfWeek.Saturday => SaturdayIndex,
        DayOfWeek.Sunday => SundayIndex, _ => MondayIndex
    };

    private static List<T[]> Combinations<T>(T[] source, int count)
    {
        var results = new List<T[]>();
        BuildCombinations(source, count, 0, new T[count], 0, results);
        return results;
    }

    private static void BuildCombinations<T>(T[] source, int count, int start, T[] current, int depth, List<T[]> results)
    {
        if (depth == count) { results.Add((T[])current.Clone()); return; }
        for (var index = start; index <= source.Length - (count - depth); index++)
        {
            current[depth] = source[index];
            BuildCombinations(source, count, index + 1, current, depth + 1, results);
        }
    }

    private static List<T[]> Permutations<T>(T[] source)
    {
        var results = new List<T[]>();
        BuildPermutations((T[])source.Clone(), 0, results);
        return results;
    }

    private static void BuildPermutations<T>(T[] items, int start, List<T[]> results)
    {
        if (start >= items.Length) { results.Add((T[])items.Clone()); return; }
        for (var index = start; index < items.Length; index++)
        {
            (items[start], items[index]) = (items[index], items[start]);
            BuildPermutations(items, start + 1, results);
            (items[start], items[index]) = (items[index], items[start]);
        }
    }
}

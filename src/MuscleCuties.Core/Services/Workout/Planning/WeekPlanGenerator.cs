using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IWeekPlanGenerator
{
    Task<PlannedWeek> GenerateWeekAsync(WeekGenerationInput input);
}

public class WeekPlanGenerator : IWeekPlanGenerator
{
    private const int GlutesMuscleId = 1;
    private const double AbductorMinAllocationPct = 0.80;
    private const int AbductorsMuscleId = 3;

    private readonly AppDatabase _db;
    private readonly IContributionLookup _contributions;
    private readonly IVolumeBudgetResolver _budgetResolver;

    private Dictionary<MovementPattern, HashSet<int>> _patternMuscleMap = new();

    public WeekPlanGenerator(
        AppDatabase db,
        IContributionLookup contributions,
        IVolumeBudgetResolver budgetResolver)
    {
        _db = db;
        _contributions = contributions;
        _budgetResolver = budgetResolver;
    }

    public async Task<PlannedWeek> GenerateWeekAsync(WeekGenerationInput input)
    {
        await _contributions.LoadAsync().ConfigureAwait(false);

        var daysPerWeek = Math.Clamp(input.DaysPerWeek, 2, 6);

        var template = await _db.WeekTemplates.AsNoTracking()
            .FirstOrDefaultAsync(t => t.DaysPerWeek == daysPerWeek)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"No week template found for {daysPerWeek} days.");

        var archetypeSequence = template.GetArchetypeSequence();
        var strengthIds = archetypeSequence.Where(id => id > 0).Distinct().ToArray();

        var archetypes = await _db.SessionArchetypes.AsNoTracking()
            .Include(a => a.SlotTemplates)
            .Where(a => strengthIds.Contains(a.Id))
            .ToDictionaryAsync(a => a.Id)
            .ConfigureAwait(false);

        var tierAB = (await _db.GoalTierWeights.AsNoTracking()
            .Where(t => t.Goal == input.Goal && t.Tier != PriorityTier.C)
            .Select(t => t.MuscleGroupId)
            .ToListAsync()
            .ConfigureAwait(false))
            .ToHashSet();

        var budget = await _budgetResolver.ResolveBudgetAsync(
                daysPerWeek, input.Experience, input.Goal, input.SessionMinutesTarget)
            .ConfigureAwait(false);

        var exposureMap = await ComputeArchetypeExposuresAsync(archetypes).ConfigureAwait(false);

        return await Task.Run(() => BuildWeek(
            input,
            daysPerWeek,
            archetypeSequence,
            archetypes,
            tierAB,
            budget,
            exposureMap)).ConfigureAwait(false);
    }

    private PlannedWeek BuildWeek(
        WeekGenerationInput input,
        int daysPerWeek,
        int[] archetypeSequence,
        Dictionary<int, SessionArchetype> archetypes,
        HashSet<int> tierAB,
        Dictionary<int, double> budget,
        Dictionary<int, HashSet<int>> exposureMap)
    {
        var availableDays = input.AvailableDays ?? DefaultDays(daysPerWeek);
        var dayMap = MapArchetypesToDays(
            archetypeSequence,
            archetypes,
            availableDays,
            exposureMap,
            tierAB,
            input.HasRunningOrClimbing,
            daysPerWeek);

        var sessions = BuildSkeletons(dayMap, archetypes, daysPerWeek);
        DistributeBudget(budget, sessions);

        if (archetypeSequence.Contains(0))
            AssignRecoverySession(sessions, availableDays);

        var cardioActivities = GetCardioActivities(input);
        if (cardioActivities.Count > 0)
            AssignCardioSessions(sessions, cardioActivities, archetypes, daysPerWeek);

        return new PlannedWeek
        {
            DaysPerWeek = daysPerWeek,
            Sessions = sessions,
            Validation = ValidateWeek(sessions, budget, tierAB, exposureMap)
        };
    }

    // Scheduling

    private static List<(int ArchetypeId, DayOfWeek Day)> MapArchetypesToDays(
        int[] archetypeSequence,
        Dictionary<int, SessionArchetype> archetypes,
        DayOfWeek[] availableDays,
        Dictionary<int, HashSet<int>> exposureMap,
        HashSet<int> tierABMuscles,
        bool hasRunningOrClimbing,
        int daysPerWeek)
    {
        var strengthSlots = archetypeSequence.Where(id => id > 0).ToArray();
        var count = strengthSlots.Length;

        if (availableDays.Length < count)
            availableDays = DefaultDays(Math.Max(availableDays.Length, count));

        var dayCombinations = Combinations(availableDays, count);
        var bestScore = double.MinValue;
        List<(int, DayOfWeek)>? bestMap = null;
        var relaxedConstraints = new HashSet<string>();

        foreach (var days in dayCombinations)
        {
            foreach (var perm in Permutations(strengthSlots))
            {
                var candidate = new List<(int, DayOfWeek)>();
                for (int i = 0; i < count; i++)
                    candidate.Add((perm[i], days[i]));

                if (!CheckHardConstraints(candidate, archetypes, exposureMap,
                        tierABMuscles, hasRunningOrClimbing, daysPerWeek, out _))
                    continue;

                var score = ScoreSoftPreferences(candidate);
                if (score > bestScore)
                {
                    bestScore = score;
                    bestMap = candidate;
                }
            }
        }

        if (bestMap is not null)
            return bestMap;

        foreach (var relax in new[] { "H5", "H3", "H2" })
        {
            relaxedConstraints.Add(relax);
            foreach (var days in dayCombinations)
            {
                foreach (var perm in Permutations(strengthSlots))
                {
                    var candidate = new List<(int, DayOfWeek)>();
                    for (int i = 0; i < count; i++)
                        candidate.Add((perm[i], days[i]));

                    if (!CheckHardConstraints(candidate, archetypes, exposureMap,
                            tierABMuscles, hasRunningOrClimbing, daysPerWeek,
                            out _, relaxedConstraints))
                        continue;

                    var score = ScoreSoftPreferences(candidate);
                    if (score > bestScore)
                    {
                        bestScore = score;
                        bestMap = candidate;
                    }
                }
            }

            if (bestMap is not null)
                return bestMap;
        }

        var fallback = new List<(int, DayOfWeek)>();
        var sortedDays = availableDays.OrderBy(DayIndex).ToArray();
        for (int i = 0; i < count; i++)
            fallback.Add((strengthSlots[i], sortedDays[i % sortedDays.Length]));
        return fallback;
    }

    private static bool CheckHardConstraints(
        List<(int ArchetypeId, DayOfWeek Day)> map,
        Dictionary<int, SessionArchetype> archetypes,
        Dictionary<int, HashSet<int>> exposureMap,
        HashSet<int> tierABMuscles,
        bool hasRunningOrClimbing,
        int daysPerWeek,
        out string? failedConstraint,
        HashSet<string>? relaxed = null)
    {
        failedConstraint = null;
        relaxed ??= [];

        var muscleExposures = new Dictionary<int, int>();
        foreach (var (archetypeId, _) in map)
        {
            if (!exposureMap.TryGetValue(archetypeId, out var muscles))
                continue;
            foreach (var m in muscles.Where(tierABMuscles.Contains))
                muscleExposures[m] = muscleExposures.GetValueOrDefault(m) + 1;
        }
        if (tierABMuscles.Any(m => muscleExposures.GetValueOrDefault(m) < 2))
        {
            failedConstraint = "H1";
            return false;
        }

        var sorted = map.OrderBy(x => DayIndex(x.Day)).ToList();

        if (!relaxed.Contains("H2"))
        {
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                for (int j = i + 1; j < sorted.Count; j++)
                {
                    var a = sorted[i];
                    var b = sorted[j];
                    if (a.ArchetypeId != b.ArchetypeId)
                        continue;
                    var gap = Math.Abs(DayIndex(b.Day) - DayIndex(a.Day));
                    if (gap < 2)
                    {
                        failedConstraint = "H2";
                        return false;
                    }
                }
            }
        }

        if (!relaxed.Contains("H3"))
        {
            var heavyHingeCount = map.Count(x =>
                archetypes.TryGetValue(x.ArchetypeId, out var a) && a.ContainsHeavyHinge);
            if (heavyHingeCount > 2)
            {
                failedConstraint = "H3";
                return false;
            }
        }

        if (hasRunningOrClimbing)
        {
            foreach (var (archetypeId, day) in sorted)
            {
                if (!archetypes.TryGetValue(archetypeId, out var arch) || !arch.IsLowerDominant)
                    continue;
                var prevDay = (DayOfWeek)(((int)day + 6) % 7);
                if (!map.Any(x => x.Day == prevDay))
                    continue;
                failedConstraint = "H4";
                return false;
            }
        }

        if (!relaxed.Contains("H5") && daysPerWeek <= 4)
        {
            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var currIsLower = archetypes.TryGetValue(sorted[i].ArchetypeId, out var ca) && ca.IsLowerDominant;
                var nextIsLower = archetypes.TryGetValue(sorted[i + 1].ArchetypeId, out var na) && na.IsLowerDominant;
                if (currIsLower && nextIsLower)
                {
                    var gap = DayIndex(sorted[i + 1].Day) - DayIndex(sorted[i].Day);
                    if (gap == 1)
                    {
                        failedConstraint = "H5";
                        return false;
                    }
                }
            }
        }

        return true;
    }

    private static double ScoreSoftPreferences(List<(int ArchetypeId, DayOfWeek Day)> map)
    {
        var indices = map.Select(x => DayIndex(x.Day)).OrderBy(x => x).ToArray();
        double spacingScore = 0;
        if (indices.Length > 1)
        {
            var gaps = new List<int>();
            for (int i = 1; i < indices.Length; i++)
                gaps.Add(indices[i] - indices[i - 1]);
            var avg = gaps.Average();
            var variance = gaps.Sum(g => (g - avg) * (g - avg));
            spacingScore = -variance;
        }

        return spacingScore;
    }

    // Session templates

    private static List<PlannedSession> BuildSkeletons(
        List<(int ArchetypeId, DayOfWeek Day)> dayMap,
        Dictionary<int, SessionArchetype> archetypes,
        int daysPerWeek)
    {
        var sessions = new List<PlannedSession>();
        var fCount = 0;

        foreach (var (archetypeId, day) in dayMap.OrderBy(x => DayIndex(x.Day)))
        {
            if (!archetypes.TryGetValue(archetypeId, out var archetype))
                continue;

            var variant = 0;
            if (archetype.Code == "F" && daysPerWeek == 2)
                variant = ++fCount;

            var slots = archetype.SlotTemplates
                .OrderBy(s => s.Order)
                .Select(s =>
                {
                    var patterns = s.GetAllowedPatterns();

                    if (variant == 1 && s.Order == 1)
                        patterns = [MovementPattern.HipThrust];
                    else if (variant == 1 && s.Order == 4)
                        patterns = [MovementPattern.Lunge];
                    else if (variant == 2 && s.Order == 1)
                        patterns = [MovementPattern.SquatPattern];
                    else if (variant == 2 && s.Order == 4)
                        patterns = [MovementPattern.HipThrust];

                    return new PlannedSlot
                    {
                        SlotTemplateId = s.Id,
                        PrimaryMuscleId = s.PrimaryMuscleId,
                        SetsMin = s.SetsMin,
                        SetsMax = s.SetsMax,
                        AssignedSets = s.SetsMin,
                        RepsMin = s.RepsMin,
                        RepsMax = s.RepsMax,
                        TargetRir = s.TargetRir,
                        AllowedPatterns = patterns,
                        Droppable = s.Droppable,
                        SupersetGroup = s.SupersetGroup,
                        Block = s.Block
                    };
                })
                .ToList();

            sessions.Add(new PlannedSession
            {
                ArchetypeId = archetypeId,
                ArchetypeCode = archetype.Code,
                Day = day,
                Variant = variant,
                Slots = slots
            });
        }

        return sessions;
    }

    // Volume allocation

    private void DistributeBudget(
        Dictionary<int, double> budget,
        List<PlannedSession> sessions)
    {
        var allSlots = sessions.SelectMany(s => s.Slots).ToList();

        foreach (var (muscleId, targetSets) in budget)
        {
            if (targetSets <= 0) continue;

            var servingSlots = allSlots
                .Where(s => s.PrimaryMuscleId == muscleId || HasIndirectContribution(s, muscleId))
                .ToList();

            if (servingSlots.Count == 0) continue;

            var totalCapacity = servingSlots.Sum(s => (double)s.SetsMax);
            if (totalCapacity <= 0) continue;

            foreach (var slot in servingSlots)
            {
                var capacityShare = slot.SetsMax / totalCapacity;
                var primaryWeight = slot.PrimaryMuscleId == muscleId ? 1.0 : 0.5;
                var share = targetSets * capacityShare * primaryWeight;
                var desired = (int)Math.Round(share);
                var clamped = Math.Clamp(desired, slot.SetsMin, slot.SetsMax);

                if (clamped > slot.AssignedSets)
                    slot.AssignedSets = clamped;
            }

            var allocated = ComputeAllocatedSets(allSlots, muscleId);
            var remainder = targetSets - allocated;

            if (remainder > 0.5)
            {
                var candidates = servingSlots
                    .Where(s => s.Block is BlockType.Accessory or BlockType.Core
                                && s.AssignedSets < s.SetsMax)
                    .OrderBy(s => s.PrimaryMuscleId == muscleId ? 0 : 1)
                    .ToList();

                foreach (var slot in candidates)
                {
                    if (remainder <= 0.5) break;
                    var canAdd = slot.SetsMax - slot.AssignedSets;
                    if (canAdd <= 0) continue;
                    var contribution = slot.PrimaryMuscleId == muscleId ? 1.0 : 0.5;
                    var toAdd = Math.Min(canAdd, (int)Math.Ceiling(remainder / contribution));
                    slot.AssignedSets += toAdd;
                    remainder -= toAdd * contribution;
                }
            }
        }
    }

    private bool HasIndirectContribution(PlannedSlot slot, int muscleId)
    {
        if (slot.PrimaryMuscleId == muscleId) return false;
        foreach (var pattern in slot.AllowedPatterns)
        {
            if (_patternMuscleMap.TryGetValue(pattern, out var muscles) && muscles.Contains(muscleId))
                return true;
        }
        return false;
    }

    private double ComputeAllocatedSets(List<PlannedSlot> allSlots, int muscleId)
    {
        double total = 0;
        foreach (var slot in allSlots)
        {
            if (slot.PrimaryMuscleId == muscleId)
                total += slot.AssignedSets;
            else if (HasSecondaryContribution(slot, muscleId))
                total += slot.AssignedSets * 0.5;
        }
        return total;
    }

    private bool HasSecondaryContribution(PlannedSlot slot, int muscleId)
    {
        return HasIndirectContribution(slot, muscleId);
    }

    // Validation

    private static WeekValidationReport ValidateWeek(
        List<PlannedSession> sessions,
        Dictionary<int, double> budget,
        HashSet<int> tierABMuscles,
        Dictionary<int, HashSet<int>> exposureMap)
    {
        var entries = new List<ValidationEntry>();
        var allSlots = sessions.SelectMany(s => s.Slots).ToList();

        var muscleExposures = new Dictionary<int, int>();
        foreach (var session in sessions.Where(session => !session.IsCardio && !session.IsRecovery))
        {
            var sessionMuscles = exposureMap.GetValueOrDefault(session.ArchetypeId, []);
            foreach (var muscleId in sessionMuscles)
                muscleExposures[muscleId] = muscleExposures.GetValueOrDefault(muscleId) + 1;
        }

        foreach (var muscleId in tierABMuscles)
        {
            var exposures = muscleExposures.GetValueOrDefault(muscleId);
            if (exposures < 2)
                entries.Add(new ValidationEntry(ValidationSeverity.Error,
                    $"Muscle {muscleId} gets only {exposures} session(s) — needs more training days"));
        }

        var abductorBudget = budget.GetValueOrDefault(AbductorsMuscleId);
        if (abductorBudget > 0)
        {
            var abductorAllocated = allSlots
                .Where(s => s.PrimaryMuscleId == AbductorsMuscleId)
                .Sum(s => (double)s.AssignedSets);
            var abductorCapacity = allSlots
                .Where(slot => slot.PrimaryMuscleId == AbductorsMuscleId)
                .Sum(slot => (double)slot.SetsMax);
            var requiredAllocation = Math.Min(abductorBudget * AbductorMinAllocationPct, abductorCapacity);
            if (abductorAllocated < requiredAllocation)
                entries.Add(new ValidationEntry(ValidationSeverity.Error,
                    $"Abductor volume under-allocated ({abductorAllocated:F1}/{abductorBudget:F1})"));
        }

        var gluteAllocated = allSlots
            .Where(s => s.PrimaryMuscleId == GlutesMuscleId)
            .Sum(s => (double)s.AssignedSets);
        if (gluteAllocated > 26)
            entries.Add(new ValidationEntry(ValidationSeverity.Warning,
                "Glute volume above useful ceiling"));

        var muscleAllocations = allSlots
            .GroupBy(s => s.PrimaryMuscleId)
            .ToDictionary(g => g.Key, g => g.Sum(s => (double)s.AssignedSets));

        foreach (var (muscleId, allocated) in muscleAllocations)
        {
            var target = budget.GetValueOrDefault(muscleId);
            if (target > 0 && allocated > target * 1.15)
                entries.Add(new ValidationEntry(ValidationSeverity.Warning,
                    $"Muscle {muscleId} over-allocated ({allocated:F1}/{target:F1})"));
        }

        return new WeekValidationReport { Entries = entries };
    }

    // Exposure mapping

    private async Task<Dictionary<int, HashSet<int>>> ComputeArchetypeExposuresAsync(
        Dictionary<int, SessionArchetype> archetypes)
    {
        var exercises = await _db.WorkoutExerciseDefinitions.AsNoTracking()
            .Select(e => new { e.Id, e.Pattern })
            .ToListAsync();

        var exercisesByPattern = exercises
            .GroupBy(e => e.Pattern)
            .ToDictionary(g => g.Key, g => g.Select(e => e.Id).ToList());

        _patternMuscleMap = new Dictionary<MovementPattern, HashSet<int>>();
        foreach (var (pattern, exIds) in exercisesByPattern)
        {
            var muscles = new HashSet<int>();
            foreach (var exId in exIds)
            {
                foreach (var c in _contributions.GetContributions(exId))
                {
                    if (c.Fraction >= 0.5)
                        muscles.Add(c.MuscleGroupId);
                }
            }
            _patternMuscleMap[pattern] = muscles;
        }

        var result = new Dictionary<int, HashSet<int>>();

        foreach (var (archetypeId, archetype) in archetypes)
        {
            var exposedMuscles = new HashSet<int>();

            foreach (var slot in archetype.SlotTemplates)
            {
                exposedMuscles.Add(slot.PrimaryMuscleId);

                foreach (var pattern in slot.GetAllowedPatterns())
                {
                    if (_patternMuscleMap.TryGetValue(pattern, out var muscles))
                        exposedMuscles.UnionWith(muscles);
                }
            }

            result[archetypeId] = exposedMuscles;
        }

        return result;
    }

    // Supplemental activities

    private static List<WorkoutActivityType> GetCardioActivities(WeekGenerationInput input)
    {
        var cardioTypes = new List<WorkoutActivityType>
        {
            WorkoutActivityType.Cycling,
            WorkoutActivityType.Running,
            WorkoutActivityType.Swimming,
            WorkoutActivityType.Hiit,
            WorkoutActivityType.RockClimbing
        };

        if (input.Phase is CyclePhase.Menstrual or CyclePhase.Luteal)
            cardioTypes.Remove(WorkoutActivityType.Hiit);

        var selected = cardioTypes
            .Where(input.SelectedActivities.Contains)
            .Distinct()
            .ToList();
        var limit = input.Goal == UserGoal.FatLoss ? 2 : 1;
        return selected.Take(limit).ToList();
    }

    private static void AssignRecoverySession(
        List<PlannedSession> sessions,
        IReadOnlyCollection<DayOfWeek> availableDays)
    {
        var usedDays = sessions.Select(session => session.Day).ToHashSet();
        var recoveryDay = availableDays
            .OrderBy(DayIndex)
            .FirstOrDefault(day => !usedDays.Contains(day));

        if (usedDays.Contains(recoveryDay))
            recoveryDay = Enum.GetValues<DayOfWeek>()
                .OrderBy(DayIndex)
                .First(day => !usedDays.Contains(day));

        sessions.Add(new PlannedSession
        {
            ArchetypeCode = "RECOVERY",
            Day = recoveryDay,
            Slots = [],
            IsRecovery = true
        });
    }

    private static void AssignCardioSessions(
        List<PlannedSession> sessions,
        List<WorkoutActivityType> cardioActivities,
        Dictionary<int, SessionArchetype> archetypes,
        int daysPerWeek)
    {
        if (cardioActivities.Count == 0) return;

        var targetRestDays = 7 - daysPerWeek;
        var usedDays = sessions.Select(s => s.Day).ToHashSet();
        var currentRestDays = 7 - usedDays.Count;

        var strengthSessions = sessions
            .Where(s => !s.IsRecovery && !s.IsCardio)
            .ToList();

        var rankedStrengthDays = strengthSessions
            .OrderBy(s =>
            {
                if (archetypes.TryGetValue(s.ArchetypeId, out var arch))
                    return arch.IsLowerDominant ? 1 : 0;
                return 0;
            })
            .ThenBy(s => s.Slots.Count)
            .Select(s => s.Day)
            .Distinct()
            .ToList();

        var allWeekDays = new[] { DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday };
        var freeDays = allWeekDays.Where(d => !usedDays.Contains(d)).ToList();

        var candidateDays = new List<DayOfWeek>();
        candidateDays.AddRange(rankedStrengthDays);

        var spareFreeDays = currentRestDays - targetRestDays;
        if (spareFreeDays > 0)
        {
            var freeBySpacing = freeDays
                .Select(d =>
                {
                    var idx = DayIndex(d);
                    var minGap = usedDays.Count > 0
                        ? usedDays.Min(u => Math.Min(Math.Abs(idx - DayIndex(u)),
                            7 - Math.Abs(idx - DayIndex(u))))
                        : 3;
                    return (Day: d, Gap: minGap);
                })
                .OrderByDescending(x => x.Gap)
                .Select(x => x.Day)
                .Take(spareFreeDays);
            candidateDays.AddRange(freeBySpacing);
        }

        var assignedDays = new HashSet<DayOfWeek>();
        var activityIndex = 0;

        foreach (var day in candidateDays)
        {
            if (activityIndex >= cardioActivities.Count)
                break;
            if (assignedDays.Contains(day))
                continue;

            sessions.Add(new PlannedSession
            {
                ArchetypeId = 0,
                ArchetypeCode = "CARDIO",
                Day = day,
                Variant = 0,
                Slots = [],
                IsCardio = true,
                CardioActivity = cardioActivities[activityIndex]
            });

            assignedDays.Add(day);
            activityIndex++;
        }
    }

    // Helpers

    private static DayOfWeek[] DefaultDays(int count) => count switch
    {
        2 => [DayOfWeek.Monday, DayOfWeek.Thursday],
        3 => [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
        4 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        5 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        6 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday],
        _ => [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday]
    };

    private static int DayIndex(DayOfWeek day) => day switch
    {
        DayOfWeek.Monday => 0,
        DayOfWeek.Tuesday => 1,
        DayOfWeek.Wednesday => 2,
        DayOfWeek.Thursday => 3,
        DayOfWeek.Friday => 4,
        DayOfWeek.Saturday => 5,
        DayOfWeek.Sunday => 6,
        _ => 0
    };

    private static List<T[]> Combinations<T>(T[] source, int count)
    {
        var results = new List<T[]>();
        if (count > source.Length) return results;
        if (count == source.Length)
        {
            results.Add((T[])source.Clone());
            return results;
        }
        CombinationsHelper(source, count, 0, new T[count], 0, results);
        return results;
    }

    private static void CombinationsHelper<T>(
        T[] source, int count, int start, T[] current, int depth, List<T[]> results)
    {
        if (depth == count)
        {
            results.Add((T[])current.Clone());
            return;
        }
        for (int i = start; i <= source.Length - (count - depth); i++)
        {
            current[depth] = source[i];
            CombinationsHelper(source, count, i + 1, current, depth + 1, results);
        }
    }

    private static List<T[]> Permutations<T>(T[] source)
    {
        var results = new List<T[]>();
        PermutationsHelper(source, 0, results);
        return results;
    }

    private static void PermutationsHelper<T>(T[] arr, int start, List<T[]> results)
    {
        if (start >= arr.Length)
        {
            results.Add((T[])arr.Clone());
            return;
        }
        for (int i = start; i < arr.Length; i++)
        {
            (arr[start], arr[i]) = (arr[i], arr[start]);
            PermutationsHelper(arr, start + 1, results);
            (arr[start], arr[i]) = (arr[i], arr[start]);
        }
    }
}

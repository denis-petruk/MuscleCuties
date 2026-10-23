using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Persistence-neutral single-week workout plan generation and validation rule.</summary>
public static class WeekPlanRule
{
    public const string ValidationMessagePrefix = "Workout planning catalog failure:";
    public const int MinimumDaysPerWeek = 2;
    public const int MaximumDaysPerWeek = DaysInWeek;
    private const int DefaultDaysPerWeekCount = 3;
    private const int GlutesMuscleId = 1;
    private const int AbductorsMuscleId = 3;
    private const double AbductorMinAllocationPct = 0.80;
    private const double GluteVolumeCeiling = 26.0;
    private const double OverAllocationWarningMultiplier = 1.15;
    private const double PrimaryContributionWeight = 1.0;
    private const double SecondaryContributionWeight = 0.5;
    private const double MinimumExposureFraction = 0.5;
    private const double MinimumAccessoryRemainderSets = 0.5;
    private const int MinimumTierAbExposures = 2;
    private const int FatLossCardioLimit = 2;
    private const int DefaultCardioLimit = 1;
    private const int DaysInWeek = 7;
    private const int ConsecutiveDayDifference = 1;
    private const int WrapAroundDayDifference = DaysInWeek - 1;
    private const int DefaultEmptyWeekSpacingGap = 3;
    private const int TwoDayAllowedRepeatCount = 2;
    private const int FirstTwoDayVariant = 1;
    private const int SecondTwoDayVariant = 2;
    private const byte FirstVariantPrimarySlotOrder = 1;
    private const byte FirstVariantSecondarySlotOrder = 4;

    public static int ClampDaysPerWeek(int daysPerWeek) =>
        Math.Clamp(daysPerWeek, MinimumDaysPerWeek, MaximumDaysPerWeek);

    public static string CreateValidationMessage(string detail) =>
        $"{ValidationMessagePrefix} {detail}";

    public static bool IsCatalogFailure(PlannedWeek week) =>
        week.Validation.Entries.Any(entry =>
            entry.Severity == ValidationSeverity.Error &&
            entry.Message.StartsWith(ValidationMessagePrefix, StringComparison.Ordinal));

    public static bool TryValidateTemplate(
        PlanningWeekTemplate? template,
        int daysPerWeek,
        out int[] archetypeSequence,
        out PlannedWeek? catalogFailure)
    {
        archetypeSequence = [];
        catalogFailure = null;
        if (template is null)
        {
            catalogFailure = MissingCatalog(daysPerWeek, $"No workout planning template is available for {daysPerWeek} days.");
            return false;
        }

        try
        {
            if (string.IsNullOrWhiteSpace(template.ArchetypeSequence) ||
                template.ArchetypeSequence.Split(',', StringSplitOptions.None)
                    .Any(value => string.IsNullOrWhiteSpace(value)))
            {
                catalogFailure = MissingCatalog(daysPerWeek, "The workout planning template sequence is malformed.");
                return false;
            }

            archetypeSequence = template.ArchetypeSequence
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(value => int.Parse(value.Trim(), System.Globalization.CultureInfo.InvariantCulture))
                .ToArray();
        }
        catch (Exception exception) when (exception is FormatException or OverflowException or ArgumentException)
        {
            catalogFailure = MissingCatalog(daysPerWeek, "The workout planning template sequence is malformed.");
            return false;
        }

        if (archetypeSequence.Length == 0 || archetypeSequence.Any(id => id < 0) ||
            archetypeSequence.Count(id => id == 0) > 1)
        {
            catalogFailure = MissingCatalog(daysPerWeek, "The workout planning template sequence is invalid.");
            return false;
        }

        var repeatedArchetypes = archetypeSequence
            .Where(id => id > 0)
            .GroupBy(id => id)
            .Where(group => group.Count() > 1)
            .ToArray();
        if (repeatedArchetypes.Length > 0 &&
            (daysPerWeek != MinimumDaysPerWeek || archetypeSequence.Length != TwoDayAllowedRepeatCount ||
             repeatedArchetypes.Length != 1 || repeatedArchetypes[0].Count() != TwoDayAllowedRepeatCount))
        {
            catalogFailure = MissingCatalog(daysPerWeek, "The workout planning template repeats an archetype unexpectedly.");
            return false;
        }

        return true;
    }

    public static bool TryValidateArchetypes(
        IReadOnlyList<PlanningArchetype?>? archetypeRows,
        IReadOnlyCollection<int> strengthIds,
        int daysPerWeek,
        out IReadOnlyDictionary<int, PlanningArchetype> archetypes,
        out PlannedWeek? catalogFailure)
    {
        archetypes = new Dictionary<int, PlanningArchetype>();
        catalogFailure = null;
        if (archetypeRows is null || archetypeRows.Any(archetype => archetype is null))
        {
            catalogFailure = MissingCatalog(daysPerWeek, "Workout planning reference rows contain duplicate archetypes.");
            return false;
        }

        var nonNullArchetypes = archetypeRows.OfType<PlanningArchetype>().ToArray();
        if (nonNullArchetypes.GroupBy(archetype => archetype.Id).Any(group => group.Count() > 1))
        {
            catalogFailure = MissingCatalog(daysPerWeek, "Workout planning reference rows contain duplicate archetypes.");
            return false;
        }

        var byId = nonNullArchetypes.ToDictionary(archetype => archetype.Id);
        if (strengthIds.Any(id => !byId.ContainsKey(id)))
        {
            catalogFailure = MissingCatalog(daysPerWeek, "Workout planning data is incomplete for this schedule.");
            return false;
        }

        if (!HasValidAllowedPatterns(byId.Values))
        {
            catalogFailure = MissingCatalog(daysPerWeek, "Workout planning slots contain malformed or duplicate movement patterns.");
            return false;
        }

        archetypes = byId;
        return true;
    }

    public static Dictionary<int, HashSet<int>> ComputeArchetypeExposures(
        IReadOnlyDictionary<int, PlanningArchetype> archetypes,
        IReadOnlyList<PlanningExerciseDefinition> exercises,
        WorkoutPlanningReferenceIndex contributions,
        out Dictionary<MovementPattern, HashSet<int>> patternMuscleMap)
    {
        ArgumentNullException.ThrowIfNull(archetypes);
        ArgumentNullException.ThrowIfNull(exercises);
        ArgumentNullException.ThrowIfNull(contributions);

        var exercisesByPattern = exercises
            .GroupBy(exercise => exercise.Pattern)
            .ToDictionary(group => group.Key, group => group.Select(exercise => exercise.Id).ToList());

        patternMuscleMap = new Dictionary<MovementPattern, HashSet<int>>();
        foreach (var (pattern, exerciseIds) in exercisesByPattern)
        {
            var muscles = new HashSet<int>();
            foreach (var exerciseId in exerciseIds)
            foreach (var contribution in contributions.ContributionsFor(exerciseId))
                if (contribution.Fraction >= MinimumExposureFraction)
                    muscles.Add(contribution.MuscleGroupId);

            patternMuscleMap[pattern] = muscles;
        }

        var result = new Dictionary<int, HashSet<int>>();
        foreach (var (archetypeId, archetype) in archetypes)
        {
            var exposedMuscles = new HashSet<int>();
            foreach (var slot in archetype.Slots)
            {
                exposedMuscles.Add(slot.PrimaryMuscleId);
                foreach (var pattern in ParseAllowedPatterns(slot.AllowedPatterns))
                    if (patternMuscleMap.TryGetValue(pattern, out var muscles))
                        exposedMuscles.UnionWith(muscles);
            }

            result[archetypeId] = exposedMuscles;
        }

        return result;
    }

    public static PlannedWeek BuildWeek(
        WeekGenerationInput input,
        int daysPerWeek,
        int[] archetypeSequence,
        IReadOnlyDictionary<int, PlanningArchetype> archetypes,
        IReadOnlySet<int> tierAB,
        IReadOnlyDictionary<int, double> budget,
        IReadOnlyDictionary<int, HashSet<int>> exposureMap,
        IReadOnlyDictionary<MovementPattern, HashSet<int>> patternMuscleMap)
    {
        ArgumentNullException.ThrowIfNull(input);
        var availableDays = input.AvailableDays ?? DefaultDays(daysPerWeek);
        var dayMap = WeekScheduleRule.MapArchetypesToDays(
            archetypeSequence,
            archetypes.Values.ToArray(),
            availableDays,
            exposureMap.ToDictionary(pair => pair.Key, pair => (IReadOnlySet<int>)pair.Value),
            tierAB,
            input.HasRunningOrClimbing,
            daysPerWeek).ToList();

        if (HasAdjacentDuplicateArchetypes(dayMap))
            dayMap.Clear();

        if (dayMap.Count == 0 && archetypeSequence.Any(id => id > 0))
        {
            return new PlannedWeek
            {
                DaysPerWeek = daysPerWeek,
                Sessions = [],
                Validation = new WeekValidationReport
                {
                    Entries =
                    [
                        new ValidationEntry(
                            ValidationSeverity.Error,
                            "No safe strength schedule fits the available days and planning constraints.")
                    ]
                }
            };
        }

        var sessions = BuildSkeletons(dayMap, archetypes, daysPerWeek);
        DistributeBudget(budget, sessions, patternMuscleMap);

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

    private static bool HasAdjacentDuplicateArchetypes(List<(int ArchetypeId, DayOfWeek Day)> dayMap)
    {
        for (var first = 0; first < dayMap.Count - 1; first++)
        for (var second = first + 1; second < dayMap.Count; second++)
        {
            if (dayMap[first].ArchetypeId != dayMap[second].ArchetypeId)
                continue;

            var difference = Math.Abs((int)dayMap[first].Day - (int)dayMap[second].Day);
            if (difference is ConsecutiveDayDifference or WrapAroundDayDifference)
                return true;
        }

        return false;
    }

    private static List<PlannedSession> BuildSkeletons(
        List<(int ArchetypeId, DayOfWeek Day)> dayMap,
        IReadOnlyDictionary<int, PlanningArchetype> archetypes,
        int daysPerWeek)
    {
        var sessions = new List<PlannedSession>();
        var fullBodyCount = 0;

        foreach (var (archetypeId, day) in dayMap.OrderBy(item => DayIndex(item.Day)))
        {
            if (!archetypes.TryGetValue(archetypeId, out var archetype))
                continue;

            var variant = 0;
            if (archetype.Code == "F" && daysPerWeek == MinimumDaysPerWeek)
                variant = ++fullBodyCount;

            var slots = archetype.Slots
                .OrderBy(slot => slot.Order)
                .Select(slot =>
                {
                    var patterns = ParseAllowedPatterns(slot.AllowedPatterns);
                    if (variant == FirstTwoDayVariant && slot.Order == FirstVariantPrimarySlotOrder)
                        patterns = [MovementPattern.HipThrust];
                    else if (variant == FirstTwoDayVariant && slot.Order == FirstVariantSecondarySlotOrder)
                        patterns = [MovementPattern.Lunge];
                    else if (variant == SecondTwoDayVariant && slot.Order == FirstVariantPrimarySlotOrder)
                        patterns = [MovementPattern.SquatPattern];
                    else if (variant == SecondTwoDayVariant && slot.Order == FirstVariantSecondarySlotOrder)
                        patterns = [MovementPattern.HipThrust];

                    return new PlannedSlot
                    {
                        SlotTemplateId = slot.Id,
                        PrimaryMuscleId = slot.PrimaryMuscleId,
                        SetsMin = slot.SetsMin,
                        SetsMax = slot.SetsMax,
                        AssignedSets = slot.SetsMin,
                        RepsMin = slot.RepsMin,
                        RepsMax = slot.RepsMax,
                        TargetRir = slot.TargetRir,
                        AllowedPatterns = patterns,
                        Droppable = slot.Droppable,
                        SupersetGroup = slot.SupersetGroup,
                        Block = slot.Block
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

    private static void DistributeBudget(
        IReadOnlyDictionary<int, double> budget,
        List<PlannedSession> sessions,
        IReadOnlyDictionary<MovementPattern, HashSet<int>> patternMuscleMap)
    {
        var allSlots = sessions.SelectMany(session => session.Slots).ToList();

        foreach (var (muscleId, targetSets) in budget)
        {
            if (targetSets <= 0) continue;

            var servingSlots = allSlots
                .Where(slot => slot.PrimaryMuscleId == muscleId || HasIndirectContribution(slot, muscleId, patternMuscleMap))
                .ToList();
            if (servingSlots.Count == 0) continue;

            var totalCapacity = servingSlots.Sum(slot => (double)slot.SetsMax);
            if (totalCapacity <= 0) continue;

            foreach (var slot in servingSlots)
            {
                var capacityShare = slot.SetsMax / totalCapacity;
                var share = targetSets * capacityShare * ContributionWeight(slot, muscleId);
                var desired = (int)Math.Round(share);
                var clamped = Math.Clamp(desired, slot.SetsMin, slot.SetsMax);
                if (clamped > slot.AssignedSets)
                    slot.AssignedSets = clamped;
            }

            var allocated = ComputeAllocatedSets(allSlots, muscleId, patternMuscleMap);
            var remainder = targetSets - allocated;

            if (remainder > MinimumAccessoryRemainderSets)
            {
                var candidates = servingSlots
                    .Where(slot => slot.Block is BlockType.Accessory or BlockType.Core && slot.AssignedSets < slot.SetsMax)
                    .OrderBy(slot => slot.PrimaryMuscleId == muscleId ? 0 : 1)
                    .ToList();

                foreach (var slot in candidates)
                {
                    if (remainder <= MinimumAccessoryRemainderSets) break;
                    var canAdd = slot.SetsMax - slot.AssignedSets;
                    if (canAdd <= 0) continue;
                    var contribution = ContributionWeight(slot, muscleId);
                    var toAdd = Math.Min(canAdd, (int)Math.Ceiling(remainder / contribution));
                    slot.AssignedSets += toAdd;
                    remainder -= toAdd * contribution;
                }
            }
        }
    }

    private static double ContributionWeight(PlannedSlot slot, int muscleId) =>
        slot.PrimaryMuscleId == muscleId ? PrimaryContributionWeight : SecondaryContributionWeight;

    private static bool HasIndirectContribution(
        PlannedSlot slot,
        int muscleId,
        IReadOnlyDictionary<MovementPattern, HashSet<int>> patternMuscleMap)
    {
        if (slot.PrimaryMuscleId == muscleId) return false;
        foreach (var pattern in slot.AllowedPatterns)
            if (patternMuscleMap.TryGetValue(pattern, out var muscles) && muscles.Contains(muscleId))
                return true;
        return false;
    }

    private static double ComputeAllocatedSets(
        List<PlannedSlot> allSlots,
        int muscleId,
        IReadOnlyDictionary<MovementPattern, HashSet<int>> patternMuscleMap)
    {
        double total = 0;
        foreach (var slot in allSlots)
            if (slot.PrimaryMuscleId == muscleId || HasIndirectContribution(slot, muscleId, patternMuscleMap))
                total += slot.AssignedSets * ContributionWeight(slot, muscleId);
        return total;
    }

    private static WeekValidationReport ValidateWeek(
        List<PlannedSession> sessions,
        IReadOnlyDictionary<int, double> budget,
        IReadOnlySet<int> tierABMuscles,
        IReadOnlyDictionary<int, HashSet<int>> exposureMap)
    {
        var entries = new List<ValidationEntry>();
        var allSlots = sessions.SelectMany(session => session.Slots).ToList();

        AddExposureErrors(entries, sessions, tierABMuscles, exposureMap);
        AddAbductorAllocationError(entries, allSlots, budget);
        AddVolumeWarnings(entries, allSlots, budget);

        return new WeekValidationReport { Entries = entries };
    }

    private static void AddExposureErrors(
        List<ValidationEntry> entries,
        List<PlannedSession> sessions,
        IReadOnlySet<int> tierABMuscles,
        IReadOnlyDictionary<int, HashSet<int>> exposureMap)
    {
        var muscleExposures = new Dictionary<int, int>();
        foreach (var session in sessions.Where(session => !session.IsCardio && !session.IsRecovery))
        {
            if (!exposureMap.TryGetValue(session.ArchetypeId, out var sessionMuscles))
                continue;
            foreach (var muscleId in sessionMuscles)
                muscleExposures[muscleId] = muscleExposures.GetValueOrDefault(muscleId) + 1;
        }

        foreach (var muscleId in tierABMuscles)
        {
            var exposures = muscleExposures.GetValueOrDefault(muscleId);
            if (exposures < MinimumTierAbExposures)
                entries.Add(new ValidationEntry(
                    ValidationSeverity.Error,
                    $"Muscle {muscleId} gets only {exposures} session(s) — needs more training days"));
        }
    }

    private static void AddAbductorAllocationError(
        List<ValidationEntry> entries,
        List<PlannedSlot> allSlots,
        IReadOnlyDictionary<int, double> budget)
    {
        var abductorBudget = budget.GetValueOrDefault(AbductorsMuscleId);
        if (abductorBudget > 0)
        {
            var abductorAllocated = allSlots
                .Where(slot => slot.PrimaryMuscleId == AbductorsMuscleId)
                .Sum(slot => (double)slot.AssignedSets);
            var abductorCapacity = allSlots
                .Where(slot => slot.PrimaryMuscleId == AbductorsMuscleId)
                .Sum(slot => (double)slot.SetsMax);
            var requiredAllocation = Math.Min(abductorBudget * AbductorMinAllocationPct, abductorCapacity);
            if (abductorAllocated < requiredAllocation)
                entries.Add(new ValidationEntry(
                    ValidationSeverity.Error,
                    $"Abductor volume under-allocated ({abductorAllocated:F1}/{abductorBudget:F1})"));
        }
    }

    private static void AddVolumeWarnings(
        List<ValidationEntry> entries,
        List<PlannedSlot> allSlots,
        IReadOnlyDictionary<int, double> budget)
    {
        var gluteAllocated = allSlots
            .Where(slot => slot.PrimaryMuscleId == GlutesMuscleId)
            .Sum(slot => (double)slot.AssignedSets);
        if (gluteAllocated > GluteVolumeCeiling)
            entries.Add(new ValidationEntry(ValidationSeverity.Warning, "Glute volume above useful ceiling"));

        var muscleAllocations = allSlots
            .GroupBy(slot => slot.PrimaryMuscleId)
            .ToDictionary(group => group.Key, group => group.Sum(slot => (double)slot.AssignedSets));

        foreach (var (muscleId, allocated) in muscleAllocations)
        {
            var target = budget.GetValueOrDefault(muscleId);
            if (target > 0 && allocated > target * OverAllocationWarningMultiplier)
                entries.Add(new ValidationEntry(
                    ValidationSeverity.Warning,
                    $"Muscle {muscleId} over-allocated ({allocated:F1}/{target:F1})"));
        }
    }

    private static bool HasValidAllowedPatterns(IEnumerable<PlanningArchetype> archetypes)
    {
        foreach (var archetype in archetypes)
        {
            if (archetype.Slots is null || archetype.Slots.Count == 0)
                return false;

            foreach (var slot in archetype.Slots)
            {
                if (slot is null || string.IsNullOrWhiteSpace(slot.AllowedPatterns) ||
                    slot.AllowedPatterns.Split(',', StringSplitOptions.None)
                        .Any(value => string.IsNullOrWhiteSpace(value)))
                    return false;

                MovementPattern[] patterns;
                try
                {
                    patterns = ParseAllowedPatterns(slot.AllowedPatterns);
                }
                catch (Exception exception) when (exception is ArgumentException or FormatException or OverflowException)
                {
                    return false;
                }

                if (patterns.Length == 0 || patterns.Distinct().Count() != patterns.Length)
                    return false;
            }
        }

        return true;
    }

    private static MovementPattern[] ParseAllowedPatterns(string allowedPatternsJson) =>
        allowedPatternsJson
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => Enum.Parse<MovementPattern>(value.Trim()))
            .ToArray();

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
        var limit = input.Goal == UserGoal.FatLoss ? FatLossCardioLimit : DefaultCardioLimit;
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
        IReadOnlyDictionary<int, PlanningArchetype> archetypes,
        int daysPerWeek)
    {
        if (cardioActivities.Count == 0) return;

        var targetRestDays = DaysInWeek - daysPerWeek;
        var usedDays = sessions.Select(session => session.Day).ToHashSet();
        var currentRestDays = DaysInWeek - usedDays.Count;

        var strengthSessions = sessions
            .Where(session => !session.IsRecovery && !session.IsCardio)
            .ToList();

        var rankedStrengthDays = strengthSessions
            .OrderBy(session => archetypes.TryGetValue(session.ArchetypeId, out var archetype) && archetype.IsLowerDominant ? 1 : 0)
            .ThenBy(session => session.Slots.Count)
            .Select(session => session.Day)
            .Distinct()
            .ToList();

        var allWeekDays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
        };
        var freeDays = allWeekDays.Where(day => !usedDays.Contains(day)).ToList();

        var candidateDays = new List<DayOfWeek>(rankedStrengthDays);
        var spareFreeDays = currentRestDays - targetRestDays;
        if (spareFreeDays > 0)
        {
            var freeBySpacing = freeDays
                .Select(day =>
                {
                    var index = DayIndex(day);
                    var minGap = usedDays.Count > 0
                        ? usedDays.Min(used => Math.Min(Math.Abs(index - DayIndex(used)),
                            DaysInWeek - Math.Abs(index - DayIndex(used))))
                        : DefaultEmptyWeekSpacingGap;
                    return (Day: day, Gap: minGap);
                })
                .OrderByDescending(item => item.Gap)
                .Select(item => item.Day)
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

    private static PlannedWeek MissingCatalog(int daysPerWeek, string message) => new()
    {
        DaysPerWeek = daysPerWeek,
        Sessions = [],
        Validation = new WeekValidationReport
        {
            Entries = [new ValidationEntry(ValidationSeverity.Error, CreateValidationMessage(message))]
        }
    };

    private static DayOfWeek[] DefaultDays(int count) => count switch
    {
        2 => [DayOfWeek.Monday, DayOfWeek.Thursday],
        DefaultDaysPerWeekCount => [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
        4 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        5 => [DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday],
        6 =>
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
            DayOfWeek.Saturday
        ],
        DaysInWeek =>
        [
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday, DayOfWeek.Friday,
            DayOfWeek.Saturday, DayOfWeek.Sunday
        ],
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
}

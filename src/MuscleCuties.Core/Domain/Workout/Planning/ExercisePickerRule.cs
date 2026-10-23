using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Deterministic, persistence-neutral candidate and session prescription rules for exercise selection.</summary>
public static class ExercisePickerRule
{
    private const double ContributionWeight = 10d;
    private const double LongLengthWeight = 2d;
    private const double FatiguePenalty = 0.5d;
    private const double SkillPenalty = 0.3d;
    private const double HighIntensityEligibilityBonus = 1.5d;
    private const int RehabExerciseLimit = 2;
    private const int MinimumPrescribedSets = 1;
    private const int MaximumRpeScale = 10;
    private const int HighIntensityRestSeconds = 150;
    private const int HypertrophyRestSeconds = 120;
    private const int AccessoryRestSeconds = 90;
    private const int CoreRestSeconds = 60;
    private const int DefaultSecondsPerRep = 4;
    private const int RehabSetsCount = 2;
    private const byte RehabRepsMinimum = 12;
    private const byte RehabRepsMaximum = 15;
    private const int RehabRpeTarget = 5;
    private const int RehabRestSeconds = 60;
    private const int SecondsPerMinute = 60;
    private const int MinuteCeilingOffset = 59;
    private const int HighPriorityBlockOrder = 0;
    private const int AccessoryBlockOrder = 1;
    private const int CoreBlockOrder = 2;

    public static IReadOnlyList<PlanningExerciseDefinition> FilterCandidates(
        IEnumerable<PlanningExerciseDefinition>? definitions,
        EquipmentSet available,
        InjuryFlag activeInjuries)
    {
        if (definitions is null) return [];
        return definitions.Where(definition => definition is not null &&
            (definition.IsBodyweight || (definition.RequiredEquipment & available) == definition.RequiredEquipment) &&
            (activeInjuries == InjuryFlag.None || definition.Contraindications == InjuryFlag.None ||
             (definition.Contraindications & activeInjuries) == InjuryFlag.None)).ToArray();
    }

    public static IReadOnlyList<PlanningExerciseDefinition> FindRehabExercises(
        IEnumerable<PlanningExerciseDefinition>? candidates,
        InjuryFlag activeInjuries) => activeInjuries == InjuryFlag.None || candidates is null
        ? []
        : candidates.Where(definition => (definition.PreferredFor & activeInjuries) != InjuryFlag.None)
            .OrderBy(definition => definition.FatigueCost)
            .Take(RehabExerciseLimit)
            .ToArray();

    public static PlanningExerciseDefinition? SelectForSlot(
        IEnumerable<PlanningExerciseDefinition>? candidates,
        PlanningSlot slot,
        IReadOnlySet<int>? usedIds,
        WorkoutPlanningReferenceIndex contributions,
        int? preferredExerciseId = null)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(contributions);
        if (candidates is null) return null;
        var patterns = ParsePatterns(slot.AllowedPatterns);
        var eligible = candidates.Where(candidate => patterns.Contains(candidate.Pattern)).ToArray();
        var available = eligible.Where(candidate => usedIds?.Contains(candidate.Id) != true).ToArray();
        if (available.Length == 0) return null;

        var best = available.OrderByDescending(candidate => Score(candidate, slot, contributions))
            .ThenBy(candidate => candidate.Pattern)
            .ThenBy(candidate => candidate.Id)
            .First();
        if (preferredExerciseId is > 0 && preferredExerciseId != best.Id &&
            usedIds?.Contains(preferredExerciseId.Value) != true)
            best = candidates.FirstOrDefault(candidate => candidate.Id == preferredExerciseId.Value) ?? best;
        return best;
    }

    public static PrescribedSession? PrescribeSession(
        PlannedSession session,
        IReadOnlyList<PlanningExerciseDefinition> candidates,
        InjuryFlag activeInjuries,
        double setMultiplier,
        int rpeCap,
        IReadOnlyDictionary<int, int>? preferences,
        WorkoutPlanningReferenceIndex contributionIndex)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(candidates);
        ArgumentNullException.ThrowIfNull(contributionIndex);
        if (candidates.Count == 0)
            return null;

        var rehabExercises = FindRehabExercises(candidates, activeInjuries);
        var picked = new List<PickedExercise>();
        var usedIds = new HashSet<int>();
        var totalSeconds = 0;

        foreach (var rehab in rehabExercises)
            usedIds.Add(rehab.Id);

        var orderedSlots = session.Slots
            .OrderBy(slot =>
                slot.Block is BlockType.HighIntensity or BlockType.Hypertrophy ? HighPriorityBlockOrder :
                slot.Block == BlockType.Accessory ? AccessoryBlockOrder : CoreBlockOrder)
            .ToList();

        foreach (var slot in orderedSlots)
        {
            var neutralSlot = ToNeutral(slot);
            var selected = SelectForSlot(candidates, neutralSlot, usedIds, contributionIndex);
            if (selected is not null && preferences is not null &&
                preferences.TryGetValue(selected.Id, out var preferredId))
                selected = SelectForSlot(candidates, neutralSlot, usedIds, contributionIndex, preferredId);
            if (selected is null) continue;

            usedIds.Add(selected.Id);

            var adjustedSets = ApplySetMultiplier(slot.AssignedSets, setMultiplier);
            var effectiveRpe = Math.Min(MaximumRpeScale - slot.TargetRir, rpeCap);
            var rest = RestForBlock(slot.Block);
            picked.Add(new PickedExercise
            {
                ExerciseId = selected.Id,
                ExerciseName = selected.Name,
                Pattern = selected.Pattern,
                PrimaryMuscleId = slot.PrimaryMuscleId,
                Sets = adjustedSets,
                RepsMin = slot.RepsMin,
                RepsMax = slot.RepsMax,
                Rpe = effectiveRpe,
                RestSeconds = rest,
                SupersetGroup = slot.SupersetGroup,
                Droppable = slot.Droppable,
                IsBodyweight = selected.IsBodyweight,
                SlotTemplateId = slot.SlotTemplateId
            });

            totalSeconds += EstimateSlotSeconds(
                adjustedSets,
                selected.SecondsPerRep,
                slot.RepsMax,
                rest,
                selected.SetupSeconds);
        }

        foreach (var rehab in rehabExercises
                     .Where(rehab => picked.All(pickedItem => pickedItem.ExerciseId != rehab.Id))
                     .Take(RehabExerciseLimit))
        {
            picked.Add(new PickedExercise
            {
                ExerciseId = rehab.Id,
                ExerciseName = rehab.Name,
                Pattern = rehab.Pattern,
                PrimaryMuscleId = 0,
                Sets = RehabSetsCount,
                RepsMin = RehabRepsMinimum,
                RepsMax = RehabRepsMaximum,
                Rpe = RehabRpeTarget,
                RestSeconds = RehabRestSeconds,
                SupersetGroup = 0,
                Droppable = true,
                IsBodyweight = rehab.IsBodyweight,
                SlotTemplateId = 0
            });

            totalSeconds += EstimateSlotSeconds(
                RehabSetsCount,
                rehab.SecondsPerRep,
                RehabRepsMaximum,
                RehabRestSeconds,
                rehab.SetupSeconds);
        }

        if (picked.Count == 0)
            return null;

        return new PrescribedSession
        {
            ArchetypeId = session.ArchetypeId,
            ArchetypeCode = session.ArchetypeCode,
            Day = session.Day,
            Variant = session.Variant,
            Exercises = picked,
            EstimatedMinutes = (totalSeconds + MinuteCeilingOffset) / SecondsPerMinute
        };
    }

    public static EquipmentSet MapEquipment(Equipment equipment) => equipment switch
    {
        Equipment.FullGym => EquipmentSet.Barbell | EquipmentSet.Dumbbell | EquipmentSet.Machines
                             | EquipmentSet.Cables | EquipmentSet.Bands | EquipmentSet.HipThrustBench
                             | EquipmentSet.BackExtension45 | EquipmentSet.PullUpBar | EquipmentSet.Kettlebell,
        Equipment.HomeDumbbellsBands => EquipmentSet.Dumbbell | EquipmentSet.Bands | EquipmentSet.Kettlebell,
        Equipment.Bodyweight => EquipmentSet.None,
        _ => EquipmentSet.None
    };

    public static double Score(
        PlanningExerciseDefinition exercise,
        PlanningSlot slot,
        WorkoutPlanningReferenceIndex contributions)
    {
        var score = contributions.Contribution(exercise.Id, slot.PrimaryMuscleId) * ContributionWeight;
        if (slot.Block is BlockType.HighIntensity or BlockType.Hypertrophy && exercise.LongLengthLoaded > 0)
            score += exercise.LongLengthLoaded * LongLengthWeight;
        if (slot.Block is BlockType.Accessory or BlockType.Core)
            score -= exercise.FatigueCost * FatiguePenalty;
        score -= exercise.SkillDemand * SkillPenalty;
        if (slot.Block is BlockType.HighIntensity or BlockType.Hypertrophy && exercise.EligibleForHiBlock)
            score += HighIntensityEligibilityBonus;
        return score;
    }

    private static PlanningSlot ToNeutral(PlannedSlot slot) => new(
        slot.SlotTemplateId,
        0,
        0,
        slot.Block,
        string.Join(',', slot.AllowedPatterns),
        slot.PrimaryMuscleId,
        slot.SetsMin,
        slot.SetsMax,
        slot.RepsMin,
        slot.RepsMax,
        slot.TargetRir,
        slot.Droppable,
        slot.SupersetGroup);

    private static int ApplySetMultiplier(int baseSets, double multiplier) =>
        Math.Max(MinimumPrescribedSets, (int)Math.Round(baseSets * multiplier));

    private static int RestForBlock(BlockType block) => block switch
    {
        BlockType.HighIntensity => HighIntensityRestSeconds,
        BlockType.Hypertrophy => HypertrophyRestSeconds,
        BlockType.Accessory => AccessoryRestSeconds,
        BlockType.Core => CoreRestSeconds,
        _ => AccessoryRestSeconds
    };

    private static int EstimateSlotSeconds(
        int sets,
        byte secondsPerRep,
        byte repsMax,
        int restSeconds,
        int setupSeconds)
    {
        var repTime = secondsPerRep > 0 ? secondsPerRep : DefaultSecondsPerRep;
        var workPerSet = repTime * repsMax;
        return setupSeconds + sets * (workPerSet + restSeconds);
    }

    private static HashSet<MovementPattern> ParsePatterns(string? allowedPatterns)
    {
        if (string.IsNullOrWhiteSpace(allowedPatterns)) return [];
        var patterns = new HashSet<MovementPattern>();
        foreach (var text in allowedPatterns.Split(',', StringSplitOptions.RemoveEmptyEntries))
            if (Enum.TryParse<MovementPattern>(text.Trim(), out var pattern)) patterns.Add(pattern);
        return patterns;
    }
}

using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IExercisePickerService
{
    Task<PrescribedSession> PickForSessionAsync(
        PlannedSession session,
        EquipmentSet available,
        double setMultiplier,
        int rpeCap);
}

public class ExercisePickerService : IExercisePickerService
{
    private readonly AppDatabase _db;
    private readonly IContributionLookup _contributions;

    public ExercisePickerService(
        AppDatabase db,
        IContributionLookup contributions)
    {
        _db = db;
        _contributions = contributions;
    }

    public async Task<PrescribedSession> PickForSessionAsync(
        PlannedSession session,
        EquipmentSet available,
        double setMultiplier,
        int rpeCap)
    {
        var candidates = await _db.WorkoutExerciseDefinitions.AsNoTracking()
            .ToListAsync();

        candidates = FilterByEquipment(candidates, available);

        var picked = new List<PickedExercise>();
        var usedIds = new HashSet<int>();
        var totalSeconds = 0;

        var orderedSlots = session.Slots
            .OrderBy(s => s.Block is BlockType.HighIntensity or BlockType.Hypertrophy ? 0 : s.Block == BlockType.Accessory ? 1 : 2)
            .ToList();

        foreach (var slot in orderedSlots)
        {
            var slotCandidates = candidates
                .Where(e => _contributions.Contribution(e.Id, slot.PrimaryMuscleId) > 0)
                .Where(e => !usedIds.Contains(e.Id))
                .ToList();

            if (slotCandidates.Count == 0)
            {
                if (slot.Droppable) continue;

                slotCandidates = candidates
                    .Where(e => _contributions.Contribution(e.Id, slot.PrimaryMuscleId) > 0)
                    .ToList();

                if (slotCandidates.Count == 0) continue;
            }

            var best = slotCandidates
                .OrderByDescending(e => ScoreExercise(e, slot))
                .First();

            usedIds.Add(best.Id);

            var adjustedSets = ApplySetMultiplier(slot.AssignedSets, setMultiplier);
            var effectiveRpe = Math.Min(10 - slot.TargetRir, rpeCap);
            var rest = RestForBlock(slot.Block);
            picked.Add(new PickedExercise
            {
                ExerciseId = best.Id,
                ExerciseName = best.Name,
                PrimaryMuscleId = slot.PrimaryMuscleId,
                Sets = adjustedSets,
                RepsMin = slot.RepsMin,
                RepsMax = slot.RepsMax,
                Rpe = effectiveRpe,
                RestSeconds = rest,
                SupersetGroup = slot.SupersetGroup,
                Droppable = slot.Droppable,
                IsBodyweight = best.IsBodyweight,
                SlotTemplateId = slot.SlotTemplateId
            });

            totalSeconds += EstimateSlotSeconds(adjustedSets, best.SecondsPerRep, slot.RepsMax, rest, best.SetupSeconds);
        }

        return new PrescribedSession
        {
            ArchetypeId = session.ArchetypeId,
            ArchetypeCode = session.ArchetypeCode,
            Day = session.Day,
            Variant = session.Variant,
            Exercises = picked,
            EstimatedMinutes = (totalSeconds + 59) / 60
        };
    }

    private double ScoreExercise(WorkoutExerciseDefinition exercise, PlannedSlot slot)
    {
        double score = 0;

        var contribution = _contributions.Contribution(exercise.Id, slot.PrimaryMuscleId);
        score += contribution * 10;

        if (slot.Block is (BlockType.HighIntensity or BlockType.Hypertrophy) && exercise.LongLengthLoaded > 0)
            score += exercise.LongLengthLoaded * 2;

        if (slot.Block is BlockType.Accessory or BlockType.Core)
            score -= exercise.FatigueCost * 0.5;

        score -= exercise.SkillDemand * 0.3;

        if (slot.Block is (BlockType.HighIntensity or BlockType.Hypertrophy) && exercise.EligibleForHiBlock)
            score += 1.5;

        return score;
    }

    private static List<WorkoutExerciseDefinition> FilterByEquipment(List<WorkoutExerciseDefinition> exercises, EquipmentSet available)
    {
        return exercises
            .Where(e => e.IsBodyweight || (e.Required & available) == e.Required)
            .ToList();
    }

    private static int ApplySetMultiplier(int baseSets, double multiplier)
    {
        return Math.Max(1, (int)Math.Round(baseSets * multiplier));
    }

    private static int RestForBlock(BlockType block) => block switch
    {
        BlockType.HighIntensity => 150,
        BlockType.Hypertrophy => 120,
        BlockType.Accessory => 90,
        BlockType.Core => 60,
        _ => 90
    };

    private static int EstimateSlotSeconds(int sets, byte secondsPerRep, byte repsMax, int restSeconds, int setupSeconds)
    {
        var repTime = secondsPerRep > 0 ? secondsPerRep : 4;
        var workPerSet = repTime * repsMax;
        return setupSeconds + sets * (workPerSet + restSeconds);
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

}

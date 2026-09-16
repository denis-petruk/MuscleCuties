using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Entities.Workout.Planning;

public class WorkoutMuscleGroup
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class GoalTierWeight
{
    public int Id { get; set; }
    public UserGoal Goal { get; set; }
    public int MuscleGroupId { get; set; }
    public PriorityTier Tier { get; set; }

    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

public class WorkoutExerciseDefinition
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public EquipmentSet Required { get; set; }
    public byte LongLengthLoaded { get; set; }
    public byte FatigueCost { get; set; }
    public byte SkillDemand { get; set; }
    public int SetupSeconds { get; set; }
    public byte SecondsPerRep { get; set; }
    public bool EligibleForHiBlock { get; set; }
    public bool UnilateralDoublesTime { get; set; }
    public bool IsBodyweight { get; set; }
    public byte SupineOrProne { get; set; }
    public byte ValsalvaDemand { get; set; }
    public int? SubstituteGroupId { get; set; }

    public ICollection<ExerciseMuscleContribution> MuscleContributions { get; set; } = [];
}

public class ExerciseMuscleContribution
{
    public int Id { get; set; }
    public int ExerciseId { get; set; }
    public int MuscleGroupId { get; set; }
    public double Fraction { get; set; }

    public WorkoutExerciseDefinition? Exercise { get; set; }
    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

public class SessionArchetype
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsLowerDominant { get; set; }
    public bool ContainsHeavyHinge { get; set; }

    public ICollection<SlotTemplate> SlotTemplates { get; set; } = [];
}

public class SlotTemplate
{
    public int Id { get; set; }
    public int ArchetypeId { get; set; }
    public byte Order { get; set; }
    public BlockType Block { get; set; }
    public int PrimaryMuscleId { get; set; }
    public byte SetsMin { get; set; }
    public byte SetsMax { get; set; }
    public byte RepsMin { get; set; }
    public byte RepsMax { get; set; }
    public byte TargetRir { get; set; }
    public bool Droppable { get; set; }
    public byte SupersetGroup { get; set; }

    public SessionArchetype? Archetype { get; set; }
    public WorkoutMuscleGroup? PrimaryMuscle { get; set; }

}

public class WeekTemplate
{
    public int Id { get; set; }
    public int DaysPerWeek { get; set; }
    public string ArchetypeSequenceJson { get; set; } = string.Empty;

    private int[]? _archetypeSequence;

    public int[] GetArchetypeSequence()
    {
        if (_archetypeSequence is not null)
            return _archetypeSequence;

        if (string.IsNullOrEmpty(ArchetypeSequenceJson))
            return _archetypeSequence = [];

        _archetypeSequence = ArchetypeSequenceJson
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => int.Parse(s.Trim()))
            .ToArray();
        return _archetypeSequence;
    }

    public void SetArchetypeSequence(params int[] ids)
    {
        ArchetypeSequenceJson = string.Join(",", ids);
        _archetypeSequence = ids;
    }
}

public class VolumeBudgetRow
{
    public int Id { get; set; }
    public int DaysPerWeek { get; set; }
    public int MuscleGroupId { get; set; }
    public double FractionalSets { get; set; }

    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

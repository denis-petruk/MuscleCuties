using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.Workout.Planning;

/// <summary>Persistence-neutral reference records consumed by workout planning.</summary>
public sealed record PlanningExercise(
    int Id,
    string Code,
    string Name,
    string Description,
    MuscleGroup PrimaryMuscle,
    string? SecondaryMuscles,
    string JointAreas,
    bool IsInjuryFriendly);

public sealed record PlanningExerciseDefinition(
    int Id,
    string Name,
    MovementPattern Pattern,
    EquipmentSet RequiredEquipment,
    byte LongLengthLoaded,
    byte FatigueCost,
    byte SkillDemand,
    int SetupSeconds,
    byte SecondsPerRep,
    bool EligibleForHiBlock,
    bool UnilateralDoublesTime,
    bool IsBodyweight,
    InjuryFlag Contraindications,
    InjuryFlag PreferredFor,
    byte SupineOrProne,
    byte ValsalvaDemand,
    int? SubstituteGroupId,
    IReadOnlyList<PlanningMuscleContribution> MuscleContributions);

public sealed record PlanningMuscleContribution(int Id, int ExerciseDefinitionId, int MuscleGroupId, double Fraction);

public sealed record PlanningArchetype(
    int Id,
    string Code,
    string DisplayName,
    bool IsLowerDominant,
    bool ContainsHeavyHinge,
    IReadOnlyList<PlanningSlot> Slots);

public sealed record PlanningSlot(
    int Id,
    int ArchetypeId,
    byte Order,
    BlockType Block,
    string AllowedPatterns,
    int PrimaryMuscleId,
    byte SetsMin,
    byte SetsMax,
    byte RepsMin,
    byte RepsMax,
    byte TargetRir,
    bool Droppable,
    byte SupersetGroup);

public sealed record PlanningWeekTemplate(int Id, int DaysPerWeek, string ArchetypeSequence);

public sealed record PlanningVolumeBudget(int Id, int DaysPerWeek, int MuscleGroupId, double FractionalSets);

public sealed record PlanningGoalTierWeight(int Id, UserGoal Goal, int MuscleGroupId, PriorityTier Tier);

public sealed record PlanningExercisePreference(Guid Id, Guid UserId, int OriginalExerciseId, int PreferredExerciseId);

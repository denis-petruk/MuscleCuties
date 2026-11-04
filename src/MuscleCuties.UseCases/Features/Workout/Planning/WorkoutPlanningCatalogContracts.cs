using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

public sealed record WorkoutPlanningCatalogResponse(
    IReadOnlyList<WorkoutCatalogExercise> Exercises,
    IReadOnlyList<WorkoutCatalogMuscleGroup> MuscleGroups,
    IReadOnlyList<WorkoutCatalogExerciseDefinition> ExerciseDefinitions,
    IReadOnlyList<WorkoutCatalogArchetype> Archetypes,
    IReadOnlyList<WorkoutCatalogWeekTemplate> WeekTemplates,
    IReadOnlyList<WorkoutCatalogVolumeBudget> VolumeBudgets,
    IReadOnlyList<WorkoutCatalogGoalTierWeight> GoalTierWeights,
    IReadOnlyList<WorkoutCatalogConfigEntry> PlanningConfigEntries);

public sealed record WorkoutCatalogExercise(
    int Id,
    string Code,
    string Name,
    string Description,
    MuscleGroup PrimaryMuscle,
    string? SecondaryMuscles,
    string JointAreas,
    bool IsInjuryFriendly);

public sealed record WorkoutCatalogMuscleGroup(int Id, string Name);

public sealed record WorkoutCatalogExerciseDefinition(
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
    IReadOnlyList<WorkoutCatalogContribution> MuscleContributions);

public sealed record WorkoutCatalogContribution(int MuscleGroupId, decimal Fraction);

public sealed record WorkoutCatalogArchetype(
    int Id,
    string Code,
    string DisplayName,
    bool IsLowerDominant,
    bool ContainsHeavyHinge,
    IReadOnlyList<WorkoutCatalogSlot> Slots);

public sealed record WorkoutCatalogSlot(
    int Id,
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

public sealed record WorkoutCatalogWeekTemplate(int DaysPerWeek, string ArchetypeSequence);

public sealed record WorkoutCatalogVolumeBudget(int DaysPerWeek, int MuscleGroupId, decimal FractionalSets);

public sealed record WorkoutCatalogGoalTierWeight(UserGoal Goal, int MuscleGroupId, PriorityTier Tier);

public sealed record WorkoutCatalogConfigEntry(string Section, string Key, string Value);

public sealed record WorkoutExercisePreference(int OriginalExerciseId, int PreferredExerciseId);

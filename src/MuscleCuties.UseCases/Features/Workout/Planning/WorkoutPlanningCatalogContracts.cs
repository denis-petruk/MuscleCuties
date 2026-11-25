using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

/// <summary>The catalog the planner builds from, in one payload.</summary>
public sealed record WorkoutPlanningCatalogResponse(
    IReadOnlyList<WorkoutCatalogExercise> Exercises,
    IReadOnlyList<WorkoutCatalogMuscleGroup> MuscleGroups,
    IReadOnlyList<WorkoutCatalogExerciseDefinition> ExerciseDefinitions,
    IReadOnlyList<WorkoutCatalogArchetype> Archetypes,
    IReadOnlyList<WorkoutCatalogWeekTemplate> WeekTemplates,
    IReadOnlyList<WorkoutCatalogVolumeBudget> VolumeBudgets,
    IReadOnlyList<WorkoutCatalogGoalTierWeight> GoalTierWeights,
    IReadOnlyList<WorkoutCatalogConfigEntry> PlanningConfigEntries);

/// <summary>One exercise in the catalog.</summary>
public sealed record WorkoutCatalogExercise(
    int Id,
    string Code,
    string Name,
    string Description,
    MuscleGroup PrimaryMuscle,
    string? SecondaryMuscles,
    string JointAreas,
    bool IsInjuryFriendly);

/// <summary>One muscle group, with the exercises that train it.</summary>
public sealed record WorkoutCatalogMuscleGroup(int Id, string Name);

/// <summary>An exercise's definition: its movement pattern and equipment.</summary>
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

/// <summary>How much one exercise contributes to a muscle group.</summary>
public sealed record WorkoutCatalogContribution(int MuscleGroupId, decimal Fraction);

/// <summary>A session archetype the planner fills.</summary>
public sealed record WorkoutCatalogArchetype(
    int Id,
    string Code,
    string DisplayName,
    bool IsLowerDominant,
    bool ContainsHeavyHinge,
    IReadOnlyList<WorkoutCatalogSlot> Slots);

/// <summary>One slot of an archetype, with the muscle group it must train.</summary>
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

/// <summary>A week template, with its days and their archetypes.</summary>
public sealed record WorkoutCatalogWeekTemplate(int DaysPerWeek, string ArchetypeSequence);

/// <summary>The weekly volume a muscle group may receive.</summary>
public sealed record WorkoutCatalogVolumeBudget(int DaysPerWeek, int MuscleGroupId, decimal FractionalSets);

/// <summary>The volume weighting for one goal tier.</summary>
public sealed record WorkoutCatalogGoalTierWeight(UserGoal Goal, int MuscleGroupId, PriorityTier Tier);

/// <summary>One tunable planner setting, so constants stay out of code.</summary>
public sealed record WorkoutCatalogConfigEntry(string Section, string Key, string Value);

/// <summary>Her preference for one exercise, which the planner weighs.</summary>
public sealed record WorkoutExercisePreference(int OriginalExerciseId, int PreferredExerciseId);

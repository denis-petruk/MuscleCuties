using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

public sealed class Exercise : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Code { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string Description { get; set; } = null!;
    public string? ImageUrl { get; set; }
    public string? VideoUrl { get; set; }
    public string? TechniqueNotes { get; set; }
    public MuscleGroup PrimaryMuscle { get; set; }
    public string? SecondaryMuscles { get; set; }
    public string JointAreas { get; set; } = string.Empty;
    public bool IsInjuryFriendly { get; set; }
}

public sealed class WorkoutMuscleGroup : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
}

public sealed class GoalTierWeight : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public UserGoal Goal { get; set; }
    public int MuscleGroupId { get; set; }
    public PriorityTier Tier { get; set; }
    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

public sealed class WorkoutExerciseDefinition : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public MovementPattern Pattern { get; set; }
    public EquipmentSet Required { get; set; }
    public byte LongLengthLoaded { get; set; }
    public byte FatigueCost { get; set; }
    public byte SkillDemand { get; set; }
    public int SetupSeconds { get; set; }
    public byte SecondsPerRep { get; set; }
    public bool EligibleForHiBlock { get; set; }
    public bool UnilateralDoublesTime { get; set; }
    public bool IsBodyweight { get; set; }
    public InjuryFlag Contraindications { get; set; }
    public InjuryFlag PreferredFor { get; set; }
    public byte SupineOrProne { get; set; }
    public byte ValsalvaDemand { get; set; }
    public int? SubstituteGroupId { get; set; }
    public string? TrainingCatalogVersionV020 { get; set; }
    public string? SupportedExperiencesV020Json { get; set; }
    public string? DemandTagsV020Json { get; set; }
    public bool? DemandTagsReviewedV020 { get; set; }
    public string? RestrictionConflictTagsV020Json { get; set; }
    public bool? RestrictionMappingsReviewedV020 { get; set; }
    public string? SupportedTimingMethodsV020Json { get; set; }
    public string? ReviewedSubstituteExerciseIdsV020Json { get; set; }
    public ICollection<ExerciseMuscleContribution> MuscleContributions { get; set; } = new List<ExerciseMuscleContribution>();
}

public sealed class ExerciseMuscleContribution : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public int ExerciseId { get; set; }
    public int MuscleGroupId { get; set; }
    public decimal Fraction { get; set; }
    public WorkoutExerciseDefinition? Exercise { get; set; }
    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

public sealed class SessionArchetype : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Code { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsLowerDominant { get; set; }
    public bool ContainsHeavyHinge { get; set; }
    public ICollection<SlotTemplate> SlotTemplates { get; set; } = new List<SlotTemplate>();
}

public sealed class SlotTemplate : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public int ArchetypeId { get; set; }
    public byte Order { get; set; }
    public BlockType Block { get; set; }
    public string AllowedPatternsJson { get; set; } = string.Empty;
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

public sealed class WeekTemplate : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public int DaysPerWeek { get; set; }
    public string ArchetypeSequenceJson { get; set; } = string.Empty;
}

public sealed class VolumeBudgetRow : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public int DaysPerWeek { get; set; }
    public int MuscleGroupId { get; set; }
    public decimal FractionalSets { get; set; }
    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

public sealed class WorkoutPlanningConfigEntry : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Section { get; set; } = string.Empty;
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}

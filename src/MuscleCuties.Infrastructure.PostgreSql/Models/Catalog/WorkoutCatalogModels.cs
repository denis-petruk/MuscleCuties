using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

/// <summary>A browsable exercise: how it is shown, which muscles and joints it involves, and whether it is injury friendly.</summary>
public sealed class Exercise : ICatalogSeedRecord
{
    /// <summary>Primary key of the browsable exercise.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The stable app-facing code; clients refer to it instead of the numeric id.</summary>
    public string Code { get; set; } = null!;
    /// <summary>The exercise's display name.</summary>
    public string Name { get; set; } = null!;
    /// <summary>How to perform it, in the app's own words.</summary>
    public string Description { get; set; } = null!;
    /// <summary>Illustration URL; null when the app falls back to the generated artwork.</summary>
    public string? ImageUrl { get; set; }
    /// <summary>Demonstration video URL; null when there is none.</summary>
    public string? VideoUrl { get; set; }
    /// <summary>Coaching notes shown during the set.</summary>
    public string? TechniqueNotes { get; set; }
    /// <summary>The single muscle the exercise is filed under, stored as text.</summary>
    public MuscleGroup PrimaryMuscle { get; set; }
    /// <summary>Comma-separated muscles, free text because the app only displays it.</summary>
    public string? SecondaryMuscles { get; set; }
    /// <summary>The joints it loads, which is what injury rules match on.</summary>
    public string JointAreas { get; set; } = string.Empty;
    /// <summary>The pre-reviewed flag; it never overrides a per-account injury restriction.</summary>
    public bool IsInjuryFriendly { get; set; }
}

/// <summary>A muscle group, the unit volume and coverage are measured in.</summary>
public sealed class WorkoutMuscleGroup : ICatalogSeedRecord
{
    /// <summary>Primary key of the muscle group.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>Unique, because volume is reported under one label per group.</summary>
    public string Name { get; set; } = string.Empty;
}

/// <summary>How strongly one muscle group is favoured for a goal and priority tier.</summary>
public sealed class GoalTierWeight : ICatalogSeedRecord
{
    /// <summary>Primary key of the weighting row.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The goal this weighting applies to, stored as text.</summary>
    public UserGoal Goal { get; set; }
    /// <summary>The group being weighted.</summary>
    public int MuscleGroupId { get; set; }
    /// <summary>The priority tier that changes how much the group is favoured, stored as text.</summary>
    public PriorityTier Tier { get; set; }
    /// <summary>Navigation to the weighted group.</summary>
    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

/// <summary>The planner's view of an exercise: movement pattern, cost attributes and the reviewed restriction tags.</summary>
public sealed class WorkoutExerciseDefinition : ICatalogSeedRecord
{
    /// <summary>Primary key of the planning definition.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The name the plan shows for this exercise.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The movement pattern the planner slots it into, stored as text.</summary>
    public MovementPattern Pattern { get; set; }
    /// <summary>The equipment it needs; it is only offered when the account has all of it.</summary>
    public EquipmentSet Required { get; set; }
    /// <summary>Ordinal 0–255 for how much long-length loading it involves.</summary>
    public byte LongLengthLoaded { get; set; }
    /// <summary>Ordinal cost used to budget fatigue within a session.</summary>
    public byte FatigueCost { get; set; }
    /// <summary>Ordinal skill demand, which limits it for less experienced accounts.</summary>
    public byte SkillDemand { get; set; }
    /// <summary>Seconds of setup, counted into the session's time budget.</summary>
    public int SetupSeconds { get; set; }
    /// <summary>Seconds per rep, used to estimate a set's duration.</summary>
    public byte SecondsPerRep { get; set; }
    /// <summary>Whether it may fill a high-intensity block.</summary>
    public bool EligibleForHiBlock { get; set; }
    /// <summary>Whether one set costs two sides' time, which the time budget must assume.</summary>
    public bool UnilateralDoublesTime { get; set; }
    /// <summary>Whether it needs no external load.</summary>
    public bool IsBodyweight { get; set; }
    /// <summary>Injury flags that rule the exercise out.</summary>
    public InjuryFlag Contraindications { get; set; }
    /// <summary>Injury flags it suits; used to break ties, never to override a contraindication.</summary>
    public InjuryFlag PreferredFor { get; set; }
    /// <summary>Ordinal for how much it puts her on her back or front, which some accounts avoid.</summary>
    public byte SupineOrProne { get; set; }
    /// <summary>Ordinal for how much breath-holding effort it demands.</summary>
    public byte ValsalvaDemand { get; set; }
    /// <summary>Exercises sharing this value may replace one another.</summary>
    public int? SubstituteGroupId { get; set; }
    /// <summary>Planning-catalog identity: the <c>ENGINE_&lt;n&gt;</c> source exercise this definition mirrors.</summary>
    public int? SourceExerciseId { get; set; }
    /// <summary>The reviewed catalog version this row's v020 fields belong to.</summary>
    public string? TrainingCatalogVersionV020 { get; set; }
    /// <summary>The experience levels the reviewed row allows, as JSON.</summary>
    public string? SupportedExperiencesV020Json { get; set; }
    /// <summary>The reviewed demand tags the planner matches against, as JSON.</summary>
    public string? DemandTagsV020Json { get; set; }
    /// <summary>Whether a human checked the demand tags; an unreviewed row is never used for restrictions.</summary>
    public bool? DemandTagsReviewedV020 { get; set; }
    /// <summary>The tags that conflict with injury and cycle restrictions, as JSON.</summary>
    public string? RestrictionConflictTagsV020Json { get; set; }
    /// <summary>Whether the restriction mappings were checked; the same gate as the demand tags.</summary>
    public bool? RestrictionMappingsReviewedV020 { get; set; }
    /// <summary>The timing methods (tempo, rest-pause) the row supports, as JSON.</summary>
    public string? SupportedTimingMethodsV020Json { get; set; }
    /// <summary>The substitutes a reviewer approved, as JSON; the planner picks only from these.</summary>
    public string? ReviewedSubstituteExerciseIdsV020Json { get; set; }
    /// <summary>The evidence recorded for the review, kept with the row so the decision can be audited.</summary>
    public string? ReviewEvidenceV020Json { get; set; }
    /// <summary>How the exercise's work is shared across muscle groups.</summary>
    public ICollection<ExerciseMuscleContribution> MuscleContributions { get; set; } = new List<ExerciseMuscleContribution>();
}

/// <summary>The share of an exercise's work credited to one muscle group.</summary>
public sealed class ExerciseMuscleContribution : ICatalogSeedRecord
{
    /// <summary>Primary key of the contribution.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The exercise whose work is being shared.</summary>
    public int ExerciseId { get; set; }
    /// <summary>The group receiving a share.</summary>
    public int MuscleGroupId { get; set; }
    /// <summary>Share of the work as 0–1; the shares across one exercise's groups are expected to sum to 1.</summary>
    public decimal Fraction { get; set; }
    /// <summary>Navigation to the exercise.</summary>
    public WorkoutExerciseDefinition? Exercise { get; set; }
    /// <summary>Navigation to the group.</summary>
    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

/// <summary>A session shape the week templates are built from.</summary>
public sealed class SessionArchetype : ICatalogSeedRecord
{
    /// <summary>Primary key of the archetype.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The stable app-facing code, unique, used by templates instead of the numeric id.</summary>
    public string Code { get; set; } = string.Empty;
    /// <summary>The name shown when the session shape is explained.</summary>
    public string DisplayName { get; set; } = string.Empty;
    /// <summary>Whether the session is mostly lower body, which the week's balance checks read.</summary>
    public bool IsLowerDominant { get; set; }
    /// <summary>Whether it already contains a heavy hinge, so the next day does not repeat one.</summary>
    public bool ContainsHeavyHinge { get; set; }
    /// <summary>The slots the session is built from, in order.</summary>
    public ICollection<SlotTemplate> SlotTemplates { get; set; } = new List<SlotTemplate>();
}

/// <summary>One slot of an archetype: what may fill it and the set and rep ranges the planner may prescribe inside.</summary>
public sealed class SlotTemplate : ICatalogSeedRecord
{
    /// <summary>Primary key of the slot.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The archetype the slot belongs to.</summary>
    public int ArchetypeId { get; set; }
    /// <summary>Position in the session, unique within its archetype.</summary>
    public byte Order { get; set; }
    /// <summary>Which block the slot sits in, stored as text.</summary>
    public BlockType Block { get; set; }
    /// <summary>The movement patterns allowed in this slot, as JSON.</summary>
    public string AllowedPatternsJson { get; set; } = string.Empty;
    /// <summary>The muscle the slot is meant to train.</summary>
    public int PrimaryMuscleId { get; set; }
    /// <summary>Inclusive lower bound on sets.</summary>
    public byte SetsMin { get; set; }
    /// <summary>Inclusive upper bound on sets.</summary>
    public byte SetsMax { get; set; }
    /// <summary>Inclusive lower bound on reps.</summary>
    public byte RepsMin { get; set; }
    /// <summary>Inclusive upper bound on reps.</summary>
    public byte RepsMax { get; set; }
    /// <summary>Effort target in reps in reserve.</summary>
    public byte TargetRir { get; set; }
    /// <summary>Whether the slot may be dropped to fit the session's time budget.</summary>
    public bool Droppable { get; set; }
    /// <summary>Slots sharing it are performed back to back; 0 means it stands alone.</summary>
    public byte SupersetGroup { get; set; }
    /// <summary>Navigation to the archetype.</summary>
    public SessionArchetype? Archetype { get; set; }
    /// <summary>Navigation to the muscle it trains.</summary>
    public WorkoutMuscleGroup? PrimaryMuscle { get; set; }
}

/// <summary>The archetype sequence for one training frequency.</summary>
public sealed class WeekTemplate : ICatalogSeedRecord
{
    /// <summary>Primary key of the week shape.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>Training frequency, unique, so one week shape exists per frequency.</summary>
    public int DaysPerWeek { get; set; }
    /// <summary>The archetype codes in day order, as JSON.</summary>
    public string ArchetypeSequenceJson { get; set; } = string.Empty;
}

/// <summary>The weekly fractional-set budget for one muscle group at one training frequency.</summary>
public sealed class VolumeBudgetRow : ICatalogSeedRecord
{
    /// <summary>Primary key of the budget row.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The training frequency the budget applies to.</summary>
    public int DaysPerWeek { get; set; }
    /// <summary>The group the budget is for.</summary>
    public int MuscleGroupId { get; set; }
    /// <summary>Weekly fractional sets the plan may spend on this group.</summary>
    public decimal FractionalSets { get; set; }
    /// <summary>Navigation to the group.</summary>
    public WorkoutMuscleGroup? MuscleGroup { get; set; }
}

/// <summary>One tunable planner threshold, keyed by section and name.</summary>
public sealed class WorkoutPlanningConfigEntry : ICatalogSeedRecord
{
    /// <summary>Primary key of the setting.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>Groups related settings, unique together with the key.</summary>
    public string Section { get; set; } = string.Empty;
    /// <summary>The setting's name within its section.</summary>
    public string Key { get; set; } = string.Empty;
    /// <summary>Stored as text; the planner parses it according to the key.</summary>
    public string Value { get; set; } = string.Empty;
}

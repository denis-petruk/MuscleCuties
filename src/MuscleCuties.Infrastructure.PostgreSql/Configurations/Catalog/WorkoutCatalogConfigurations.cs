using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.Catalog;

public sealed class ExerciseConfiguration : IEntityTypeConfiguration<Exercise>
{
    public void Configure(EntityTypeBuilder<Exercise> builder)
    {
        builder.ToTable("exercises", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").IsRequired().HasMaxLength(CatalogColumnLimits.ExerciseCode);
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(CatalogColumnLimits.ExerciseName);
        builder.Property(x => x.Description).HasColumnName("description").IsRequired().HasMaxLength(CatalogColumnLimits.ExerciseDescription);
        builder.Property(x => x.ImageUrl).HasColumnName("image_url").HasMaxLength(CatalogColumnLimits.ExerciseUrl);
        builder.Property(x => x.VideoUrl).HasColumnName("video_url").HasMaxLength(CatalogColumnLimits.ExerciseUrl);
        builder.Property(x => x.TechniqueNotes).HasColumnName("technique_notes").HasMaxLength(CatalogColumnLimits.ExerciseDescription);
        builder.Property(x => x.PrimaryMuscle).HasColumnName("primary_muscle").HasConversion<string>().IsRequired();
        builder.Property(x => x.SecondaryMuscles).HasColumnName("secondary_muscles").HasMaxLength(CatalogColumnLimits.ExerciseTags);
        builder.Property(x => x.JointAreas).HasColumnName("joint_areas").IsRequired().HasMaxLength(CatalogColumnLimits.ExerciseTags);
        builder.Property(x => x.IsInjuryFriendly).HasColumnName("is_injury_friendly").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasIndex(x => x.Name);
    }
}

public sealed class WorkoutMuscleGroupConfiguration : IEntityTypeConfiguration<WorkoutMuscleGroup>
{
    public void Configure(EntityTypeBuilder<WorkoutMuscleGroup> builder)
    {
        builder.ToTable("workout_muscle_groups", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(CatalogColumnLimits.MuscleGroupName);
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => x.Name).IsUnique();
    }
}

public sealed class GoalTierWeightConfiguration : IEntityTypeConfiguration<GoalTierWeight>
{
    public void Configure(EntityTypeBuilder<GoalTierWeight> builder)
    {
        builder.ToTable("goal_tier_weights", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Goal).HasColumnName("goal").HasConversion<string>().IsRequired();
        builder.Property(x => x.MuscleGroupId).HasColumnName("muscle_group_id").IsRequired();
        builder.Property(x => x.Tier).HasColumnName("tier").HasConversion<string>().IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => new { x.Goal, x.MuscleGroupId }).IsUnique();
        builder.HasOne(x => x.MuscleGroup).WithMany().HasForeignKey(x => x.MuscleGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkoutExerciseDefinitionConfiguration : IEntityTypeConfiguration<WorkoutExerciseDefinition>
{
    public void Configure(EntityTypeBuilder<WorkoutExerciseDefinition> builder)
    {
        builder.ToTable("workout_exercise_definitions", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(CatalogColumnLimits.PlanningExerciseName);
        builder.Property(x => x.Pattern).HasColumnName("pattern").HasConversion<string>().IsRequired();
        builder.Property(x => x.Required).HasColumnName("required_equipment").HasConversion<string>().IsRequired();
        builder.Property(x => x.LongLengthLoaded).HasColumnName("long_length_loaded").IsRequired();
        builder.Property(x => x.FatigueCost).HasColumnName("fatigue_cost").IsRequired();
        builder.Property(x => x.SkillDemand).HasColumnName("skill_demand").IsRequired();
        builder.Property(x => x.SetupSeconds).HasColumnName("setup_seconds").IsRequired();
        builder.Property(x => x.SecondsPerRep).HasColumnName("seconds_per_rep").IsRequired();
        builder.Property(x => x.EligibleForHiBlock).HasColumnName("eligible_for_hi_block").IsRequired();
        builder.Property(x => x.UnilateralDoublesTime).HasColumnName("unilateral_doubles_time").IsRequired();
        builder.Property(x => x.IsBodyweight).HasColumnName("is_bodyweight").IsRequired();
        builder.Property(x => x.Contraindications).HasColumnName("contraindications").HasConversion<string>().IsRequired();
        builder.Property(x => x.PreferredFor).HasColumnName("preferred_for").HasConversion<string>().IsRequired();
        builder.Property(x => x.SupineOrProne).HasColumnName("supine_or_prone").IsRequired();
        builder.Property(x => x.ValsalvaDemand).HasColumnName("valsalva_demand").IsRequired();
        builder.Property(x => x.SubstituteGroupId).HasColumnName("substitute_group_id");
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.Pattern);
        builder.HasMany(x => x.MuscleContributions).WithOne(x => x.Exercise).HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class ExerciseMuscleContributionConfiguration : IEntityTypeConfiguration<ExerciseMuscleContribution>
{
    public void Configure(EntityTypeBuilder<ExerciseMuscleContribution> builder)
    {
        builder.ToTable("exercise_muscle_contributions", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.ExerciseId).HasColumnName("exercise_id").IsRequired();
        builder.Property(x => x.MuscleGroupId).HasColumnName("muscle_group_id").IsRequired();
        builder.Property(x => x.Fraction).HasColumnName("fraction").HasColumnType("numeric").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => new { x.ExerciseId, x.MuscleGroupId }).IsUnique();
        builder.HasOne(x => x.Exercise).WithMany(x => x.MuscleContributions).HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.MuscleGroup).WithMany().HasForeignKey(x => x.MuscleGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SessionArchetypeConfiguration : IEntityTypeConfiguration<SessionArchetype>
{
    public void Configure(EntityTypeBuilder<SessionArchetype> builder)
    {
        builder.ToTable("session_archetypes", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Code).HasColumnName("code").IsRequired().HasMaxLength(CatalogColumnLimits.SessionArchetypeCode);
        builder.Property(x => x.DisplayName).HasColumnName("display_name").IsRequired().HasMaxLength(CatalogColumnLimits.SessionArchetypeName);
        builder.Property(x => x.IsLowerDominant).HasColumnName("is_lower_dominant").IsRequired();
        builder.Property(x => x.ContainsHeavyHinge).HasColumnName("contains_heavy_hinge").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => x.Code).IsUnique();
        builder.HasMany(x => x.SlotTemplates).WithOne(x => x.Archetype).HasForeignKey(x => x.ArchetypeId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SlotTemplateConfiguration : IEntityTypeConfiguration<SlotTemplate>
{
    public void Configure(EntityTypeBuilder<SlotTemplate> builder)
    {
        builder.ToTable("slot_templates", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.ArchetypeId).HasColumnName("archetype_id").IsRequired();
        builder.Property(x => x.Order).HasColumnName("order").IsRequired();
        builder.Property(x => x.Block).HasColumnName("block").HasConversion<string>().IsRequired();
        builder.Property(x => x.AllowedPatternsJson).HasColumnName("allowed_patterns_json").IsRequired().HasMaxLength(CatalogColumnLimits.AllowedPatterns);
        builder.Property(x => x.PrimaryMuscleId).HasColumnName("primary_muscle_id").IsRequired();
        builder.Property(x => x.SetsMin).HasColumnName("sets_min").IsRequired();
        builder.Property(x => x.SetsMax).HasColumnName("sets_max").IsRequired();
        builder.Property(x => x.RepsMin).HasColumnName("reps_min").IsRequired();
        builder.Property(x => x.RepsMax).HasColumnName("reps_max").IsRequired();
        builder.Property(x => x.TargetRir).HasColumnName("target_rir").IsRequired();
        builder.Property(x => x.Droppable).HasColumnName("droppable").IsRequired();
        builder.Property(x => x.SupersetGroup).HasColumnName("superset_group").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => new { x.ArchetypeId, x.Order }).IsUnique();
        builder.HasOne(x => x.Archetype).WithMany(x => x.SlotTemplates).HasForeignKey(x => x.ArchetypeId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.PrimaryMuscle).WithMany().HasForeignKey(x => x.PrimaryMuscleId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WeekTemplateConfiguration : IEntityTypeConfiguration<WeekTemplate>
{
    public void Configure(EntityTypeBuilder<WeekTemplate> builder)
    {
        builder.ToTable("week_templates", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.DaysPerWeek).HasColumnName("days_per_week").IsRequired();
        builder.Property(x => x.ArchetypeSequenceJson).HasColumnName("archetype_sequence_json").IsRequired().HasMaxLength(CatalogColumnLimits.ArchetypeSequence);
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => x.DaysPerWeek).IsUnique();
    }
}

public sealed class VolumeBudgetRowConfiguration : IEntityTypeConfiguration<VolumeBudgetRow>
{
    public void Configure(EntityTypeBuilder<VolumeBudgetRow> builder)
    {
        builder.ToTable("volume_budget_rows", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.DaysPerWeek).HasColumnName("days_per_week").IsRequired();
        builder.Property(x => x.MuscleGroupId).HasColumnName("muscle_group_id").IsRequired();
        builder.Property(x => x.FractionalSets).HasColumnName("fractional_sets").HasColumnType("numeric").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => new { x.DaysPerWeek, x.MuscleGroupId }).IsUnique();
        builder.HasOne(x => x.MuscleGroup).WithMany().HasForeignKey(x => x.MuscleGroupId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkoutPlanningConfigEntryConfiguration : IEntityTypeConfiguration<WorkoutPlanningConfigEntry>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanningConfigEntry> builder)
    {
        builder.ToTable("workout_planning_config_entries", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Section).HasColumnName("section").IsRequired().HasMaxLength(CatalogColumnLimits.ConfigSection);
        builder.Property(x => x.Key).HasColumnName("key").IsRequired().HasMaxLength(CatalogColumnLimits.ConfigKey);
        builder.Property(x => x.Value).HasColumnName("value").IsRequired().HasMaxLength(CatalogColumnLimits.ConfigValue);
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => new { x.Section, x.Key }).IsUnique();
    }
}

internal static class CatalogColumnLimits
{
    public const int QuizQuestion = 250;
    public const int QuizAnswer = 160;
    public const int FoodName = 240;
    public const int FoodDataType = 40;
    public const int FoodBrand = 240;
    public const int FoodGtin = 40;
    public const int FoodIngredients = 4000;
    public const int FoodUnit = 40;
    public const int FoodServingOptions = 4000;
    public const int MealTemplateName = 160;
    public const int MealTemplateDescription = 1000;
    public const int MealTemplateTags = 250;
    public const int ExerciseCode = 80;
    public const int ExerciseName = 160;
    public const int ExerciseDescription = 2000;
    public const int ExerciseUrl = 1000;
    public const int ExerciseTags = 500;
    public const int MuscleGroupName = 40;
    public const int PlanningExerciseName = 120;
    public const int SessionArchetypeCode = 10;
    public const int SessionArchetypeName = 60;
    public const int AllowedPatterns = 500;
    public const int ArchetypeSequence = 100;
    public const int ConfigSection = 30;
    public const int ConfigKey = 60;
    public const int ConfigValue = 500;
}

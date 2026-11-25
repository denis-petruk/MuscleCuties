using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

/// <summary>Maps the per-account planning state, one row per account.</summary>
public sealed class WorkoutAccountPlanningStateConfiguration : IEntityTypeConfiguration<WorkoutAccountPlanningStateRecord>
{
    /// <summary>Makes the account unique so the state row cannot fork, which is what the lease depends on.</summary>
    public void Configure(EntityTypeBuilder<WorkoutAccountPlanningStateRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_account_planning_states");
        builder.HasIndex(row => row.UserId).IsUnique();
    }
}

/// <summary>Maps the earned-day credits, unique per account and local day.</summary>
public sealed class WorkoutCompletionDayCreditConfiguration : IEntityTypeConfiguration<WorkoutCompletionDayCreditRecord>
{
    /// <summary>Makes the credit unique per account and day, so a day cannot be earned twice.</summary>
    public void Configure(EntityTypeBuilder<WorkoutCompletionDayCreditRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_completion_day_credits");
        builder.HasIndex(row => new { row.UserId, row.LocalDate }).IsUnique();
        // No FK to workout logs: removing a completed workout must preserve its credit.
    }
}

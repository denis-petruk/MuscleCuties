using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

public sealed class WorkoutAccountPlanningStateConfiguration : IEntityTypeConfiguration<WorkoutAccountPlanningStateRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutAccountPlanningStateRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_account_planning_states");
        builder.HasIndex(row => row.UserId).IsUnique();
    }
}

public sealed class WorkoutCompletionDayCreditConfiguration : IEntityTypeConfiguration<WorkoutCompletionDayCreditRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutCompletionDayCreditRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_completion_day_credits");
        builder.HasIndex(row => new { row.UserId, row.LocalDate }).IsUnique();
        // No FK to workout logs: removing a completed workout must preserve its credit.
    }
}

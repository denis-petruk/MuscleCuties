using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

public sealed class WorkoutInjuryEpisodeConfiguration : IEntityTypeConfiguration<WorkoutInjuryEpisodeRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutInjuryEpisodeRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_episodes");
        builder.HasIndex(row => new { row.UserId, row.InjuryLogId, row.ClearedOn });
        builder.HasIndex(row => new { row.UserId, row.InjuryLogId }).IsUnique()
            .HasFilter("cleared_on IS NULL AND NOT source_deleted");
    }
}

public sealed class WorkoutInjuryPrescriptionConfiguration : IEntityTypeConfiguration<WorkoutInjuryPrescriptionRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutInjuryPrescriptionRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_prescriptions");
        builder.HasIndex(row => new { row.UserId, row.EpisodeId, row.OriginalWorkoutDayExerciseId }).IsUnique();
    }
}

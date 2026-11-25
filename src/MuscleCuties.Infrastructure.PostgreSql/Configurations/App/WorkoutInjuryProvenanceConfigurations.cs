using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

/// <summary>Maps injury episodes, one per report that opened or continued one.</summary>
public sealed class WorkoutInjuryEpisodeConfiguration : IEntityTypeConfiguration<WorkoutInjuryEpisodeRecord>
{
    /// <summary>Indexes the account and site so an open episode can be found before a new one is opened.</summary>
    public void Configure(EntityTypeBuilder<WorkoutInjuryEpisodeRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_episodes");
        builder.HasIndex(row => new { row.UserId, row.InjuryLogId, row.ClearedOn });
        builder.HasIndex(row => new { row.UserId, row.InjuryLogId }).IsUnique()
            .HasFilter("cleared_on IS NULL AND NOT source_deleted");
    }
}

/// <summary>Maps the captured prescriptions, unique per exercise row and episode.</summary>
public sealed class WorkoutInjuryPrescriptionConfiguration : IEntityTypeConfiguration<WorkoutInjuryPrescriptionRecord>
{
    /// <summary>Maps the captured prescriptions, unique per exercise row and episode, with the episode restricted so the capture never outlives its reason.</summary>
    public void Configure(EntityTypeBuilder<WorkoutInjuryPrescriptionRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_prescriptions");
        builder.HasIndex(row => new { row.UserId, row.EpisodeId, row.OriginalWorkoutDayExerciseId }).IsUnique();
    }
}

using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

/// <summary>Maps the slots withheld for an injury, unique per plan, date, session and slot.</summary>
public sealed class WorkoutInjuryOmittedSlotConfiguration : IEntityTypeConfiguration<WorkoutInjuryOmittedSlotRecord>
{
    /// <summary>Makes the omission key unique so replanning cannot record the same withheld slot twice.</summary>
    public void Configure(EntityTypeBuilder<WorkoutInjuryOmittedSlotRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_omitted_slots");
        builder.HasIndex(row => new { row.UserId, row.PlanId, row.ScheduledDate, row.TemplateId,
            row.TemplateVersion, row.SessionIndex, row.SlotId, row.EpisodeId }).IsUnique()
            .HasDatabaseName("ix_workout_injury_omitted_slot_identity");
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

public sealed class WorkoutInjuryOmittedSlotConfiguration : IEntityTypeConfiguration<WorkoutInjuryOmittedSlotRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutInjuryOmittedSlotRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_omitted_slots");
        builder.HasIndex(row => new { row.UserId, row.PlanId, row.ScheduledDate, row.TemplateId,
            row.TemplateVersion, row.SessionIndex, row.SlotId, row.EpisodeId }).IsUnique()
            .HasDatabaseName("ix_workout_injury_omitted_slot_identity");
    }
}

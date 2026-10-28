using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

public sealed class HiitEvidenceCaptureChallengeConfiguration : IEntityTypeConfiguration<HiitEvidenceCaptureChallengeRecord>
{
    public void Configure(EntityTypeBuilder<HiitEvidenceCaptureChallengeRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "hiit_evidence_capture_challenges");
        builder.HasIndex(row => new { row.UserId, row.ExpiresAtUtc });
    }
}

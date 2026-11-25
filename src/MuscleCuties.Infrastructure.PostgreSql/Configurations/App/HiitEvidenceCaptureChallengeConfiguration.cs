using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

/// <summary>Maps the HIIT capture challenges, one open challenge per account and local day.</summary>
public sealed class HiitEvidenceCaptureChallengeConfiguration : IEntityTypeConfiguration<HiitEvidenceCaptureChallengeRecord>
{
    /// <summary>Keeps the open-challenge uniqueness in the index rather than the code, so a second request cannot open two.</summary>
    public void Configure(EntityTypeBuilder<HiitEvidenceCaptureChallengeRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "hiit_evidence_capture_challenges");
        builder.HasIndex(row => new { row.UserId, row.ExpiresAtUtc });
    }
}

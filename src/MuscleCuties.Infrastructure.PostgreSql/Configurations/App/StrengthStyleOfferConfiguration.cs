using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

public sealed class StrengthStyleOfferConfiguration : IEntityTypeConfiguration<StrengthStyleOfferRecord>
{
    public void Configure(EntityTypeBuilder<StrengthStyleOfferRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "strength_style_offers");
        builder.HasIndex(row => new { row.UserId, row.RevisionId }).IsUnique();
    }
}

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

public sealed class StrengthStyleOfferConfiguration : IEntityTypeConfiguration<StrengthStyleOfferRecord>
{
    public void Configure(EntityTypeBuilder<StrengthStyleOfferRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "strength_style_offers");
        builder.HasIndex(row => new { row.UserId, row.RevisionId }).IsUnique();
    }
}

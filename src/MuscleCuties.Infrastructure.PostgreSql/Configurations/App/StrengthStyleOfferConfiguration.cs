using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

/// <summary>Maps the offered strength style: one row per plan revision, dismissed or not.</summary>
public sealed class StrengthStyleOfferConfiguration : IEntityTypeConfiguration<StrengthStyleOfferRecord>
{
    /// <summary>Keeps one offer per account and revision so a redraw of the plan does not stack offers, and stores the alternative ids as an array.</summary>
    public void Configure(EntityTypeBuilder<StrengthStyleOfferRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "strength_style_offers");
        builder.HasIndex(row => new { row.UserId, row.RevisionId }).IsUnique();
    }
}

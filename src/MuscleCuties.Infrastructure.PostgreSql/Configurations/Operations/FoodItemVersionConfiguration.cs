using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Models.Operations;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.Operations;

/// <summary>Maps the nutrient snapshots taken before a catalog change.</summary>
public sealed class FoodItemVersionConfiguration : IEntityTypeConfiguration<FoodItemVersionRecord>
{
    /// <summary>Places the snapshots in the operations schema and indexes them by food and time, which is the read a diff makes.</summary>
    public void Configure(EntityTypeBuilder<FoodItemVersionRecord> builder)
    {
        builder.ToTable("food_item_versions", "catalog");
        builder.HasKey(record => record.Id);
        builder.Property(record => record.Id).HasColumnName("id").UseIdentityByDefaultColumn();
        builder.Property(record => record.FoodItemId).HasColumnName("food_item_id").IsRequired();
        builder.Property(record => record.NutrientJson).HasColumnName("nutrient_snapshot").HasColumnType("jsonb")
            .HasConversion(value => value.Length == 0 ? "\"\"" : value,
                value => value == "\"\"" ? string.Empty : value)
            .IsRequired();
        builder.Property(record => record.VersionedAtUtc).HasColumnName("versioned_at").HasColumnType("timestamp with time zone").IsRequired();
        builder.Property(record => record.ChangeSource).HasColumnName("change_source").IsRequired();
        builder.HasIndex(record => new { record.FoodItemId, record.VersionedAtUtc });
        builder.HasOne<FoodItem>()
            .WithMany()
            .HasForeignKey(record => record.FoodItemId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

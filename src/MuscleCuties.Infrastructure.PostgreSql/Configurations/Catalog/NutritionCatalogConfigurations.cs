using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.Catalog;

public sealed class FoodItemConfiguration : IEntityTypeConfiguration<FoodItem>
{
    public void Configure(EntityTypeBuilder<FoodItem> builder)
    {
        builder.ToTable("food_items", "catalog", table => table.HasCheckConstraint(
            "ck_food_items_fdc_seed_code_matches_id",
            "seed_code NOT LIKE 'fdc:%' OR (fdc_id IS NOT NULL AND seed_code = 'fdc:' || fdc_id::text)"));
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(CatalogColumnLimits.FoodName);
        builder.Property(x => x.Calories).HasColumnName("calories").HasColumnType("numeric");
        builder.Property(x => x.Protein).HasColumnName("protein").HasColumnType("numeric");
        builder.Property(x => x.Carbs).HasColumnName("carbs").HasColumnType("numeric");
        builder.Property(x => x.Fats).HasColumnName("fats").HasColumnType("numeric");
        builder.Property(x => x.Fiber).HasColumnName("fiber").HasColumnType("numeric");
        builder.Property(x => x.Iron).HasColumnName("iron").HasColumnType("numeric");
        builder.Property(x => x.VitaminB12).HasColumnName("vitamin_b12").HasColumnType("numeric");
        builder.Property(x => x.VitaminC).HasColumnName("vitamin_c").HasColumnType("numeric");
        builder.Property(x => x.VitaminD).HasColumnName("vitamin_d").HasColumnType("numeric");
        builder.Property(x => x.VitaminA).HasColumnName("vitamin_a").HasColumnType("numeric");
        builder.Property(x => x.VitaminB6).HasColumnName("vitamin_b6").HasColumnType("numeric");
        builder.Property(x => x.Folate).HasColumnName("folate").HasColumnType("numeric");
        builder.Property(x => x.Calcium).HasColumnName("calcium").HasColumnType("numeric");
        builder.Property(x => x.Magnesium).HasColumnName("magnesium").HasColumnType("numeric");
        builder.Property(x => x.Zinc).HasColumnName("zinc").HasColumnType("numeric");
        builder.Property(x => x.Potassium).HasColumnName("potassium").HasColumnType("numeric");
        builder.Property(x => x.FdcId).HasColumnName("fdc_id");
        builder.Property(x => x.DataType).HasColumnName("data_type").HasMaxLength(CatalogColumnLimits.FoodDataType);
        builder.Property(x => x.BrandOwner).HasColumnName("brand_owner").HasMaxLength(CatalogColumnLimits.FoodBrand);
        builder.Property(x => x.BrandName).HasColumnName("brand_name").HasMaxLength(CatalogColumnLimits.FoodBrand);
        builder.Property(x => x.GtinUpc).HasColumnName("gtin_upc").HasMaxLength(CatalogColumnLimits.FoodGtin);
        builder.Property(x => x.Ingredients).HasColumnName("ingredients").HasMaxLength(CatalogColumnLimits.FoodIngredients);
        builder.Property(x => x.ServingSize).HasColumnName("serving_size").HasColumnType("numeric");
        builder.Property(x => x.ServingSizeUnit).HasColumnName("serving_size_unit").HasMaxLength(CatalogColumnLimits.FoodUnit);
        builder.Property(x => x.ServingOptionsJson).HasColumnName("serving_options_json").HasColumnType("jsonb");
        builder.Property(x => x.LastSyncedAt).HasColumnName("last_synced_at");
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.Property(x => x.UpdatedAt).HasColumnName("updated_at").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasIndex(x => x.Name);
        builder.HasIndex(x => x.FdcId).IsUnique();
        builder.HasIndex(x => new { x.DataType, x.Name });
        builder.HasIndex(x => x.BrandOwner);
        builder.HasIndex(x => x.BrandName);
        builder.HasIndex(x => x.GtinUpc);
    }
}

public sealed class MealTemplateConfiguration : IEntityTypeConfiguration<MealTemplate>
{
    public void Configure(EntityTypeBuilder<MealTemplate> builder)
    {
        builder.ToTable("meal_templates", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.Name).HasColumnName("name").IsRequired().HasMaxLength(CatalogColumnLimits.MealTemplateName);
        builder.Property(x => x.Description).HasColumnName("description").HasMaxLength(CatalogColumnLimits.MealTemplateDescription);
        builder.Property(x => x.MealType).HasColumnName("meal_type").HasConversion<string>().IsRequired();
        builder.Property(x => x.DietaryTags).HasColumnName("dietary_tags").IsRequired().HasMaxLength(CatalogColumnLimits.MealTemplateTags);
        builder.Property(x => x.PhaseTags).HasColumnName("phase_tags").IsRequired().HasMaxLength(CatalogColumnLimits.MealTemplateTags);
        builder.Property(x => x.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(x => x.IsSystem).HasColumnName("is_system").IsRequired();
        builder.Property(x => x.CreatedAt).HasColumnName("created_at").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasMany(x => x.Entries).WithOne(x => x.MealTemplate).HasForeignKey(x => x.MealTemplateId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class MealTemplateEntryConfiguration : IEntityTypeConfiguration<MealTemplateEntry>
{
    public void Configure(EntityTypeBuilder<MealTemplateEntry> builder)
    {
        builder.ToTable("meal_template_entries", "catalog");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedOnAdd();
        builder.Property(x => x.SeedCode).HasColumnName("seed_code").IsRequired();
        builder.Property(x => x.MealTemplateId).HasColumnName("meal_template_id").IsRequired();
        builder.Property(x => x.FoodItemId).HasColumnName("food_item_id").IsRequired();
        builder.Property(x => x.Grams).HasColumnName("grams").HasColumnType("numeric").IsRequired();
        builder.HasIndex(x => x.SeedCode).IsUnique();
        builder.HasOne(x => x.MealTemplate).WithMany(x => x.Entries).HasForeignKey(x => x.MealTemplateId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.FoodItem).WithMany().HasForeignKey(x => x.FoodItemId).OnDelete(DeleteBehavior.Restrict);
    }
}

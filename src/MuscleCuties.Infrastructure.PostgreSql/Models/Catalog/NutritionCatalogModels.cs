using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

/// <summary>Canonical global food only; user-custom foods are stored in the tenant schema.</summary>
public sealed class FoodItem : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbs { get; set; }
    public decimal? Fats { get; set; }
    public decimal? Fiber { get; set; }
    public decimal? Iron { get; set; }
    public decimal? VitaminB12 { get; set; }
    public decimal? VitaminC { get; set; }
    public decimal? VitaminD { get; set; }
    public decimal? VitaminA { get; set; }
    public decimal? VitaminB6 { get; set; }
    public decimal? Folate { get; set; }
    public decimal? Calcium { get; set; }
    public decimal? Magnesium { get; set; }
    public decimal? Zinc { get; set; }
    public decimal? Potassium { get; set; }
    public int? FdcId { get; set; }
    public string? DataType { get; set; }
    public string? BrandOwner { get; set; }
    public string? BrandName { get; set; }
    public string? GtinUpc { get; set; }
    public string? Ingredients { get; set; }
    public decimal? ServingSize { get; set; }
    public string? ServingSizeUnit { get; set; }
    public string? ServingOptionsJson { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Canonical system meal template. User-owned templates live in the tenant schema.</summary>
public sealed class MealTemplate : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public MealType MealType { get; set; }
    public string DietaryTags { get; set; } = string.Empty;
    public string PhaseTags { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }
    public ICollection<MealTemplateEntry> Entries { get; set; } = new List<MealTemplateEntry>();
}

/// <summary>Canonical entry in a system meal template; FoodItemId targets only catalog.food_items.</summary>
public sealed class MealTemplateEntry : ICatalogSeedRecord
{
    public int Id { get; set; }
    public string SeedCode { get; set; } = null!;
    public int MealTemplateId { get; set; }
    public int FoodItemId { get; set; }
    public decimal Grams { get; set; }
    public MealTemplate? MealTemplate { get; set; }
    public FoodItem? FoodItem { get; set; }
}

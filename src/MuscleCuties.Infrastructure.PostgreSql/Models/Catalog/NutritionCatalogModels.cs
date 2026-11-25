using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

/// <summary>Canonical global food only; user-custom foods are stored in the tenant schema.</summary>
public sealed class FoodItem : ICatalogSeedRecord
{
    /// <summary>Primary key; the catalog is upserted by its seed code, so this is never chosen by hand.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The food's display name, indexed because search starts from it.</summary>
    public string Name { get; set; } = null!;
    /// <summary>Energy per 100 g; null when the source states none, which is not the same as zero.</summary>
    public decimal? Calories { get; set; }
    /// <summary>Protein per 100 g.</summary>
    public decimal? Protein { get; set; }
    /// <summary>Carbohydrate per 100 g.</summary>
    public decimal? Carbs { get; set; }
    /// <summary>Fat per 100 g.</summary>
    public decimal? Fats { get; set; }
    /// <summary>Fibre per 100 g.</summary>
    public decimal? Fiber { get; set; }
    /// <summary>Iron per 100 g.</summary>
    public decimal? Iron { get; set; }
    /// <summary>Vitamin B12 per 100 g.</summary>
    public decimal? VitaminB12 { get; set; }
    /// <summary>Vitamin C per 100 g.</summary>
    public decimal? VitaminC { get; set; }
    /// <summary>Vitamin D per 100 g.</summary>
    public decimal? VitaminD { get; set; }
    /// <summary>Vitamin A per 100 g.</summary>
    public decimal? VitaminA { get; set; }
    /// <summary>Vitamin B6 per 100 g.</summary>
    public decimal? VitaminB6 { get; set; }
    /// <summary>Folate per 100 g.</summary>
    public decimal? Folate { get; set; }
    /// <summary>Calcium per 100 g.</summary>
    public decimal? Calcium { get; set; }
    /// <summary>Magnesium per 100 g.</summary>
    public decimal? Magnesium { get; set; }
    /// <summary>Zinc per 100 g.</summary>
    public decimal? Zinc { get; set; }
    /// <summary>Potassium per 100 g.</summary>
    public decimal? Potassium { get; set; }
    /// <summary>The source's own record id, unique, which is what a later sync matches the row on.</summary>
    public int? FdcId { get; set; }
    /// <summary>The source's record kind, which decides how much of the row can be trusted.</summary>
    public string? DataType { get; set; }
    /// <summary>The brand owner, set only for branded records.</summary>
    public string? BrandOwner { get; set; }
    /// <summary>The brand name, set only for branded records.</summary>
    public string? BrandName { get; set; }
    /// <summary>The barcode, indexed so a scan can resolve a food.</summary>
    public string? GtinUpc { get; set; }
    /// <summary>The source's ingredient text, trimmed to the column limit.</summary>
    public string? Ingredients { get; set; }
    /// <summary>The serving amount the source states, in its own unit.</summary>
    public decimal? ServingSize { get; set; }
    /// <summary>The unit the stated serving is measured in.</summary>
    public string? ServingSizeUnit { get; set; }
    /// <summary>The parsed serving options as JSON, so the diary offers measures without re-parsing source text.</summary>
    public string? ServingOptionsJson { get; set; }
    /// <summary>When a sync last wrote this row; null for a row that came from the shipped seed.</summary>
    public DateTime? LastSyncedAt { get; set; }
    /// <summary>When the row was first written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When the row last changed, which is how a sync detects drift.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Canonical system meal template. User-owned templates live in the tenant schema.</summary>
public sealed class MealTemplate : ICatalogSeedRecord
{
    /// <summary>Primary key of the shared template.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The template's display name.</summary>
    public string Name { get; set; } = null!;
    /// <summary>A short description shown under the name.</summary>
    public string? Description { get; set; }
    /// <summary>Which meal it belongs to, stored as text so renaming an enum member cannot renumber stored rows.</summary>
    public MealType MealType { get; set; }
    /// <summary>Comma-separated dietary tags, matched against the profile's preferences.</summary>
    public string DietaryTags { get; set; } = string.Empty;
    /// <summary>Comma-separated cycle phases the template suits.</summary>
    public string PhaseTags { get; set; } = string.Empty;
    /// <summary>Position in the template list.</summary>
    public int SortOrder { get; set; }
    /// <summary>True for a shipped template, false once it has been copied into an account.</summary>
    public bool IsSystem { get; set; }
    /// <summary>When the template was seeded.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>The foods the template is made of.</summary>
    public ICollection<MealTemplateEntry> Entries { get; set; } = new List<MealTemplateEntry>();
}

/// <summary>Canonical entry in a system meal template; FoodItemId targets only catalog.food_items.</summary>
public sealed class MealTemplateEntry : ICatalogSeedRecord
{
    /// <summary>Primary key of the entry.</summary>
    public int Id { get; set; }
    /// <summary>The stable key a seed run matches on, so re-running a seed updates the row instead of duplicating it.</summary>
    public string SeedCode { get; set; } = null!;
    /// <summary>The template the food belongs to.</summary>
    public int MealTemplateId { get; set; }
    /// <summary>The catalog food used.</summary>
    public int FoodItemId { get; set; }
    /// <summary>The weight of that food in the template, which every nutrient is scaled from.</summary>
    public decimal Grams { get; set; }
    /// <summary>Navigation back to the template.</summary>
    public MealTemplate? MealTemplate { get; set; }
    /// <summary>Navigation to the food it weighs.</summary>
    public FoodItem? FoodItem { get; set; }
}

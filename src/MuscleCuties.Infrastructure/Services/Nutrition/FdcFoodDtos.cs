using System.Text.Json.Serialization;

namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>The fields every food search result and detail share.</summary>
public abstract class FdcFoodItemBase
{
    [JsonPropertyName("fdcId")] public int FdcId { get; set; }

    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dataType")] public string? DataType { get; set; }

    [JsonPropertyName("brandOwner")] public string? BrandOwner { get; set; }

    [JsonPropertyName("brandName")] public string? BrandName { get; set; }

    [JsonPropertyName("gtinUpc")] public string? GtinUpc { get; set; }

    [JsonPropertyName("ingredients")] public string? Ingredients { get; set; }

    [JsonPropertyName("servingSize")] public float? ServingSize { get; set; }

    [JsonPropertyName("servingSizeUnit")] public string? ServingSizeUnit { get; set; }

    [JsonPropertyName("householdServingFullText")]
    /// <summary>The household measure the source states, which the serving options parse.</summary>
    public string? HouseholdServingFullText { get; set; }

    [JsonPropertyName("packageWeight")] public string? PackageWeight { get; set; }
}


/// <summary>A food's full record, which carries the nutrient rows the per-100 g values are read from.</summary>
public class FdcFoodDetail : FdcFoodItemBase
{
    [JsonPropertyName("foodNutrients")] public List<FdcFoodDetailNutrient> FoodNutrients { get; set; } = [];

    [JsonPropertyName("foodPortions")] public List<FdcFoodPortion> FoodPortions { get; set; } = [];
}

/// <summary>One nutrient row of a food detail.</summary>
public class FdcFoodDetailNutrient
{
    [JsonPropertyName("nutrientId")] public int? NutrientId { get; set; }

    [JsonPropertyName("nutrient")] public FdcNutrient? Nutrient { get; set; }

    [JsonPropertyName("number")] public string? Number { get; set; }

    [JsonPropertyName("nutrientNumber")] public string? NutrientNumber { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("nutrientName")] public string? NutrientName { get; set; }

    [JsonPropertyName("unitName")] public string? UnitName { get; set; }

    [JsonPropertyName("amount")] public float? Amount { get; set; }

    [JsonPropertyName("value")] public float? Value { get; set; }
}

/// <summary>The nutrient a row refers to, whose number is what the app matches.</summary>
public class FdcNutrient
{
    [JsonPropertyName("id")] public int Id { get; set; }

    [JsonPropertyName("number")] public string? Number { get; set; }

    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("unitName")] public string? UnitName { get; set; }
}

/// <summary>One portion the source offers, with its weight and household measure.</summary>
public class FdcFoodPortion
{
    [JsonPropertyName("amount")] public float? Amount { get; set; }

    [JsonPropertyName("gramWeight")] public float? GramWeight { get; set; }

    [JsonPropertyName("modifier")] public string? Modifier { get; set; }

    [JsonPropertyName("portionDescription")]
    /// <summary>The household wording of a portion, which becomes the option's label.</summary>
    public string? PortionDescription { get; set; }

    [JsonPropertyName("measureUnit")] public FdcMeasureUnit? MeasureUnit { get; set; }
}

/// <summary>The unit a portion's amount is expressed in.</summary>
public class FdcMeasureUnit
{
    [JsonPropertyName("name")] public string? Name { get; set; }

    [JsonPropertyName("abbreviation")] public string? Abbreviation { get; set; }
}


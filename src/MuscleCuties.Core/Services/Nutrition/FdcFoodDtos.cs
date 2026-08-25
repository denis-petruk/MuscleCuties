using System.Text.Json.Serialization;

namespace MuscleCuties.Core.Services.Nutrition;

public class FdcFoodSearchResponse
{
    [JsonPropertyName("foods")]
    public List<FdcFoodSearchResult> Foods { get; set; } = [];
}

public class FdcFoodSearchResult
{
    [JsonPropertyName("fdcId")]
    public int FdcId { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }

    [JsonPropertyName("brandOwner")]
    public string? BrandOwner { get; set; }

    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    [JsonPropertyName("gtinUpc")]
    public string? GtinUpc { get; set; }

    [JsonPropertyName("ingredients")]
    public string? Ingredients { get; set; }

    [JsonPropertyName("servingSize")]
    public float? ServingSize { get; set; }

    [JsonPropertyName("servingSizeUnit")]
    public string? ServingSizeUnit { get; set; }

    [JsonPropertyName("householdServingFullText")]
    public string? HouseholdServingFullText { get; set; }

    [JsonPropertyName("packageWeight")]
    public string? PackageWeight { get; set; }

    [JsonPropertyName("foodNutrients")]
    public List<FdcSearchNutrient> FoodNutrients { get; set; } = [];
}

public class FdcSearchNutrient
{
    [JsonPropertyName("nutrientId")]
    public int NutrientId { get; set; }

    [JsonPropertyName("value")]
    public float? Value { get; set; }
}

public class FdcFoodDetail
{
    [JsonPropertyName("fdcId")]
    public int FdcId { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("dataType")]
    public string? DataType { get; set; }

    [JsonPropertyName("brandOwner")]
    public string? BrandOwner { get; set; }

    [JsonPropertyName("brandName")]
    public string? BrandName { get; set; }

    [JsonPropertyName("gtinUpc")]
    public string? GtinUpc { get; set; }

    [JsonPropertyName("ingredients")]
    public string? Ingredients { get; set; }

    [JsonPropertyName("servingSize")]
    public float? ServingSize { get; set; }

    [JsonPropertyName("servingSizeUnit")]
    public string? ServingSizeUnit { get; set; }

    [JsonPropertyName("householdServingFullText")]
    public string? HouseholdServingFullText { get; set; }

    [JsonPropertyName("packageWeight")]
    public string? PackageWeight { get; set; }

    [JsonPropertyName("foodNutrients")]
    public List<FdcFoodDetailNutrient> FoodNutrients { get; set; } = [];

    [JsonPropertyName("foodPortions")]
    public List<FdcFoodPortion> FoodPortions { get; set; } = [];
}

public class FdcFoodDetailNutrient
{
    [JsonPropertyName("nutrientId")]
    public int? NutrientId { get; set; }

    [JsonPropertyName("nutrient")]
    public FdcNutrient? Nutrient { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("nutrientNumber")]
    public string? NutrientNumber { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("nutrientName")]
    public string? NutrientName { get; set; }

    [JsonPropertyName("unitName")]
    public string? UnitName { get; set; }

    [JsonPropertyName("amount")]
    public float? Amount { get; set; }

    [JsonPropertyName("value")]
    public float? Value { get; set; }
}

public class FdcNutrient
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("number")]
    public string? Number { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("unitName")]
    public string? UnitName { get; set; }
}

public class FdcFoodPortion
{
    [JsonPropertyName("amount")]
    public float? Amount { get; set; }

    [JsonPropertyName("gramWeight")]
    public float? GramWeight { get; set; }

    [JsonPropertyName("modifier")]
    public string? Modifier { get; set; }

    [JsonPropertyName("portionDescription")]
    public string? PortionDescription { get; set; }

    [JsonPropertyName("measureUnit")]
    public FdcMeasureUnit? MeasureUnit { get; set; }
}

public class FdcMeasureUnit
{
    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("abbreviation")]
    public string? Abbreviation { get; set; }
}

public class FdcFoodsRequest
{
    [JsonPropertyName("fdcIds")]
    public List<int> FdcIds { get; set; } = [];

    [JsonPropertyName("format")]
    public string Format { get; set; } = "full";
}

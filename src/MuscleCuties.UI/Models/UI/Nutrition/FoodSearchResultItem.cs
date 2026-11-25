namespace MuscleCuties.UI.Models.UI.Nutrition;

/// <summary>A food returned by the catalog search, with the serving data the portion picker needs.</summary>
public class FoodSearchResultItem
{
    /// <summary>Catalog identity of the food.</summary>
    public int FoodItemId { get; set; }
    /// <summary>Food name as the search returned it.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Calories for one serving.</summary>
    public float Calories { get; set; }
    /// <summary>Protein grams for one serving.</summary>
    public float Protein { get; set; }
    /// <summary>Carbohydrate grams for one serving.</summary>
    public float Carbs { get; set; }
    /// <summary>Fat grams for one serving.</summary>
    public float Fats { get; set; }
    /// <summary>Size of one serving, in <c>ServingSizeUnit</c>; null when the food has no defined serving.</summary>
    public float? ServingSize { get; set; }
    /// <summary>Unit of <c>ServingSize</c> (g, ml, or a household measure).</summary>
    public string? ServingSizeUnit { get; set; }
    /// <summary>Stored serving options as JSON, parsed only when the portion picker opens.</summary>
    public string? ServingOptionsJson { get; set; }
    /// <summary>Where the nutrition values came from, shown under the name.</summary>
    public string SourceSummary { get; set; } = string.Empty;
    /// <summary>Preformatted macro line for the result row.</summary>
    public string NutritionSummary { get; set; } = string.Empty;
}

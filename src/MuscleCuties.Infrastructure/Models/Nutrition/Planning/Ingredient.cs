using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Infrastructure.Models.Nutrition;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

/// <summary>One food's contribution to a plan: the food's per-100 g values scaled to the weight being planned.</summary>
public sealed class Ingredient
{
    internal static readonly string[] MeatTerms =
        ["chicken", "salmon", "tuna", "turkey", "beef", "shrimp", "cod", "pork", "ham", "lamb"];

    internal static readonly string[] DairyTerms =
        ["cheese", "yogurt", "milk", "cream", "butter", "whey"];

    internal static readonly string[] EggTerms = ["egg"];

    internal static readonly string[] GlutenTerms =
        ["wheat", "bread", "pasta", "couscous", "barley", "rye", "flour", "noodle"];

    private static readonly string[] VeganLabelTerms =
        ["vegan", "plant-based", "plant based"];

    private static readonly string[] VegetarianLabelTerms =
        ["vegetarian"];

    private static readonly string[] GlutenFreeLabelTerms =
        ["gluten-free", "gluten free"];

    private static readonly string[] LactoseFreeLabelTerms =
        ["lactose-free", "lactose free", "dairy-free", "dairy free"];

    private static readonly string[] EggFreeLabelTerms =
        ["egg-free", "egg free"];

    private static readonly string[] PlantYogurtTerms =
        ["soy", "soya", "coconut", "almond", "oat", "cashew", "plant-based", "plant based"];

    private static readonly string[] OtherAnimalDairyTerms =
        ["cheese", "milk", "cream", "butter", "whey", "casein"];

    /// <summary>Takes the food and the weight, which is all the scaling needs.</summary>
    public Ingredient(FoodItem food, float grams)
    {
        Food = food;
        Grams = grams;
    }

    /// <summary>Food the ingredient uses.</summary>
    public FoodItem Food { get; }
    /// <summary>Grams of the food.</summary>
    public float Grams { get; }

    /// <summary>The energy this weight contributes.</summary>
    public float Calories => Scale(Food.Calories);
    /// <summary>The protein this weight contributes.</summary>
    public float Protein => Scale(Food.Protein);
    /// <summary>The carbohydrate this weight contributes.</summary>
    public float Carbs => Scale(Food.Carbs);
    /// <summary>The fat this weight contributes.</summary>
    public float Fats => Scale(Food.Fats);
    /// <summary>The fibre this weight contributes.</summary>
    public float Fiber => Scale(Food.Fiber);
    /// <summary>The iron this weight contributes.</summary>
    public float Iron => Scale(Food.Iron);
    /// <summary>The vitamin B12 this weight contributes.</summary>
    public float VitaminB12 => Scale(Food.VitaminB12);
    /// <summary>The vitamin C this weight contributes.</summary>
    public float VitaminC => Scale(Food.VitaminC);
    /// <summary>The vitamin D this weight contributes.</summary>
    public float VitaminD => Scale(Food.VitaminD);
    /// <summary>The vitamin A this weight contributes.</summary>
    public float VitaminA => Scale(Food.VitaminA);
    /// <summary>The vitamin B6 this weight contributes.</summary>
    public float VitaminB6 => Scale(Food.VitaminB6);
    /// <summary>The folate this weight contributes.</summary>
    public float Folate => Scale(Food.Folate);
    /// <summary>The calcium this weight contributes.</summary>
    public float Calcium => Scale(Food.Calcium);
    /// <summary>The magnesium this weight contributes.</summary>
    public float Magnesium => Scale(Food.Magnesium);
    /// <summary>The zinc this weight contributes.</summary>
    public float Zinc => Scale(Food.Zinc);
    /// <summary>The potassium this weight contributes.</summary>
    public float Potassium => Scale(Food.Potassium);

    /// <summary>The four macro nutrients at this weight, in the shape the planner's summaries read.</summary>
    public MacroNutrients Macros => MacroNutrientsFoodExtensions.FromFood(Food, Grams);

    /// <summary>Whether the food's name reads as vegetarian — a name-based screen, not a label claim.</summary>
    public bool IsVegetarian => CheckVegetarian(Food.Name);
    /// <summary>Whether the name reads as vegan, with the plant-yogurt exception the words alone would miss.</summary>
    public bool IsVegan => CheckVegan(Food.Name);
    /// <summary>Whether the name reads as gluten free.</summary>
    public bool IsGlutenFree => CheckGlutenFree(Food.Name);
    /// <summary>Whether the name reads as lactose free.</summary>
    public bool IsLactoseFree => CheckLactoseFree(Food.Name);

    /// <summary>Whether a food satisfies every requested dietary tag, which is what excludes it from or admits it to a</summary>
    /// plan.</summary>
    public static bool MatchesDietaryTags(FoodItem food, IReadOnlySet<DietaryTag> tags)
    {
        if (tags.Count == 0)
            return true;

        var name = food.Name;

        foreach (var tag in tags)
        {
            var passes = tag switch
            {
                DietaryTag.Vegan => CheckVegan(name),
                DietaryTag.Vegetarian => CheckVegetarian(name),
                DietaryTag.GlutenFree => CheckGlutenFree(name),
                DietaryTag.LactoseFree => CheckLactoseFree(name),
                _ => true
            };

            if (!passes)
                return false;
        }

        return true;
    }

    /// <summary>Screens a name for vegetarian markers, since the catalog carries no dietary flags of its own.</summary>
    private static bool CheckVegetarian(string name)
    {
        if (ContainsAny(name, VeganLabelTerms) || ContainsAny(name, VegetarianLabelTerms))
            return true;

        return !ContainsAny(name, MeatTerms);
    }

    /// <summary>Screens a name for vegan markers, after the plant-yogurt case is settled.</summary>
    private static bool CheckVegan(string name)
    {
        if (ContainsAny(name, VeganLabelTerms))
            return true;

        if (IsPlantYogurt(name))
            return !ContainsAny(name, MeatTerms) &&
                   !ContainsAny(name, EggTerms) &&
                   !ContainsAny(name, OtherAnimalDairyTerms) &&
                   !ContainsAny(name, "honey");

        return (!ContainsAny(name, MeatTerms) &&
                !ContainsAny(name, DairyTerms) &&
                !ContainsAny(name, EggTerms) &&
                !ContainsAny(name, "honey")) ||
               (ContainsAny(name, LactoseFreeLabelTerms) &&
                !ContainsAny(name, MeatTerms) &&
                !ContainsAny(name, EggTerms) &&
                !ContainsAny(name, "honey")) ||
               (ContainsAny(name, EggFreeLabelTerms) &&
                !ContainsAny(name, MeatTerms) &&
                !ContainsAny(name, DairyTerms) &&
                !ContainsAny(name, "honey"));
    }

    /// <summary>Screens a name for the grains and words that make it not gluten free.</summary>
    private static bool CheckGlutenFree(string name)
    {
        if (ContainsAny(name, GlutenFreeLabelTerms))
            return true;

        return !ContainsAny(name, GlutenTerms);
    }

    /// <summary>Screens a name for dairy, which is the only lactose signal the catalog has.</summary>
    private static bool CheckLactoseFree(string name)
    {
        if (IsPlantYogurt(name))
            return true;

        if (ContainsAny(name, LactoseFreeLabelTerms) || ContainsAny(name, VeganLabelTerms))
            return true;

        return !ContainsAny(name, DairyTerms);
    }

    /// <summary>A per-100 g value converted to this ingredient's weight, which is the one place the conversion happens.</summary>
    private float Scale(float per100g)
    {
        return per100g * Grams / 100f;
    }

    /// <summary>Whether a yogurt is plant based, because the word "yogurt" alone would wrongly rule it out for a vegan</summary>
    /// plan.</summary>
    private static bool IsPlantYogurt(string name)
    {
        return name.Contains("yogurt", StringComparison.OrdinalIgnoreCase) &&
               ContainsAny(name, PlantYogurtTerms);
    }

    /// <summary>Whether a value contains any of the terms, ignoring case, which is how the name screens are written.</summary>
    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (var term in terms)
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;

        return false;
    }
}

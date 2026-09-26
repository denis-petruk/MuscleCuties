using MuscleCuties.Core.Models.Entities.Nutrition;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.Nutrition.Planning;

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

    public FoodItem Food { get; }
    public float Grams { get; }

    public Ingredient(FoodItem food, float grams)
    {
        Food = food;
        Grams = grams;
    }

    public float Calories => Scale(Food.Calories);
    public float Protein => Scale(Food.Protein);
    public float Carbs => Scale(Food.Carbs);
    public float Fats => Scale(Food.Fats);
    public float Fiber => Scale(Food.Fiber);
    public float Iron => Scale(Food.Iron);
    public float VitaminB12 => Scale(Food.VitaminB12);
    public float VitaminC => Scale(Food.VitaminC);
    public float VitaminD => Scale(Food.VitaminD);
    public float VitaminA => Scale(Food.VitaminA);
    public float VitaminB6 => Scale(Food.VitaminB6);
    public float Folate => Scale(Food.Folate);
    public float Calcium => Scale(Food.Calcium);
    public float Magnesium => Scale(Food.Magnesium);
    public float Zinc => Scale(Food.Zinc);
    public float Potassium => Scale(Food.Potassium);

    public MacroNutrients Macros => MacroNutrients.FromFood(Food, Grams);

    public bool IsVegetarian => CheckVegetarian(Food.Name);
    public bool IsVegan => CheckVegan(Food.Name);
    public bool IsGlutenFree => CheckGlutenFree(Food.Name);
    public bool IsLactoseFree => CheckLactoseFree(Food.Name);

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

    private static bool CheckVegetarian(string name)
    {
        if (ContainsAny(name, VeganLabelTerms) || ContainsAny(name, VegetarianLabelTerms))
            return true;

        return !ContainsAny(name, MeatTerms);
    }

    private static bool CheckVegan(string name)
    {
        if (ContainsAny(name, VeganLabelTerms))
            return true;

        if (IsPlantYogurt(name))
            return !ContainsAny(name, MeatTerms) &&
                   !ContainsAny(name, EggTerms) &&
                   !ContainsAny(name, OtherAnimalDairyTerms) &&
                   !ContainsAny(name, ["honey"]);

        return !ContainsAny(name, MeatTerms) &&
               !ContainsAny(name, DairyTerms) &&
               !ContainsAny(name, EggTerms) &&
               !ContainsAny(name, ["honey"]) ||
               ContainsAny(name, LactoseFreeLabelTerms) &&
               !ContainsAny(name, MeatTerms) &&
               !ContainsAny(name, EggTerms) &&
               !ContainsAny(name, ["honey"]) ||
               ContainsAny(name, EggFreeLabelTerms) &&
               !ContainsAny(name, MeatTerms) &&
               !ContainsAny(name, DairyTerms) &&
               !ContainsAny(name, ["honey"]);
    }

    private static bool CheckGlutenFree(string name)
    {
        if (ContainsAny(name, GlutenFreeLabelTerms))
            return true;

        return !ContainsAny(name, GlutenTerms);
    }

    private static bool CheckLactoseFree(string name)
    {
        if (IsPlantYogurt(name))
            return true;

        if (ContainsAny(name, LactoseFreeLabelTerms) || ContainsAny(name, VeganLabelTerms))
            return true;

        return !ContainsAny(name, DairyTerms);
    }

    private float Scale(float per100g) => per100g * Grams / 100f;

    private static bool IsPlantYogurt(string name)
        => name.Contains("yogurt", StringComparison.OrdinalIgnoreCase) &&
           ContainsAny(name, PlantYogurtTerms);

    private static bool ContainsAny(string value, params string[] terms)
    {
        foreach (var term in terms)
        {
            if (value.Contains(term, StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return false;
    }
}

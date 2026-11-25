namespace MuscleCuties.Core.Models.Nutrition;

/// <summary>One set of macro figures in grams, which every nutrition rule consumes.</summary>
public readonly record struct MacroNutrients(
    float Calories,
    float Protein,
    float Carbs,
    float Fats)
{
    /// <summary>An all-zero set, used when a day has no entries.</summary>
    public static MacroNutrients Empty => new(0f, 0f, 0f, 0f);

    /// <summary>The calories the protein accounts for, at four per gram.</summary>
    public float ProteinCalories => Protein * 4f;
    /// <summary>The calories the carbs account for, at four per gram.</summary>
    public float CarbsCalories => Carbs * 4f;
    /// <summary>The calories the fats account for, at nine per gram.</summary>
    public float FatsCalories => Fats * 9f;

    /// <summary>Scales a per-100-gram nutrient profile to the given gram count.</summary>
    public static MacroNutrients FromPer100g(
        float calories,
        float protein,
        float carbs,
        float fats,
        float grams)
    {
        var ratio = grams / 100f;
        return new MacroNutrients(
            calories * ratio,
            protein * ratio,
            carbs * ratio,
            fats * ratio);
    }

    /// <summary>The two sets added together, which the day's totals are built with.</summary>
    public static MacroNutrients Sum(IEnumerable<MacroNutrients> values)
    {
        return values.Aggregate(Empty, (total, next) => total + next);
    }

    public static MacroNutrients operator +(MacroNutrients left, MacroNutrients right)
    {
        return new MacroNutrients(
            left.Calories + right.Calories,
            left.Protein + right.Protein,
            left.Carbs + right.Carbs,
            left.Fats + right.Fats);
    }

    /// <summary>The figures as the nutrition screen words them.</summary>
    public string ToNutritionText()
    {
        return $"{Calories:N0} kcal · P {Protein:N1}g · C {Carbs:N1}g · F {Fats:N1}g";
    }

    /// <summary>The figures as a compact macro line, for the row that has little room.</summary>
    public string ToMacroText()
    {
        return $"P {Protein:N1}g · C {Carbs:N1}g · F {Fats:N1}g";
    }
}

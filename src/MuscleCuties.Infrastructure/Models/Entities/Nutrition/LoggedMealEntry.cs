namespace MuscleCuties.Core.Models.Entities.Nutrition;

/// <summary>One food inside a logged meal, with the weight it was eaten at.</summary>
public class LoggedMealEntry
{
    /// <summary>Identity of the stored logged meal entry.</summary>
    public int Id { get; set; }
    /// <summary>The meal the entry belongs to.</summary>
    public int LoggedMealId { get; set; }
    /// <summary>The food it refers to.</summary>
    public int FoodItemId { get; set; }
    /// <summary>How much of the food was eaten, in grams, which is the one unit the numbers are scaled from.</summary>
    public float Grams { get; set; }

    /// <summary>The navigation to the meal.</summary>
    public LoggedMeal? LoggedMeal { get; set; }
    /// <summary>The navigation to the food.</summary>
    public FoodItem? FoodItem { get; set; }
}

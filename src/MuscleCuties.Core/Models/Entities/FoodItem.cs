using System.ComponentModel.DataAnnotations;

namespace MuscleCuties.Core.Models.Entities;

public class FoodItem
{
    public int Id { get; set; }
    [Required] public string Name { get; set; } = null!;
    public float Calories { get; set; }
    public float Protein { get; set; }
    public float Carbs { get; set; }
    public float Fats { get; set; }
    // mg per 100g
    public float Iron { get; set; }
    // µg per 100g
    public float VitaminB12 { get; set; }
    public bool IsCustom { get; set; }

    public ICollection<FoodLog> FoodLogs { get; set; } = new List<FoodLog>();
}

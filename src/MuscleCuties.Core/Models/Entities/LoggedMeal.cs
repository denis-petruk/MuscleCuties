using MuscleCuties.Core.Models.Enums;

namespace MuscleCuties.Core.Models.Entities;

public class LoggedMeal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }
    public MealType MealType { get; set; }
    public int? MealTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public MealTemplate? MealTemplate { get; set; }
    public ICollection<LoggedMealEntry> Entries { get; set; } = new List<LoggedMealEntry>();
}
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

public class LoggedMeal
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public DateTime Date { get; set; }
    public DateTime LoggedAt { get; set; }
    public MealType MealType { get; set; }
    public int? MealTemplateId { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public MealTemplate? MealTemplate { get; set; }
    public ICollection<LoggedMealEntry> Entries { get; set; } = new List<LoggedMealEntry>();
}

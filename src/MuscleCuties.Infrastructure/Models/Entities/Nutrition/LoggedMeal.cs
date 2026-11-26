using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

/// <summary>One meal on one day, holding the entries it was made of.</summary>
public class LoggedMeal
{
    /// <summary>Identity of the stored logged meal.</summary>
    public int Id { get; set; }
    /// <summary>The account the meal belongs to.</summary>
    public int UserId { get; set; }
    /// <summary>The day the meal is logged on.</summary>
    public DateTime Date { get; set; }
    /// <summary>When it was written, which orders the day's meals.</summary>
    public DateTime LoggedAt { get; set; }
    /// <summary>Which meal of the day it is.</summary>
    public MealType MealType { get; set; }
    /// <summary>The suggestion she accepted, kept so a plan can tell a suggested meal from one she built herself.</summary>
    public string? AcceptedSuggestionName { get; set; }
    /// <summary>The phase the suggestion was made for, which is how the plan learns what worked in each phase.</summary>
    public CyclePhase? AcceptedSuggestionPhase { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>The navigation to the account.</summary>
    public User? User { get; set; }
    /// <summary>The foods the meal was made of.</summary>
    public ICollection<LoggedMealEntry> Entries { get; set; } = new List<LoggedMealEntry>();
}

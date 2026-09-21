using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Nutrition;

namespace MuscleCuties.Core.Models.Entities.Nutrition;

public class MealTemplate
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public string Name { get; set; } = null!;
    public string? Description { get; set; }
    public MealType MealType { get; set; }
    public string DietaryTags { get; set; } = string.Empty;
    public string PhaseTags { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsSystem { get; set; }
    public DateTime CreatedAt { get; set; }

    public User? User { get; set; }
    public ICollection<MealTemplateEntry> Entries { get; set; } = new List<MealTemplateEntry>();
}

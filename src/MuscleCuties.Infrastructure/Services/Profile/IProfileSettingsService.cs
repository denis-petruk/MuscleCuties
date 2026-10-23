using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Profile;

public sealed record NutritionSettingsUpdate(
    UserGoal Goal,
    WeightGoalPace WeightGoalPace,
    string DietaryTags,
    string? NutritionGoalsJson);

public interface IProfileSettingsService
{
    Task<UserProfile?> GetProfileAsync(int userId);
    Task<bool> SaveNutritionAsync(int userId, NutritionSettingsUpdate update);
    Task<bool> SaveWorkoutPreferencesAsync(int userId, string preferredActivityTypes);
}

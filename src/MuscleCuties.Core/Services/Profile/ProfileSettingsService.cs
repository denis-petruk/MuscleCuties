using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Nutrition.Planning;
using MuscleCuties.Core.Repositories.Users;

namespace MuscleCuties.Core.Services.Profile;

public sealed class ProfileSettingsService(IUserRepository profiles) : IProfileSettingsService
{
    public Task<UserProfile?> GetProfileAsync(int userId) => profiles.GetProfileAsync(userId);

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    public async Task<bool> SaveNutritionAsync(int userId, NutritionSettingsUpdate update)
    {
        var profile = await profiles.GetProfileAsync(userId);
        if (profile is null)
            return false;

        profile.Goal = update.Goal;
        profile.WeightGoalPace = update.WeightGoalPace;
        profile.DietaryTags = update.DietaryTags;
        if (update.NutritionGoalsJson is not null)
            profile.NutritionGoalsJson = update.NutritionGoalsJson;
        profile.UpdatedAt = DateTime.UtcNow;

        await profiles.UpdateProfileAsync(profile);
        await profiles.AddSnapshotAsync(new UserProfileSnapshot
        {
            UserId = userId,
            SnapshotReason = "NutritionSettingsUpdate",
            ProfileJson = JsonSerializer.Serialize(new
            {
                Goal = profile.Goal.ToString(),
                WeightGoalPace = profile.WeightGoalPace.ToString(),
                profile.DietaryTags,
                NutritionGoals = ProfileNutritionGoals.FromJson(profile.NutritionGoalsJson)
            }),
            CreatedAt = DateTime.UtcNow
        });
        return true;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026", Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    public async Task<bool> SaveWorkoutPreferencesAsync(int userId, string preferredActivityTypes)
    {
        var profile = await profiles.GetProfileAsync(userId);
        if (profile is null)
            return false;

        profile.PreferredWorkoutActivityTypes = preferredActivityTypes;
        profile.UpdatedAt = DateTime.UtcNow;
        await profiles.UpdateProfileAsync(profile);
        await profiles.AddSnapshotAsync(new UserProfileSnapshot
        {
            UserId = userId,
            SnapshotReason = "WorkoutPreferencesUpdate",
            ProfileJson = JsonSerializer.Serialize(new { profile.PreferredWorkoutActivityTypes }),
            CreatedAt = DateTime.UtcNow
        });
        return true;
    }
}

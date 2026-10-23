using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Infrastructure.Nutrition.Planning;

internal static class ProfileNutritionGoalsJsonCodec
{
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The existing nutrition goals JSON shape is retained.")]
    public static ProfileNutritionGoals FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return ProfileNutritionGoals.Empty;

        try
        {
            return JsonSerializer.Deserialize<ProfileNutritionGoals>(json) ?? ProfileNutritionGoals.Empty;
        }
        catch (JsonException)
        {
            return ProfileNutritionGoals.Empty;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The existing nutrition goals JSON shape is retained.")]
    public static string ToJson(ProfileNutritionGoals goals)
    {
        return JsonSerializer.Serialize(goals);
    }
}

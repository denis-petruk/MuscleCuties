using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MuscleCuties.Core.Models.Nutrition.Planning;

namespace MuscleCuties.Infrastructure.Nutrition.Planning;

/// <summary>Reads and writes the nutrition targets in the profile's stored JSON, keeping the shape tolerant of keys it does</summary>
/// not know.</summary>
internal static class ProfileNutritionGoalsJsonCodec
{
    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "The existing nutrition goals JSON shape is retained.")]
    /// <summary>The stored JSON as goals, with missing targets staying unset rather than becoming zero.</summary>
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
    /// <summary>The goals back in the stored shape.</summary>
    public static string ToJson(ProfileNutritionGoals goals)
    {
        return JsonSerializer.Serialize(goals);
    }
}

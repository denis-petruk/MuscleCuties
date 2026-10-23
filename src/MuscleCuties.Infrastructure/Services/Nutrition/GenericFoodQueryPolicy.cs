using MuscleCuties.Core.Domain.Nutrition;

namespace MuscleCuties.Core.Services.Nutrition;

/// <summary>Compatibility entry point for the pure generic-food query rule.</summary>
public static class GenericFoodQueryPolicy
{
    public static string NormalizeOrEmpty(string? query)
    {
        return GenericFoodQueryRule.NormalizeOrEmpty(query);
    }
}

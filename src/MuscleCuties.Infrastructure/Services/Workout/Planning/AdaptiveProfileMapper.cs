using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public static class AdaptiveProfileMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    internal static string SerializeBaselines(CyclePhaseBaselines baselines)
    {
        return JsonSerializer.Serialize(baselines, JsonOptions);
    }
}

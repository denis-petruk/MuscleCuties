using System.Text.Json;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Services.Workout.Planning;

public partial class WorkoutPlanner
{
    private static PhaseBaseline ReadPhaseBaseline(UserProfileSnapshot? snapshot, CyclePhase phase)
    {
        if (snapshot is null || string.IsNullOrWhiteSpace(snapshot.ProfileJson))
            return PhaseBaseline.Default;

        try
        {
            using var document = JsonDocument.Parse(snapshot.ProfileJson);
            if (!document.RootElement.TryGetProperty("CyclePhaseBaselines", out var baselines) ||
                !baselines.TryGetProperty(phase.ToString(), out var phaseBaseline))
            {
                return PhaseBaseline.Default;
            }

            return new PhaseBaseline(
                ReadOptionalInt(phaseBaseline, "Pain"),
                ReadOptionalInt(phaseBaseline, "Energy"));
        }
        catch (JsonException)
        {
            return PhaseBaseline.Default;
        }
    }

    private static int ReadOptionalInt(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.GetInt32()
            : 0;

    private sealed record PhaseBaseline(int Pain, int Energy)
    {
        public static PhaseBaseline Default { get; } = new(0, 0);
    }
}

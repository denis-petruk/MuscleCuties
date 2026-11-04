using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutStartContractCompatibilityTests
{
    private const int SyntheticPlannedDayId = 17;

    [Fact]
    public void LegacyPlannedStartHasNoUnplannedClassification()
    {
        const string legacy = "{\"IdempotencyKey\":\"synthetic\",\"SessionId\":\"00000000-0000-0000-0000-000000000001\",\"WorkoutName\":\"Workout\",\"StartedAtUtc\":\"2026-01-01T00:00:00Z\",\"PlannedWorkoutDayId\":17}";
        var command = JsonSerializer.Deserialize<StartWorkoutSessionCommand>(legacy);

        Assert.NotNull(command);
        Assert.Equal(SyntheticPlannedDayId, command.PlannedWorkoutDayId);
        Assert.Null(command.UnplannedActivity);
    }
}

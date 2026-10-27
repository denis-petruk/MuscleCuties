using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutSessionHistoryContractTests
{
    [Fact]
    public void HistoryPreservesDistinctIdentitiesAndCompletionStates()
    {
        var started = new DateTime(2026, 1, 1, 10, 0, 0, DateTimeKind.Utc);
        var completed = started.AddMinutes(20);
        var entries = new[]
        {
            new PlannedWorkoutSessionHistoryEntry(Guid.NewGuid(), started, completed),
            new PlannedWorkoutSessionHistoryEntry(Guid.NewGuid(), completed, null)
        };

        var result = JsonSerializer.Deserialize<PlannedWorkoutSessionHistoryEntry[]>(JsonSerializer.Serialize(entries));

        Assert.NotNull(result);
        Assert.Equal(entries[0], result[0]);
        Assert.Equal(entries[1], result[1]);
        Assert.NotEqual(result[0].SessionId, result[1].SessionId);
        Assert.NotNull(result[0].CompletedAtUtc);
        Assert.Null(result[1].CompletedAtUtc);
    }
}

using System.Text.Json;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPreviewOrderContractTests
{
    private const int SyntheticActivity = 2;
    private const decimal SyntheticDuration = 20m;
    private const int SecondOrder = 2;

    [Fact]
    public void PreviewOrderRoundTripsAndOlderPayloadRemainsUnknown()
    {
        var session = new MixedActivityScheduleSessionV020(SyntheticActivity, DayOfWeek.Monday,
            SyntheticDuration, false, SecondOrder);
        var restored = JsonSerializer.Deserialize<MixedActivityScheduleSessionV020>(JsonSerializer.Serialize(session));
        var legacy = JsonSerializer.Deserialize<MixedActivityScheduleSessionV020>(
            "{\"ActivityTypeId\":2,\"Weekday\":1,\"DurationMinutes\":20,\"IsHardSession\":false}");

        Assert.Equal(SecondOrder, restored!.DayOrder);
        Assert.Null(legacy!.DayOrder);
    }
}

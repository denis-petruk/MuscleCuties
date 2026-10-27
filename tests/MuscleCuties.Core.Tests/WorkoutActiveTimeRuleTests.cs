using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutActiveTimeRuleTests
{
    private const int TenSeconds = 10;
    private const int FiveSeconds = 5;
    private const int ElevenSeconds = 11;
    private const int NegativeElapsedSeconds = -1;

    [Fact]
    public void CumulativeCheckpointCannotDecreaseOrExceedServerElapsedTime()
    {
        var elapsed = TimeSpan.FromSeconds(TenSeconds);
        Assert.True(WorkoutActiveTimeRule.IsValidCheckpoint(TenSeconds, FiveSeconds, elapsed));
        Assert.False(WorkoutActiveTimeRule.IsValidCheckpoint(FiveSeconds, TenSeconds, elapsed));
        Assert.False(WorkoutActiveTimeRule.IsValidCheckpoint(ElevenSeconds, FiveSeconds, elapsed));
        Assert.False(WorkoutActiveTimeRule.IsValidCheckpoint(FiveSeconds, FiveSeconds, TimeSpan.FromSeconds(NegativeElapsedSeconds)));
    }
}

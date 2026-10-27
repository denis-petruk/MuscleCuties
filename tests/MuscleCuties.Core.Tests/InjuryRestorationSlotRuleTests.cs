using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class InjuryRestorationSlotRuleTests
{
    private const int OriginalExerciseId = 42;

    [Fact]
    public void MatchingRepeatedOccurrencesPreserveAllCausalEpisodes()
    {
        var identity = new InjuryRestorationSlotIdentity(DayOfWeek.Monday, "template", "version", 0, "slot");
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var matched = InjuryRestorationSlotRule.Match(identity,
            [new(identity, OriginalExerciseId, [first, second]), new(identity, OriginalExerciseId, [second, first])]);
        Assert.NotNull(matched);
        Assert.Equal(OriginalExerciseId, matched.OriginalExerciseId);
        Assert.Equal(new[] { first, second }.OrderBy(id => id), matched.CausalEpisodeIds);
    }

    [Fact]
    public void ChangedSlotOrIncompleteEpisodeCoverageCannotTransfer()
    {
        var identity = new InjuryRestorationSlotIdentity(DayOfWeek.Monday, "template", "version", 0, "slot");
        var episode = Guid.NewGuid();
        Assert.Null(InjuryRestorationSlotRule.Match(identity,
            [new(identity with { SlotId = "other" }, OriginalExerciseId, [episode])]));
        Assert.Null(InjuryRestorationSlotRule.Match(identity,
            [new(identity, OriginalExerciseId, [episode]), new(identity, OriginalExerciseId, [])]));
    }
}

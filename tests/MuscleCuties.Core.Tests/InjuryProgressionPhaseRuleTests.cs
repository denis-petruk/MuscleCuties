using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class InjuryProgressionPhaseRuleTests
{
    private static readonly DateOnly Clearance = new(2024, 2, 28);
    private static readonly DateOnly LastOptionalDate = new(2024, 3, 6);
    private static readonly DateOnly FirstRequiredDate = new(2024, 3, 7);

    [Fact]
    public void SevenFullDatesAfterClearanceRemainInOptionalWindow()
    {
        var episode = new InjuryRestorationEpisode(false, Clearance);
        Assert.Equal(InjuryProgressionPhase.ClearedOptionalWindow, InjuryProgressionPhaseRule.Classify(episode, Clearance));
        Assert.Equal(InjuryProgressionPhase.ClearedOptionalWindow, InjuryProgressionPhaseRule.Classify(episode, LastOptionalDate));
        Assert.Equal(InjuryProgressionPhase.ClearedAfterWindow, InjuryProgressionPhaseRule.Classify(episode, FirstRequiredDate));
    }

    [Fact]
    public void ActiveAndMissingEvidenceDoNotImplyClearance()
    {
        Assert.Equal(InjuryProgressionPhase.Active, InjuryProgressionPhaseRule.Classify(new(true, null), Clearance));
        Assert.Equal(InjuryProgressionPhase.Unknown, InjuryProgressionPhaseRule.Classify(new(false, null), Clearance));
        Assert.Equal(InjuryProgressionPhase.Unknown, InjuryProgressionPhaseRule.Classify(new(true, Clearance), Clearance));
        Assert.Equal(InjuryProgressionPhase.Unknown, InjuryProgressionPhaseRule.Classify(new(false, Clearance), null));
        Assert.Equal(InjuryProgressionPhase.Unknown, InjuryProgressionPhaseRule.Classify(new(false, FirstRequiredDate), Clearance));
        Assert.Equal(InjuryProgressionPhase.Unknown, InjuryProgressionPhaseRule.Classify(new(false, DateOnly.MaxValue), DateOnly.MaxValue));
    }
}

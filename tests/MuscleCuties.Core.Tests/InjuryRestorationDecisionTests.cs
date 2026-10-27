using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class InjuryRestorationDecisionTests
{
    [Fact]
    public void OverlappingEpisodesUseLatestOriginalDeadlineEvenWhenRestorationIsDelayed()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var latestClearance = new DateOnly(2026, 2, 4);
        var requiredFrom = new DateOnly(2026, 2, 12);
        var input = new InjuryRestorationInput(latestClearance, true, true, true, true,
            [first, second], [new(first, new DateOnly(2026, 2, 1)), new(second, latestClearance)]);
        var optional = InjuryRestorationRule.Decide(input);
        Assert.Equal(InjuryRestorationDisposition.OriginalOptional, optional.Disposition);
        Assert.Equal(requiredFrom, optional.OriginalRequiredFrom);
        var delayed = InjuryRestorationRule.Decide(input with { EffectiveDate = requiredFrom });
        Assert.Equal(InjuryRestorationDisposition.OriginalRequired, delayed.Disposition);
        Assert.Equal(requiredFrom, delayed.OriginalRequiredFrom);
    }

    [Fact]
    public void MissingOrActiveEpisodeAndUnsafeOriginalNeverRestoreOriginal()
    {
        var first = Guid.NewGuid();
        var second = Guid.NewGuid();
        var date = new DateOnly(2026, 2, 12);
        var input = new InjuryRestorationInput(date, true, true, true, true,
            [first, second], [new(first, date)]);
        Assert.Equal(InjuryRestorationDisposition.SafeAlternative, InjuryRestorationRule.Decide(input).Disposition);
        Assert.Equal(InjuryRestorationDisposition.Omit, InjuryRestorationRule.Decide(input with
        {
            LinkedEpisodes = [new(first, date), new(second, null)], AlternativeMeetsCurrentConstraints = false
        }).Disposition);
        Assert.Equal(InjuryRestorationDisposition.SafeAlternative, InjuryRestorationRule.Decide(input with
        {
            LinkedEpisodes = [new(first, date), new(second, date)], OriginalMeetsCurrentConstraints = false
        }).Disposition);
    }

    [Fact]
    public void DuplicateOrUnknownEpisodeProvenanceNeverRestoresOriginal()
    {
        var episode = Guid.NewGuid();
        var date = new DateOnly(2026, 2, 12);
        var input = new InjuryRestorationInput(date, true, true, true, false,
            [episode], [new(episode, date), new(episode, date)]);
        Assert.Equal(InjuryRestorationDisposition.Omit, InjuryRestorationRule.Decide(input).Disposition);
        Assert.Equal(InjuryRestorationDisposition.Omit, InjuryRestorationRule.Decide(input with
        {
            LinkedEpisodes = [new(episode, date)], CausalHistoryComplete = false
        }).Disposition);
    }
}

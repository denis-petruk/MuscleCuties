namespace MuscleCuties.Core.Domain.Workout.Planning;

public enum InjuryRestorationDisposition
{
    OriginalRequired,
    OriginalOptional,
    SafeAlternative,
    Omit
}

public sealed record InjuryEpisodeClearance(Guid EpisodeId, DateOnly? ClearedDate);

public sealed record InjuryRestorationInput(
    DateOnly EffectiveDate,
    bool OriginalIdentityKnown,
    bool CausalHistoryComplete,
    bool OriginalMeetsCurrentConstraints,
    bool AlternativeMeetsCurrentConstraints,
    IReadOnlyCollection<Guid> CausalEpisodeIds,
    IReadOnlyCollection<InjuryEpisodeClearance> LinkedEpisodes);

public sealed record InjuryRestorationDecision(
    InjuryRestorationDisposition Disposition,
    DateOnly? OriginalRequiredFrom);

public static class InjuryRestorationRule
{
    public static InjuryRestorationDecision Decide(InjuryRestorationInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        ArgumentNullException.ThrowIfNull(input.CausalEpisodeIds);
        ArgumentNullException.ThrowIfNull(input.LinkedEpisodes);
        var causal = input.CausalEpisodeIds.ToArray();
        var links = input.LinkedEpisodes.ToArray();
        if (links.Any(link => link is null))
            return new(input.AlternativeMeetsCurrentConstraints
                ? InjuryRestorationDisposition.SafeAlternative : InjuryRestorationDisposition.Omit, null);
        var uniqueCausal = causal.ToHashSet();
        var linkedIds = links.Select(link => link.EpisodeId).ToHashSet();
        var complete = input.OriginalIdentityKnown && input.CausalHistoryComplete &&
            causal.Length > 0 && uniqueCausal.Count == causal.Length && !uniqueCausal.Contains(Guid.Empty) &&
            linkedIds.Count == links.Length && uniqueCausal.SetEquals(linkedIds);
        var clearances = links.Select(link => link.ClearedDate).ToArray();
        var canRestore = complete && WorkoutInjuryClearanceRule.CanRestoreForAllClearances(
            clearances, input.OriginalMeetsCurrentConstraints, input.EffectiveDate);
        if (!canRestore)
            return new(input.AlternativeMeetsCurrentConstraints
                ? InjuryRestorationDisposition.SafeAlternative : InjuryRestorationDisposition.Omit, null);
        var requiredFrom = WorkoutInjuryClearanceRule.RequiredFromForAllClearances(clearances)!.Value;
        return new(input.EffectiveDate < requiredFrom
            ? InjuryRestorationDisposition.OriginalOptional : InjuryRestorationDisposition.OriginalRequired,
            requiredFrom);
    }
}

namespace MuscleCuties.Core.Domain.Workout.Planning;

public sealed record InjuryRestorationSlotIdentity(
    DayOfWeek Weekday, string TemplateId, string TemplateVersion, int SessionIndex, string SlotId);

public sealed record InjuryRestorationSlotEvidence(
    InjuryRestorationSlotIdentity? Identity, int? OriginalExerciseId,
    IReadOnlyCollection<Guid> CausalEpisodeIds);

public static class InjuryRestorationSlotRule
{
    // All occurrences of a slot must carry the same explicit provenance before a revision can inherit it.
    public static InjuryRestorationSlotEvidence? Match(InjuryRestorationSlotIdentity? proposed,
        IReadOnlyCollection<InjuryRestorationSlotEvidence> occurrences)
    {
        ArgumentNullException.ThrowIfNull(occurrences);
        if (!Valid(proposed) || occurrences.Count == 0) return null;
        var evidence = occurrences.ToArray();
        if (evidence.Any(item => item is null || item.Identity != proposed || item.OriginalExerciseId is null ||
            item.OriginalExerciseId <= 0 || item.CausalEpisodeIds is null)) return null;
        var original = evidence[0].OriginalExerciseId;
        var causes = evidence[0].CausalEpisodeIds.ToArray();
        var uniqueCauses = causes.ToHashSet();
        if (causes.Length == 0 || uniqueCauses.Count != causes.Length || uniqueCauses.Contains(Guid.Empty)) return null;
        foreach (var item in evidence)
        {
            var ids = item.CausalEpisodeIds.ToArray();
            if (item.OriginalExerciseId != original || ids.Length != uniqueCauses.Count ||
                ids.Distinct().Count() != ids.Length || !uniqueCauses.SetEquals(ids)) return null;
        }
        return new InjuryRestorationSlotEvidence(proposed, original, uniqueCauses.OrderBy(id => id).ToArray());
    }

    private static bool Valid(InjuryRestorationSlotIdentity? value) => value is not null &&
        Enum.IsDefined(value.Weekday) && value.SessionIndex >= 0 &&
        !string.IsNullOrWhiteSpace(value.TemplateId) && !string.IsNullOrWhiteSpace(value.TemplateVersion) &&
        !string.IsNullOrWhiteSpace(value.SlotId);
}

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>The slot an injury displaced, so it can be restored to the same place.</summary>
public sealed record InjuryRestorationSlotIdentity(
    DayOfWeek Weekday, string TemplateId, string TemplateVersion, int SessionIndex, string SlotId);

/// <summary>The evidence that the slot was displaced by an injury rather than removed by her.</summary>
public sealed record InjuryRestorationSlotEvidence(
    InjuryRestorationSlotIdentity? Identity, int? OriginalExerciseId,
    IReadOnlyCollection<Guid> CausalEpisodeIds);

/// <summary>Restores a displaced slot only when the injury that took it has healed.</summary>
public static class InjuryRestorationSlotRule
{
    // All occurrences of a slot must carry the same explicit provenance before a revision can inherit it.
    /// <summary>Finds the stored evidence for a proposed restoration slot, when it matches.</summary>
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

    /// <summary>True when the slot identity carries every field it needs.</summary>
    private static bool Valid(InjuryRestorationSlotIdentity? value) => value is not null &&
        Enum.IsDefined(value.Weekday) && value.SessionIndex >= 0 &&
        !string.IsNullOrWhiteSpace(value.TemplateId) && !string.IsNullOrWhiteSpace(value.TemplateVersion) &&
        !string.IsNullOrWhiteSpace(value.SlotId);
}

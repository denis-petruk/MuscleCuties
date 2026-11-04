namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Validates and canonicalizes a locally recorded injury observation.</summary>
public static class InjuryObservationRule
{
    private static readonly DateOnly MinimumSupportedDate = new(1970, 1, 1);
    private static readonly DateOnly MaximumSupportedDate = new(2100, 12, 31);
    private const int MinimumPain = 0;
    private const int MaximumPain = 10;
    private const int KnownInjurySiteMask = 1 | 2 | 4 | 8 | 16 | 32 | 64 | 128;

    /// <summary>Matches the stored status of a resolved injury; also the one status that lifts a restriction.</summary>
    public const string ClearedStatus = "Cleared";

    /// <summary>Stored form of <see cref="InjuryWorseningTrigger.NotAnswered"/> for query translation.</summary>
    public const string NotAnsweredStorage = nameof(InjuryWorseningTrigger.NotAnswered);

    /// <summary>Stored form of <see cref="InjuryWorseningTrigger.None"/> for query translation.</summary>
    public const string NoWorseningStorage = nameof(InjuryWorseningTrigger.None);

    /// <summary>
    /// Owner rule: recorded worsening symptoms or a clinician restriction are safety concerns, and a
    /// safety concern withholds automatic dose changes. An unanswered question never counts as calm.
    /// </summary>
    public static bool IndicatesSafetyConcern(InjuryWorseningTrigger worsening, bool? clinicianAdvisedAvoid) =>
        clinicianAdvisedAvoid == true ||
        worsening is not (InjuryWorseningTrigger.NotAnswered or InjuryWorseningTrigger.None);

    public static bool TryNormalizeStatus(string? status, out string normalized)
    {
        normalized = string.Empty;
        if (string.IsNullOrWhiteSpace(status))
            return false;

        switch (status.Trim().ToLowerInvariant())
        {
            case "acute": normalized = "Acute"; return true;
            case "recovering": normalized = "Recovering"; return true;
            case "cleared": normalized = "Cleared"; return true;
            default: return false;
        }
    }

    public static bool TryNormalizeWorseningTrigger(string? value, out InjuryWorseningTrigger normalized)
    {
        normalized = InjuryWorseningTrigger.NotAnswered;
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (!Enum.TryParse<InjuryWorseningTrigger>(value.Trim(), ignoreCase: true, out var parsed) ||
            !Enum.IsDefined(parsed))
            return false;
        normalized = parsed;
        return true;
    }

    public static bool IsValid(Guid userId, Guid id, int siteFlag, string? status, DateOnly since, int pain,
        DateOnly date, InjuryWorseningTrigger worsening, bool? clinicianAdvisedAvoid, out string normalizedStatus)
    {
        var validStatus = TryNormalizeStatus(status, out normalizedStatus);
        return userId != Guid.Empty && id != Guid.Empty && siteFlag > 0 &&
               (siteFlag & ~KnownInjurySiteMask) == 0 && validStatus &&
               Enum.IsDefined(worsening) &&
               pain >= MinimumPain && pain <= MaximumPain &&
               since >= MinimumSupportedDate && date >= MinimumSupportedDate && date <= MaximumSupportedDate &&
               since <= date;
    }
}

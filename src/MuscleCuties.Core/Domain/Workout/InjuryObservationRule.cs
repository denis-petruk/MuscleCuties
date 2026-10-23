namespace MuscleCuties.Core.Domain.Workout;

/// <summary>Validates and canonicalizes a locally recorded injury observation.</summary>
public static class InjuryObservationRule
{
    private static readonly DateOnly MinimumSupportedDate = new(1970, 1, 1);
    private static readonly DateOnly MaximumSupportedDate = new(2100, 12, 31);
    private const int MinimumPain = 0;
    private const int MaximumPain = 10;
    private const int KnownInjurySiteMask = 1 | 2 | 4 | 8 | 16 | 32 | 64 | 128;

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

    public static bool IsValid(Guid userId, Guid id, int siteFlag, string? status, DateOnly since, int pain,
        DateOnly date, out string normalizedStatus)
    {
        var validStatus = TryNormalizeStatus(status, out normalizedStatus);
        return userId != Guid.Empty && id != Guid.Empty && siteFlag > 0 &&
               (siteFlag & ~KnownInjurySiteMask) == 0 && validStatus &&
               pain >= MinimumPain && pain <= MaximumPain &&
               since >= MinimumSupportedDate && date >= MinimumSupportedDate && date <= MaximumSupportedDate &&
               since <= date;
    }
}

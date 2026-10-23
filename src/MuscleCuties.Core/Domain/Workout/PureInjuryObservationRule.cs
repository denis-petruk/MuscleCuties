namespace MuscleCuties.Core.Domain.Workout;

public sealed record PureInjuryObservation(int SiteFlag, string? Status, DateOnly Since);

public sealed record PureInjuryResult(int SiteFlag, int Status, DateOnly Since);

public static class PureInjuryObservationRule
{
    private const int Metatarsal = 1;
    private const int Knee = 2;
    private const int Ankle = 4;
    private const int Shoulder = 8;
    private const int LowBack = 16;
    private const int Wrist = 32;
    private const int Hip = 64;
    private const int Neck = 128;

    private const int KnownFlags = Metatarsal | Knee | Ankle | Shoulder | LowBack | Wrist | Hip | Neck;

    public const int StatusAcute = 0;
    public const int StatusRecovering = 1;
    public const int StatusCleared = 2;

    public static List<PureInjuryResult> Map(IReadOnlyCollection<PureInjuryObservation>? observations)
    {
        if (observations is null)
            return [];

        return observations
            .Where(item => item is not null && item.SiteFlag > 0 &&
                           (item.SiteFlag & ~KnownFlags) == 0)
            .Select(item => new PureInjuryResult(item.SiteFlag, ParseStatus(item.Status), item.Since))
            .ToList();
    }

    public static int ParseStatus(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return StatusAcute;

        return value.Trim().ToLowerInvariant() switch
        {
            "acute" => StatusAcute,
            "recovering" => StatusRecovering,
            "cleared" => StatusCleared,
            _ => StatusAcute
        };
    }

    public static int ToFlags(IEnumerable<PureInjuryResult>? injuries)
    {
        if (injuries is null)
            return 0;

        var flags = 0;
        foreach (var injury in injuries.Where(i => i.Status != StatusCleared))
            flags |= injury.SiteFlag;
        return flags;
    }

    public static bool IsContraindicated(int contraindications, int activeInjuries)
    {
        if (activeInjuries == 0 || contraindications == 0)
            return false;

        return (contraindications & activeInjuries) != 0;
    }
}

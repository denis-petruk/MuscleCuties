using System.Diagnostics.CodeAnalysis;

namespace MuscleCuties.Core.Models.Health;

public enum ObservationProvenance { Reported, Measured, Calculated, Unavailable }

public enum HealthResultStatus { Available, Limited, Unavailable }

public sealed record Observation<T>(
    T? Value,
    ObservationProvenance Provenance,
    string Source,
    DateOnly? LocalDate = null,
    DateTimeOffset? ObservedAt = null,
    DateTimeOffset? IntervalStart = null,
    DateTimeOffset? IntervalEnd = null,
    string? MissingReason = null) where T : struct
{
    public bool IsAvailable => Provenance != ObservationProvenance.Unavailable &&
                               Value.HasValue && !string.IsNullOrWhiteSpace(Source);

    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Ergonomic factory method for generic record.")]
    public static Observation<T> Missing(string reason, string source = "unavailable",
        DateOnly? localDate = null)
    {
        return new Observation<T>(null, ObservationProvenance.Unavailable, source, localDate, MissingReason: reason);
    }
}

public sealed record HealthResult<T>(
    HealthResultStatus Status,
    T? Value,
    ObservationProvenance Provenance,
    double Coverage,
    IReadOnlyList<string> Reasons,
    string? ReviewedPolicyVersion) where T : struct
{
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Ergonomic factory method for generic record.")]
    public static HealthResult<T> Unavailable(string reason, double coverage = 0)
    {
        return new HealthResult<T>(HealthResultStatus.Unavailable, null, ObservationProvenance.Unavailable,
            Math.Clamp(double.IsFinite(coverage) ? coverage : 0, 0, 1), [reason], null);
    }
}

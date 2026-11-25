using System.Diagnostics.CodeAnalysis;

namespace MuscleCuties.Core.Models.Health;

/// <summary>Whether a health value was measured by a device or entered by hand.</summary>
public enum ObservationProvenance { Reported, Measured, Calculated, Unavailable }

/// <summary>Whether a health read succeeded, was unavailable, or was refused, so a missing value is never zero.</summary>
public enum HealthResultStatus { Available, Limited, Unavailable }

/// <summary>One health observation with its provenance, so an estimate is never shown as a measurement.</summary>
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
    /// <summary>True when the observation was collected and is fresh.</summary>
    public bool IsAvailable => Provenance != ObservationProvenance.Unavailable &&
                               Value.HasValue && !string.IsNullOrWhiteSpace(Source);

    /// <summary>An observation that records why the value is unavailable.</summary>
    [SuppressMessage("Design", "CA1000:Do not declare static members on generic types",
        Justification = "Ergonomic factory method for generic record.")]
    public static Observation<T> Missing(string reason, string source = "unavailable",
        DateOnly? localDate = null)
    {
        return new Observation<T>(null, ObservationProvenance.Unavailable, source, localDate, MissingReason: reason);
    }
}

/// <summary>The outcome of a health read, carrying values only when it succeeded.</summary>
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
    /// <summary>An unavailable result, which carries no value rather than a zero.</summary>
    public static HealthResult<T> Unavailable(string reason, double coverage = 0)
    {
        return new HealthResult<T>(HealthResultStatus.Unavailable, null, ObservationProvenance.Unavailable,
            Math.Clamp(double.IsFinite(coverage) ? coverage : 0, 0, 1), [reason], null);
    }
}

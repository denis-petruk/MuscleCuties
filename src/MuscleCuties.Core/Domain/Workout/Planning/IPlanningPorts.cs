using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// Explicit time abstraction for workout planning to ensure deterministic rule evaluations
/// without ambient system clock dependencies.
/// </summary>
public interface IPlanningClockPort
{
    DateOnly GetCurrentLocalDate();
    DateTimeOffset GetUtcNow();
}

/// <summary>
/// Explicit catalog provider abstraction supplying reviewed strength templates and exercise catalogs.
/// </summary>
public interface IPlanningCatalogPort
{
    IReadOnlyList<StrengthTemplateDefinition> GetTemplatesForSchedule(int strengthDaysPerWeek);
}

/// <summary>
/// Reads the machine clock and zone so planning dates come from one injectable source.
/// </summary>
public sealed class SystemPlanningClockPort(TimeProvider? timeProvider = null) : IPlanningClockPort
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    /// <summary>Today in the account's local zone, read from the injectable provider so the day can be pinned.</summary>
    public DateOnly GetCurrentLocalDate()
    {
        var localNow = _timeProvider.GetLocalNow();
        return DateOnly.FromDateTime(localNow.DateTime);
    }

    /// <summary>The current UTC time from the injected clock.</summary>
    public DateTimeOffset GetUtcNow() => _timeProvider.GetUtcNow();
}

/// <summary>
/// Supplies the built-in strength template catalog to the planning rules.
/// </summary>
public sealed class DefaultPlanningCatalogPort : IPlanningCatalogPort
{
    /// <summary>Strength templates for a week with the given number of strength days.</summary>
    public IReadOnlyList<StrengthTemplateDefinition> GetTemplatesForSchedule(int strengthDaysPerWeek) =>
        StrengthTemplateCatalog.ForSchedule(strengthDaysPerWeek);
}

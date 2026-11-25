using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
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
/// Explicit injury and clinical safety port validating activity contraindications and movement clearance.
/// </summary>
public interface IPlanningSafetyPort
{
    IReadOnlySet<WorkoutActivityType> EvaluateRestrictedActivities(IEnumerable<Injury>? injuries);
    bool IsMovementPatternAllowed(MovementPattern pattern, IReadOnlySet<MovementPattern>? blockedPatterns);
    bool IsActivityRestricted(WorkoutActivityType activity, IReadOnlySet<WorkoutActivityType>? restrictedActivities, bool hasCurrentSymptoms);
}

/// <summary>
/// Default system-backed implementation of <see cref="IPlanningClockPort"/>.
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
/// Default catalog-backed implementation of <see cref="IPlanningCatalogPort"/>.
/// </summary>
public sealed class DefaultPlanningCatalogPort : IPlanningCatalogPort
{
    /// <summary>Strength templates for a week with the given number of strength days.</summary>
    public IReadOnlyList<StrengthTemplateDefinition> GetTemplatesForSchedule(int strengthDaysPerWeek) =>
        StrengthTemplateCatalog.ForSchedule(strengthDaysPerWeek);
}

/// <summary>
/// Default rule-backed implementation of <see cref="IPlanningSafetyPort"/> delegating to reviewed safety policies.
/// </summary>
public sealed class DefaultPlanningSafetyPort : IPlanningSafetyPort
{
    /// <summary>Activities the current injuries rule out.</summary>
    public IReadOnlySet<WorkoutActivityType> EvaluateRestrictedActivities(IEnumerable<Injury>? injuries) =>
        InjuryActivityRestrictionRule.GetBlockedActivities(injuries);

    /// <summary>True when the movement pattern is not blocked by the current restrictions.</summary>
    public bool IsMovementPatternAllowed(MovementPattern pattern, IReadOnlySet<MovementPattern>? blockedPatterns) =>
        blockedPatterns is null || !blockedPatterns.Contains(pattern);

    /// <summary>True when the activity is restricted for this profile.</summary>
    public bool IsActivityRestricted(WorkoutActivityType activity, IReadOnlySet<WorkoutActivityType>? restrictedActivities, bool hasCurrentSymptoms) =>
        restrictedActivities?.Contains(activity) ?? hasCurrentSymptoms;
}

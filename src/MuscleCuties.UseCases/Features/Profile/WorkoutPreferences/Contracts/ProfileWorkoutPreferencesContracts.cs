using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

/// <summary>The limits the workout preferences must satisfy before saving.</summary>
public static class WorkoutPreferencesConstraints
{
    public const int MinimumAvailableWeekdays = WorkoutPreferencesRule.MinimumDaysPerWeek;
    public const int MaximumAvailableWeekdays = WorkoutPreferencesRule.MaximumDaysPerWeek;
    public const int MinimumActivitySessionDurationMinutes =
        WorkoutPreferencesRule.MinimumActivitySessionDurationMinutes;
    public const int MaximumActivitySessionDurationMinutes =
        WorkoutPreferencesRule.MaximumActivitySessionDurationMinutes;
    public const int ActivitySessionDurationIncrementMinutes =
        WorkoutPreferencesRule.ActivitySessionDurationIncrementMinutes;
    public const int DefaultActivitySessionDurationMinutes =
        WorkoutPreferencesRule.DefaultActivitySessionDurationMinutes;

    /// <summary>The activities a new profile starts with.</summary>
    public static IReadOnlySet<WorkoutActivityDto> BuildDefaultActivitySelection() =>
        WorkoutActivityPreferences.BuildDefaultSelection()
            .Select(WorkoutActivityContractMapper.ToDto)
            .ToHashSet();

    /// <summary>The selection with the required activities added.</summary>
    public static IReadOnlySet<WorkoutActivityDto> EnsureRequiredActivities(
        IEnumerable<WorkoutActivityDto> activities) =>
        WorkoutActivityPreferences.EnsureRequired(activities
                .Where(activity => Enum.IsDefined(activity))
                .Select(WorkoutActivityContractMapper.ToCore))
            .Select(WorkoutActivityContractMapper.ToDto)
            .ToHashSet();

    /// <summary>True when the activity needs a session length.</summary>
    public static bool ActivityRequiresSessionDuration(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.RequiresSessionDuration(activity);

    /// <summary>True when the activity cannot use a default length.</summary>
    public static bool ActivityRequiresExplicitSessionDuration(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.RequiresExplicitSessionDuration(activity);

    /// <summary>The length the activity assumes.</summary>
    public static int? DefaultSessionDurationMinutes(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity)
            ? WorkoutPreferencesRule.DefaultSessionDurationMinutes(activity)
            : null;

    /// <summary>The length the activity assumes, allowing for demanding yoga.</summary>
    public static int? DefaultSessionDurationMinutes(WorkoutActivityDto activityType, bool isDemandingYoga) =>
        TryToCore(activityType, out var activity)
            ? WorkoutPreferencesRule.DefaultSessionDurationMinutes(activity, isDemandingYoga)
            : null;

    /// <summary>The lengths the activity offers.</summary>
    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity)
            ? WorkoutPreferencesRule.SessionDurationChoices(activity)
            : [];

    /// <summary>The lengths the activity offers, allowing for demanding yoga.</summary>
    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityDto activityType, bool isDemandingYoga) =>
        TryToCore(activityType, out var activity)
            ? WorkoutPreferencesRule.SessionDurationChoices(activity, isDemandingYoga)
            : [];

    /// <summary>True when the length is one the activity offers.</summary>
    public static bool IsValidSessionDuration(WorkoutActivityDto activityType, int minutes) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.IsValidSessionDuration(activity, minutes);

    /// <summary>True when the activity's weekly frequency must be chosen.</summary>
    public static bool ActivityRequiresExplicitWeeklyFrequency(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency(activity);

    /// <summary>The weekly frequencies the flow offers.</summary>
    public static IReadOnlyList<int> WeeklyFrequencyChoices() =>
        WorkoutPreferencesRule.WeeklyFrequencyChoices();

    /// <summary>Sessions per week the activity assumes for the available weekdays.</summary>
    public static int? DefaultWeeklyFrequency(WorkoutActivityDto activityType, int availableWeekdays) =>
        TryToCore(activityType, out var activity)
            ? ActivityPreferenceDefaults.SessionsPerWeek(activity, availableWeekdays)
            : null;

    /// <summary>True when the sessions per week is offered.</summary>
    public static bool IsValidRequestedWeeklyFrequency(int sessionsPerWeek) =>
        WorkoutPreferencesRule.IsValidRequestedWeeklyFrequency(sessionsPerWeek);

    /// <summary>Maps a wire activity onto the core type, answering false for one the core cannot represent.</summary>
    private static bool TryToCore(WorkoutActivityDto activityType, out WorkoutActivityType activity)
    {
        if (Enum.IsDefined(activityType))
        {
            activity = WorkoutActivityContractMapper.ToCore(activityType);
            return true;
        }

        activity = default;
        return false;
    }

}

/// <summary>The workout preferences as the page shows them.</summary>
public sealed record WorkoutPreferencesDto(
    IReadOnlyList<int> ActivityTypes,
    IReadOnlyList<int> AvailableWeekdays,
    IReadOnlyDictionary<int, int> ActivitySessionDurationsMinutes,
    string? EquipmentLevel = null,
    IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek = null,
    HashSet<int>? ExcludedExerciseIds = null,
    bool? GluteFocus = null,
    bool? HasSliders = null,
    bool? HasConfirmedBandAnchor = null,
    bool? HasDumbbells = null,
    bool? HasBands = null);

/// <summary>The workout preferences page.</summary>
public interface IProfileWorkoutPreferencesUseCase
{
    Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default);

    Task<bool> SaveAsync(WorkoutPreferencesDto preferences, CancellationToken cancellationToken = default);
}

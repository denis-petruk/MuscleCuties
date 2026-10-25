using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;

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

    public static IReadOnlySet<WorkoutActivityDto> BuildDefaultActivitySelection() =>
        WorkoutActivityPreferences.BuildDefaultSelection()
            .Select(WorkoutActivityContractMapper.ToDto)
            .ToHashSet();

    public static IReadOnlySet<WorkoutActivityDto> EnsureRequiredActivities(
        IEnumerable<WorkoutActivityDto> activities) =>
        WorkoutActivityPreferences.EnsureRequired(activities
                .Where(activity => Enum.IsDefined(activity))
                .Select(WorkoutActivityContractMapper.ToCore))
            .Select(WorkoutActivityContractMapper.ToDto)
            .ToHashSet();

    public static bool ActivityRequiresSessionDuration(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.RequiresSessionDuration(activity);

    public static bool ActivityRequiresExplicitSessionDuration(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.RequiresExplicitSessionDuration(activity);

    public static int? DefaultSessionDurationMinutes(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity)
            ? WorkoutPreferencesRule.DefaultSessionDurationMinutes(activity)
            : null;

    public static IReadOnlyList<int> SessionDurationChoices(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity)
            ? WorkoutPreferencesRule.SessionDurationChoices(activity)
            : [];

    public static bool IsValidSessionDuration(WorkoutActivityDto activityType, int minutes) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.IsValidSessionDuration(activity, minutes);

    public static bool ActivityRequiresExplicitWeeklyFrequency(WorkoutActivityDto activityType) =>
        TryToCore(activityType, out var activity) &&
        WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency(activity);

    public static IReadOnlyList<int> WeeklyFrequencyChoices() =>
        WorkoutPreferencesRule.WeeklyFrequencyChoices();

    public static int? DefaultWeeklyFrequency(WorkoutActivityDto activityType, int availableWeekdays) =>
        TryToCore(activityType, out var activity)
            ? ActivityPreferenceDefaults.SessionsPerWeek(activity, availableWeekdays)
            : null;

    public static bool IsValidRequestedWeeklyFrequency(int sessionsPerWeek) =>
        WorkoutPreferencesRule.IsValidRequestedWeeklyFrequency(sessionsPerWeek);

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

public sealed record WorkoutPreferencesDto(
    IReadOnlyList<int> ActivityTypes,
    IReadOnlyList<int> AvailableWeekdays,
    IReadOnlyDictionary<int, int> ActivitySessionDurationsMinutes,
    string? EquipmentLevel = null,
    IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek = null,
    IReadOnlySet<int>? ExcludedExerciseIds = null);

public enum WorkoutPreferencesError { None, ProfileMissing, InvalidPreferences, PlanUpdateFailed }

public sealed record WorkoutPreferencesResult(WorkoutPreferencesError Error)
{
    public bool Succeeded => Error == WorkoutPreferencesError.None;
}

public interface IProfileWorkoutPreferencesUseCase
{
    Task<WorkoutPreferencesDto?> LoadAsync(CancellationToken cancellationToken = default);

    Task<WorkoutPreferencesResult> SaveAndUpdatePlanAsync(WorkoutPreferencesDto preferences,
        CancellationToken cancellationToken = default);

    Task<WorkoutPreferencesResult> RegenerateSavedPlanAsync(CancellationToken cancellationToken = default);
}

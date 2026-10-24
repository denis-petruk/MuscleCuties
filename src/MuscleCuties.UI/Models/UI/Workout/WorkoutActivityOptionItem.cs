using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class WorkoutActivityOptionItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int? _sessionDurationMinutes;
    [ObservableProperty] private int? _requestedSessionsPerWeek;

    public IReadOnlyList<int> SessionDurationOptions { get; init; } = [];
    public IReadOnlyList<int> WeeklyFrequencyOptions { get; init; } = [];
    public bool AllowInlinePreferenceConfiguration { get; init; } = true;

    public WorkoutActivityDto ActivityType { get; init; }
    public bool ShowSessionDurationPicker => AllowInlinePreferenceConfiguration &&
        IsSelected &&
        WorkoutPreferencesConstraints.ActivityRequiresSessionDuration(ActivityType) &&
        !WorkoutPreferencesConstraints.ActivityRequiresExplicitSessionDuration(ActivityType);
    public bool ShowWeeklyFrequencyPicker => AllowInlinePreferenceConfiguration &&
        IsSelected &&
        WorkoutPreferencesConstraints.ActivityRequiresExplicitWeeklyFrequency(ActivityType);
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string GroupTitle { get; init; } = string.Empty;
    public string GroupDescription { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool IsExtra => !IsRequired;

    partial void OnIsSelectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowSessionDurationPicker));
        OnPropertyChanged(nameof(ShowWeeklyFrequencyPicker));
    }
}

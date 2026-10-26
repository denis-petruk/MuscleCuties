using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class WorkoutActivityOptionItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private int? _requestedSessionsPerWeek;

    public WorkoutActivityDto ActivityType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string GroupTitle { get; init; } = string.Empty;
    public string GroupDescription { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool IsExtra => !IsRequired;

}

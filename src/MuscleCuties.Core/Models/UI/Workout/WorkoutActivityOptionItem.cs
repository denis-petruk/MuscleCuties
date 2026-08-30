using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.UI.Workout;

public partial class WorkoutActivityOptionItem : ObservableObject
{
    public WorkoutActivityType ActivityType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string GroupTitle { get; init; } = string.Empty;
    public string GroupDescription { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool IsExtra => !IsRequired;
    [ObservableProperty] private bool _isSelected;
}

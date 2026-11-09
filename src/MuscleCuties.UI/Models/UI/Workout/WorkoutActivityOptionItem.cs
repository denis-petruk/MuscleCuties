using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class WorkoutActivityOptionItem : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanToggle))]
    private bool _isSelected;
    [ObservableProperty] private int? _requestedSessionsPerWeek;

    /// <summary>
    /// C1/OD-7: localized reason this option is not offered as a new selection, or empty when it is.
    /// An already-selected unavailable option stays tappable so it can be deselected.
    /// </summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsOffered))]
    [NotifyPropertyChangedFor(nameof(HasUnavailableReason))]
    [NotifyPropertyChangedFor(nameof(CanToggle))]
    private string _unavailableReason = string.Empty;

    public WorkoutActivityDto ActivityType { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    public string GroupTitle { get; init; } = string.Empty;
    public string GroupDescription { get; init; } = string.Empty;
    public string Tag { get; init; } = string.Empty;
    public string IconGlyph { get; init; } = string.Empty;
    public bool IsRequired { get; init; }
    public bool IsExtra => !IsRequired;
    public bool IsOffered => string.IsNullOrEmpty(UnavailableReason);
    public bool HasUnavailableReason => !IsOffered;
    public bool CanToggle => IsOffered || IsSelected;
}

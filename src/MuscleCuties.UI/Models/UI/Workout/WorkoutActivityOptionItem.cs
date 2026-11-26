using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One selectable activity in the generation chooser, which can also be unavailable for a</summary>
/// stated reason while still needing to be deselectable.</summary>
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

    /// <summary>Planner activity this option enables.</summary>
    public WorkoutActivityDto ActivityType { get; init; }
    /// <summary>Option name shown on the cell.</summary>
    public string Title { get; init; } = string.Empty;
    /// <summary>One-line description under the name.</summary>
    public string Subtitle { get; init; } = string.Empty;
    /// <summary>Heading of the group this option is listed under.</summary>
    public string GroupTitle { get; init; } = string.Empty;
    /// <summary>Explanation shown once under the group heading.</summary>
    public string GroupDescription { get; init; } = string.Empty;
    /// <summary>Tag that selects the activity's icon and colour.</summary>
    public string Tag { get; init; } = string.Empty;
    /// <summary>The glyph used when no image is set.</summary>
    public string IconGlyph { get; init; } = string.Empty;
    /// <summary>The image used for the activity.</summary>
    public string IconImage { get; init; } = string.Empty;
    /// <summary>True when the option has an image.</summary>
    public bool HasIconImage => !string.IsNullOrEmpty(IconImage);
    /// <summary>True when the option falls back to a glyph.</summary>
    public bool HasGlyphIcon => !HasIconImage;
    /// <summary>True when the activity is part of the plan.</summary>
    public bool IsRequired { get; init; }
    /// <summary>True when the activity can still be chosen.</summary>
    public bool IsOffered => string.IsNullOrEmpty(UnavailableReason);
    /// <summary>True when the activity is unavailable, with a reason.</summary>
    public bool HasUnavailableReason => !IsOffered;
    /// <summary>True when the option can be toggled.</summary>
    public bool CanToggle => IsOffered || IsSelected;
}

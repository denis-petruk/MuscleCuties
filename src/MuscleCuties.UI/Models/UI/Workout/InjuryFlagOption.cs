using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One body-site flag in the injury editor, which can mark several sites at once.</summary>
public partial class InjuryFlagOption : ObservableObject
{
    /// <summary>The injury site the option stands for.</summary>
    [ObservableProperty] private bool _isSelected;
    public WorkoutInjurySite Flag { get; init; }
    /// <summary>Localized site name shown on the chip.</summary>
    public string Label { get; init; } = string.Empty;
}

using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One status choice in the injury editor.</summary>
public partial class InjuryStatusOption : ObservableObject
{
    /// <summary>The injury status the option stands for.</summary>
    [ObservableProperty] private bool _isSelected;
    public WorkoutInjuryStatus Status { get; init; }
    /// <summary>Localized status name shown on the chip.</summary>
    public string Label { get; init; } = string.Empty;
}

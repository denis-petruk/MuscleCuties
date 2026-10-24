using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class InjuryFlagOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public WorkoutInjurySite Flag { get; init; }
    public string Label { get; init; } = string.Empty;
}

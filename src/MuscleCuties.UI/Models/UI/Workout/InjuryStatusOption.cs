using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class InjuryStatusOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public WorkoutInjuryStatus Status { get; init; }
    public string Label { get; init; } = string.Empty;
}

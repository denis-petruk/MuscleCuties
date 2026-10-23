using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class InjuryFlagOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public InjuryFlag Flag { get; init; }
    public string Label { get; init; } = string.Empty;
}

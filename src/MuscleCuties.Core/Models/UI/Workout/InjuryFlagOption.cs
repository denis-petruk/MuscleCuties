using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.UI.Workout;

public partial class InjuryFlagOption : ObservableObject
{
    public InjuryFlag Flag { get; init; }
    public string Label { get; init; } = string.Empty;

    [ObservableProperty] private bool _isSelected;
}

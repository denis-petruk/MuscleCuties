using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class InjuryStatusOption : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    public InjuryStatus Status { get; init; }
    public string Label { get; init; } = string.Empty;
}

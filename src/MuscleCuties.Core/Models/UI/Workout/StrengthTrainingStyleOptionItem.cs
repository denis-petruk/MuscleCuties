using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Models.UI.Workout;

public partial class StrengthTrainingStyleOptionItem : ObservableObject
{
    public StrengthTrainingStyle Style { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Subtitle { get; init; } = string.Empty;
    [ObservableProperty] private bool _isSelected;
}

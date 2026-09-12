using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.Core.Models.UI.Workout;

public partial class FilterChipItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _label = string.Empty;
}

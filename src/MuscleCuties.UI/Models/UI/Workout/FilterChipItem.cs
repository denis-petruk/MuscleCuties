using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class FilterChipItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private int _index;

    /// <summary>Zero-based slot in a fixed-count chip row; drives the row's grid column.</summary>
    public int Column { get; init; }
}

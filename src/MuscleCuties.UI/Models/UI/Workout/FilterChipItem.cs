
namespace MuscleCuties.UI.Models.UI.Workout;

public partial class FilterChipItem : ObservableObject
{
    [ObservableProperty] private bool _isSelected;
    [ObservableProperty] private string _label = string.Empty;
    [ObservableProperty] private int _index;

    /// <summary>Stable filter identity. The localized label is display copy and never decides visibility.</summary>
    public string Tag { get; init; } = string.Empty;

    /// <summary>Zero-based slot in a fixed-count chip row; drives the row's grid column.</summary>
    public int Column { get; init; }
}

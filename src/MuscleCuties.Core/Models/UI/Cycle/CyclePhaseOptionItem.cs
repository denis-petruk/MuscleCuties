using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Models.UI.Cycle;

public partial class CyclePhaseOptionItem : ObservableObject
{
    public CyclePhase Phase { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public Color BackgroundColor { get; init; } = Colors.Transparent;
    public Color TextColor { get; init; } = Colors.Black;

    [ObservableProperty] private bool _isSelected;
}

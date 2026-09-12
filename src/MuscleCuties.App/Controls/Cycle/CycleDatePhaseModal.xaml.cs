using System.Runtime.CompilerServices;
using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Cycle;

public partial class CycleDatePhaseModal : ContentView
{
    public CycleDatePhaseModal()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        base.OnPropertyChanged(propertyName);
        if (propertyName == nameof(IsVisible) && IsVisible)
            ModalTransition.PlayShow(this);
    }
}

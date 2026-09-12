using System.Runtime.CompilerServices;
using MuscleCuties.App.Controls.Shared;

namespace MuscleCuties.App.Controls.Nutrition;

public partial class NutritionBreakdownModal : ContentView
{
    public NutritionBreakdownModal()
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

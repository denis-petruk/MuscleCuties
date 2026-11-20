using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Dashboard;

public partial class DashboardWorkoutCard : ContentView
{
    public DashboardWorkoutCard()
    {
        InitializeComponent();
    }

    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        var accessibility = AccessibilityDecoration.IsAccessibilityTextSize();
        HeaderGrid.ColumnDefinitions[0].Width = accessibility ? new GridLength(0) : GridLength.Auto;
        HeaderGrid.ColumnSpacing = accessibility
            ? 0
            : AppThemeResources.GetDouble("UiSpaceCompact");
    }
}


namespace MuscleCuties.UI.Controls.Dashboard;

/// <summary>
///     Dashboard card for today's workout and its start action; drops the leading
///     icon when the reader uses accessibility text sizes so the title keeps its width.
/// </summary>
public partial class DashboardWorkoutCard : ContentView
{
    /// <summary>Initializes the card declared in its XAML.</summary>
    public DashboardWorkoutCard()
    {
        InitializeComponent();
    }

    /// <summary>Collapses the header icon column when accessibility text sizing is on.</summary>
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

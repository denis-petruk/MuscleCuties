namespace MuscleCuties.App.Controls.Shared;

public partial class MacroProgressGrid : ContentView
{
    public static readonly BindableProperty ProteinTextProperty =
        BindableProperty.Create(nameof(ProteinText), typeof(string), typeof(MacroProgressGrid), string.Empty);

    public static readonly BindableProperty ProteinProgressProperty =
        BindableProperty.Create(nameof(ProteinProgress), typeof(double), typeof(MacroProgressGrid), 0d);

    public static readonly BindableProperty CarbsTextProperty =
        BindableProperty.Create(nameof(CarbsText), typeof(string), typeof(MacroProgressGrid), string.Empty);

    public static readonly BindableProperty CarbsProgressProperty =
        BindableProperty.Create(nameof(CarbsProgress), typeof(double), typeof(MacroProgressGrid), 0d);

    public static readonly BindableProperty FatsTextProperty =
        BindableProperty.Create(nameof(FatsText), typeof(string), typeof(MacroProgressGrid), string.Empty);

    public static readonly BindableProperty FatsProgressProperty =
        BindableProperty.Create(nameof(FatsProgress), typeof(double), typeof(MacroProgressGrid), 0d);

    public MacroProgressGrid()
    {
        InitializeComponent();
    }

    public string ProteinText
    {
        get => (string)GetValue(ProteinTextProperty);
        set => SetValue(ProteinTextProperty, value);
    }

    public double ProteinProgress
    {
        get => (double)GetValue(ProteinProgressProperty);
        set => SetValue(ProteinProgressProperty, value);
    }

    public string CarbsText
    {
        get => (string)GetValue(CarbsTextProperty);
        set => SetValue(CarbsTextProperty, value);
    }

    public double CarbsProgress
    {
        get => (double)GetValue(CarbsProgressProperty);
        set => SetValue(CarbsProgressProperty, value);
    }

    public string FatsText
    {
        get => (string)GetValue(FatsTextProperty);
        set => SetValue(FatsTextProperty, value);
    }

    public double FatsProgress
    {
        get => (double)GetValue(FatsProgressProperty);
        set => SetValue(FatsProgressProperty, value);
    }
}

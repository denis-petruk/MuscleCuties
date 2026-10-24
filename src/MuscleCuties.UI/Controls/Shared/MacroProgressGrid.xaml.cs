using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using MuscleCuties.UI.Resources.Styles;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace MuscleCuties.UI.Controls.Shared;

public class MacroProgressGrid : ContentView
{
    public static readonly BindableProperty ProteinTextProperty =
        BindableProperty.Create(nameof(ProteinText), typeof(string), typeof(MacroProgressGrid), string.Empty,
            propertyChanged: (b, _, value) => ((MacroProgressGrid)b).UpdateValueText(0, (string?)value));

    public static readonly BindableProperty ProteinProgressProperty =
        BindableProperty.Create(nameof(ProteinProgress), typeof(double), typeof(MacroProgressGrid), 0d,
            propertyChanged: (b, _, _) => ((MacroProgressGrid)b).UpdateBarScale(0));

    public static readonly BindableProperty CarbsTextProperty =
        BindableProperty.Create(nameof(CarbsText), typeof(string), typeof(MacroProgressGrid), string.Empty,
            propertyChanged: (b, _, value) => ((MacroProgressGrid)b).UpdateValueText(1, (string?)value));

    public static readonly BindableProperty CarbsProgressProperty =
        BindableProperty.Create(nameof(CarbsProgress), typeof(double), typeof(MacroProgressGrid), 0d,
            propertyChanged: (b, _, _) => ((MacroProgressGrid)b).UpdateBarScale(1));

    public static readonly BindableProperty FatsTextProperty =
        BindableProperty.Create(nameof(FatsText), typeof(string), typeof(MacroProgressGrid), string.Empty,
            propertyChanged: (b, _, value) => ((MacroProgressGrid)b).UpdateValueText(2, (string?)value));

    public static readonly BindableProperty FatsProgressProperty =
        BindableProperty.Create(nameof(FatsProgress), typeof(double), typeof(MacroProgressGrid), 0d,
            propertyChanged: (b, _, _) => ((MacroProgressGrid)b).UpdateBarScale(2));

    private static readonly string[] LightKeys = ["ProteinLight", "CarbsLight", "FatsLight"];
    private static readonly string[] DarkKeys = ["ProteinDark", "CarbsDark", "FatsDark"];

    private static readonly string[] SvgPaths =
    [
        "M4 9v6M7 8v8M17 9v6M14 8v8M7 12h7",
        "M9 18V5M9 7C6.5 7 5 5.8 5 4M9 10C6.5 10 5 8.8 5 7M9 13C6.5 13 5 11.8 5 10M9 7c2.5 0 4-1.2 4-3M9 10c2.5 0 4-1.2 4-3M9 13c2.5 0 4-1.2 4-3",
        "M10 3C7 7 5 10 5 13a5 5 0 0 0 10 0c0-3-2-6-5-10z"
    ];

    private const string AppFontFamilyName = "Inter";
    private const double MacroColumnSpacing = 12;
    private const double MacroRowSpacing = 4;
    private const double MacroIconDimension = 15;
    private const double MacroHeaderSpacing = 5;
    private const double MacroNameFontSize = 13;
    private const double MacroValueFontSize = 15;
    private const double MacroBarHeight = 5;
    private const double MacroBarCornerRadius = 2.5;

    private static readonly string[] MacroLabels = ["Protein", "Carbs", "Fats"];
    private static readonly double[] StrokeWidths = [1.7, 1.5, 1.6];
    private readonly BoxView[] _fillBars = new BoxView[3];
    private readonly Path[] _icons = new Path[3];

    private readonly Label[] _valueLabels = new Label[3];

    public MacroProgressGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new(), new(), new()],
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)],
            ColumnSpacing = MacroColumnSpacing,
            RowSpacing = MacroRowSpacing
        };

        var converter = new PathGeometryConverter();

        for (var i = 0; i < 3; i++)
        {
            var macroColor = AppThemeResources.GetColor(LightKeys[i], DarkKeys[i]);
            var trackColor = AppThemeResources.GetColor("Gray200", "Gray800");

            var icon = new Path
            {
                Data = (Geometry?)converter.ConvertFromInvariantString(SvgPaths[i]),
                Stroke = macroColor,
                StrokeThickness = StrokeWidths[i],
                StrokeLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Aspect = Stretch.Uniform,
                WidthRequest = MacroIconDimension,
                HeightRequest = MacroIconDimension,
                VerticalOptions = LayoutOptions.Center
            };
            _icons[i] = icon;

            var nameLabel = new Label
            {
                Text = MacroLabels[i],
                Style = (Style)Application.Current!.Resources["MutedLabel"],
                FontFamily = AppFontFamilyName,
                FontSize = MacroNameFontSize
            };

            var header = new HorizontalStackLayout
            {
                Spacing = MacroHeaderSpacing,
                Children = { icon, nameLabel }
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, i);
            grid.Add(header);

            var valueLabel = new Label
            {
                FontFamily = AppFontFamilyName,
                FontSize = MacroValueFontSize,
                FontAttributes = FontAttributes.Bold,
                TextColor = AppThemeResources.GetColor("TextPrimary", "TextPrimaryDark")
            };
            _valueLabels[i] = valueLabel;
            Grid.SetRow(valueLabel, 1);
            Grid.SetColumn(valueLabel, i);
            grid.Add(valueLabel);

            var track = new BoxView
            {
                Color = trackColor,
                HeightRequest = MacroBarHeight,
                HorizontalOptions = LayoutOptions.Fill,
                CornerRadius = MacroBarCornerRadius
            };

            var fill = new BoxView
            {
                Color = macroColor,
                HeightRequest = MacroBarHeight,
                HorizontalOptions = LayoutOptions.Fill,
                AnchorX = 0,
                ScaleX = 0,
                CornerRadius = MacroBarCornerRadius
            };
            _fillBars[i] = fill;

            var barContainer = new Grid
            {
                Children = { track, fill }
            };
            Grid.SetRow(barContainer, 2);
            Grid.SetColumn(barContainer, i);
            grid.Add(barContainer);
        }

        UpdateValueText(0, ProteinText);
        UpdateValueText(1, CarbsText);
        UpdateValueText(2, FatsText);

        Content = grid;
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

    protected override void OnParentSet()
    {
        base.OnParentSet();

        if (Parent is null)
        {
            if (Application.Current is not null)
                Application.Current.RequestedThemeChanged -= OnThemeChanged;
        }
        else if (Application.Current is not null)
        {
            Application.Current.RequestedThemeChanged -= OnThemeChanged;
            Application.Current.RequestedThemeChanged += OnThemeChanged;
        }
    }

    private void UpdateValueText(int index, string? text)
    {
        if (_valueLabels[index] is { } label)
            label.Text = text;
    }

    private void UpdateBarScale(int index)
    {
        var progress = index switch
        {
            0 => ProteinProgress,
            1 => CarbsProgress,
            _ => FatsProgress
        };
        _fillBars[index].ScaleX = double.IsFinite(progress) ? Math.Clamp(progress, 0, 1) : 0;
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        for (var i = 0; i < 3; i++)
        {
            var macroColor = AppThemeResources.GetColor(e.RequestedTheme, LightKeys[i], DarkKeys[i]);
            _icons[i].Stroke = macroColor;
            _valueLabels[i].TextColor = AppThemeResources.GetColor(e.RequestedTheme, "TextPrimary", "TextPrimaryDark");
            _fillBars[i].Color = macroColor;
        }

        var trackColor = AppThemeResources.GetColor(e.RequestedTheme, "Gray300", "Gray800");
        if (Content is Grid grid)
            foreach (var child in grid.Children)
                if (child is Grid barContainer && barContainer.Children.Count == 2 &&
                    barContainer.Children[0] is BoxView trackBox)
                    trackBox.Color = trackColor;
    }
}

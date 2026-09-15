using Microsoft.Maui.Controls.Shapes;
using MuscleCuties.App.Resources.Styles;
using Path = Microsoft.Maui.Controls.Shapes.Path;

namespace MuscleCuties.App.Controls.Shared;

public class MacroProgressGrid : ContentView
{
    public static readonly BindableProperty ProteinTextProperty =
        BindableProperty.Create(nameof(ProteinText), typeof(string), typeof(MacroProgressGrid), string.Empty);

    public static readonly BindableProperty ProteinProgressProperty =
        BindableProperty.Create(nameof(ProteinProgress), typeof(double), typeof(MacroProgressGrid), 0d,
            propertyChanged: (b, _, _) => ((MacroProgressGrid)b).UpdateBarScale(0));

    public static readonly BindableProperty CarbsTextProperty =
        BindableProperty.Create(nameof(CarbsText), typeof(string), typeof(MacroProgressGrid), string.Empty);

    public static readonly BindableProperty CarbsProgressProperty =
        BindableProperty.Create(nameof(CarbsProgress), typeof(double), typeof(MacroProgressGrid), 0d,
            propertyChanged: (b, _, _) => ((MacroProgressGrid)b).UpdateBarScale(1));

    public static readonly BindableProperty FatsTextProperty =
        BindableProperty.Create(nameof(FatsText), typeof(string), typeof(MacroProgressGrid), string.Empty);

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

    private static readonly string[] MacroLabels = ["Protein", "Carbs", "Fats"];
    private static readonly double[] StrokeWidths = [1.7, 1.5, 1.6];

    private readonly Label[] _valueLabels = new Label[3];
    private readonly BoxView[] _fillBars = new BoxView[3];
    private readonly Path[] _icons = new Path[3];

    public MacroProgressGrid()
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new(), new(), new()],
            RowDefinitions = [new(GridLength.Auto), new(GridLength.Auto), new(GridLength.Auto)],
            ColumnSpacing = 10,
            RowSpacing = 4
        };

        var converter = new PathGeometryConverter();

        for (var i = 0; i < 3; i++)
        {
            var macroColor = AppThemeResources.GetColor(LightKeys[i], DarkKeys[i]);
            var trackColor = AppThemeResources.GetColor("Gray300", "Gray800");

            var icon = new Path
            {
                Data = (Geometry?)converter.ConvertFromInvariantString(SvgPaths[i]),
                Stroke = macroColor,
                StrokeThickness = StrokeWidths[i],
                StrokeLineCap = PenLineCap.Round,
                StrokeLineJoin = PenLineJoin.Round,
                Aspect = Stretch.Uniform,
                WidthRequest = 16,
                HeightRequest = 16,
                VerticalOptions = LayoutOptions.Center
            };
            _icons[i] = icon;

            var nameLabel = new Label
            {
                Text = MacroLabels[i],
                Style = (Style)Application.Current!.Resources["CaptionLabel"]
            };

            var header = new HorizontalStackLayout
            {
                Spacing = 5,
                Children = { icon, nameLabel }
            };
            Grid.SetRow(header, 0);
            Grid.SetColumn(header, i);
            grid.Add(header);

            var valueLabel = new Label
            {
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
                TextColor = macroColor
            };
            _valueLabels[i] = valueLabel;
            Grid.SetRow(valueLabel, 1);
            Grid.SetColumn(valueLabel, i);
            grid.Add(valueLabel);

            var track = new BoxView
            {
                Color = trackColor,
                HeightRequest = 5,
                HorizontalOptions = LayoutOptions.Fill,
                CornerRadius = 2.5
            };

            var fill = new BoxView
            {
                Color = macroColor,
                HeightRequest = 5,
                HorizontalOptions = LayoutOptions.Fill,
                AnchorX = 0,
                ScaleX = 0,
                CornerRadius = 2.5
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

        _valueLabels[0].SetBinding(Label.TextProperty, new Binding(nameof(ProteinText), source: this));
        _valueLabels[1].SetBinding(Label.TextProperty, new Binding(nameof(CarbsText), source: this));
        _valueLabels[2].SetBinding(Label.TextProperty, new Binding(nameof(FatsText), source: this));

        Content = grid;

        if (Application.Current is not null)
            Application.Current.RequestedThemeChanged += OnThemeChanged;
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

    private void UpdateBarScale(int index)
    {
        var progress = index switch
        {
            0 => ProteinProgress,
            1 => CarbsProgress,
            _ => FatsProgress
        };
        _fillBars[index].ScaleX = Math.Clamp(progress, 0, 1);
    }

    private void OnThemeChanged(object? sender, AppThemeChangedEventArgs e)
    {
        for (var i = 0; i < 3; i++)
        {
            var macroColor = AppThemeResources.GetColor(e.RequestedTheme, LightKeys[i], DarkKeys[i]);
            _icons[i].Stroke = macroColor;
            _valueLabels[i].TextColor = macroColor;
            _fillBars[i].Color = macroColor;
        }

        var trackColor = AppThemeResources.GetColor(e.RequestedTheme, "Gray300", "Gray800");
        if (Content is Grid grid)
        {
            foreach (var child in grid.Children)
            {
                if (child is Grid barContainer && barContainer.Children.Count == 2 &&
                    barContainer.Children[0] is BoxView trackBox)
                {
                    trackBox.Color = trackColor;
                }
            }
        }
    }
}

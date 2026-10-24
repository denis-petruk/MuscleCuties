using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using MuscleCuties.UI.Models.UI.Cycle;
using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Controls.Cycle;

public sealed class CycleMonthGrid : Grid
{
    private const int DaysPerWeek = 7;
    private const double EstimatedDayOpacity = 0.72;
    private const double StandardDayOpacity = 1.0;
    private const double SelectedDayRingThickness = 2.8;
    private const double HiddenDayRingThickness = 0;
    private const double DayCellSpacing = 0;
    private const double DayCellPadding = 0;
    private const double DayCellMargin = 2;
    private const double DayTapTargetSize = 44;
    private const int DayButtonCornerRadius = 10;
    private const int DayCellCornerRadius = 12;
    private const double DayLabelFontSize = 13;
    private const string AppFontFamilyName = "Inter";
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IReadOnlyList<CycleDayItem>),
        typeof(CycleMonthGrid),
        defaultValue: null,
        propertyChanged: OnItemsSourceChanged);

    public static readonly BindableProperty DaySelectedCommandProperty = BindableProperty.Create(
        nameof(DaySelectedCommand),
        typeof(ICommand),
        typeof(CycleMonthGrid),
        defaultValue: null,
        propertyChanged: OnDaySelectedCommandChanged);

    public static readonly BindableProperty IsExpandedProperty = BindableProperty.Create(
        nameof(IsExpanded),
        typeof(bool),
        typeof(CycleMonthGrid),
        false,
        propertyChanged: OnIsExpandedChanged);

    private readonly List<Border> _dayCells = [];
    private CycleDayItem? _visuallySelectedDay;

    public CycleMonthGrid()
    {
        RowSpacing = DayCellSpacing;
        ColumnSpacing = DayCellSpacing;

        for (var column = 0; column < DaysPerWeek; column++)
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
    }

    public IReadOnlyList<CycleDayItem>? ItemsSource
    {
        get => (IReadOnlyList<CycleDayItem>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public ICommand? DaySelectedCommand
    {
        get => (ICommand?)GetValue(DaySelectedCommandProperty);
        set => SetValue(DaySelectedCommandProperty, value);
    }

    public bool IsExpanded
    {
        get => (bool)GetValue(IsExpandedProperty);
        set => SetValue(IsExpandedProperty, value);
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grid = (CycleMonthGrid)bindable;
        grid._visuallySelectedDay = null;
        if (grid.IsExpanded)
            grid.UpdateDays((IReadOnlyList<CycleDayItem>?)newValue);
    }

    private static void OnIsExpandedChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grid = (CycleMonthGrid)bindable;
        if (newValue is true)
            grid.UpdateDays(grid.ItemsSource);
    }

    private static void OnDaySelectedCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grid = (CycleMonthGrid)bindable;
        foreach (var cell in grid._dayCells)
            if (cell.Content is Button button)
                button.Command = grid.WrapCommand((ICommand?)newValue);
    }

    private Command<CycleDayItem>? WrapCommand(ICommand? inner)
    {
        if (inner is null) return null;
        return new Command<CycleDayItem>(day =>
        {
            _visuallySelectedDay = day;
            ApplySelectionRing();
            if (inner.CanExecute(day))
                inner.Execute(day);
        });
    }

    private void UpdateDays(IReadOnlyList<CycleDayItem>? days)
    {
        if (days is null || days.Count == 0)
        {
            ClearCells();
            return;
        }

        EnsureCellCount(days.Count);
        UpdateExistingCells(days);
        _visuallySelectedDay = days.FirstOrDefault(day => day.IsSelected);
        ApplySelectionRing();
    }

    private void EnsureCellCount(int count)
    {
        if (_dayCells.Count == count)
            return;

        ClearCells();
        var rowCount = (int)Math.Ceiling(count / (double)DaysPerWeek);
        for (var row = 0; row < rowCount; row++)
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var index = 0; index < count; index++)
        {
            var cell = CreateDayCell();
            cell.SetValue(RowProperty, index / DaysPerWeek);
            cell.SetValue(ColumnProperty, index % DaysPerWeek);
            _dayCells.Add(cell);
            Children.Add(cell);
        }
    }

    private void UpdateExistingCells(IReadOnlyList<CycleDayItem> days)
    {
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var cell = _dayCells[index];

            if (cell.Content is not Button button)
                continue;

            button.Text = day.DisplayDayLabel;
            button.BackgroundColor = day.BackgroundColor;
            button.TextColor = day.TextColor;
            button.BorderColor = day.StrokeColor;
            button.BorderWidth = day.StrokeThickness;
            button.Command = WrapCommand(DaySelectedCommand);
            button.CommandParameter = day;
            button.Opacity = day.IsEstimated ? EstimatedDayOpacity : StandardDayOpacity;
            cell.BindingContext = day;
            SemanticProperties.SetDescription(button, day.AccessibilityDescription);
        }
    }

    private void ApplySelectionRing()
    {
        var selected = _visuallySelectedDay;

        foreach (var cell in _dayCells)
        {
            if (cell.BindingContext is not CycleDayItem day)
                continue;

            var isSelected = selected is not null
                             && day.Date.HasValue
                             && selected.Date.HasValue
                             && day.Date.Value.Date == selected.Date.Value.Date;

            if (isSelected)
            {
                cell.Stroke = ResolveSelectionRingColor();
                cell.StrokeThickness = SelectedDayRingThickness;
                if (cell.Content is Button btn)
                    btn.FontAttributes = FontAttributes.Bold;
            }
            else
            {
                cell.Stroke = Colors.Transparent;
                cell.StrokeThickness = HiddenDayRingThickness;
                if (cell.Content is Button btn)
                    btn.FontAttributes = FontAttributes.Bold;
            }
        }
    }

    private static Color ResolveSelectionRingColor()
    {
        return AppThemeResources.GetColor("PrimaryDark", "SecondaryDarkText");
    }

    private void ClearCells()
    {
        Children.Clear();
        RowDefinitions.Clear();
        _dayCells.Clear();
    }

    private static Border CreateDayCell()
    {
        var button = new Button
        {
            Padding = DayCellPadding,
            Margin = DayCellPadding,
            MinimumHeightRequest = DayTapTargetSize,
            MinimumWidthRequest = DayTapTargetSize,
            HeightRequest = DayTapTargetSize,
            CornerRadius = DayButtonCornerRadius,
            FontFamily = AppFontFamilyName,
            FontSize = DayLabelFontSize,
            FontAttributes = FontAttributes.Bold
        };

        return new Border
        {
            Padding = DayCellPadding,
            Margin = DayCellMargin,
            StrokeThickness = HiddenDayRingThickness,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = DayCellCornerRadius },
            BackgroundColor = Colors.Transparent,
            Content = button,
            MinimumHeightRequest = DayTapTargetSize,
            MinimumWidthRequest = DayTapTargetSize
        };
    }
}

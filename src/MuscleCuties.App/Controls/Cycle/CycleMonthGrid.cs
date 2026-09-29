using System.Windows.Input;
using Microsoft.Maui.Controls.Shapes;
using MuscleCuties.Core.Models.UI.Cycle;

namespace MuscleCuties.App.Controls.Cycle;

public sealed class CycleMonthGrid : Grid
{
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

    private readonly List<Border> _dayCells = [];
    private CycleDayItem? _visuallySelectedDay;

    public CycleMonthGrid()
    {
        RowSpacing = 0;
        ColumnSpacing = 0;

        for (var column = 0; column < 7; column++)
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

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grid = (CycleMonthGrid)bindable;
        grid._visuallySelectedDay = null;
        grid.UpdateDays((IReadOnlyList<CycleDayItem>?)newValue);
    }

    private static void OnDaySelectedCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grid = (CycleMonthGrid)bindable;
        foreach (var cell in grid._dayCells)
        {
            if (cell.Content is Button button)
                button.Command = grid.WrapCommand((ICommand?)newValue);
        }
    }

    private ICommand? WrapCommand(ICommand? inner)
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
        ApplySelectionRing();
    }

    private void EnsureCellCount(int count)
    {
        if (_dayCells.Count == count)
            return;

        ClearCells();
        var rowCount = (int)Math.Ceiling(count / 7d);
        for (var row = 0; row < rowCount; row++)
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var index = 0; index < count; index++)
        {
            var cell = CreateDayCell();
            cell.SetValue(RowProperty, index / 7);
            cell.SetValue(ColumnProperty, index % 7);
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

            button.Text = day.IsPredictedFuture ? $"{day.Day}*" : day.Day.ToString();
            button.BackgroundColor = day.BackgroundColor;
            button.TextColor = day.TextColor;
            button.BorderColor = day.StrokeColor;
            button.BorderWidth = day.StrokeThickness;
            button.Command = WrapCommand(DaySelectedCommand);
            button.CommandParameter = day;
            button.Opacity = day.IsPredictedFuture ? 0.72 : 1.0;
            cell.BindingContext = day;
            SemanticProperties.SetDescription(button, BuildDescription(day));
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
                cell.StrokeThickness = 2.8;
                if (cell.Content is Button btn)
                    btn.FontAttributes = FontAttributes.Bold;
            }
            else
            {
                cell.Stroke = Colors.Transparent;
                cell.StrokeThickness = 0;
                if (cell.Content is Button btn)
                    btn.FontAttributes = FontAttributes.Bold;
            }
        }
    }

    private static Color ResolveSelectionRingColor()
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        return isDark
            ? Color.FromArgb("#F0B8CF")   // ActionSurfaceDark token value
            : Color.FromArgb("#A6446D");  // PrimaryDark token value
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
            Padding = 0,
            Margin = 0,
            MinimumHeightRequest = 44,
            MinimumWidthRequest = 44,
            HeightRequest = 44,
            CornerRadius = 10,
            FontSize = 13,
            FontAttributes = FontAttributes.Bold
        };

        return new Border
        {
            Padding = 0,
            Margin = 2,
            StrokeThickness = 0,
            Stroke = Colors.Transparent,
            StrokeShape = new RoundRectangle { CornerRadius = 12 },
            BackgroundColor = Colors.Transparent,
            Content = button,
            MinimumHeightRequest = 44,
            MinimumWidthRequest = 44
        };
    }

    private static string BuildDescription(CycleDayItem day)
    {
        var date = day.Date?.ToString("MMMM d") ?? $"Day {day.Day}";
        var phase = day.IsNeutral ? "not logged" : $"{day.Phase} phase";
        var dataSource = day.HasPhaseShiftLog
            ? ", logged"
            : day.IsPredictedFuture
                ? ", estimated"
                : string.Empty;
        var state = day.IsToday ? ", today" : string.Empty;

        return $"{date}, {phase}{dataSource}{state}. Double tap to edit.";
    }
}

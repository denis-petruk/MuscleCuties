using System.Windows.Input;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using MuscleCuties.UI.Models.UI.Cycle;

namespace MuscleCuties.UI.Controls.Cycle;

public sealed class CycleMonthGrid : Grid
{
    private const int DaysPerWeek = 7;
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
    private int _currentLeadingOffset = -1;
    private CycleDayItem? _visuallySelectedDay;

    public CycleMonthGrid()
    {
        RowSpacing = AppThemeResources.GetDouble("UiSpacing0");
        ColumnSpacing = AppThemeResources.GetDouble("UiSpacing0");

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

        var firstDate = days[0].Date;
        var leadingOffset = firstDate.HasValue
            ? ((int)firstDate.Value.DayOfWeek + 6) % DaysPerWeek
            : 0;

        EnsureCellCount(days.Count, leadingOffset);
        UpdateExistingCells(days);
        _visuallySelectedDay = days.FirstOrDefault(day => day.IsSelected);
        ApplySelectionRing();
    }

    private void EnsureCellCount(int count, int leadingOffset)
    {
        if (_dayCells.Count == count && _currentLeadingOffset == leadingOffset)
            return;

        ClearCells();
        _currentLeadingOffset = leadingOffset;

        // Row 0: localized Monday-first weekday headers; Rows 1..N: calendar day cells aligned to DayOfWeek.
        var dayRowCount = (int)Math.Ceiling((count + leadingOffset) / (double)DaysPerWeek);
        RowDefinitions.Add(new RowDefinition(GridLength.Auto));
        for (var row = 0; row < dayRowCount; row++)
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        var shortestNames = CultureInfo.CurrentCulture.DateTimeFormat.ShortestDayNames;
        var weekdayOrder = new[] { 1, 2, 3, 4, 5, 6, 0 };
        var headerStyle = Application.Current?.Resources["CaptionTinyCentered"] as Style;
        for (var col = 0; col < DaysPerWeek; col++)
        {
            var headerLabel = new Label
            {
                Text = shortestNames[weekdayOrder[col]],
                HorizontalTextAlignment = TextAlignment.Center,
                Margin = AppThemeResources.GetThickness("UiInsets0_0_0_4"),
                LineBreakMode = LineBreakMode.NoWrap
            };
            if (headerStyle is not null)
                headerLabel.Style = headerStyle;
            CappedTextSize.SetIsCompactLabel(headerLabel, true);
            AutomationProperties.SetIsInAccessibleTree(headerLabel, false);
            headerLabel.SetValue(RowProperty, 0);
            headerLabel.SetValue(ColumnProperty, col);
            Children.Add(headerLabel);
        }

        for (var index = 0; index < count; index++)
        {
            var slot = index + leadingOffset;
            var cell = CreateDayCell();
            cell.SetValue(RowProperty, 1 + slot / DaysPerWeek);
            cell.SetValue(ColumnProperty, slot % DaysPerWeek);
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
            button.Opacity = AppThemeResources.GetDouble(day.IsEstimated ? "UiOpacityEstimatedDay" : "UiOpacityFull");
            cell.BindingContext = day;
            SemanticProperties.SetDescription(button, day.AccessibilityDescription);
        }
    }

    private void ApplySelectionRing()
    {
        var selected = _visuallySelectedDay;
        var selectedHint = UiTextCatalog.Get("Common_Selected");
        var unselectedHint = UiTextCatalog.Get("Common_NotSelected");

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
                VisualStateManager.GoToState(cell, "Selected");
            }
            else
            {
                VisualStateManager.GoToState(cell, "Unselected");
            }

            if (cell.Content is Button button)
                SemanticProperties.SetHint(button, isSelected ? selectedHint : unselectedHint);
        }
    }

    private void ClearCells()
    {
        Children.Clear();
        RowDefinitions.Clear();
        _dayCells.Clear();
        _currentLeadingOffset = -1;
    }

    private static Border CreateDayCell()
    {
        // A calendar cell shows a day number and its marker in a seventh of the card width. The shared
        // button padding leaves too little of that for two digits, so the label wraps one character per
        // line ("1 / 2 / *"); compact padding and an unwrapped label keep the cell readable at any width.
        var button = new Button
        {
            Style = Application.Current?.Resources["ButtonSecondary"] as Style
                    ?? throw new InvalidOperationException("The shared secondary button style is unavailable."),
            Padding = AppThemeResources.GetThickness("UiInsets2"),
            LineBreakMode = LineBreakMode.NoWrap
        };
        CappedTextSize.SetIsCompactControl(button, true);

        return new Border
        {
            Style = Application.Current?.Resources["CalendarDayCellSurface"] as Style
                    ?? throw new InvalidOperationException("The shared calendar cell style is unavailable."),
            Content = button,
        };
    }
}

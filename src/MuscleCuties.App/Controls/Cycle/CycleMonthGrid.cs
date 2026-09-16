using System.Windows.Input;
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

    private readonly List<Button> _dayButtons = [];

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
        grid.UpdateDays((IReadOnlyList<CycleDayItem>?)newValue);
    }

    private static void OnDaySelectedCommandChanged(BindableObject bindable, object oldValue, object newValue)
    {
        var grid = (CycleMonthGrid)bindable;
        foreach (var button in grid._dayButtons)
            button.Command = (ICommand?)newValue;
    }

    private void UpdateDays(IReadOnlyList<CycleDayItem>? days)
    {
        if (days is null || days.Count == 0)
        {
            ClearButtons();
            return;
        }

        EnsureButtonCount(days.Count);
        UpdateExistingButtons(days);
    }

    private void EnsureButtonCount(int count)
    {
        if (_dayButtons.Count == count)
            return;

        ClearButtons();
        var rowCount = (int)Math.Ceiling(count / 7d);
        for (var row = 0; row < rowCount; row++)
            RowDefinitions.Add(new RowDefinition(GridLength.Auto));

        for (var index = 0; index < count; index++)
        {
            var button = CreateDayButton();
            SetRow((BindableObject)button, index / 7);
            SetColumn((BindableObject)button, index % 7);
            _dayButtons.Add(button);
            Children.Add(button);
        }
    }

    private void UpdateExistingButtons(IReadOnlyList<CycleDayItem> days)
    {
        for (var index = 0; index < days.Count; index++)
        {
            var day = days[index];
            var button = _dayButtons[index];

            button.Text = day.Day.ToString();
            button.BackgroundColor = day.BackgroundColor;
            button.TextColor = day.TextColor;
            button.BorderColor = day.StrokeColor;
            button.BorderWidth = day.StrokeThickness;
            button.Command = DaySelectedCommand;
            button.CommandParameter = day;
            SemanticProperties.SetDescription(button, BuildDescription(day));
        }
    }

    private void ClearButtons()
    {
        Children.Clear();
        RowDefinitions.Clear();
        _dayButtons.Clear();
    }

    private static Button CreateDayButton()
    {
        return new Button
        {
            Padding = 0,
            Margin = 2.5,
            MinimumHeightRequest = 36,
            HeightRequest = 36,
            CornerRadius = 10,
            FontSize = 12,
            FontAttributes = FontAttributes.Bold
        };
    }

    private static string BuildDescription(CycleDayItem day)
    {
        var date = day.Date?.ToString("MMMM d") ?? $"Day {day.Day}";
        var phase = day.IsNeutral ? "not logged" : $"{day.Phase} phase";
        var state = day.IsToday
            ? ", today"
            : day.HasPhaseShiftLog
                ? ", logged phase shift"
                : day.IsPredictedFuture
                    ? ", predicted"
                    : string.Empty;

        return $"{date}, {phase}{state}. Double tap to edit.";
    }
}

using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.UI.Dashboard;

namespace MuscleCuties.App.Controls.Dashboard;

public sealed class DashboardMonthGrid : Grid
{
    public static readonly BindableProperty ItemsSourceProperty = BindableProperty.Create(
        nameof(ItemsSource),
        typeof(IReadOnlyList<DashboardCalendarDay>),
        typeof(DashboardMonthGrid),
        defaultValue: null,
        propertyChanged: OnItemsSourceChanged);

    public IReadOnlyList<DashboardCalendarDay>? ItemsSource
    {
        get => (IReadOnlyList<DashboardCalendarDay>?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public DashboardMonthGrid()
    {
        ColumnSpacing = 0;
        RowSpacing = 2;
        for (var c = 0; c < 7; c++)
            ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
    }

    private static void OnItemsSourceChanged(BindableObject bindable, object oldValue, object newValue)
    {
        if (bindable is DashboardMonthGrid grid)
            grid.UpdateDays();
    }

    private void UpdateDays()
    {
        var items = ItemsSource;
        if (items is null || items.Count == 0)
        {
            Children.Clear();
            RowDefinitions.Clear();
            return;
        }

        var rowCount = (int)Math.Ceiling(items.Count / 7.0);
        EnsureRows(rowCount);

        // Remove excess children
        while (Children.Count > items.Count)
            Children.RemoveAt(Children.Count - 1);

        // Update existing + add new
        for (var i = 0; i < items.Count; i++)
        {
            var row = i / 7;
            var col = i % 7;
            var day = items[i];

            if (i < Children.Count)
            {
                UpdateCell((Grid)Children[i], day, row, col);
            }
            else
            {
                var cell = CreateCell(day, row, col);
                Children.Add(cell);
            }
        }
    }

    private void EnsureRows(int rowCount)
    {
        while (RowDefinitions.Count < rowCount)
            RowDefinitions.Add(new RowDefinition(new GridLength(38)));
        while (RowDefinitions.Count > rowCount)
            RowDefinitions.RemoveAt(RowDefinitions.Count - 1);
    }

    private static Grid CreateCell(DashboardCalendarDay day, int row, int col)
    {
        var cell = new Grid
        {
            RowDefinitions =
            {
                new RowDefinition(GridLength.Auto),
                new RowDefinition(new GridLength(8))
            },
            RowSpacing = 1,
            HorizontalOptions = LayoutOptions.Fill,
            VerticalOptions = LayoutOptions.Center
        };

        ApplyCellContent(cell, day);
        SetRow(cell, row);
        SetColumn(cell, col);
        return cell;
    }

    private static void UpdateCell(Grid cell, DashboardCalendarDay day, int row, int col)
    {
        cell.Children.Clear();
        ApplyCellContent(cell, day);
        SetRow(cell, row);
        SetColumn(cell, col);
    }

    private static readonly Color BrandRose = Color.FromArgb("#C85A87");
    private static readonly Color BlushLight = Color.FromArgb("#F8DFF1");
    private static readonly Color BlushDark = Color.FromArgb("#3A2931");
    private static readonly Color WorkoutGreen = Color.FromArgb("#4CAF50");
    private static readonly Color MealBlue = Color.FromArgb("#42A5F5");

    private static void ApplyCellContent(Grid cell, DashboardCalendarDay day)
    {
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;

        var label = new Label
        {
            Text = day.Day.ToString(),
            FontSize = 12,
            HorizontalOptions = LayoutOptions.Center,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalOptions = LayoutOptions.Center
        };

        if (day.IsToday)
        {
            label.FontAttributes = FontAttributes.Bold;

            var todayBorder = new Border
            {
                WidthRequest = 30,
                HeightRequest = 30,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                BackgroundColor = isDark == true ? BlushDark : BlushLight,
                Stroke = BrandRose,
                StrokeThickness = 1.5,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Content = label
            };
            cell.Add(todayBorder);
        }
        else
        {
            var hasPhase = day.PhaseColor != Colors.Transparent;

            var dayBorder = new Border
            {
                WidthRequest = 30,
                HeightRequest = 30,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
                BackgroundColor = hasPhase ? day.PhaseColor : Colors.Transparent,
                StrokeThickness = 0,
                HorizontalOptions = LayoutOptions.Center,
                VerticalOptions = LayoutOptions.Center,
                Opacity = day.IsCurrentMonth ? 1.0 : 0.25,
                Content = label
            };
            cell.Add(dayBorder);
        }

        // Activity dots row
        var dotsLayout = new HorizontalStackLayout
        {
            Spacing = 3,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Start
        };
        SetRow(dotsLayout, 1);

        if (day.HasWorkout)
        {
            dotsLayout.Add(new BoxView
            {
                WidthRequest = 5,
                HeightRequest = 5,
                CornerRadius = 2.5,
                Color = WorkoutGreen
            });
        }
        else if (day.HasPlannedWorkout && day.IsCurrentMonth)
        {
            dotsLayout.Add(new Border
            {
                WidthRequest = 5,
                HeightRequest = 5,
                StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 2.5 },
                Stroke = WorkoutGreen,
                StrokeThickness = 1,
                BackgroundColor = Colors.Transparent
            });
        }

        if (day.HasMealLogged)
        {
            dotsLayout.Add(new BoxView
            {
                WidthRequest = 5,
                HeightRequest = 5,
                CornerRadius = 2.5,
                Color = MealBlue
            });
        }

        if (dotsLayout.Children.Count > 0)
            cell.Add(dotsLayout);
    }
}

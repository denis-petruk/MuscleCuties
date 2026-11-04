using System.ComponentModel;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Controls.Cycle;

public partial class CycleCalendarCard : ContentView
{
    public CycleCalendarCard()
    {
        InitializeComponent();
        BindingContextChanged += OnCardBindingContextChanged;
        Loaded += OnCardLoaded;
    }

    private void OnCardBindingContextChanged(object? sender, EventArgs e)
    {
        if (_observedViewModel is not null)
            _observedViewModel.PropertyChanged -= OnViewModelPropertyChanged;

        _observedViewModel = BindingContext as CycleViewModel;
        if (_observedViewModel is not null)
            _observedViewModel.PropertyChanged += OnViewModelPropertyChanged;

        QueueScrollToSelectedDay();
    }

    private CycleViewModel? _observedViewModel;

    private void OnCardLoaded(object? sender, EventArgs e) => QueueScrollToSelectedDay();

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CycleViewModel.CalendarStripDays))
            QueueScrollToSelectedDay();
    }

    private void QueueScrollToSelectedDay()
    {
        Dispatcher.Dispatch(() => _ = ScrollToSelectedDayAsync());
    }

    private async Task ScrollToSelectedDayAsync()
    {
        if (_observedViewModel is null || MonthStripDaysLayout.Children.Count == 0)
            return;

        var selectedDate = _observedViewModel.SelectedDate.Date;
        var selectedIndex = _observedViewModel.CalendarStripDays.ToList()
            .FindIndex(day => day.Date?.Date == selectedDate);
        if (selectedIndex < 0 || selectedIndex >= MonthStripDaysLayout.Children.Count)
            return;

        if (MonthStripDaysLayout.Children[selectedIndex] is not VisualElement selectedDay)
            return;

        try
        {
            await MonthStripScrollView.ScrollToAsync(
                selectedDay, ScrollToPosition.Center, false);
        }
        catch (InvalidOperationException)
        {
            // A month reload can replace the strip before layout completes.
        }
    }
}

using System.ComponentModel;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Controls.Cycle;

public partial class CycleCalendarCard : ContentView
{
    private readonly ViewModelSubscription<CycleViewModel> _subscription;

    public CycleCalendarCard()
    {
        InitializeComponent();
        _subscription = ViewModelSubscriptionBinding.BindToControl<CycleViewModel>(this,
            OnViewModelPropertyChanged);
        BindingContextChanged += (_, _) => QueueScrollToSelectedDay();
        Loaded += OnCardLoaded;
    }

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
        if (_subscription.Current is not { } viewModel || MonthStripDaysLayout.Children.Count == 0)
            return;

        var selectedDate = viewModel.SelectedDate.Date;
        var selectedIndex = viewModel.CalendarStripDays.ToList()
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

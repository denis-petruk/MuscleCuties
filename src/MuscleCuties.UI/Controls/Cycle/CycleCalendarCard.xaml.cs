using System.ComponentModel;
using MuscleCuties.UI.Controls.Shared;
using MuscleCuties.UI.ViewModels.Cycle;

namespace MuscleCuties.UI.Controls.Cycle;

/// <summary>
///     Weekly strip that selects a calendar day and shows which cycle phase that day falls in.
/// </summary>
public partial class CycleCalendarCard : ContentView
{
    private readonly ViewModelSubscription<CycleViewModel> _subscription;

    /// <summary>Wires the card to its view model and queues the initial scroll.</summary>
    public CycleCalendarCard()
    {
        InitializeComponent();
        _subscription = ViewModelSubscriptionBinding.BindToControl<CycleViewModel>(this,
            OnViewModelPropertyChanged);
        BindingContextChanged += (_, _) => QueueScrollToSelectedDay();
        Loaded += OnCardLoaded;
    }

    /// <summary>Queues a scroll to the selected day when the card loads.</summary>
    private void OnCardLoaded(object? sender, EventArgs e) => QueueScrollToSelectedDay();

    /// <summary>Queues a scroll when the calendar strip's days change.</summary>
    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(CycleViewModel.CalendarStripDays))
            QueueScrollToSelectedDay();
    }

    /// <summary>Defers the scroll to the selected day until the strip has laid out, so it scrolls to a cell that already has a</summary>
    /// position rather than to zero.</summary>
    private void QueueScrollToSelectedDay()
    {
        Dispatcher.Dispatch(() => _ = ScrollToSelectedDayAsync());
    }

    /// <summary>Scrolls the strip so the selected day is in view.</summary>
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

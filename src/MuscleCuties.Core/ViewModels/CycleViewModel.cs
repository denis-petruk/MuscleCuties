using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Maui.Graphics;
using MuscleCuties.Core.Models.Enums;
using MuscleCuties.Core.Services;

namespace MuscleCuties.Core.ViewModels;

public partial class CycleViewModel : ObservableObject
{
    private readonly IAuthService _authService;
    private readonly ICycleService _cycleService;

    [ObservableProperty] private CyclePhase _currentPhase;
    [ObservableProperty] private int _currentDay;
    [ObservableProperty] private int _cycleLength;
    [ObservableProperty] private int _daysUntilPeriod;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private ObservableCollection<CycleDayItem> _calendarDays = new();
    [ObservableProperty] private ObservableCollection<PhaseItem> _phases = new();

    public string PhaseLabel => CurrentPhase.ToString();
    public string CurrentMonthLabel => DateTime.Today.ToString("MMMM yyyy");

    // Legacy alias kept for existing tests
    public int CycleDay => CurrentDay;

    public AsyncRelayCommand LoadDataCommand { get; }

    public CycleViewModel(IAuthService authService, ICycleService cycleService)
    {
        _authService = authService;
        _cycleService = cycleService;
        LoadDataCommand = new AsyncRelayCommand(LoadDataAsync);
        Phases = BuildPhaseItems();
    }

    private async Task LoadDataAsync()
    {
        IsBusy = true;
        try
        {
            var userId = await _authService.GetCurrentUserIdAsync();
            var cycle = await _cycleService.GetCurrentCycleAsync(userId);

            if (cycle is null)
            {
                CurrentDay = 0;
                CycleLength = 0;
            }
            else
            {
                CurrentDay = _cycleService.CalculateCycleDay(cycle.CycleStartDate);
                CycleLength = cycle.CycleLength;
            }

            DaysUntilPeriod = CycleLength > 0 ? Math.Max(0, CycleLength - CurrentDay) : 0;
            CurrentPhase = await _cycleService.GetCurrentPhaseAsync(userId);
            CalendarDays = BuildCalendarDays(CurrentDay, CycleLength > 0 ? CycleLength : 0);
            OnPropertyChanged(nameof(PhaseLabel));
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static ObservableCollection<CycleDayItem> BuildCalendarDays(int currentDay, int cycleLength)
    {
        var items = new ObservableCollection<CycleDayItem>();
        for (int d = 1; d <= cycleLength; d++)
        {
            var isToday = d == currentDay;
            var bg = GetDayBackground(d, isToday);
            var text = isToday ? Colors.White : GetDayTextColor(d);
            items.Add(new CycleDayItem
            {
                Day = d,
                BackgroundColor = bg,
                TextColor = text,
                StrokeColor = isToday ? Color.FromArgb("#C2185B") : Colors.Transparent,
                StrokeThickness = isToday ? 2 : 0
            });
        }
        return items;
    }

    private static Color GetDayBackground(int day, bool isToday)
    {
        if (isToday) return Color.FromArgb("#C2185B");
        if (day <= 5)  return Color.FromArgb("#FFE4EC");
        if (day <= 13) return Color.FromArgb("#E8F5E9");
        if (day <= 16) return Color.FromArgb("#FFFDE7");
        return Color.FromArgb("#EDE7F6");
    }

    private static Color GetDayTextColor(int day)
    {
        if (day <= 5)  return Color.FromArgb("#880E4F");
        if (day <= 13) return Color.FromArgb("#1B5E20");
        if (day <= 16) return Color.FromArgb("#F57F17");
        return Color.FromArgb("#4527A0");
    }

    private static ObservableCollection<PhaseItem> BuildPhaseItems() =>
        new()
        {
            new PhaseItem
            {
                Name = "Menstrual",
                Description = "Days 1-5 · Low intensity, rest and recover",
                BackgroundColor = Color.FromArgb("#FFE4EC"),
                TextColor = Color.FromArgb("#880E4F")
            },
            new PhaseItem
            {
                Name = "Follicular",
                Description = "Days 6-13 · Energy rising, great for strength training",
                BackgroundColor = Color.FromArgb("#E8F5E9"),
                TextColor = Color.FromArgb("#1B5E20")
            },
            new PhaseItem
            {
                Name = "Ovulatory",
                Description = "Days 14-16 · Peak performance, max intensity",
                BackgroundColor = Color.FromArgb("#FFFDE7"),
                TextColor = Color.FromArgb("#F57F17")
            },
            new PhaseItem
            {
                Name = "Luteal",
                Description = "Days 17-28 · Moderate exercise, listen to your body",
                BackgroundColor = Color.FromArgb("#EDE7F6"),
                TextColor = Color.FromArgb("#4527A0")
            }
        };

    partial void OnCurrentPhaseChanged(CyclePhase value)
    {
        OnPropertyChanged(nameof(PhaseLabel));
    }
}

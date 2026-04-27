using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;

namespace MuscleCuties.ViewModels.Cycle;

public class CycleDayItem
{
    public int Day { get; set; }
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
    public bool IsToday { get; set; }
    public Color StrokeColor => IsToday ? Color.FromArgb("#C85A87") : Colors.Transparent;
    public double StrokeThickness => IsToday ? 2 : 0;
}

public class PhaseItem
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public Color BackgroundColor { get; set; } = Colors.Transparent;
    public Color TextColor { get; set; } = Colors.Black;
}

public partial class CycleViewModel : ObservableObject
{
    [ObservableProperty]
    private int _currentDay = 14;

    [ObservableProperty]
    private int _daysUntilPeriod = 14;

    [ObservableProperty]
    private string _currentMonthLabel = DateTime.Now.ToString("MMMM");

    public ObservableCollection<CycleDayItem> CalendarDays { get; } = new();
    public ObservableCollection<PhaseItem> Phases { get; } = new();

    public CycleViewModel()
    {
        BuildCalendar();
        BuildPhases();
    }

    private void BuildCalendar()
    {
        // Phase mapping: days 1-5 menstrual, 6-13 follicular, 14-16 ovulatory, 17-28 luteal
        for (int d = 1; d <= 28; d++)
        {
            var (bg, fg) = GetPhaseColors(d);
            CalendarDays.Add(new CycleDayItem
            {
                Day = d,
                BackgroundColor = bg,
                TextColor = fg,
                IsToday = d == CurrentDay
            });
        }
    }

    private static (Color bg, Color fg) GetPhaseColors(int day)
    {
        if (day <= 5)  return (Color.FromArgb("#F9D6D8"), Color.FromArgb("#7A3A48"));
        if (day <= 13) return (Color.FromArgb("#D6EED6"), Color.FromArgb("#3A6B3A"));
        if (day <= 16) return (Color.FromArgb("#FFF0C4"), Color.FromArgb("#7A6000"));
        return (Color.FromArgb("#E8D8F5"), Color.FromArgb("#5A3B80"));
    }

    private void BuildPhases()
    {
        Phases.Add(new PhaseItem
        {
            Name = "Menstrual",
            Description = "Rest, renewal · days 1–5",
            BackgroundColor = Color.FromArgb("#F9D6D8"),
            TextColor = Color.FromArgb("#7A3A48")
        });
        Phases.Add(new PhaseItem
        {
            Name = "Follicular",
            Description = "Energy, growth · days 6–13",
            BackgroundColor = Color.FromArgb("#D6EED6"),
            TextColor = Color.FromArgb("#3A6B3A")
        });
        Phases.Add(new PhaseItem
        {
            Name = "Ovulatory",
            Description = "Peak, radiance · days 14–16",
            BackgroundColor = Color.FromArgb("#FFF0C4"),
            TextColor = Color.FromArgb("#7A6000")
        });
        Phases.Add(new PhaseItem
        {
            Name = "Luteal",
            Description = "Calm, reflection · days 17–28",
            BackgroundColor = Color.FromArgb("#E8D8F5"),
            TextColor = Color.FromArgb("#5A3B80")
        });
    }
}
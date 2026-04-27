using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace MuscleCuties.ViewModels.Workout;

public class WorkoutItem
{
    public string Tag { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public Color PhaseBackground { get; set; } = Colors.Transparent;
    public Color PhaseTextColor { get; set; } = Colors.Black;
}

public class FilterChipItem : ObservableObject
{
    private bool _isSelected;
    public string Label { get; set; } = string.Empty;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public partial class WorkoutViewModel : ObservableObject
{
    [ObservableProperty]
    private string _currentPhaseName = "Follicular";

    [ObservableProperty]
    private string _weekTitle = "This week";

    private static readonly Color FollicularBg = Color.FromArgb("#D6EED6");
    private static readonly Color FollicularText = Color.FromArgb("#3A6B3A");
    private static readonly Color CardioColor = Color.FromArgb("#FFF0C4");
    private static readonly Color CardioText = Color.FromArgb("#7A6000");
    private static readonly Color LutealBg = Color.FromArgb("#E8D8F5");
    private static readonly Color LutealText = Color.FromArgb("#5A3B80");

    public ObservableCollection<FilterChipItem> Filters { get; } = new();
    public ObservableCollection<WorkoutItem> Workouts { get; } = new();

    public WorkoutViewModel()
    {
        BuildFilters();
        LoadWorkouts();
    }

    private void BuildFilters()
    {
        var labels = new[] { "All", "Strength", "Cardio", "Yoga", "Recovery" };
        foreach (var label in labels)
        {
            Filters.Add(new FilterChipItem { Label = label, IsSelected = label == "All" });
        }
    }

    [RelayCommand]
    private void SelectFilter(FilterChipItem chip)
    {
        foreach (var f in Filters)
            f.IsSelected = false;
        chip.IsSelected = true;
    }

    private void LoadWorkouts()
    {
        Workouts.Clear();
        Workouts.Add(new WorkoutItem
        {
            Tag = "STRENGTH",
            Title = "Upper body push",
            Duration = "50 min · heavy",
            PhaseBackground = FollicularBg,
            PhaseTextColor = FollicularText
        });
        Workouts.Add(new WorkoutItem
        {
            Tag = "CARDIO",
            Title = "HIIT intervals",
            Duration = "25 min · high",
            PhaseBackground = CardioColor,
            PhaseTextColor = CardioText
        });
        Workouts.Add(new WorkoutItem
        {
            Tag = "RECOVERY",
            Title = "Slow flow yoga",
            Duration = "30 min · low",
            PhaseBackground = LutealBg,
            PhaseTextColor = LutealText
        });
    }
}
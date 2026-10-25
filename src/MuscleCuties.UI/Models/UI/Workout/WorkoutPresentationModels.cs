using Microsoft.Maui.Graphics;
using CommunityToolkit.Mvvm.ComponentModel;

using MuscleCuties.UI.Resources.Styles;

namespace MuscleCuties.UI.Models.UI.Workout;

public partial class WorkoutItem : ObservableObject
{
    public int WorkoutDayId { get; set; }
    public string Tag { get; set; } = string.Empty;
    public string DayLabel { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public int ExerciseCount { get; set; }
    public int ActivityCount { get; set; }
    public string DetailsText { get; set; } = string.Empty;
    public Color ActivityBackground { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    public Color ActivityTextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    public bool IsRestDay { get; set; }
    public string SessionProgressText { get; set; } = "Upcoming";
    public bool IsCompleted { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EasierAlternativeStatusText))]
    private bool _isEasierAlternative;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(OptionalStatusText), nameof(CanMarkEasierAlternative))]
    private bool _isOptional;
    public string OptionalStatusText => IsOptional ? "Workout_Readiness_OptionalSession" : "Workout_Readiness_RequiredSession";
    public string EasierAlternativeStatusText => IsEasierAlternative
        ? "Workout_Readiness_EasierAlternativeMarked"
        : "Workout_Readiness_MarkEasierAlternative";
    public bool CanMarkOptional => !IsCompleted && !IsRestDay;
    public bool CanMarkEasierAlternative => CanMarkOptional && IsOptional;
}

public sealed record WorkoutActivitySection(
    string Tag,
    string Title,
    int ExerciseCount,
    Color Background,
    Color TextColor)
{
    public string ExerciseCountText => ExerciseCount == 1 ? "1 exercise" : $"{ExerciseCount} exercises";
}

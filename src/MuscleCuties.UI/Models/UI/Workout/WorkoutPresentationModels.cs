using Microsoft.Maui.Graphics;


namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>One session card in the workout list: what the session is, how long it takes, whether</summary>
/// it is done, and the optional/easier markers the person can set.</summary>
public partial class WorkoutItem : ObservableObject
{
    /// <summary>Planned day this card opens; zero for a session that is not on the plan.</summary>
    public int WorkoutDayId { get; set; }
    /// <summary>Activity tag, which selects the card's glyph and colours.</summary>
    public string Tag { get; set; } = string.Empty;
    /// <summary>Glyph for the card's activity tag.</summary>
    public string IconGlyph => WorkoutActivityIcons.ForTag(Tag);
    /// <summary>Weekday label shown above the title.</summary>
    public string DayLabel { get; set; } = string.Empty;
    /// <summary>Session name.</summary>
    public string Title { get; set; } = string.Empty;
    /// <summary>Estimated length, already formatted with its unit.</summary>
    public string Duration { get; set; } = string.Empty;
    /// <summary>Movements in the session, used for the summary line.</summary>
    public int ExerciseCount { get; set; }
    /// <summary>Activity blocks in the session, used when the session has no exercise rows.</summary>
    public int ActivityCount { get; set; }
    /// <summary>Secondary line combining the counts and focus of the session.</summary>
    public string DetailsText { get; set; } = string.Empty;
    /// <summary>Card tint for the session's activity.</summary>
    public Color ActivityBackground { get; set; } = AppThemeResources.GetRequired<Color>("Transparent");
    /// <summary>Text colour that stays readable on the card tint.</summary>
    public Color ActivityTextColor { get; set; } = AppThemeResources.GetRequired<Color>("TextPrimary");
    /// <summary>Rest days keep their card but offer no start action.</summary>
    public bool IsRestDay { get; set; }
    /// <summary>Where the session sits in its week, as shown on the card badge.</summary>
    public string SessionProgressText { get; set; } = "Upcoming";
    /// <summary>Whether the session has been finished, which switches the card to its completed state.</summary>
    public bool IsCompleted { get; set; }
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(EasierAlternativeStatusText))]
    private bool _isEasierAlternative;
    [ObservableProperty] [NotifyPropertyChangedFor(nameof(OptionalStatusText), nameof(CanMarkEasierAlternative))]
    private bool _isOptional;
    /// <summary>Resource key naming the session as optional or required (the view localizes it).</summary>
    public string OptionalStatusText => IsOptional ? "Workout_Readiness_OptionalSession" : "Workout_Readiness_RequiredSession";
    /// <summary>Resource key for the easier-alternative marker, phrased as a state or as the action.</summary>
    public string EasierAlternativeStatusText => IsEasierAlternative
        ? "Workout_Readiness_EasierAlternativeMarked"
        : "Workout_Readiness_MarkEasierAlternative";
    /// <summary>True when the day can be marked optional.</summary>
    public bool CanMarkOptional => !IsCompleted && !IsRestDay;
    /// <summary>True when the day can be swapped for an easier option.</summary>
    public bool CanMarkEasierAlternative => CanMarkOptional && IsOptional;
    // Beta 1.0.0 gates rescheduling to 1.1, so the move action never renders for an eligible day.
    /// <summary>False while the current release keeps rescheduling behind its scope gate.</summary>
    public bool CanReschedule =>
        MuscleCuties.Core.Domain.Workout.BetaWorkoutScope.CanOfferReschedule(IsCompleted, IsRestDay);
}

/// <summary>Header of an activity block in the list: its tag, title, movement count and colours.</summary>
public sealed record WorkoutActivitySection(
    string Tag,
    string Title,
    int ExerciseCount,
    Color Background,
    Color TextColor)
{
    /// <summary>Movement count with the singular form used for a one-movement block.</summary>
    public string ExerciseCountText => ExerciseCount == 1 ? "1 exercise" : $"{ExerciseCount} exercises";
}

/// <summary>One selectable date in the reschedule sheet.</summary>
public partial class RescheduleDateOption : ObservableObject
{
    /// <summary>The date of the entry.</summary>
    public DateOnly Date { get; }
    /// <summary>The day label shown in the list.</summary>
    public string DayLabel { get; }
    /// <summary>The date formatted for display.</summary>
    public string FormattedDate { get; }
    [ObservableProperty] private bool _isSelected;

    /// <summary>Builds an option from the date and the two label forms the row shows.</summary>
    public RescheduleDateOption(DateOnly date, string dayLabel, string formattedDate)
    {
        Date = date;
        DayLabel = dayLabel;
        FormattedDate = formattedDate;
    }

    /// <summary>Weekday and date on one line, which is what the sheet row shows.</summary>
    public string DisplayText => $"{DayLabel}, {FormattedDate}";
}

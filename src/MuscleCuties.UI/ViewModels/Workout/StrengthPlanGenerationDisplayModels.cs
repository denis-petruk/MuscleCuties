using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>One selectable activity on the generation page, carrying its label and the description that explains</summary>
/// what the activity involves.</summary>
public partial class StrengthPlanActivityChoice : ObservableObject
{
    /// <summary>Builds a choice from the activity and the already-localized text for it.</summary>
    public StrengthPlanActivityChoice(WorkoutActivityDto activity, string label, string description = "")
    {
        Activity = activity;
        Label = label;
        Description = description;
    }

    /// <summary>The activity the choice stands for.</summary>
    public WorkoutActivityDto Activity { get; }
    /// <summary>Localized label shown for the choice.</summary>
    public string Label { get; }
    /// <summary>Localized supporting line shown under the label.</summary>
    public string Description { get; }

    [ObservableProperty] private bool _isSelected;
}

/// <summary>One week of the recent-activity strip: how many sessions were completed, and how tall its bar should be.</summary>
public sealed record StrengthPlanRecentWeekDisplay(
    string Label,
    string SessionCount,
    string AccessibilityDescription,
    int Count = 0)
{
    /// <summary>Four points per session, capped for the compact week strip; zero stays absent.</summary>
    public double BarHeight => Count <= 0 ? 0d : Math.Clamp(Count * 4d, 4d, 40d);
}

/// <summary>A yes/no answer offered for a HIIT evidence question, paired with the value it stands for.</summary>
public sealed record ActivityEvidenceAnswerChoice(string Label, bool Value);

/// <summary>One planned session inside a day: what it is, when, how long, and its activity-specific picture.</summary>
public sealed record MixedActivitySessionDisplay(string Title, string Details)
{
    /// <summary>Localized weekday this session lands on, empty when the plan is not laid out by day.</summary>
    public string Day { get; init; } = string.Empty;
    /// <summary>Localized name of the activity this session trains.</summary>
    public string Activity { get; init; } = string.Empty;
    /// <summary>Activity-specific glyph, so two different planned activities never share a picture.</summary>
    public string IconGlyph { get; init; } = "CircleSmall24";
    /// <summary>The session's duration text.</summary>
    public string Duration { get; init; } = string.Empty;
    /// <summary>Warm-up, main, cool-down and interval split of a timed phase.</summary>
    public string PhaseDetail { get; init; } = string.Empty;
    /// <summary>True when a phase detail is shown.</summary>
    public bool HasPhaseDetail => !string.IsNullOrWhiteSpace(PhaseDetail);
}

/// <summary>One row of the mixed-activity schedule: its sessions, or a rest day with none.</summary>
public sealed record MixedActivityDayDisplay(
    string Day,
    IReadOnlyList<MixedActivitySessionDisplay> Sessions,
    bool IsRestDay)
{
    /// <summary>The icon is bound as an image so the day header is never an empty decorative box.
    /// A training day shows a calendar, never the session's own activity glyph: the header names
    /// the day, the rows name the activity.</summary>
    public string DayIconGlyph => IsRestDay ? "WeatherMoon24" : "CalendarDay24";
}

/// <summary>How one activity was decided for the plan — accepted, adjusted or refused — with the reasons and the</summary>
/// frequency it ends up at.</summary>
public sealed record MixedActivityDecisionDisplay(string Activity, string Status, string Frequency,
    ObservableCollection<string> Reasons)
{
    /// <summary>True when the item has reasons.</summary>
    public bool HasReasons => Reasons.Count > 0;
    /// <summary>True when a frequency line is shown.</summary>
    public bool HasFrequency => !string.IsNullOrWhiteSpace(Frequency);
}

/// <summary>One movement slot in a generated session, with whether the plan treats it as optional.</summary>
public sealed record StrengthPlanSlotDisplay(string Name, string Details, bool IsRequired)
{
    private readonly string _rationale = string.Empty;

    /// <summary>Why this movement was selected for the slot, in the user's language. A blank
    /// source value (null, empty or whitespace-only) is normalized to an empty string, so the
    /// rationale row can never render empty text and <see cref="HasRationale"/> can never be true
    /// for a movement without genuine rationale content.</summary>
    public string Rationale
    {
        get => _rationale;
        init => _rationale = string.IsNullOrWhiteSpace(value) ? string.Empty : value;
    }

    /// <summary>Whether the slot has a reason to show; the rationale is normalized on assignment so this is never true for</summary>
    /// blank text.</summary>
    public bool HasRationale => _rationale.Length > 0;
}

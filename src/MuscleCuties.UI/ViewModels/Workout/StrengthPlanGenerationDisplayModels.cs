using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.ViewModels.Workout;

public partial class StrengthPlanActivityChoice : ObservableObject
{
    public StrengthPlanActivityChoice(WorkoutActivityDto activity, string label, string description = "")
    {
        Activity = activity;
        Label = label;
        Description = description;
    }

    public WorkoutActivityDto Activity { get; }
    public string Label { get; }
    public string Description { get; }

    [ObservableProperty] private bool _isSelected;
}

public sealed record StrengthPlanRecentWeekDisplay(
    string Label,
    string SessionCount,
    string AccessibilityDescription,
    int Count = 0)
{
    /// <summary>Four points per session, capped for the compact week strip; zero stays absent.</summary>
    public double BarHeight => Count <= 0 ? 0d : Math.Clamp(Count * 4d, 4d, 40d);
}

public sealed record ActivityEvidenceAnswerChoice(string Label, bool Value);

public sealed record MixedActivitySessionDisplay(string Title, string Details)
{
    public string Day { get; init; } = string.Empty;
    public string Activity { get; init; } = string.Empty;
    /// <summary>Activity-specific glyph, so two different planned activities never share a picture.</summary>
    public string IconGlyph { get; init; } = "CircleSmall24";
    public string Duration { get; init; } = string.Empty;
    /// <summary>Warm-up, main, cool-down and interval split of a timed phase.</summary>
    public string PhaseDetail { get; init; } = string.Empty;
    public bool HasPhaseDetail => !string.IsNullOrWhiteSpace(PhaseDetail);
}

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

public sealed record MixedActivityDecisionDisplay(string Activity, string Status, string Frequency,
    ObservableCollection<string> Reasons)
{
    public bool HasReasons => Reasons.Count > 0;
    public bool HasFrequency => !string.IsNullOrWhiteSpace(Frequency);
}

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

    public bool HasRationale => _rationale.Length > 0;
}

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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
    string AccessibilityDescription);

public sealed record ActivityEvidenceAnswerChoice(string Label, bool Value);

public sealed record MixedActivitySessionDisplay(string Title, string Details)
{
    public string Day { get; init; } = string.Empty;
    public string Activity { get; init; } = string.Empty;
    public string Duration { get; init; } = string.Empty;
    /// <summary>Warm-up, main, cool-down and interval split of a timed phase.</summary>
    public string PhaseDetail { get; init; } = string.Empty;
    public bool HasPhaseDetail => !string.IsNullOrWhiteSpace(PhaseDetail);
}

public sealed record MixedActivityDayDisplay(
    string Day,
    IReadOnlyList<MixedActivitySessionDisplay> Sessions,
    bool IsRestDay);

public sealed record MixedActivityDecisionDisplay(string Activity, string Status, string Frequency,
    ObservableCollection<string> Reasons)
{
    public bool HasReasons => Reasons.Count > 0;
    public bool HasFrequency => !string.IsNullOrWhiteSpace(Frequency);
}

public sealed record StrengthPlanSlotDisplay(string Name, string Details, bool IsRequired)
{
    /// <summary>Why this movement was selected for the slot, in the user's language.</summary>
    public string Rationale { get; init; } = string.Empty;
    public bool HasRationale => !string.IsNullOrWhiteSpace(Rationale);
}

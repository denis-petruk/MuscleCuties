using System.Collections.ObjectModel;
using MuscleCuties.UseCases.Features.Workout.Contracts;

namespace MuscleCuties.UI.ViewModels.Workout;

public sealed record StrengthPlanActivityChoice(WorkoutActivityDto Activity, string Label);

public sealed record ActivityEvidenceAnswerChoice(string Label, bool Value);

public sealed record MixedActivitySessionDisplay(string Title, string Details);

public sealed record MixedActivityDecisionDisplay(string Activity, string Status, string Frequency,
    ObservableCollection<string> Reasons)
{
    public bool HasReasons => Reasons.Count > 0;
}

public sealed record StrengthPlanSlotDisplay(string Name, string Details, bool IsRequired);

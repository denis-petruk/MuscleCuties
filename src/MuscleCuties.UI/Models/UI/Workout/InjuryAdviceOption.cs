
namespace MuscleCuties.UI.Models.UI.Workout;

/// <summary>
/// F01: one answer to the clinician-advice question. An unanswered answer is a distinct choice
/// whose value is <c>null</c>, so it is never downgraded to an explicit "No". The shared
/// <c>ChoiceCell</c> style projects <see cref="IsSelected"/> into selected/not-selected semantics.
/// </summary>
public partial class InjuryAdviceOption : ObservableObject
{
    /// <summary>The answer the option stands for.</summary>
    [ObservableProperty] private bool _isSelected;
    public bool? Value { get; init; }
    /// <summary>Resource key of the answer's label, so the view localizes it at render time.</summary>
    public string LabelKey { get; init; } = string.Empty;
}

using CommunityToolkit.Mvvm.ComponentModel;

namespace MuscleCuties.UI.ViewModels.Workout;

public sealed partial class StrengthPlanHighIntensityCandidateDisplay : ObservableObject
{
    public StrengthPlanHighIntensityCandidateDisplay(
        int exerciseId,
        string name,
        string assignments,
        IReadOnlyList<ActivityEvidenceAnswerChoice> answerChoices,
        ActivityEvidenceAnswerChoice? familiarityChoice = null,
        ActivityEvidenceAnswerChoice? manageableLoadChoice = null)
    {
        ExerciseId = exerciseId;
        Name = name;
        Assignments = assignments;
        AnswerChoices = answerChoices;
        FamiliarityChoice = familiarityChoice;
        ManageableLoadChoice = manageableLoadChoice;
    }

    public int ExerciseId { get; }
    public string Name { get; }
    public string Assignments { get; }
    public IReadOnlyList<ActivityEvidenceAnswerChoice> AnswerChoices { get; }

    [ObservableProperty]
    private ActivityEvidenceAnswerChoice? _familiarityChoice;

    [ObservableProperty]
    private ActivityEvidenceAnswerChoice? _manageableLoadChoice;
}

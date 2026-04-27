using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.ViewModels;

public class SelectableQuizAnswer
{
    public QuizAnswer Answer { get; set; } = null!;
    public bool IsSelected { get; set; }
}

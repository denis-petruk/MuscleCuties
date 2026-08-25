using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Entities.Quiz;

namespace MuscleCuties.Core.Models.UI.Quiz;

public partial class SelectableQuizAnswer : ObservableObject
{
    public QuizAnswer Answer { get; init; } = null!;
    [ObservableProperty] private bool _isSelected;
}

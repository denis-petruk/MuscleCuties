using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Entities;

namespace MuscleCuties.Core.ViewModels;

public partial class SelectableQuizAnswer : ObservableObject
{
    public QuizAnswer Answer { get; set; } = null!;
    [ObservableProperty] private bool _isSelected;
}

using CommunityToolkit.Mvvm.ComponentModel;
using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Models.UI.Quiz;

public partial class SelectableQuizAnswer : ObservableObject
{
    public QuizAnswer Answer { get; init; } = null!;
    public QuizQuestionType QuestionType { get; init; }
    public string IconGlyph => QuestionType switch
    {
        QuizQuestionType.Goal => BuildGoalIcon(),
        QuizQuestionType.ExperienceLevel => BuildExperienceIcon(),
        QuizQuestionType.WorkoutDaysPerWeek => "CalendarWorkWeek24",
        QuizQuestionType.DietaryPreference => BuildDietIcon(),
        QuizQuestionType.CurrentCyclePhase => BuildPhaseIcon(),
        _ when QuestionType.ToString().Contains("Pain", StringComparison.OrdinalIgnoreCase) => BuildPainIcon(),
        _ when QuestionType.ToString().Contains("Energy", StringComparison.OrdinalIgnoreCase) => BuildEnergyIcon(),
        _ => "CheckmarkCircle24"
    };

    [ObservableProperty] private bool _isSelected;

    private string BuildGoalIcon() => (UserGoal)Answer.MappedValue switch
    {
        UserGoal.FatLoss => "Fire24",
        UserGoal.MuscleTone => "Dumbbell24",
        UserGoal.Strength => "Target24",
        UserGoal.MaintainHealth => "HeartCircle24",
        _ => "Target24"
    };

    private string BuildExperienceIcon() => Answer.MappedValue switch
    {
        <= 1 => "LeafThree24",
        2 => "Dumbbell24",
        _ => "TargetArrow24"
    };

    private string BuildDietIcon() => (DietaryTag)Answer.MappedValue switch
    {
        DietaryTag.Vegetarian => "LeafThree24",
        DietaryTag.Vegan => "LeafOne24",
        DietaryTag.GlutenFree => "FoodGrains24",
        DietaryTag.LactoseFree => "Drop24",
        _ => "Food24"
    };

    private string BuildPhaseIcon() => (CyclePhase)Answer.MappedValue switch
    {
        CyclePhase.Menstrual => "Drop24",
        CyclePhase.Follicular => "LeafThree24",
        CyclePhase.Ovulatory => "Fire24",
        CyclePhase.Luteal => "WeatherMoon24",
        _ => "HeartCircle24"
    };

    private string BuildPainIcon() => Answer.MappedValue switch
    {
        <= 1 => "CheckmarkCircle24",
        2 => "ShieldCheckmark24",
        3 => "BatteryWarning24",
        4 => "ShieldError24",
        _ => "HeartBroken24"
    };

    private string BuildEnergyIcon() => Answer.MappedValue switch
    {
        <= 1 => "Battery024",
        2 => "Battery224",
        3 => "Battery524",
        4 => "Battery824",
        _ => "BatteryCharge24"
    };
}

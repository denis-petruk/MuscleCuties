using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;

namespace MuscleCuties.UI.Models.UI.Quiz;

/// <summary>One answer option in the quiz: the catalog answer, its selection state, and the glyph</summary>
/// chosen for the question it belongs to.</summary>
public partial class SelectableQuizAnswer : ObservableObject
{
    /// <summary>The answer this option selects.</summary>
    [ObservableProperty] private bool _isSelected;
    public QuizAnswerDto Answer { get; init; } = null!;
    /// <summary>Which question this option belongs to. It selects the glyph family and the automation id.</summary>
    public QuizQuestionType QuestionType { get; init; }

    /// <summary>Stable E2E selector: question type plus the answer's catalog order.</summary>
    public string AutomationId => $"QuizAnswer{QuestionType}{Answer.OrderIndex}";

    /// <summary>The glyph for this option: mapped from the answer's value where the question has a scale,</summary>
    /// and from the question type otherwise.</summary>
    public string IconGlyph => QuestionType switch
    {
        QuizQuestionType.Goal => BuildGoalIcon(),
        QuizQuestionType.GoalPace => Answer.MappedValue == (int)WeightGoalPace.Aggressive
            ? "Flash24"
            : "Gauge24",
        QuizQuestionType.ExperienceLevel => BuildExperienceIcon(),
        QuizQuestionType.AvailableWeekdays => "CalendarWorkWeek24",
        QuizQuestionType.DietaryPreference => BuildDietIcon(),
        QuizQuestionType.SessionDuration => "Clock24",
        QuizQuestionType.Equipment => "Dumbbell24",
        QuizQuestionType.CurrentCyclePhase => BuildPhaseIcon(),
        _ when QuestionType.ToString().Contains("Pain", StringComparison.OrdinalIgnoreCase) => BuildPainIcon(),
        _ when QuestionType.ToString().Contains("Energy", StringComparison.OrdinalIgnoreCase) => BuildEnergyIcon(),
        _ => "CheckmarkCircle24"
    };

    /// <summary>Goal options are told apart by outcome, so each goal gets its own glyph.</summary>
    private string BuildGoalIcon()
    {
        return (UserGoal)Answer.MappedValue switch
        {
            UserGoal.FatLoss => "Fire24",
            UserGoal.MuscleTone => "Dumbbell24",
            UserGoal.Strength => "Target24",
            UserGoal.MaintainHealth => "HeartCircle24",
            _ => "Target24"
        };
    }

    /// <summary>Experience is a scale: the lower the mapped level, the lighter the glyph.</summary>
    private string BuildExperienceIcon()
    {
        return Answer.MappedValue switch
        {
            <= 1 => "LeafThree24",
            2 => "Dumbbell24",
            _ => "TargetArrow24"
        };
    }

    /// <summary>Dietary preference glyph per restriction tag.</summary>
    private string BuildDietIcon()
    {
        return (DietaryTag)Answer.MappedValue switch
        {
            DietaryTag.Vegetarian => "LeafThree24",
            DietaryTag.Vegan => "LeafOne24",
            DietaryTag.GlutenFree => "FoodGrains24",
            DietaryTag.LactoseFree => "Drop24",
            _ => "Food24"
        };
    }

    /// <summary>Cycle phase glyph per phase.</summary>
    private string BuildPhaseIcon()
    {
        return (CyclePhase)Answer.MappedValue switch
        {
            CyclePhase.Menstrual => "Drop24",
            CyclePhase.Follicular => "LeafThree24",
            CyclePhase.Ovulatory => "Fire24",
            CyclePhase.Luteal => "WeatherMoon24",
            _ => "HeartCircle24"
        };
    }

    /// <summary>Pain is a severity scale, from a checkmark up to a broken heart.</summary>
    private string BuildPainIcon()
    {
        return Answer.MappedValue switch
        {
            <= 1 => "CheckmarkCircle24",
            2 => "ShieldCheckmark24",
            3 => "BatteryWarning24",
            4 => "ShieldError24",
            _ => "HeartBroken24"
        };
    }

    /// <summary>Energy is a battery scale, filled in proportion to the answer.</summary>
    private string BuildEnergyIcon()
    {
        return Answer.MappedValue switch
        {
            <= 1 => "Battery024",
            2 => "Battery224",
            3 => "Battery524",
            4 => "Battery824",
            _ => "BatteryCharge24"
        };
    }
}

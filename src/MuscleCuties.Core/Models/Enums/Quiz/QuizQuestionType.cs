namespace MuscleCuties.Core.Models.Enums.Quiz;

/// <summary>How a question is answered, which decides the control and the validation.</summary>
public enum QuizQuestionType
{
    Goal = 0,
    ExperienceLevel = 1,
    WorkoutDaysPerWeek = 2,
    DietaryPreference = 3,
    MenstrualPain = 4,
    MenstrualEnergy = 5,
    FollicularPain = 6,
    FollicularEnergy = 7,
    OvulatoryPain = 8,
    OvulatoryEnergy = 9,
    LutealPain = 10,
    LutealEnergy = 11,
    CurrentCyclePhase = 13,
    SessionDuration = 14,
    Equipment = 15,
    GoalPace = 16,
    AvailableWeekdays = 17,
    RetiredLegacy = 1000
}

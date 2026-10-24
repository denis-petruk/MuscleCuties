namespace MuscleCuties.Core.Models.Enums.Quiz;

public static class QuizQuestionTypeRules
{
    public static bool IsAvailableInCurrentOnboarding(this QuizQuestionType questionType) =>
        questionType is not (QuizQuestionType.WorkoutDaysPerWeek or
            QuizQuestionType.RetiredLegacy);
}

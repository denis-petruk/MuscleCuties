namespace MuscleCuties.Core.Models.Enums.Quiz;

/// <summary>The rules that follow from a question type, so the client and server agree on what a valid answer is.</summary>
public static class QuizQuestionTypeRules
{
    /// <summary>True when the question type is one the current onboarding flow asks.</summary>
    public static bool IsAvailableInCurrentOnboarding(this QuizQuestionType questionType) =>
        questionType is not (QuizQuestionType.WorkoutDaysPerWeek or
            QuizQuestionType.RetiredLegacy);
}

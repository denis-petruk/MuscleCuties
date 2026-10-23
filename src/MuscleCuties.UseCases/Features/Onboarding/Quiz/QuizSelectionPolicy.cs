using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.UseCases.Features.Onboarding.Quiz;

public static class QuizSelectionPolicy
{
    public static string? Validate(
        IReadOnlyList<QuizSelectionDto> selections,
        IReadOnlyList<QuizQuestionDto> questions)
    {
        if (selections.Count == 0)
            return "Please complete the quiz to finish setting up your account.";
        if (questions.Count == 0 || questions.Any(question =>
                question.Id <= 0 || question.Answers is null || question.Answers.Count == 0) ||
            questions.GroupBy(question => question.Id).Any(group => group.Count() != 1))
            return "Onboarding questions are unavailable. Please try again.";

        var byId = questions.ToDictionary(question => question.Id);
        if (selections.GroupBy(selection => selection).Any(group => group.Count() != 1))
            return "Duplicate answers submitted.";

        foreach (var group in selections.GroupBy(selection => selection.QuestionId))
        {
            if (group.Key <= 0 || !byId.TryGetValue(group.Key, out var question) ||
                group.Any(selection => selection.AnswerId <= 0 ||
                                       question.Answers.All(answer => answer.Id != selection.AnswerId)))
                return "One or more selected answers are invalid.";

            if (question.QuestionType != QuizQuestionType.DietaryPreference && group.Count() > 1)
                return "Conflicting answers selected for a single-choice question.";

            if (question.QuestionType == QuizQuestionType.DietaryPreference && group.Count() > 1 &&
                question.Answers.Any(answer => answer.MappedValue == (int)DietaryTag.None &&
                                               group.Any(selection => selection.AnswerId == answer.Id)))
                return "Conflicting answers selected for dietary preferences.";
        }

        return null;
    }
}

using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Models.Entities.Quiz;

namespace MuscleCuties.Core.Services.Auth;

// Maps persisted and staged legacy shapes into detached validation inputs.
public static class OnboardingValidator
{
    public static OnboardingValidationResult ValidateStagedProfile(
        StagedProfileData? staged,
        string? username,
        DateTime now)
    {
        var observation = staged is null
            ? null
            : new StagedProfileObservation(
                staged.Name, staged.BirthDate, staged.Height, staged.Weight,
                staged.CycleLength, staged.WorkoutDaysPerWeek);
        return OnboardingValidationRule.ValidateStagedProfile(observation, username, now);
    }

    public static OnboardingValidationResult ValidateQuizResponses(
        IReadOnlyList<UserQuizResponse>? responses,
        IReadOnlyList<QuizQuestion>? questions)
    {
        var detachedResponses = responses?.Where(response => response is not null)
            .Select(response => new QuizResponseObservation(
                response.QuizQuestionId, response.QuizAnswerId))
            .ToArray();
        var detachedQuestions = questions?.Where(question => question is not null)
            .Select(question => new QuizQuestionObservation(question.Id,
                question.Question ?? string.Empty, question.QuestionType,
                question.Answers?.Where(answer => answer is not null)
                    .Select(answer => new QuizAnswerObservation(answer.Id, answer.MappedValue))
                    .ToArray() ?? []))
            .ToArray();
        return OnboardingValidationRule.ValidateQuizResponses(
            detachedResponses, detachedQuestions);
    }
}

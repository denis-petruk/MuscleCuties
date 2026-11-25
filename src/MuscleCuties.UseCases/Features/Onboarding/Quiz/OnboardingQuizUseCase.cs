using MuscleCuties.Core.Domain.Onboarding;

namespace MuscleCuties.UseCases.Features.Onboarding.Quiz;

/// <summary>Commits the quiz answers once and folds them into the profile.</summary>
public sealed class OnboardingQuizUseCase(IOnboardingQuizStore store) : IOnboardingQuizUseCase
{
    /// <summary>Loads the onboarding quiz questions.</summary>
    public Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(
        bool refresh = false, CancellationToken cancellationToken = default)
    {
        return store.LoadQuestionsAsync(refresh, cancellationToken);
    }

    /// <summary>Saves the quiz answers and finishes onboarding.</summary>
    public async Task<QuizCommitResult> CommitAsync(
        IReadOnlyList<QuizSelectionDto> selections, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(selections);

        var questions = await store.LoadQuestionsAsync(false, cancellationToken).ConfigureAwait(false);

        var responseObservations = selections
            .Where(selection => selection is not null)
            .Select(selection => new QuizResponseObservation(selection.QuestionId, selection.AnswerId))
            .ToArray();
        var questionObservations = questions
            .Where(question => question is not null)
            .Select(question => new QuizQuestionObservation(
                question.Id,
                question.Question,
                question.QuestionType,
                question.Answers?.Where(answer => answer is not null)
                    .Select(answer => new QuizAnswerObservation(answer.Id, answer.MappedValue))
                    .ToArray() ?? []))
            .ToArray();
        var validation = OnboardingValidationRule.ValidateQuizResponses(
            responseObservations, questionObservations);
        if (!validation.IsValid)
            return new QuizCommitResult(false, false, validation.ErrorMessage);

        return await store.CommitAsync(selections, cancellationToken).ConfigureAwait(false);
    }
}

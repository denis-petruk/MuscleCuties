using MuscleCuties.Core.Domain.Onboarding;

namespace MuscleCuties.UseCases.Features.Onboarding.Quiz;

public sealed class OnboardingQuizUseCase(IOnboardingQuizStore store) : IOnboardingQuizUseCase
{
    public Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(
        bool refresh = false, CancellationToken cancellationToken = default)
    {
        return store.LoadQuestionsAsync(refresh, cancellationToken);
    }

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

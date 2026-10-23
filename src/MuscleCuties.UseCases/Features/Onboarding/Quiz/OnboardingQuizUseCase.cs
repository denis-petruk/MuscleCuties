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
        if (selections.Count == 0)
            return new QuizCommitResult(false, false,
                "Please complete the quiz to finish setting up your account.");
        var questions = await store.LoadQuestionsAsync(false, cancellationToken).ConfigureAwait(false);
        var error = QuizSelectionPolicy.Validate(selections, questions);
        if (error is not null)
            return new QuizCommitResult(false, false, error);
        return await store.CommitAsync(selections, cancellationToken).ConfigureAwait(false);
    }
}

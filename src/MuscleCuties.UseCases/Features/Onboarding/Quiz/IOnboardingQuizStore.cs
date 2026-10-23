namespace MuscleCuties.UseCases.Features.Onboarding.Quiz;

public interface IOnboardingQuizStore
{
    Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(bool refresh, CancellationToken cancellationToken);

    Task<QuizCommitResult> CommitAsync(IReadOnlyList<QuizSelectionDto> selections,
        CancellationToken cancellationToken);
}

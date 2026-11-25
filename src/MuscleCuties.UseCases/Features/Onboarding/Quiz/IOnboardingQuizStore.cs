namespace MuscleCuties.UseCases.Features.Onboarding.Quiz;

/// <summary>The quiz persistence: questions, answers and the commit.</summary>
public interface IOnboardingQuizStore
{
    Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(bool refresh, CancellationToken cancellationToken);

    Task<QuizCommitResult> CommitAsync(IReadOnlyList<QuizSelectionDto> selections,
        CancellationToken cancellationToken);
}

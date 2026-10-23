using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.UseCases.Features.Onboarding.Quiz;

public sealed record QuizAnswerDto(int Id, string Text, int OrderIndex, int MappedValue);

public sealed record QuizQuestionDto(
    int Id,
    string Question,
    int OrderIndex,
    QuizQuestionType QuestionType,
    IReadOnlyList<QuizAnswerDto> Answers);

public sealed record QuizSelectionDto(int QuestionId, int AnswerId);

public sealed record QuizCommitResult(bool Succeeded, bool AccountCreatedNeedsLogin, string? ErrorMessage);

public interface IOnboardingQuizUseCase
{
    Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(bool refresh = false,
        CancellationToken cancellationToken = default);

    Task<QuizCommitResult> CommitAsync(IReadOnlyList<QuizSelectionDto> selections,
        CancellationToken cancellationToken = default);
}

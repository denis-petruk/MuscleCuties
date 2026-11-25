global using QuizSelectionDto = MuscleCuties.Contracts.Onboarding.QuizSelectionDto;
using MuscleCuties.Core.Models.Enums.Quiz;

namespace MuscleCuties.UseCases.Features.Onboarding.Quiz;

/// <summary>One answer option as the quiz shows it.</summary>
public sealed record QuizAnswerDto(int Id, string Text, int OrderIndex, int MappedValue);

/// <summary>One quiz question with its options.</summary>
public sealed record QuizQuestionDto(
    int Id,
    string Question,
    int OrderIndex,
    QuizQuestionType QuestionType,
    IReadOnlyList<QuizAnswerDto> Answers);


/// <summary>What committing the quiz stored.</summary>
public sealed record QuizCommitResult(bool Succeeded, bool AccountCreatedNeedsLogin, string? ErrorMessage);

/// <summary>The onboarding quiz.</summary>
public interface IOnboardingQuizUseCase
{
    Task<IReadOnlyList<QuizQuestionDto>> LoadQuestionsAsync(bool refresh = false,
        CancellationToken cancellationToken = default);

    Task<QuizCommitResult> CommitAsync(IReadOnlyList<QuizSelectionDto> selections,
        CancellationToken cancellationToken = default);
}

using MuscleCuties.UseCases.Features.Onboarding.Quiz;

namespace MuscleCuties.UseCases.Features.Account.Registration;

public sealed class GetRegistrationQuizUseCase(ICompleteAccountRegistrationStore store)
{
    public async Task<IReadOnlyList<QuizQuestionDto>> ExecuteAsync(
        CancellationToken cancellationToken = default)
    {
        var questions = await store.LoadQuizQuestionsAsync(cancellationToken).ConfigureAwait(false);
        if (questions.Count == 0)
            throw new InvalidOperationException("The registration quiz catalog is unavailable.");

        return questions.OrderBy(question => question.OrderIndex)
            .Select(question => new QuizQuestionDto(
                question.Id,
                question.Question,
                question.OrderIndex,
                question.QuestionType,
                question.Answers.OrderBy(answer => answer.OrderIndex)
                    .Select(answer => new QuizAnswerDto(answer.Id, answer.Text,
                        answer.OrderIndex, answer.MappedValue))
                    .ToArray()))
            .ToArray();
    }
}

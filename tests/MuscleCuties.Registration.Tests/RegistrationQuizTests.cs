using MuscleCuties.Core.Models.Enums.Quiz;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.Registration.Tests;

public sealed class RegistrationQuizTests
{
    [Xunit.Fact]
    public async Task CanonicalCatalogPreservesQuestionAndAnswerMetadata()
    {
        var store = new FakeRegistrationStore([
            new AccountRegistrationQuizQuestion(42, "Second question", 2,
                QuizQuestionType.ExperienceLevel,
                [new AccountRegistrationQuizAnswer(9, "Later answer", 2, 3),
                    new AccountRegistrationQuizAnswer(8, "Earlier answer", 1, 2)]),
            new AccountRegistrationQuizQuestion(41, "First question", 1,
                QuizQuestionType.Goal,
                [new AccountRegistrationQuizAnswer(7, "Goal answer", 1, 1)])
        ]);

        var result = await new GetRegistrationQuizUseCase(store).ExecuteAsync();

        Xunit.Assert.Equal([41, 42], result.Select(question => question.Id));
        Xunit.Assert.Equal("First question", result[0].Question);
        Xunit.Assert.Equal([8, 9], result[1].Answers.Select(answer => answer.Id));
        Xunit.Assert.Equal("Earlier answer", result[1].Answers[0].Text);
        Xunit.Assert.Equal(2, result[1].Answers[0].MappedValue);
    }

    [Xunit.Fact]
    public async Task MissingCatalogFailsInsteadOfShowingInventedQuestions()
    {
        var store = new FakeRegistrationStore([]);

        await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GetRegistrationQuizUseCase(store).ExecuteAsync());
    }

    private sealed class FakeRegistrationStore(
        IReadOnlyList<AccountRegistrationQuizQuestion> questions) : ICompleteAccountRegistrationStore
    {
        public Task<IReadOnlyList<AccountRegistrationQuizQuestion>> LoadQuizQuestionsAsync(
            CancellationToken cancellationToken) => Task.FromResult(questions);

        public Task<RegistrationPrivacyConsent?> GetPendingPrivacyConsentAsync(
            string emailReceiptId, string challengeId, string normalizedEmail,
            CancellationToken cancellationToken) => throw new NotSupportedException();

        public Task<CompleteAccountRegistrationResult> CreateAccountAsync(
            CompleteAccountRegistrationRequest request, string normalizedEmail,
            string normalizedUsername, string passwordHash, string emailReceiptId,
            DateTimeOffset emailReceiptExpiresAt, RegistrationPrivacyConsent privacyConsent,
            CancellationToken cancellationToken) => throw new NotSupportedException();
    }
}

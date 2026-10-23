using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Registration;

public sealed record AccountRegistrationQuizQuestion(
    int Id,
    string Question,
    int OrderIndex,
    MuscleCuties.Core.Models.Enums.Quiz.QuizQuestionType QuestionType,
    IReadOnlyList<AccountRegistrationQuizAnswer> Answers);

public sealed record AccountRegistrationQuizAnswer(int Id, string Text, int OrderIndex, int MappedValue);

public sealed record RegistrationEmailReceipt(string Id, DateTimeOffset ExpiresAt);

public interface IRegistrationEmailReceiptVerifier
{
    bool TryVerify(string token, string email, string challengeId, out RegistrationEmailReceipt? receipt);
}

public interface IRegistrationPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string encodedHash);
}

public interface ICompleteAccountRegistrationStore
{
    Task<IReadOnlyList<AccountRegistrationQuizQuestion>> LoadQuizQuestionsAsync(
        CancellationToken cancellationToken);

    Task<RegistrationPrivacyConsent?> GetPendingPrivacyConsentAsync(
        string emailReceiptId, string challengeId, string normalizedEmail, CancellationToken cancellationToken);

    Task<CompleteAccountRegistrationResult> CreateAccountAsync(
        CompleteAccountRegistrationRequest request,
        string normalizedEmail,
        string normalizedUsername,
        string passwordHash,
        string emailReceiptId,
        DateTimeOffset emailReceiptExpiresAt,
        RegistrationPrivacyConsent privacyConsent,
        CancellationToken cancellationToken);
}

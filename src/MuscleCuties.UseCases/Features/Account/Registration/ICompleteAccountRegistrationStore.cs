using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Registration;

/// <summary>A quiz question carried into registration, with its options.</summary>
public sealed record AccountRegistrationQuizQuestion(
    int Id,
    string Question,
    int OrderIndex,
    MuscleCuties.Core.Models.Enums.Quiz.QuizQuestionType QuestionType,
    IReadOnlyList<AccountRegistrationQuizAnswer> Answers);

/// <summary>One option of that question, with the profile value it maps to.</summary>
public sealed record AccountRegistrationQuizAnswer(int Id, string Text, int OrderIndex, int MappedValue);

/// <summary>Proof that the provider delivered the registration email.</summary>
public sealed record RegistrationEmailReceipt(string Id, DateTimeOffset ExpiresAt);

/// <summary>Verifies such a receipt, so registration cannot complete without delivery.</summary>
public interface IRegistrationEmailReceiptVerifier
{
    bool TryVerify(string token, string email, string challengeId, out RegistrationEmailReceipt? receipt);
}

/// <summary>Hashes the password while the registration is pending.</summary>
public interface IRegistrationPasswordHasher
{
    string HashPassword(string password);
    bool VerifyPassword(string password, string encodedHash);
}

/// <summary>The single write that creates the account and everything it starts with.</summary>
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

using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Account.Registration;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using Xunit;

namespace MuscleCuties.Registration.Tests;

/// <summary>G-09: the server enforces the same strength-style requirement as saved preferences.</summary>
public sealed class RegistrationStrengthStyleRequirementTests
{
    [Fact]
    public async Task Registration_without_a_strength_style_is_rejected_before_any_account_write()
    {
        var store = new Store();
        var result = await UseCase(store).ExecuteAsync(Request(
            [(int)WorkoutActivityType.Walking, (int)WorkoutActivityType.Yoga]));

        Assert.Equal(CompleteAccountRegistrationStatus.InvalidProfile, result.Status);
        Assert.False(store.CreateCalled);
    }

    [Fact]
    public async Task Registration_with_a_strength_style_passes_the_activity_check()
    {
        var store = new Store();
        var result = await UseCase(store).ExecuteAsync(Request(
            [(int)WorkoutActivityType.GeneralStrength, (int)WorkoutActivityType.Walking]));

        // The synthetic quiz is empty, so validation continues past the activity check to the quiz.
        Assert.NotEqual(CompleteAccountRegistrationStatus.InvalidProfile, result.Status);
    }

    private static CompleteAccountRegistrationUseCase UseCase(Store store) =>
        new(store, new Verifier(), new Hasher(), TimeProvider.System);

    private static CompleteAccountRegistrationRequest Request(IReadOnlyList<int> activities) => new(
        "tester@example.invalid", "tester_one", "Synthetic-Passw0rd!", "token", "challenge",
        new ProfileSetupDraft("Tester", new DateTime(1995, 1, 1), 170f, 64f, string.Empty, 28, activities, true),
        [],
        PrivacyConsent: new RegistrationPrivacyConsent(PrivacyNotice.CurrentVersion, true, DateTimeOffset.UtcNow));

    private sealed class Verifier : IRegistrationEmailReceiptVerifier
    {
        public bool TryVerify(string token, string email, string challengeId, out RegistrationEmailReceipt? receipt)
        {
            receipt = new RegistrationEmailReceipt("receipt", DateTimeOffset.UtcNow.AddMinutes(10));
            return true;
        }
    }

    private sealed class Hasher : IRegistrationPasswordHasher
    {
        public string HashPassword(string password) => "synthetic-hash";
        public bool VerifyPassword(string password, string encodedHash) => false;
    }

    private sealed class Store : ICompleteAccountRegistrationStore
    {
        public bool CreateCalled { get; private set; }

        public Task<IReadOnlyList<AccountRegistrationQuizQuestion>> LoadQuizQuestionsAsync(
            CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<AccountRegistrationQuizQuestion>>([]);

        public Task<RegistrationPrivacyConsent?> GetPendingPrivacyConsentAsync(string emailReceiptId,
            string challengeId, string normalizedEmail, CancellationToken cancellationToken) =>
            Task.FromResult<RegistrationPrivacyConsent?>(
                new RegistrationPrivacyConsent(PrivacyNotice.CurrentVersion, true, DateTimeOffset.UtcNow));

        public Task<CompleteAccountRegistrationResult> CreateAccountAsync(CompleteAccountRegistrationRequest request,
            string normalizedEmail, string normalizedUsername, string passwordHash, string emailReceiptId,
            DateTimeOffset emailReceiptExpiresAt, RegistrationPrivacyConsent privacyConsent,
            CancellationToken cancellationToken)
        {
            CreateCalled = true;
            return Task.FromResult(new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.Created));
        }
    }
}

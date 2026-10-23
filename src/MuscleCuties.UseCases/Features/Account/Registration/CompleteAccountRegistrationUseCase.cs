using MuscleCuties.Core.Domain.Auth;
using MuscleCuties.Core.Domain.Onboarding;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;

namespace MuscleCuties.UseCases.Features.Account.Registration;

public sealed class CompleteAccountRegistrationUseCase(
    ICompleteAccountRegistrationStore store,
    IRegistrationEmailReceiptVerifier emailReceiptVerifier,
    IRegistrationPasswordHasher passwordHasher,
    TimeProvider timeProvider) : ICompleteAccountRegistrationUseCase
{
    public async Task<CompleteAccountRegistrationResult> ExecuteAsync(
        CompleteAccountRegistrationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var email = request.Email?.Trim() ?? string.Empty;
        if (!AuthInputValidator.IsValidEmail(email))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidEmail);

        var username = request.Username?.Trim() ?? string.Empty;
        if (!AuthInputValidator.IsValidUsername(username))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidUsername);

        if (!AuthInputValidator.IsStrongPassword(request.Password))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidPassword);

        if (!emailReceiptVerifier.TryVerify(
                request.EmailVerificationToken,
                email,
                request.EmailChallengeId,
                out var verifiedReceipt) || verifiedReceipt is null ||
            verifiedReceipt.ExpiresAt <= timeProvider.GetUtcNow())
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidEmailVerification);

        var normalizedEmail = AuthInputValidator.NormalizeEmail(email);
        var pendingConsent = await store.GetPendingPrivacyConsentAsync(
            verifiedReceipt.Id, request.EmailChallengeId, normalizedEmail, cancellationToken).ConfigureAwait(false);
        if (!IsValidPrivacyConsent(request.PrivacyConsent) || pendingConsent is null ||
            request.PrivacyConsent!.NoticeVersion != pendingConsent.NoticeVersion)
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.PrivacyConsentRequired);

        var profile = request.Profile;
        if (profile is null || profile.SelectedActivityTypes is null ||
            profile.SelectedActivityTypes.Count == 0 ||
            profile.SelectedActivityTypes.Any(activity => !Enum.IsDefined((WorkoutActivityType)activity)))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidProfile);
        if (request.QuizResponses is null || request.QuizResponses.Any(selection => selection is null))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidQuiz);
        // Plan activation requires a separate, reviewed proposal and user confirmation.
        if (request.InitialWorkoutPlanDays is not null ||
            !string.IsNullOrWhiteSpace(request.InitialWorkoutPlanName))
            return new CompleteAccountRegistrationResult(CompleteAccountRegistrationStatus.InvalidWorkoutPlan);

        var profileValidation = profile is null
            ? OnboardingValidationResult.Failure("Profile information is required.")
            : OnboardingValidationRule.ValidateStagedProfile(
                new StagedProfileObservation(
                    profile.Name,
                    profile.BirthDate,
                    profile.HeightCm,
                    profile.WeightKg,
                    profile.CycleLength,
                    profile.WorkoutDaysPerWeek),
                username,
                timeProvider.GetUtcNow().UtcDateTime);
        if (!profileValidation.IsValid)
            return new CompleteAccountRegistrationResult(
                CompleteAccountRegistrationStatus.InvalidProfile,
                ErrorMessage: profileValidation.ErrorMessage);

        var questions = await store.LoadQuizQuestionsAsync(cancellationToken).ConfigureAwait(false);
        var quizValidation = OnboardingValidationRule.ValidateQuizResponses(
            request.QuizResponses?.Select(selection =>
                new QuizResponseObservation(selection.QuestionId, selection.AnswerId)).ToArray(),
            questions.Select(question => new QuizQuestionObservation(
                question.Id,
                question.Question,
                question.QuestionType,
                question.Answers.Select(answer => new QuizAnswerObservation(answer.Id, answer.MappedValue)).ToArray()))
                .ToArray());
        if (!quizValidation.IsValid)
            return new CompleteAccountRegistrationResult(
                CompleteAccountRegistrationStatus.InvalidQuiz,
                ErrorMessage: quizValidation.ErrorMessage);

        cancellationToken.ThrowIfCancellationRequested();
        var passwordHash = passwordHasher.HashPassword(request.Password);
        return await store.CreateAccountAsync(
            request,
            normalizedEmail,
            username.ToLowerInvariant(),
            passwordHash,
            verifiedReceipt.Id,
            verifiedReceipt.ExpiresAt,
            pendingConsent,
            cancellationToken).ConfigureAwait(false);
    }

    private static bool IsValidPrivacyConsent(RegistrationPrivacyConsent? consent) =>
        consent is { Accepted: true } && consent.NoticeVersion == PrivacyNotice.CurrentVersion;
}

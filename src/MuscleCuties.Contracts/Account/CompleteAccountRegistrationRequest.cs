using MuscleCuties.Contracts.Onboarding;

namespace MuscleCuties.Contracts.Account;

public sealed record CompleteAccountRegistrationRequest(
    string Email,
    string Username,
    string Password,
    string EmailVerificationToken,
    string EmailChallengeId,
    ProfileSetupDraft Profile,
    IReadOnlyList<QuizSelectionDto> QuizResponses,
    string? InitialWorkoutPlanName = null,
    IReadOnlyList<InitialWorkoutPlanDayInput>? InitialWorkoutPlanDays = null,
    RegistrationPrivacyConsent? PrivacyConsent = null);

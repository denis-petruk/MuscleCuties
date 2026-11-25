using MuscleCuties.Contracts.Onboarding;

namespace MuscleCuties.Contracts.Account;

/// <summary>
///     Everything the completion step needs in one call: the credentials again, the verified email
///     receipt (token plus the challenge it answers), the profile setup draft, the quiz answers, and the
///     optional first plan. The plan is optional because a device may register before generating one.
/// </summary>
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

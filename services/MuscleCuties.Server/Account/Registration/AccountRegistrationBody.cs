using MuscleCuties.Contracts.Account;
using MuscleCuties.Contracts.Onboarding;
using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;

namespace MuscleCuties.Server.Account.Registration;

// Keep the mobile wire names at the HTTP boundary; application drafts retain their existing contract.
/// <summary>The body the native app posts to create an account: the credentials plus the profile it is starting with.</summary>
internal sealed record AccountRegistrationBody(
    string Email,
    string Username,
    string Password,
    string EmailVerificationToken,
    string EmailChallengeId,
    AccountRegistrationProfileBody? Profile,
    IReadOnlyList<QuizSelectionDto> QuizResponses,
    string? InitialWorkoutPlanName = null,
    IReadOnlyList<InitialWorkoutPlanDayInput>? InitialWorkoutPlanDays = null,
    RegistrationPrivacyConsent? PrivacyConsent = null)
{
    /// <summary>Translates the wire body into the use case's account-registration command.</summary>
    public CompleteAccountRegistrationRequest ToInput() => new(
        Email, Username, Password, EmailVerificationToken, EmailChallengeId,
        Profile?.ToInput()!, QuizResponses, InitialWorkoutPlanName, InitialWorkoutPlanDays, PrivacyConsent);
}

/// <summary>The body that finishes onboarding, carrying the answers the quiz collected so the profile can be completed in</summary>
/// one call.</summary>
internal sealed record CompleteAccountOnboardingBody(
    AccountRegistrationProfileBody? Profile,
    IReadOnlyList<QuizSelectionDto> QuizResponses)
{
    /// <summary>Translates the onboarding body into the use case's onboarding command.</summary>
    public CompleteAccountOnboardingRequest ToInput() => new(
        Profile?.ToInput()!,
        QuizResponses);
}

/// <summary>The profile fields a new account may be created with.</summary>
internal sealed record AccountRegistrationProfileBody(
    string Name,
    DateTime BirthDate,
    float HeightCm,
    float WeightKg,
    IReadOnlyList<int> SelectedActivityTypes,
    bool UseMetricSystem,
    int? CycleLengthDays = null,
    int? CycleLength = null,
    string? ProfileImagePath = null,
    bool UpdateActivityPreferences = true)
{
    /// <summary>True when the client sent two different cycle lengths, which the endpoint rejects instead of picking one.</summary>
    public bool HasConflictingCycleLengths =>
        CycleLengthDays.HasValue && CycleLength.HasValue && CycleLengthDays != CycleLength;

    /// <summary>Translates the profile fields into the setup draft, treating an absent cycle length as unset.</summary>
    public ProfileSetupDraft ToInput() => new(
        Name, BirthDate, HeightCm, WeightKg, ProfileImagePath ?? string.Empty,
        CycleLengthDays ?? CycleLength ?? default, SelectedActivityTypes, UseMetricSystem, UpdateActivityPreferences);
}

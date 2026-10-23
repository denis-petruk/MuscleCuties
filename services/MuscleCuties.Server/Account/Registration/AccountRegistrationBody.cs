using MuscleCuties.UseCases.Features.Account.Registration.Contracts;
using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;

namespace MuscleCuties.Server.Account.Registration;

// Keep the mobile wire names at the HTTP boundary; application drafts retain their existing contract.
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
    public CompleteAccountRegistrationRequest ToInput() => new(
        Email, Username, Password, EmailVerificationToken, EmailChallengeId,
        Profile?.ToInput()!, QuizResponses, InitialWorkoutPlanName, InitialWorkoutPlanDays, PrivacyConsent);
}

internal sealed record AccountRegistrationProfileBody(
    string Name,
    DateTime BirthDate,
    float HeightCm,
    float WeightKg,
    int WorkoutDaysPerWeek,
    IReadOnlyList<int> SelectedActivityTypes,
    bool UseMetricSystem,
    int? CycleLengthDays = null,
    int? CycleLength = null,
    string? ProfileImagePath = null,
    bool UpdateActivityPreferences = true)
{
    public bool HasConflictingCycleLengths =>
        CycleLengthDays.HasValue && CycleLength.HasValue && CycleLengthDays != CycleLength;

    public ProfileSetupDraft ToInput() => new(
        Name, BirthDate, HeightCm, WeightKg, ProfileImagePath ?? string.Empty, WorkoutDaysPerWeek,
        CycleLengthDays ?? CycleLength ?? default, SelectedActivityTypes, UseMetricSystem, UpdateActivityPreferences);
}

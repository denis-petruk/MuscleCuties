using MuscleCuties.UseCases.Features.Onboarding.ProfileSetup;
using MuscleCuties.UseCases.Features.Onboarding.Quiz;

namespace MuscleCuties.UseCases.Features.Account.Registration.Contracts;

public enum WorkoutType
{
    Strength,
    Cardio,
    Recovery,
    Rest
}

public sealed record InitialWorkoutPlanExerciseInput(int ExerciseId, int Sets, int Reps);

public sealed record InitialWorkoutPlanDayInput(
    int WeekIndex,
    int DayOfWeek,
    WorkoutType WorkoutType,
    string Name,
    IReadOnlyList<InitialWorkoutPlanExerciseInput> Exercises);

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

public enum CompleteAccountRegistrationStatus
{
    Created,
    InvalidEmail,
    InvalidUsername,
    InvalidPassword,
    InvalidEmailVerification,
    InvalidProfile,
    InvalidQuiz,
    InvalidWorkoutPlan,
    EmailAlreadyRegistered,
    UsernameTaken,
    EmailReceiptAlreadyUsed,
    PrivacyConsentRequired,
    Failed
}

public sealed record CompleteAccountRegistrationResult(
    CompleteAccountRegistrationStatus Status,
    Guid? UserId = null,
    string? ErrorMessage = null)
{
    public bool Succeeded => Status == CompleteAccountRegistrationStatus.Created;
}

public interface ICompleteAccountRegistrationUseCase
{
    Task<CompleteAccountRegistrationResult> ExecuteAsync(
        CompleteAccountRegistrationRequest request,
        CancellationToken cancellationToken = default);
}

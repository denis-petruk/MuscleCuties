namespace MuscleCuties.Contracts.Account;

public static class PrivacyNotice
{
    public const string CurrentVersion = "privacy-notice-2026-10-17";
}

public sealed record RegistrationPrivacyConsent(
    string NoticeVersion,
    bool Accepted,
    DateTimeOffset AcceptedAtUtc);

public enum AccountRegistrationStatus
{
    Started,
    ExistingEmail,
    InvalidEmail,
    InvalidUsername,
    UsernameTaken,
    InvalidPassword,
    ChallengeFailed,
    PrivacyConsentRequired,
    ReplacedOrCancelled
}

public sealed record StartAccountRegistrationRequest(
    string Email,
    string Username,
    string Password,
    string ConfirmPassword,
    RegistrationPrivacyConsent? PrivacyConsent = null);

public sealed record StartAccountRegistrationResult(
    AccountRegistrationStatus Status,
    string? ErrorMessage = null,
    string? ExistingEmail = null,
    Guid? AttemptId = null);

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

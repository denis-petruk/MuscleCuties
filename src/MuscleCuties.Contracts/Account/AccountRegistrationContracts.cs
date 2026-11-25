namespace MuscleCuties.Contracts.Account;

/// <summary>Identifies the privacy notice version a registration consented to.</summary>
public static class PrivacyNotice
{
    public const string CurrentVersion = "privacy-notice-2026-10-17";
}

/// <summary>
///     The privacy consent captured with a registration: which notice version, whether it was accepted,
///     and when. The timestamp is the evidence the account was created under that version.
/// </summary>
public sealed record RegistrationPrivacyConsent(
    string NoticeVersion,
    bool Accepted,
    DateTimeOffset AcceptedAtUtc);

/// <summary>
///     Outcome of the first registration step, which claims the email and username and sends the code.
///     Everything except <see cref="Started" /> means nothing was reserved.
/// </summary>
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

/// <summary>
///     The first registration step: credentials plus the optional consent record. The confirmation
///     password is checked here and never reaches the account record.
/// </summary>
public sealed record StartAccountRegistrationRequest(
    string Email,
    string Username,
    string Password,
    string ConfirmPassword,
    RegistrationPrivacyConsent? PrivacyConsent = null);

/// <summary>
///     First-step result. <see cref="AttemptId" /> is the handle the completion step redeems, so a
///     successful start yields one and a rejected one does not.
/// </summary>
public sealed record StartAccountRegistrationResult(
    AccountRegistrationStatus Status,
    string? ErrorMessage = null,
    string? ExistingEmail = null,
    Guid? AttemptId = null);

/// <summary>The session shapes a first generated plan can place on a day.</summary>
public enum WorkoutType
{
    Strength,
    Cardio,
    Recovery,
    Rest
}

/// <summary>One prescribed exercise inside a day of the first generated plan.</summary>
public sealed record InitialWorkoutPlanExerciseInput(int ExerciseId, int Sets, int Reps);

/// <summary>
///     One day of the first generated plan handed in with registration: the week it belongs to, the
///     weekday, its session type, and the exercises prescribed for it.
/// </summary>
public sealed record InitialWorkoutPlanDayInput(
    int WeekIndex,
    int DayOfWeek,
    WorkoutType WorkoutType,
    string Name,
    IReadOnlyList<InitialWorkoutPlanExerciseInput> Exercises);

/// <summary>
///     Outcome of the completion step, which redeems the email challenge and writes the account.
///     Only <see cref="Created" /> leaves a user behind.
/// </summary>
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

/// <summary>Completion result, carrying the new user id only when the account was created.</summary>
public sealed record CompleteAccountRegistrationResult(
    CompleteAccountRegistrationStatus Status,
    Guid? UserId = null,
    string? ErrorMessage = null)
{
    /// <summary>True only for <see cref="CompleteAccountRegistrationStatus.Created" />.</summary>
    public bool Succeeded => Status == CompleteAccountRegistrationStatus.Created;
}

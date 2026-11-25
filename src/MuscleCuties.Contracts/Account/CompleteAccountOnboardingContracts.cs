using MuscleCuties.Contracts.Onboarding;

namespace MuscleCuties.Contracts.Account;

/// <summary>
///     Profile setup and quiz answers submitted together to finish onboarding for an account that
///     skipped them at registration.
/// </summary>
public sealed record CompleteAccountOnboardingRequest(
    ProfileSetupDraft Profile,
    IReadOnlyList<QuizSelectionDto> QuizResponses);

/// <summary>
///     Onboarding completion outcome. <see cref="AlreadyCompleted" /> is not an error: the answers were
///     ignored because the account had already finished onboarding.
/// </summary>
public enum CompleteAccountOnboardingStatus
{
    Completed,
    InvalidProfile,
    InvalidQuiz,
    AccountNotFound,
    AlreadyCompleted,
    Failed
}

/// <summary>Onboarding completion result, carrying the user id only when the flow completed.</summary>
public sealed record CompleteAccountOnboardingResult(
    CompleteAccountOnboardingStatus Status,
    Guid? UserId = null,
    string? ErrorMessage = null)
{
    /// <summary>True only for <see cref="CompleteAccountOnboardingStatus.Completed" />.</summary>
    public bool Succeeded => Status == CompleteAccountOnboardingStatus.Completed;
}

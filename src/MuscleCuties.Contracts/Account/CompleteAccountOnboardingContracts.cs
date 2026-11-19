using MuscleCuties.Contracts.Onboarding;

namespace MuscleCuties.Contracts.Account;

public sealed record CompleteAccountOnboardingRequest(
    ProfileSetupDraft Profile,
    IReadOnlyList<QuizSelectionDto> QuizResponses);

public enum CompleteAccountOnboardingStatus
{
    Completed,
    InvalidProfile,
    InvalidQuiz,
    AccountNotFound,
    AlreadyCompleted,
    Failed
}

public sealed record CompleteAccountOnboardingResult(
    CompleteAccountOnboardingStatus Status,
    Guid? UserId = null,
    string? ErrorMessage = null)
{
    public bool Succeeded => Status == CompleteAccountOnboardingStatus.Completed;
}

using MuscleCuties.Core.Models.Entities.Quiz;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Auth;

public sealed record OnboardingCommitResult(
    bool Succeeded,
    User? User,
    string? ErrorMessage = null);

public interface IOnboardingCommitService
{
    Task<OnboardingCommitResult> CommitNewUserAsync(
        IReadOnlyList<UserQuizResponse> quizResponses,
        CancellationToken cancellationToken = default);

    Task<OnboardingCommitResult> CommitExistingUserOnboardingAsync(
        int userId,
        StagedProfileData? stagedProfile,
        IReadOnlyList<UserQuizResponse> quizResponses,
        CancellationToken cancellationToken = default);
}

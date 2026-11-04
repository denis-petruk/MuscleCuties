using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Auth;

public sealed record PendingRegistrationData(
    string Email,
    string Username,
    string? Password,
    UserCredentialType CredentialType,
    string? AppleUserId = null,
    string? ChallengeId = null,
    string? VerificationToken = null,
    bool IsVerified = false,
    string? PrivacyNoticeVersion = null,
    DateTimeOffset? PrivacyConsentAcceptedAtUtc = null);

public sealed record StagedProfileData(
    string Name,
    DateTime BirthDate,
    float Height,
    float Weight,
    string ProfileImagePath,
    int CycleLength,
    IReadOnlySet<WorkoutActivityType> SelectedActivities,
    bool UseMetricSystem = true);

public interface IPendingRegistrationService
{
    bool HasPendingRegistration { get; }
    bool IsVerified { get; }
    PendingRegistrationData? CurrentRegistration { get; }
    StagedProfileData? StagedProfile { get; }

    PendingRegistrationData StartEmailRegistration(string email, string username, string password, string? challengeId,
        string? privacyNoticeVersion = null, DateTimeOffset? privacyConsentAcceptedAtUtc = null);
    void StartAppleRegistration(string appleUserId, string email, string username);
    void UpdateChallengeId(string challengeId);
    void ConfirmChallenge(string challengeId, string verificationToken);

    bool TryUpdateChallengeId(PendingRegistrationData expectedRegistration, string challengeId,
        out PendingRegistrationData? updatedRegistration);

    bool TryConfirmChallenge(PendingRegistrationData expectedRegistration, string verificationToken);
    bool TryClear(PendingRegistrationData expectedRegistration);
    void StageProfile(StagedProfileData profile);
    bool TryStageProfile(PendingRegistrationData expectedRegistration, StagedProfileData profile);
    void Clear();
}

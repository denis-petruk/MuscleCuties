using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Auth;

/// <summary>A registration that is waiting for its code: the details typed and the challenge it must answer.</summary>
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

/// <summary>The profile answers held while the code is being confirmed, so nothing is saved before the account exists.</summary>
public sealed record StagedProfileData(
    string Name,
    DateTime BirthDate,
    float Height,
    float Weight,
    string ProfileImagePath,
    int CycleLength,
    IReadOnlySet<WorkoutActivityType> SelectedActivities,
    bool UseMetricSystem = true);

/// <summary>Holds a registration between the form and the code that confirms it.</summary>
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

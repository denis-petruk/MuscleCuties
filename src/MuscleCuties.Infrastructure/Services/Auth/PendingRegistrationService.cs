using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Auth;

public sealed class PendingRegistrationService : IPendingRegistrationService
{
    private readonly object _syncRoot = new();
    private PendingRegistrationData? _currentRegistration;
    private StagedProfileData? _stagedProfile;

    public bool HasPendingRegistration
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentRegistration is not null;
            }
        }
    }

    public bool IsVerified
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentRegistration?.IsVerified == true;
            }
        }
    }

    public PendingRegistrationData? CurrentRegistration
    {
        get
        {
            lock (_syncRoot)
            {
                return _currentRegistration;
            }
        }
    }

    public StagedProfileData? StagedProfile
    {
        get
        {
            lock (_syncRoot)
            {
                return _stagedProfile;
            }
        }
    }

    public PendingRegistrationData StartEmailRegistration(string email, string username, string password,
        string? challengeId, string? privacyNoticeVersion = null, DateTimeOffset? privacyConsentAcceptedAtUtc = null)
    {
        lock (_syncRoot)
        {
            _stagedProfile = null;
            _currentRegistration = new PendingRegistrationData(
                email.Trim().ToLowerInvariant(),
                username.Trim(),
                password,
                UserCredentialType.Password,
                null,
                challengeId,
                null,
                false);
            if (privacyNoticeVersion is not null && privacyConsentAcceptedAtUtc is not null)
            {
                _currentRegistration = _currentRegistration with
                {
                    PrivacyNoticeVersion = privacyNoticeVersion,
                    PrivacyConsentAcceptedAtUtc = privacyConsentAcceptedAtUtc
                };
            }
            return _currentRegistration;
        }
    }

    public void StartAppleRegistration(string appleUserId, string email, string username)
    {
        lock (_syncRoot)
        {
            _stagedProfile = null;
            _currentRegistration = new PendingRegistrationData(
                email.Trim().ToLowerInvariant(),
                username.Trim(),
                null,
                UserCredentialType.Apple,
                appleUserId.Trim(),
                null,
                null,
                true); // Native Apple authorization already verifies identity
        }
    }

    public void UpdateChallengeId(string challengeId)
    {
        if (string.IsNullOrWhiteSpace(challengeId))
            throw new ArgumentException("A challenge session is required.", nameof(challengeId));

        lock (_syncRoot)
        {
            if (_currentRegistration is null || _currentRegistration.CredentialType != UserCredentialType.Password)
                throw new InvalidOperationException("No email registration is awaiting a challenge.");

            _currentRegistration = _currentRegistration with
            {
                ChallengeId = challengeId,
                VerificationToken = null,
                IsVerified = false
            };
        }
    }

    public void ConfirmChallenge(string challengeId, string verificationToken)
    {
        lock (_syncRoot)
        {
            if (_currentRegistration is null)
                throw new InvalidOperationException("No pending registration found to confirm.");

            if (_currentRegistration.CredentialType != UserCredentialType.Password ||
                string.IsNullOrWhiteSpace(challengeId) ||
                string.IsNullOrWhiteSpace(verificationToken) ||
                string.IsNullOrWhiteSpace(_currentRegistration.ChallengeId) ||
                !string.Equals(_currentRegistration.ChallengeId, challengeId, StringComparison.Ordinal))
                throw new InvalidOperationException(
                    "A valid email challenge session and verification token are required.");

            _currentRegistration = _currentRegistration with
            {
                ChallengeId = challengeId,
                VerificationToken = verificationToken,
                IsVerified = true
            };
        }
    }

    public void StageProfile(StagedProfileData profile)
    {
        lock (_syncRoot)
        {
            _stagedProfile = profile;
        }
    }

    public bool TryStageProfile(PendingRegistrationData expectedRegistration, StagedProfileData profile)
    {
        lock (_syncRoot)
        {
            if (expectedRegistration is null || !ReferenceEquals(_currentRegistration, expectedRegistration))
                return false;

            _stagedProfile = profile;
            return true;
        }
    }

    // Compare the immutable snapshot under the same lock as the mutation. Even a
    // replacement with identical email/challenge values is a different session.
    public bool TryUpdateChallengeId(PendingRegistrationData expectedRegistration, string challengeId,
        out PendingRegistrationData? updatedRegistration)
    {
        lock (_syncRoot)
        {
            updatedRegistration = null;
            if (expectedRegistration is null || !ReferenceEquals(_currentRegistration, expectedRegistration) ||
                expectedRegistration.CredentialType != UserCredentialType.Password ||
                string.IsNullOrWhiteSpace(challengeId) ||
                string.Equals(expectedRegistration.ChallengeId, challengeId, StringComparison.Ordinal))
                return false;

            UpdateChallengeId(challengeId);
            updatedRegistration = _currentRegistration;
            return true;
        }
    }

    public bool TryConfirmChallenge(PendingRegistrationData expectedRegistration, string verificationToken)
    {
        lock (_syncRoot)
        {
            if (expectedRegistration is null || !ReferenceEquals(_currentRegistration, expectedRegistration) ||
                expectedRegistration.CredentialType != UserCredentialType.Password ||
                string.IsNullOrWhiteSpace(expectedRegistration.ChallengeId) ||
                string.IsNullOrWhiteSpace(verificationToken))
                return false;

            ConfirmChallenge(expectedRegistration.ChallengeId, verificationToken);
            return true;
        }
    }

    public bool TryClear(PendingRegistrationData expectedRegistration)
    {
        lock (_syncRoot)
        {
            if (expectedRegistration is null || !ReferenceEquals(_currentRegistration, expectedRegistration))
                return false;

            Clear();
            return true;
        }
    }

    public void Clear()
    {
        lock (_syncRoot)
        {
            _currentRegistration = null;
            _stagedProfile = null;
        }
    }
}

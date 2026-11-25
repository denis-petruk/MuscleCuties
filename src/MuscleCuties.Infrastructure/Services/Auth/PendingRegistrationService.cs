using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Auth;

/// <summary>Keeps one registration in progress: the details, the challenge and the code step's outcome.</summary>
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

    /// <summary>Starts an email registration and holds it until the code is verified.</summary>
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

    /// <summary>Begins a registration from an Apple identity, where there is no password to stage.</summary>
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

    /// <summary>Records the new challenge after a resend, so the old one can no longer confirm the registration.</summary>
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

    /// <summary>Marks a challenge confirmed, which is what allows the account to be created.</summary>
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

    /// <summary>Holds the profile answers until the account exists.</summary>
    public void StageProfile(StagedProfileData profile)
    {
        lock (_syncRoot)
        {
            _stagedProfile = profile;
        }
    }

    /// <summary>Stages the answers only for the registration the caller owns, so a stale page cannot overwrite a newer one.</summary>
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
    /// <summary>Attaches the new challenge id to a pending registration.</summary>
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

    /// <summary>Confirms the challenge only when it matches the pending registration.</summary>
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

    /// <summary>Clears the pending registration only when it is the one the caller still holds.</summary>
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

    /// <summary>Forgs the pending registration entirely.</summary>
    public void Clear()
    {
        lock (_syncRoot)
        {
            _currentRegistration = null;
            _stagedProfile = null;
        }
    }
}

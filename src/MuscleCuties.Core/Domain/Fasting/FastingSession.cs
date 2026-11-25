namespace MuscleCuties.Core.Domain.Fasting;

/// <summary>Whether fasting may be offered, and the reason it may not.</summary>
public enum FastingEligibilityResponse
{
    Unanswered,
    YesAllApply,
    No,
    Unsure
}

/// <summary>The state the eligibility gate resolved, so the client can explain a refusal.</summary>
public enum FastingEligibilityGateState
{
    Eligible,
    Unavailable,
    ProfileConflict
}

/// <summary>Where a fast stands, which decides the transitions it accepts.</summary>
public enum FastingSessionStatus
{
    Active,
    Paused,
    Ended
}

/// <summary>One pause inside a fast, which is subtracted from its duration.</summary>
public sealed record FastingPauseInterval(DateTimeOffset PausedAtUtc, DateTimeOffset? ResumedAtUtc)
{
    /// <summary>The pause's length, or null when it is still open.</summary>
    public TimeSpan? GetDuration(DateTimeOffset asOfUtc)
    {
        var end = ResumedAtUtc ?? asOfUtc;
        return end >= PausedAtUtc ? end - PausedAtUtc : null;
    }
}

/// <summary>The elapsed time of a fast with its pauses removed, and whether it passed its target.</summary>
public sealed record FastingElapsedEvaluation(bool IsTimeUncertain, TimeSpan? Elapsed);

/// <summary>
/// Optional, user-initiated elapsed-time record governed by policy P06-01-v0.
/// Never recommends a fasting duration, infers meal intake, or attaches physiological claims.
/// </summary>
public sealed class FastingSession
{
    public const string PolicyVersion = "P06-01-v0";
    public const int InitialVersion = 1;

    private readonly List<FastingPauseInterval> _pauses;

    /// <summary>Rebuilds a stored fast without re-running the eligibility gate.</summary>
    private FastingSession(
        Guid id,
        Guid accountId,
        string attestationPolicyVersion,
        DateTimeOffset startedAtUtc,
        int displayUtcOffsetMinutes,
        FastingSessionStatus status,
        DateTimeOffset? endedAtUtc,
        DateTimeOffset? plannedEndAtUtc,
        bool enablePlannedEndReminder,
        bool isManuallyCorrected,
        DateTimeOffset? lastCorrectedAtUtc,
        int version,
        IEnumerable<FastingPauseInterval> pauses)
    {
        Id = id;
        AccountId = accountId;
        AttestationPolicyVersion = attestationPolicyVersion;
        StartedAtUtc = startedAtUtc.ToUniversalTime();
        DisplayUtcOffsetMinutes = displayUtcOffsetMinutes;
        Status = status;
        EndedAtUtc = endedAtUtc?.ToUniversalTime();
        PlannedEndAtUtc = plannedEndAtUtc?.ToUniversalTime();
        EnablePlannedEndReminder = enablePlannedEndReminder;
        IsManuallyCorrected = isManuallyCorrected;
        LastCorrectedAtUtc = lastCorrectedAtUtc?.ToUniversalTime();
        Version = version;
        _pauses = pauses.ToList();
    }

    /// <summary>Identity of the fast.</summary>
    public Guid Id { get; }
    /// <summary>Account that owns the fast.</summary>
    public Guid AccountId { get; }
    /// <summary>Version of the eligibility attestation the user accepted when starting.</summary>
    public string AttestationPolicyVersion { get; }
    /// <summary>When the fast started, in UTC so the stored duration does not depend on the device.</summary>
    public DateTimeOffset StartedAtUtc { get; private set; }
    /// <summary>The offset the times are shown in, captured at start so a later travel does not rewrite history.</summary>
    public int DisplayUtcOffsetMinutes { get; private set; }
    /// <summary>Where the fast stands; only a running fast accepts a pause or an end.</summary>
    public FastingSessionStatus Status { get; private set; }
    /// <summary>When the fast ended, absent while it is still running.</summary>
    public DateTimeOffset? EndedAtUtc { get; private set; }
    /// <summary>The target end she set, which the reminder follows.</summary>
    public DateTimeOffset? PlannedEndAtUtc { get; private set; }
    /// <summary>Whether she wants to be reminded at the planned end.</summary>
    public bool EnablePlannedEndReminder { get; private set; }
    /// <summary>Whether the times were corrected by hand, so the record does not read as measured.</summary>
    public bool IsManuallyCorrected { get; private set; }
    /// <summary>When the last correction was made, absent when none was.</summary>
    public DateTimeOffset? LastCorrectedAtUtc { get; private set; }
    /// <summary>The row version a write must present, so two devices cannot overwrite each other.</summary>
    public int Version { get; private set; }
    /// <summary>The pauses in order, read-only so a caller cannot change the duration by accident.</summary>
    public IReadOnlyList<FastingPauseInterval> Pauses => _pauses.AsReadOnly();

    /// <summary>Decides whether a fast may start, refusing when the profile is pregnant and the answers allow it.</summary>
    public static FastingEligibilityGateState EvaluateEligibility(
        FastingEligibilityResponse response,
        bool? isProfilePregnant = null)
    {
        if (isProfilePregnant == true && response == FastingEligibilityResponse.YesAllApply)
            return FastingEligibilityGateState.ProfileConflict;
        if (isProfilePregnant == true)
            return FastingEligibilityGateState.Unavailable;
        return response == FastingEligibilityResponse.YesAllApply
            ? FastingEligibilityGateState.Eligible
            : FastingEligibilityGateState.Unavailable;
    }

    /// <summary>Starts a new fast once the eligibility answers are accepted, recording the attestation in force.</summary>
    public static FastingSession Start(
        Guid id,
        Guid accountId,
        FastingEligibilityResponse eligibilityResponse,
        DateTimeOffset startedAtUtc,
        int displayUtcOffsetMinutes,
        DateTimeOffset? plannedEndAtUtc = null,
        bool enablePlannedEndReminder = false,
        bool? isProfilePregnant = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Session identity is required.", nameof(id));
        if (accountId == Guid.Empty) throw new ArgumentException("Account identity is required.", nameof(accountId));
        var gate = EvaluateEligibility(eligibilityResponse, isProfilePregnant);
        if (gate == FastingEligibilityGateState.ProfileConflict)
            throw new InvalidOperationException("Eligibility answers conflict with the current profile.");
        if (gate != FastingEligibilityGateState.Eligible)
            throw new InvalidOperationException("The fasting timer requires an eligible P06-01-v0 attestation before starting.");
        ValidateOffset(displayUtcOffsetMinutes);
        var startUtc = startedAtUtc.ToUniversalTime();
        DateTimeOffset? normalizedPlannedEnd = plannedEndAtUtc?.ToUniversalTime();
        if (normalizedPlannedEnd.HasValue && normalizedPlannedEnd.Value <= startUtc)
            throw new ArgumentException("Planned end time must be after the start time.", nameof(plannedEndAtUtc));

        return new FastingSession(
            id,
            accountId,
            PolicyVersion,
            startUtc,
            displayUtcOffsetMinutes,
            FastingSessionStatus.Active,
            null,
            normalizedPlannedEnd,
            normalizedPlannedEnd.HasValue && enablePlannedEndReminder,
            false,
            null,
            InitialVersion,
            []);
    }

    /// <summary>Restores a fast from persisted state, keeping its version and timestamps.</summary>
    public static FastingSession Restore(
        Guid id,
        Guid accountId,
        string attestationPolicyVersion,
        DateTimeOffset startedAtUtc,
        int displayUtcOffsetMinutes,
        FastingSessionStatus status,
        DateTimeOffset? endedAtUtc,
        DateTimeOffset? plannedEndAtUtc,
        bool enablePlannedEndReminder,
        bool isManuallyCorrected,
        DateTimeOffset? lastCorrectedAtUtc,
        int version,
        IEnumerable<FastingPauseInterval>? pauses = null)
    {
        if (id == Guid.Empty) throw new ArgumentException("Session identity is required.", nameof(id));
        if (accountId == Guid.Empty) throw new ArgumentException("Account identity is required.", nameof(accountId));
        ArgumentOutOfRangeException.ThrowIfLessThan(version, InitialVersion);
        ValidateOffset(displayUtcOffsetMinutes);
        return new FastingSession(
            id,
            accountId,
            string.IsNullOrWhiteSpace(attestationPolicyVersion) ? PolicyVersion : attestationPolicyVersion,
            startedAtUtc,
            displayUtcOffsetMinutes,
            status,
            endedAtUtc,
            plannedEndAtUtc,
            enablePlannedEndReminder,
            isManuallyCorrected,
            lastCorrectedAtUtc,
            version,
            pauses ?? []);
    }

    /// <summary>Starts a pause, refusing one on a fast that is not running.</summary>
    public void Pause(int expectedVersion, DateTimeOffset pausedAtUtc)
    {
        EnsureVersion(expectedVersion);
        if (Status != FastingSessionStatus.Active)
            throw new InvalidOperationException("Only an active session can be paused.");
        var instant = pausedAtUtc.ToUniversalTime();
        if (instant < StartedAtUtc || (_pauses.Count > 0 && _pauses[^1].ResumedAtUtc is { } lastResume && instant < lastResume))
            throw new ArgumentException("Pause time cannot precede the latest session transition.", nameof(pausedAtUtc));

        _pauses.Add(new FastingPauseInterval(instant, null));
        Status = FastingSessionStatus.Paused;
        EnablePlannedEndReminder = false;
        Version = checked(Version + 1);
    }

    /// <summary>Resumes a paused fast under the expected version.</summary>
    public void Resume(
        int expectedVersion,
        DateTimeOffset resumedAtUtc,
        FastingEligibilityResponse eligibilityResponse = FastingEligibilityResponse.YesAllApply,
        bool? isProfilePregnant = null)
    {
        EnsureVersion(expectedVersion);
        if (Status != FastingSessionStatus.Paused || _pauses.Count == 0 || _pauses[^1].ResumedAtUtc.HasValue)
            throw new InvalidOperationException("Only a paused session can be resumed.");
        var gate = EvaluateEligibility(eligibilityResponse, isProfilePregnant);
        if (gate != FastingEligibilityGateState.Eligible)
            throw new InvalidOperationException("Resuming requires an eligible P06-01-v0 state.");
        var instant = resumedAtUtc.ToUniversalTime();
        var openPause = _pauses[^1];
        if (instant < openPause.PausedAtUtc)
            throw new ArgumentException("Resume time cannot precede the pause start.", nameof(resumedAtUtc));

        _pauses[^1] = openPause with { ResumedAtUtc = instant };
        Status = FastingSessionStatus.Active;
        Version = checked(Version + 1);
    }

    /// <summary>Ends the fast, refusing an end on one that is already closed.</summary>
    public void End(int expectedVersion, DateTimeOffset endedAtUtc)
    {
        EnsureVersion(expectedVersion);
        if (Status == FastingSessionStatus.Ended)
            throw new InvalidOperationException("This session has already ended.");
        var instant = endedAtUtc.ToUniversalTime();
        if (instant < StartedAtUtc)
            throw new ArgumentException("End time cannot precede the start time.", nameof(endedAtUtc));
        if (_pauses.Count > 0 && _pauses[^1].ResumedAtUtc is null)
        {
            var openPause = _pauses[^1];
            if (instant < openPause.PausedAtUtc)
                throw new ArgumentException("End time cannot precede an active pause.", nameof(endedAtUtc));
            _pauses[^1] = openPause with { ResumedAtUtc = instant };
        }

        EndedAtUtc = instant;
        Status = FastingSessionStatus.Ended;
        EnablePlannedEndReminder = false;
        Version = checked(Version + 1);
    }

    /// <summary>Rewrites the fast's start and end times under the expected version.</summary>
    public void CorrectTimes(
        int expectedVersion,
        DateTimeOffset correctedStartedAtUtc,
        DateTimeOffset? correctedEndedAtUtc,
        DateTimeOffset correctedAtUtc)
    {
        EnsureVersion(expectedVersion);
        var startUtc = correctedStartedAtUtc.ToUniversalTime();
        var endUtc = correctedEndedAtUtc?.ToUniversalTime();
        if (endUtc.HasValue && endUtc.Value < startUtc)
            throw new ArgumentException("Corrected end time cannot precede the corrected start time.", nameof(correctedEndedAtUtc));
        if (Status == FastingSessionStatus.Ended && !endUtc.HasValue)
            throw new ArgumentException("An ended session requires an end timestamp.", nameof(correctedEndedAtUtc));

        StartedAtUtc = startUtc;
        if (endUtc.HasValue)
        {
            EndedAtUtc = endUtc.Value;
            Status = FastingSessionStatus.Ended;
            EnablePlannedEndReminder = false;
        }

        _pauses.RemoveAll(p => p.PausedAtUtc < startUtc || (endUtc.HasValue && p.PausedAtUtc > endUtc.Value));
        IsManuallyCorrected = true;
        LastCorrectedAtUtc = correctedAtUtc.ToUniversalTime();
        Version = checked(Version + 1);
    }

    /// <summary>Sets the planned end and reminder flag under the expected version.</summary>
    public void UpdatePlannedEnd(
        int expectedVersion,
        DateTimeOffset? plannedEndAtUtc,
        bool enablePlannedEndReminder)
    {
        EnsureVersion(expectedVersion);
        if (Status == FastingSessionStatus.Ended)
            throw new InvalidOperationException("Cannot update the planned end of an ended session.");
        var normalized = plannedEndAtUtc?.ToUniversalTime();
        if (normalized.HasValue && normalized.Value <= StartedAtUtc)
            throw new ArgumentException("Planned end time must be after the start time.", nameof(plannedEndAtUtc));

        PlannedEndAtUtc = normalized;
        EnablePlannedEndReminder = normalized.HasValue && enablePlannedEndReminder && Status == FastingSessionStatus.Active;
        Version = checked(Version + 1);
    }

    /// <summary>The elapsed time net of pauses, and whether the target was reached.</summary>
    public FastingElapsedEvaluation EvaluateElapsed(DateTimeOffset referenceNowUtc, bool isClockUncertain = false)
    {
        if (isClockUncertain)
            return new FastingElapsedEvaluation(true, null);

        var effectiveEnd = Status == FastingSessionStatus.Ended
            ? EndedAtUtc
            : referenceNowUtc.ToUniversalTime();
        if (!effectiveEnd.HasValue || effectiveEnd.Value < StartedAtUtc)
            return new FastingElapsedEvaluation(true, null);

        var totalSpan = effectiveEnd.Value - StartedAtUtc;
        var pausedSpan = TimeSpan.Zero;
        foreach (var pause in _pauses)
        {
            var duration = pause.GetDuration(effectiveEnd.Value);
            if (!duration.HasValue)
                return new FastingElapsedEvaluation(true, null);
            pausedSpan += duration.Value;
        }

        var net = totalSpan - pausedSpan;
        return net < TimeSpan.Zero
            ? new FastingElapsedEvaluation(true, null)
            : new FastingElapsedEvaluation(false, net);
    }

    /// <summary>Refuses a write whose version is behind, so a stale device cannot undo a correction.</summary>
    private void EnsureVersion(int expectedVersion)
    {
        if (expectedVersion != Version)
            throw new InvalidOperationException("The fasting record changed elsewhere. Reload before trying again.");
    }

    /// <summary>Refuses an offset outside the real range, since the display times depend on it.</summary>
    private static void ValidateOffset(int offsetMinutes)
    {
        if (offsetMinutes is < -14 * 60 or > 14 * 60)
            throw new ArgumentOutOfRangeException(nameof(offsetMinutes), "UTC offset must be within [-840, 840] minutes.");
    }
}

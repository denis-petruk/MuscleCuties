namespace MuscleCuties.Core.Domain.Fasting;

public enum FastingEligibilityResponse
{
    Unanswered,
    YesAllApply,
    No,
    Unsure
}

public enum FastingEligibilityGateState
{
    Eligible,
    Unavailable,
    ProfileConflict
}

public enum FastingSessionStatus
{
    Active,
    Paused,
    Ended
}

public sealed record FastingPauseInterval(DateTimeOffset PausedAtUtc, DateTimeOffset? ResumedAtUtc)
{
    public TimeSpan? GetDuration(DateTimeOffset asOfUtc)
    {
        var end = ResumedAtUtc ?? asOfUtc;
        return end >= PausedAtUtc ? end - PausedAtUtc : null;
    }
}

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

    public Guid Id { get; }
    public Guid AccountId { get; }
    public string AttestationPolicyVersion { get; }
    public DateTimeOffset StartedAtUtc { get; private set; }
    public int DisplayUtcOffsetMinutes { get; private set; }
    public FastingSessionStatus Status { get; private set; }
    public DateTimeOffset? EndedAtUtc { get; private set; }
    public DateTimeOffset? PlannedEndAtUtc { get; private set; }
    public bool EnablePlannedEndReminder { get; private set; }
    public bool IsManuallyCorrected { get; private set; }
    public DateTimeOffset? LastCorrectedAtUtc { get; private set; }
    public int Version { get; private set; }
    public IReadOnlyList<FastingPauseInterval> Pauses => _pauses.AsReadOnly();

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

    private void EnsureVersion(int expectedVersion)
    {
        if (expectedVersion != Version)
            throw new InvalidOperationException("The fasting record changed elsewhere. Reload before trying again.");
    }

    private static void ValidateOffset(int offsetMinutes)
    {
        if (offsetMinutes is < -14 * 60 or > 14 * 60)
            throw new ArgumentOutOfRangeException(nameof(offsetMinutes), "UTC offset must be within [-840, 840] minutes.");
    }
}

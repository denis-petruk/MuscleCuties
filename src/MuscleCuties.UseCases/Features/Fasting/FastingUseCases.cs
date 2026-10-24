using MuscleCuties.Core.Domain.Fasting;

namespace MuscleCuties.UseCases.Features.Fasting;

public enum FastingEligibilityResponseDto
{
    Unanswered,
    YesAllApply,
    No,
    Unsure
}

public enum FastingSessionStatusDto
{
    Active,
    Paused,
    Ended
}

public sealed record FastingPauseIntervalDto(DateTimeOffset PausedAtUtc, DateTimeOffset? ResumedAtUtc);

public sealed record FastingSessionOutputDto(
    Guid Id,
    Guid AccountId,
    string AttestationPolicyVersion,
    DateTimeOffset StartedAtUtc,
    int DisplayUtcOffsetMinutes,
    FastingSessionStatusDto Status,
    DateTimeOffset? EndedAtUtc,
    DateTimeOffset? PlannedEndAtUtc,
    bool EnablePlannedEndReminder,
    bool IsManuallyCorrected,
    DateTimeOffset? LastCorrectedAtUtc,
    int Version,
    bool IsTimeUncertain,
    long? ElapsedSeconds,
    IReadOnlyList<FastingPauseIntervalDto> Pauses);

public sealed record FastingStateOutputDto(
    FastingSessionOutputDto? ActiveSession,
    IReadOnlyList<FastingSessionOutputDto> History);

public sealed record StartFastingSessionInputDto(
    Guid AccountId,
    Guid SessionId,
    Guid CommandId,
    FastingEligibilityResponseDto EligibilityResponse,
    DateTimeOffset StartedAtUtc,
    int DisplayUtcOffsetMinutes,
    DateTimeOffset? PlannedEndAtUtc = null,
    bool EnablePlannedEndReminder = false,
    bool? IsProfilePregnant = null);

public sealed record TransitionFastingSessionInputDto(
    Guid AccountId,
    Guid SessionId,
    Guid CommandId,
    int ExpectedVersion,
    DateTimeOffset TransitionAtUtc,
    FastingEligibilityResponseDto EligibilityResponse = FastingEligibilityResponseDto.YesAllApply,
    bool? IsProfilePregnant = null);

public sealed record CorrectFastingSessionInputDto(
    Guid AccountId,
    Guid SessionId,
    Guid CommandId,
    int ExpectedVersion,
    DateTimeOffset CorrectedStartedAtUtc,
    DateTimeOffset? CorrectedEndedAtUtc,
    DateTimeOffset CorrectedAtUtc);

public sealed record UpdateFastingPlannedEndInputDto(
    Guid AccountId,
    Guid SessionId,
    Guid CommandId,
    int ExpectedVersion,
    DateTimeOffset? PlannedEndAtUtc,
    bool EnablePlannedEndReminder);

public interface IFastingSessionStore
{
    Task<FastingSessionOutputDto?> TryReplayCommandAsync(Guid accountId, Guid commandId, string payloadSignature, CancellationToken cancellationToken);
    Task<IReadOnlyList<FastingSession>> LoadAllAsync(Guid accountId, CancellationToken cancellationToken);
    Task<FastingSession?> LoadByIdAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken);
    Task<FastingSession> SaveAsync(Guid accountId, Guid commandId, string payloadSignature, FastingSession session, int? expectedVersion, CancellationToken cancellationToken);
}

public sealed class FastingSessionUseCase(IFastingSessionStore store, TimeProvider timeProvider)
{
    public async Task<FastingStateOutputDto> GetStateAsync(Guid accountId, bool isClockUncertain = false, CancellationToken cancellationToken = default)
    {
        RequireAccount(accountId);
        var now = timeProvider.GetUtcNow();
        var sessions = await store.LoadAllAsync(accountId, cancellationToken).ConfigureAwait(false);
        var mapped = sessions
            .OrderByDescending(s => s.StartedAtUtc)
            .Select(s => Map(s, now, isClockUncertain))
            .ToArray();
        var active = mapped.FirstOrDefault(s =>
            s.Status is FastingSessionStatusDto.Active or FastingSessionStatusDto.Paused);
        return new FastingStateOutputDto(active, mapped);
    }

    public async Task<FastingSessionOutputDto> StartAsync(StartFastingSessionInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireAccount(input.AccountId);
        RequireCommand(input.CommandId);
        var sig = $"start:{input.SessionId:N}:{input.EligibilityResponse}:{input.StartedAtUtc:O}:{input.DisplayUtcOffsetMinutes}:{input.PlannedEndAtUtc:O}:{input.EnablePlannedEndReminder}";
        var replay = await store.TryReplayCommandAsync(input.AccountId, input.CommandId, sig, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;

        var existing = await store.LoadAllAsync(input.AccountId, cancellationToken).ConfigureAwait(false);
        if (existing.Any(s => s.Status is FastingSessionStatus.Active or FastingSessionStatus.Paused))
            throw new InvalidOperationException("An active or paused fasting record already exists.");

        var session = FastingSession.Start(
            input.SessionId,
            input.AccountId,
            ToDomain(input.EligibilityResponse),
            input.StartedAtUtc,
            input.DisplayUtcOffsetMinutes,
            input.PlannedEndAtUtc,
            input.EnablePlannedEndReminder,
            input.IsProfilePregnant);
        var saved = await store.SaveAsync(input.AccountId, input.CommandId, sig, session, null, cancellationToken).ConfigureAwait(false);
        return Map(saved, timeProvider.GetUtcNow(), false);
    }

    public async Task<FastingSessionOutputDto> PauseAsync(TransitionFastingSessionInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireAccount(input.AccountId);
        RequireCommand(input.CommandId);
        var sig = $"pause:{input.SessionId:N}:{input.ExpectedVersion}:{input.TransitionAtUtc:O}";
        var replay = await store.TryReplayCommandAsync(input.AccountId, input.CommandId, sig, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;

        var session = await RequireSessionAsync(input.AccountId, input.SessionId, cancellationToken).ConfigureAwait(false);
        session.Pause(input.ExpectedVersion, input.TransitionAtUtc);
        var saved = await store.SaveAsync(input.AccountId, input.CommandId, sig, session, input.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        return Map(saved, timeProvider.GetUtcNow(), false);
    }

    public async Task<FastingSessionOutputDto> ResumeAsync(TransitionFastingSessionInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireAccount(input.AccountId);
        RequireCommand(input.CommandId);
        var sig = $"resume:{input.SessionId:N}:{input.ExpectedVersion}:{input.TransitionAtUtc:O}:{input.EligibilityResponse}";
        var replay = await store.TryReplayCommandAsync(input.AccountId, input.CommandId, sig, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;

        var session = await RequireSessionAsync(input.AccountId, input.SessionId, cancellationToken).ConfigureAwait(false);
        session.Resume(input.ExpectedVersion, input.TransitionAtUtc,
            ToDomain(input.EligibilityResponse), input.IsProfilePregnant);
        var saved = await store.SaveAsync(input.AccountId, input.CommandId, sig, session, input.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        return Map(saved, timeProvider.GetUtcNow(), false);
    }

    public async Task<FastingSessionOutputDto> EndAsync(TransitionFastingSessionInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireAccount(input.AccountId);
        RequireCommand(input.CommandId);
        var sig = $"end:{input.SessionId:N}:{input.ExpectedVersion}:{input.TransitionAtUtc:O}";
        var replay = await store.TryReplayCommandAsync(input.AccountId, input.CommandId, sig, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;

        var session = await RequireSessionAsync(input.AccountId, input.SessionId, cancellationToken).ConfigureAwait(false);
        session.End(input.ExpectedVersion, input.TransitionAtUtc);
        var saved = await store.SaveAsync(input.AccountId, input.CommandId, sig, session, input.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        return Map(saved, timeProvider.GetUtcNow(), false);
    }

    public async Task<FastingSessionOutputDto> CorrectTimesAsync(CorrectFastingSessionInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireAccount(input.AccountId);
        RequireCommand(input.CommandId);
        var sig = $"correct:{input.SessionId:N}:{input.ExpectedVersion}:{input.CorrectedStartedAtUtc:O}:{input.CorrectedEndedAtUtc:O}";
        var replay = await store.TryReplayCommandAsync(input.AccountId, input.CommandId, sig, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;

        var session = await RequireSessionAsync(input.AccountId, input.SessionId, cancellationToken).ConfigureAwait(false);
        session.CorrectTimes(input.ExpectedVersion, input.CorrectedStartedAtUtc, input.CorrectedEndedAtUtc, input.CorrectedAtUtc);
        var saved = await store.SaveAsync(input.AccountId, input.CommandId, sig, session, input.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        return Map(saved, timeProvider.GetUtcNow(), false);
    }

    public async Task<FastingSessionOutputDto> UpdatePlannedEndAsync(UpdateFastingPlannedEndInputDto input, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        RequireAccount(input.AccountId);
        RequireCommand(input.CommandId);
        var sig = $"planned-end:{input.SessionId:N}:{input.ExpectedVersion}:{input.PlannedEndAtUtc:O}:{input.EnablePlannedEndReminder}";
        var replay = await store.TryReplayCommandAsync(input.AccountId, input.CommandId, sig, cancellationToken).ConfigureAwait(false);
        if (replay is not null) return replay;

        var session = await RequireSessionAsync(input.AccountId, input.SessionId, cancellationToken).ConfigureAwait(false);
        session.UpdatePlannedEnd(input.ExpectedVersion, input.PlannedEndAtUtc, input.EnablePlannedEndReminder);
        var saved = await store.SaveAsync(input.AccountId, input.CommandId, sig, session, input.ExpectedVersion, cancellationToken).ConfigureAwait(false);
        return Map(saved, timeProvider.GetUtcNow(), false);
    }

    private async Task<FastingSession> RequireSessionAsync(Guid accountId, Guid sessionId, CancellationToken cancellationToken) =>
        await store.LoadByIdAsync(accountId, sessionId, cancellationToken).ConfigureAwait(false)
        ?? throw new KeyNotFoundException("Fasting record was not found for the current account.");

    private static void RequireAccount(Guid accountId)
    {
        if (accountId == Guid.Empty)
            throw new UnauthorizedAccessException("An authenticated account is required.");
    }

    private static void RequireCommand(Guid commandId)
    {
        if (commandId == Guid.Empty)
            throw new ArgumentException("A non-empty command ID is required.", nameof(commandId));
    }

    private static FastingEligibilityResponse ToDomain(FastingEligibilityResponseDto response) => response switch
    {
        FastingEligibilityResponseDto.Unanswered => FastingEligibilityResponse.Unanswered,
        FastingEligibilityResponseDto.YesAllApply => FastingEligibilityResponse.YesAllApply,
        FastingEligibilityResponseDto.No => FastingEligibilityResponse.No,
        FastingEligibilityResponseDto.Unsure => FastingEligibilityResponse.Unsure,
        _ => throw new ArgumentOutOfRangeException(nameof(response), response, "Unknown fasting eligibility response.")
    };

    private static FastingSessionStatusDto ToDto(FastingSessionStatus status) => status switch
    {
        FastingSessionStatus.Active => FastingSessionStatusDto.Active,
        FastingSessionStatus.Paused => FastingSessionStatusDto.Paused,
        FastingSessionStatus.Ended => FastingSessionStatusDto.Ended,
        _ => throw new ArgumentOutOfRangeException(nameof(status), status, "Unknown fasting session status.")
    };

    public static FastingSessionOutputDto Map(FastingSession session, DateTimeOffset referenceNowUtc, bool isClockUncertain)
    {
        var evaluation = session.EvaluateElapsed(referenceNowUtc, isClockUncertain);
        return new FastingSessionOutputDto(
            session.Id,
            session.AccountId,
            session.AttestationPolicyVersion,
            session.StartedAtUtc,
            session.DisplayUtcOffsetMinutes,
            ToDto(session.Status),
            session.EndedAtUtc,
            session.PlannedEndAtUtc,
            session.EnablePlannedEndReminder,
            session.IsManuallyCorrected,
            session.LastCorrectedAtUtc,
            session.Version,
            evaluation.IsTimeUncertain,
            evaluation.Elapsed.HasValue ? (long)Math.Floor(evaluation.Elapsed.Value.TotalSeconds) : null,
            session.Pauses.Select(p => new FastingPauseIntervalDto(p.PausedAtUtc, p.ResumedAtUtc)).ToArray());
    }
}

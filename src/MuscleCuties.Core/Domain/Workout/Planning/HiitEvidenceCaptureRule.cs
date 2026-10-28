namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>Bounds a server-issued, single-use HIIT evidence capture to its account calendar.</summary>
public static class HiitEvidenceCaptureRule
{
    public static readonly TimeSpan ChallengeLifetime = TimeSpan.FromMinutes(15);
    public const int EvidenceValidityCalendarDays = 30;

    public static bool IsCaptureCurrent(DateOnly captureLocalDate, long captureTimeZoneVersion,
        DateOnly? currentLocalDate, long currentTimeZoneVersion) =>
        currentLocalDate is { } current && captureLocalDate <= current &&
        current.DayNumber - captureLocalDate.DayNumber < EvidenceValidityCalendarDays &&
        captureTimeZoneVersion == currentTimeZoneVersion;

    public static bool CanConsume(DateTimeOffset issuedAt, DateTimeOffset expiresAt, DateTimeOffset now,
        DateOnly issuedLocalDate, DateOnly currentLocalDate, long issuedTimeZoneVersion,
        long currentTimeZoneVersion, bool alreadyConsumed) =>
        !alreadyConsumed && issuedAt <= now && now < expiresAt && expiresAt > issuedAt &&
        expiresAt - issuedAt <= ChallengeLifetime && issuedLocalDate == currentLocalDate &&
        issuedTimeZoneVersion == currentTimeZoneVersion;
}

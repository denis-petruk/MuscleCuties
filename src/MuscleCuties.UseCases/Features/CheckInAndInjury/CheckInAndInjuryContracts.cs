namespace MuscleCuties.UseCases.Features.CheckInAndInjury;

public sealed record DailyCheckInRecord(
    Guid Id,
    DateOnly Date,
    decimal? SleepHours,
    decimal? Sleep3dAvg,
    int? StepsYesterday,
    int? Steps7dAvg,
    int? Energy,
    int? Pain,
    bool? Bloating,
    decimal? WeightKg,
    int? SleepQuality,
    int? Soreness,
    decimal? PreviousDayLoad,
    int ReadinessScore,
    string ReadinessTier,
    string Phase,
    int? PlanningSleepQuality = null,
    int? PlanningSoreness = null,
    int? PlanningStress = null,
    string? PlanningTimeZoneId = null,
    DateTimeOffset? PlanningRecordedAtUtc = null,
    long PlanningRevision = 0);

public sealed record SaveDailyCheckInRequest(
    Guid UserId,
    Guid Id,
    DateOnly Date,
    decimal? SleepHours,
    int? StepsYesterday,
    int? Energy,
    int? Pain,
    bool? Bloating,
    decimal? WeightKg,
    int? SleepQuality,
    int? Soreness,
    decimal? PreviousDayLoad,
    string? Phase = null,
    decimal? Sleep3dAvg = null,
    int? Steps7dAvg = null,
    int? PlanningSleepQuality = null,
    int? PlanningSoreness = null,
    int? PlanningStress = null,
    string? PlanningTimeZoneId = null);

public sealed record InjuryObservationRecord(
    Guid Id,
    int SiteFlag,
    string Status,
    DateOnly Since,
    int Pain,
    DateOnly Date,
    bool IsActive);

public sealed record SaveInjuryObservationRequest(
    Guid UserId,
    Guid Id,
    int SiteFlag,
    string Status,
    DateOnly Since,
    int Pain,
    DateOnly Date);

public sealed class CheckInAndInjuryIdConflictException : Exception
{
    public CheckInAndInjuryIdConflictException()
        : base("The supplied record ID cannot be used.")
    {
    }
}

public sealed class CheckInAndInjuryDateConflictException : Exception
{
    public CheckInAndInjuryDateConflictException()
        : base("A check-in already exists for this owner and date.")
    {
    }
}

public interface ICheckInAndInjuryStore
{
    Task<IReadOnlyList<DailyCheckInRecord>> LoadCheckInsAsync(
        Guid userId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken);

    Task<DailyCheckInRecord> SaveCheckInAsync(
        SaveDailyCheckInRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteCheckInAsync(
        Guid userId,
        Guid checkInId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<InjuryObservationRecord>> LoadInjuriesAsync(
        Guid userId,
        bool activeOnly,
        CancellationToken cancellationToken);

    Task<InjuryObservationRecord> SaveInjuryAsync(
        SaveInjuryObservationRequest request,
        CancellationToken cancellationToken);

    Task<bool> DeleteInjuryAsync(
        Guid userId,
        Guid injuryId,
        CancellationToken cancellationToken);
}

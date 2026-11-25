namespace MuscleCuties.UseCases.Features.CheckInAndInjury;

/// <summary>One day's check-in as stored: what she reported and what the planner derived.</summary>
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
    long PlanningRevision = 0,
    int? PlanningFatigue = null);

/// <summary>A day's answers sent up, idempotent on its key.</summary>
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
    string? PlanningTimeZoneId = null,
    int? PlanningFatigue = null);

/// <summary>One reported injury as stored, with the status that restricts planning.</summary>
public sealed record InjuryObservationRecord(
    Guid Id,
    int SiteFlag,
    string Status,
    DateOnly Since,
    int Pain,
    DateOnly Date,
    bool IsActive,
    MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger WorseningTrigger =
        MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
    bool? ClinicianAdvisedAvoid = null,
    DateTimeOffset? PainReassessmentDueAtUtc = null);

/// <summary>An injury report sent up.</summary>
public sealed record SaveInjuryObservationRequest(
    Guid UserId,
    Guid Id,
    int SiteFlag,
    string Status,
    DateOnly Since,
    int Pain,
    DateOnly Date,
    MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger WorseningTrigger =
        MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered,
    bool? ClinicianAdvisedAvoid = null);

/// <summary>A conflicting id was reused, which is a client bug rather than a retry.</summary>
public sealed class CheckInAndInjuryIdConflictException : Exception
{
    /// <summary>A conflicting id was reused, which the caller must fix rather than retry.</summary>
    public CheckInAndInjuryIdConflictException()
        : base("The supplied record ID cannot be used.")
    {
    }
}

/// <summary>An injury cannot be recorded while a session is in progress, because the plan it affects is in use.</summary>
public sealed class ActiveWorkoutPreventsInjuryException : Exception
{
    /// <summary>An injury cannot change mid-session, because the plan in use would become unsafe.</summary>
    public ActiveWorkoutPreventsInjuryException()
        : base("End or abandon the active workout before recording an injury.") { }
}

/// <summary>The date is locked by another write, so the caller must re-read before saving.</summary>
public sealed class CheckInAndInjuryDateConflictException : Exception
{
    /// <summary>The date is locked by another write, so the caller must re-read and merge.</summary>
    public CheckInAndInjuryDateConflictException()
        : base("A check-in already exists for this owner and date.")
    {
    }
}

/// <summary>The persistence check-ins and injuries need.</summary>
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

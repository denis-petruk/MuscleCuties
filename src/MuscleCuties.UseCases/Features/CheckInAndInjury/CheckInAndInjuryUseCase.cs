using MuscleCuties.Core.Domain.Workout;

namespace MuscleCuties.UseCases.Features.CheckInAndInjury;

public sealed class CheckInAndInjuryUseCase(ICheckInAndInjuryStore store)
{

    public Task<IReadOnlyList<DailyCheckInRecord>> LoadCheckInsAsync(
        Guid userId,
        DateOnly from,
        DateOnly through,
        CancellationToken cancellationToken = default)
    {
        if (!CheckInObservationRule.IsSupportedRange(userId, from, through))
            throw new ArgumentOutOfRangeException(nameof(from), "A valid owner and supported local date range are required.");
        return store.LoadCheckInsAsync(userId, from, through, cancellationToken);
    }

    public Task<DailyCheckInRecord> SaveCheckInAsync(
        SaveDailyCheckInRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!CheckInObservationRule.IsValid(request.UserId, request.Id, request.Date, request.SleepHours,
                request.Sleep3dAvg, request.StepsYesterday, request.Steps7dAvg, request.Energy, request.Pain,
                request.WeightKg, request.SleepQuality, request.Soreness, request.PreviousDayLoad))
            throw new ArgumentOutOfRangeException(nameof(request), "Check-in fields must be within supported clinical bounds.");

        return store.SaveCheckInAsync(request, cancellationToken);
    }

    public Task<bool> DeleteCheckInAsync(
        Guid userId,
        Guid checkInId,
        CancellationToken cancellationToken = default) =>
        userId == Guid.Empty || checkInId == Guid.Empty
            ? Task.FromResult(false)
            : store.DeleteCheckInAsync(userId, checkInId, cancellationToken);

    public Task<IReadOnlyList<InjuryObservationRecord>> LoadInjuriesAsync(
        Guid userId,
        bool activeOnly = false,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            throw new ArgumentOutOfRangeException(nameof(userId), "A valid owner ID is required.");
        return store.LoadInjuriesAsync(userId, activeOnly, cancellationToken);
    }

    public Task<InjuryObservationRecord> SaveInjuryAsync(
        SaveInjuryObservationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!InjuryObservationRule.IsValid(request.UserId, request.Id, request.SiteFlag, request.Status,
                request.Since, request.Pain, request.Date, out var normalizedStatus))
            throw new ArgumentOutOfRangeException(nameof(request), "Injury observation fields must be within supported bounds.");

        return store.SaveInjuryAsync(request with { Status = normalizedStatus }, cancellationToken);
    }

    public Task<bool> DeleteInjuryAsync(
        Guid userId,
        Guid injuryId,
        CancellationToken cancellationToken = default) =>
        userId == Guid.Empty || injuryId == Guid.Empty
            ? Task.FromResult(false)
            : store.DeleteInjuryAsync(userId, injuryId, cancellationToken);

}

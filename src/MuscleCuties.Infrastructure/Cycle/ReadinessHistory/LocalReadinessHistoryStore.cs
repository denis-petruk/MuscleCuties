using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.UseCases.Features.Cycle.ReadinessHistory;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.Infrastructure.Cycle.ReadinessHistory;

public sealed class LocalReadinessHistoryStore(IServiceScopeFactory scopeFactory) : IReadinessHistoryStore
{
    public async Task<IReadOnlyList<ReadinessCheckInEntry>> LoadAsync(
        DateOnly start, DateOnly end, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var session = scope.ServiceProvider.GetRequiredService<ICurrentProfileUserIdProvider>();
        var userId = await session.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false);
        if (userId <= 0)
            return [];

        var logs = await scope.ServiceProvider.GetRequiredService<IDailyCheckInDataService>()
            .GetCheckInsAsync(userId, start, end).ConfigureAwait(false);
        if (await session.GetCurrentUserIdAsync(cancellationToken).ConfigureAwait(false) != userId)
            return [];

        return logs.Select(log => new ReadinessCheckInEntry(log.Date,
            double.IsFinite(log.SleepHours) ? Math.Clamp(log.SleepHours, 0, 24) : 0,
            Math.Clamp(log.StepsYesterday, 0, 200_000),
            log.Energy is >= 1 and <= 5 ? log.Energy : 1,
            log.Pain is >= 0 and <= 3 ? log.Pain : 3,
            log.Bloating,
            log.WeightKg is { } weight && double.IsFinite(weight) && weight is > 0 and <= 500
                ? weight
                : null,
            log.SleepQuality is >= 1 and <= 5 ? log.SleepQuality : null,
            log.Soreness is >= 0 and <= 3 ? log.Soreness : null,
            log.PreviousDayLoad is { } load && double.IsFinite(load) && load >= 0
                ? load
                : null)).ToArray();
    }
}

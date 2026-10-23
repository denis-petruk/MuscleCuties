using System.Diagnostics;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Services;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Health;
using MuscleCuties.Core.Services.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.Infrastructure.Dashboard.CheckIn;

public sealed class LocalDailyCheckInStore(
    IAuthService auth,
    IDailyCheckInDataService data,
    IHealthReadinessBridge health,
    ICycleService cycle,
    IReadinessEngine readinessEngine,
    IWorkoutService workouts,
    IAppPreloadService preload) : IDailyCheckInStore
{
    public async Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request,
        CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        var userId = await RequireUserAsync().ConfigureAwait(false);
        var existing = await data.GetCheckInAsync(userId, request.Date).ConfigureAwait(false);
        if (await RequireUserAsync().ConfigureAwait(false) != userId)
            throw new UnauthorizedAccessException("Account changed during daily check-in.");
        if (existing is null)
            return new DailyCheckInStatusResponse(false, string.Empty, string.Empty);
        var score = existing.ReadinessScore is >= 0 and <= 100
            ? existing.ReadinessScore.ToString()
            : "—";
        var tier = Enum.IsDefined(typeof(ReadinessTier), existing.ReadinessTier)
            ? ((ReadinessTier)existing.ReadinessTier).ToString()
            : "Check-in complete";
        return new DailyCheckInStatusResponse(true, score, tier);
    }

    public async Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        if (request.Date != DateOnly.FromDateTime(DateTime.Today))
            throw new ArgumentException("Daily check-in must be for today.", nameof(request));
        var userId = await RequireUserAsync().ConfigureAwait(false);
        if (await data.GetCheckInAsync(userId, request.Date).ConfigureAwait(false) is not null)
            throw new InvalidOperationException("Today's check-in is already complete.");

        var phase = await cycle.GetCurrentPhaseAsync(userId).ConfigureAwait(false);
        var log = new DailyReadinessLog
        {
            UserId = userId,
            Date = request.Date,
            SleepHours = double.IsFinite(request.SleepHours) ? Math.Clamp(request.SleepHours, 0, 14) : 0,
            Sleep3dAvg = 0,
            StepsYesterday = 0,
            Steps7dAvg = 0,
            Energy = Math.Clamp(request.EnergyLevel, 1, 5),
            Pain = Math.Clamp(request.PainLevel, 0, 3),
            Bloating = request.HasBloating,
            WeightKg = request.BodyWeightKg is > 0 && double.IsFinite(request.BodyWeightKg.Value)
                ? request.BodyWeightKg
                : null,
            Phase = phase.ToString()
        };
        var healthSummary = await health.EnrichAsync(userId, log).ConfigureAwait(false);
        var baselines = await data.GetPhaseBaselinesAsync(userId).ConfigureAwait(false);
        var safeInputs = DailyInputs.CreateSafe(
            log.Date, log.SleepHours, log.Sleep3dAvg, log.StepsYesterday, log.Steps7dAvg,
            log.Energy, log.Pain, log.Bloating, log.WeightKg,
            healthSummary?.RestingHeartRate, healthSummary?.AverageActiveEnergyKcal,
            log.SleepQuality.HasValue ? (SleepQuality)log.SleepQuality.Value : null,
            log.Soreness.HasValue ? (SorenessLevel)log.Soreness.Value : null,
            log.PreviousDayLoad.HasValue
                ? new WorkoutLoad(WorkoutLoadKind.AggregateOnly, log.PreviousDayLoad.Value)
                : null);
        var result = readinessEngine.Compute(safeInputs, phase, baselines);
        log.ReadinessScore = result.Score;
        log.ReadinessTier = (int)result.Tier;

        if (await RequireUserAsync().ConfigureAwait(false) != userId)
            throw new UnauthorizedAccessException("Account changed during daily check-in.");
        await data.SaveAsync(log).ConfigureAwait(false);
        preload.InvalidateDashboard();
        preload.InvalidateWorkout();
        if (await RequireUserAsync().ConfigureAwait(false) != userId)
            throw new UnauthorizedAccessException("Account changed during daily check-in.");

        try
        {
            await workouts.RegenerateActivePlanAsync(userId, phase).ConfigureAwait(false);
            var workout = await workouts.GetTodaysSummaryAsync(userId, phase, DateTime.Today).ConfigureAwait(false);
            return new SubmitDailyCheckInResponse(result.Score, result.Tier.ToString(), workout.Title, false);
        }
        catch (Exception exception)
        {
            Trace.WriteLine($"[DailyCheckIn] Plan refresh failed ({exception.GetType().Name}).");
            return new SubmitDailyCheckInResponse(result.Score, result.Tier.ToString(), string.Empty, true);
        }
    }

    private async Task<int> RequireUserAsync()
    {
        var id = await auth.GetCurrentUserIdAsync().ConfigureAwait(false);
        if (id <= 0)
            throw new UnauthorizedAccessException("Sign in to use daily check-in.");
        return id;
    }
}

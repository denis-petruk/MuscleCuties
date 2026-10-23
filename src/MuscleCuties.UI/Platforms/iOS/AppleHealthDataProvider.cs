using Foundation;
using HealthKit;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.UI.Platforms.iOS;

// A one-shot, read-only StepCount reader. HealthKit does not reveal whether read access was denied.
public sealed class AppleHealthDataProvider : IHealthDataProvider, IHealthAuthorizationProvider,
    IHealthDataProviderDiagnostics, IDisposable
{
    private const int DaysInStepSummary = 7;
    private const int YesterdayOffsetDays = 1;
    private static readonly HKQuantityType StepsType = HKQuantityType.Create(HKQuantityTypeIdentifier.StepCount)!;
    private readonly HKHealthStore _store = new();

    public HealthDataSource Source => HealthDataSource.AppleHealth;
    public string DisplayName => "Apple Health";
    public string UnavailableMessage => "Apple Health is unavailable on this device.";
    public string EmptyDataMessage =>
        "No step counts were available from Apple Health. Check step access in Health settings and try again.";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(HKHealthStore.IsHealthDataAvailable);

    public async Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        if (!HKHealthStore.IsHealthDataAvailable)
            return false;

        cancellationToken.ThrowIfCancellationRequested();
        var completed = await MainThread.InvokeOnMainThreadAsync(async () =>
            await _store.RequestAuthorizationToShareAsync(null, new NSSet<HKObjectType>(StepsType)));
        cancellationToken.ThrowIfCancellationRequested();
        // Completion means the sheet closed successfully; it does not prove read permission was granted.
        return completed.Item1 && completed.Item2 is null;
    }

    public async Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(
        DateTime today,
        CancellationToken cancellationToken = default)
    {
        if (!HKHealthStore.IsHealthDataAvailable)
            return null;

        var end = today.Date.AddDays(1);
        var start = today.Date.AddDays(1 - DaysInStepSummary);
        var yesterday = today.Date.AddDays(-YesterdayOffsetDays);
        var stepsTask = ReadStepsAsync(start, end, cancellationToken);
        var yesterdayTask = ReadStepsAsync(yesterday, today.Date, cancellationToken);
        await Task.WhenAll(stepsTask, yesterdayTask);

        var steps = await stepsTask;
        if (steps is null)
            return null;

        var yesterdaySteps = await yesterdayTask;
        return new HealthWeeklySummary(
            Source,
            start,
            today.Date,
            SafeInt(steps.Value / DaysInStepSummary),
            0,
            0,
            0,
            0,
            DateTime.UtcNow,
            yesterdaySteps is null ? null : SafeInt(yesterdaySteps.Value));
    }

    private async Task<double?> ReadStepsAsync(DateTime start, DateTime end, CancellationToken cancellationToken)
    {
        var completion = new TaskCompletionSource<double?>(TaskCreationOptions.RunContinuationsAsynchronously);
        var predicate = HKQuery.GetPredicateForSamples(ToNSDate(start), ToNSDate(end), HKQueryOptions.StrictStartDate);
        using var query = new HKStatisticsQuery(StepsType, predicate, HKStatisticsOptions.CumulativeSum,
            (_, statistics, error) =>
            {
                var value = error is null ? statistics?.SumQuantity()?.GetDoubleValue(HKUnit.Count) : null;
                completion.TrySetResult(value is >= 0 && double.IsFinite(value.Value) ? value : null);
            });
        using var registration = cancellationToken.Register(() =>
        {
            _store.StopQuery(query);
            completion.TrySetCanceled(cancellationToken);
        });
        _store.ExecuteQuery(query);
        return await completion.Task;
    }

    private static NSDate ToNSDate(DateTime local) =>
        NSDate.FromTimeIntervalSince1970(new DateTimeOffset(local).ToUnixTimeSeconds());

    private static int SafeInt(double value) =>
        value > 0 && double.IsFinite(value)
            ? (int)Math.Clamp(Math.Round(value), 0, int.MaxValue)
            : 0;

    public void Dispose()
    {
        _store.Dispose();
        GC.SuppressFinalize(this);
    }
}

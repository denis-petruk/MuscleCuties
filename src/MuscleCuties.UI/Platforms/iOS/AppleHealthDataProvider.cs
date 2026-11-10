using Foundation;
using HealthKit;
using Microsoft.Maui.ApplicationModel;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.UI.Platforms.iOS;

public sealed class AppleHealthDataProvider : IHealthDataProvider, IHealthAuthorizationProvider, IHealthDataProviderDiagnostics, IDisposable
{
    private readonly HKHealthStore? _store;
    private bool _disposed;

    public AppleHealthDataProvider()
    {
        if (HKHealthStore.IsHealthDataAvailable)
        {
            _store = new HKHealthStore();
        }
    }

    public HealthDataSource Source => HealthDataSource.AppleHealth;

    public string DisplayName => "Apple Health";

    public string UnavailableMessage => "Apple Health is unavailable on this device.";

    public string EmptyDataMessage => "No health data was available from Apple Health. Check permissions in Health settings and try again.";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        return Task.FromResult(HKHealthStore.IsHealthDataAvailable);
    }

    public async Task<bool> RequestReadAuthorizationAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HKHealthStore.IsHealthDataAvailable || _store == null)
        {
            return false;
        }

        var stepType = HKQuantityType.Create(HKQuantityTypeIdentifier.StepCount);
        var energyType = HKQuantityType.Create(HKQuantityTypeIdentifier.ActiveEnergyBurned);
        var rhrType = HKQuantityType.Create(HKQuantityTypeIdentifier.RestingHeartRate);
        var hrvType = HKQuantityType.Create(HKQuantityTypeIdentifier.HeartRateVariabilitySdnn);
        var sleepType = HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis);

        var typesList = new List<NSObject>();
        if (stepType != null) typesList.Add(stepType);
        if (energyType != null) typesList.Add(energyType);
        if (rhrType != null) typesList.Add(rhrType);
        if (hrvType != null) typesList.Add(hrvType);
        if (sleepType != null) typesList.Add(sleepType);

        if (typesList.Count == 0)
        {
            return false;
        }

        var readTypes = new NSSet(typesList.ToArray());

        try
        {
            var (success, error) = await MainThread.InvokeOnMainThreadAsync(
                () => _store.RequestAuthorizationToShareAsync(new NSSet(), readTypes));

            return success && error == null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return false;
        }
    }

    public async Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(DateTime today, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!HKHealthStore.IsHealthDataAvailable || _store == null)
        {
            return null;
        }

        var start7d = today.Date.AddDays(-6);
        var end7d = today.Date.AddDays(1);

        var yesterdayStart = today.Date.AddDays(-1);
        var yesterdayEnd = today.Date;

        var lastNightStart = today.Date.AddDays(-1).AddHours(18);
        var lastNightEnd = today.Date.AddHours(12);

        var start3d = today.Date.AddDays(-2);

        var stepType = HKQuantityType.Create(HKQuantityTypeIdentifier.StepCount);
        var energyType = HKQuantityType.Create(HKQuantityTypeIdentifier.ActiveEnergyBurned);
        var rhrType = HKQuantityType.Create(HKQuantityTypeIdentifier.RestingHeartRate);
        var hrvType = HKQuantityType.Create(HKQuantityTypeIdentifier.HeartRateVariabilitySdnn);
        var sleepType = HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis);

        var steps7dPredicate = HKQuery.GetPredicateForSamples(ToNSDate(start7d), ToNSDate(end7d), HKQueryOptions.None);
        var stepsYesterdayPredicate = HKQuery.GetPredicateForSamples(ToNSDate(yesterdayStart), ToNSDate(yesterdayEnd), HKQueryOptions.None);
        var energy7dPredicate = HKQuery.GetPredicateForSamples(ToNSDate(start7d), ToNSDate(end7d), HKQueryOptions.None);
        var lastNightSleepPredicate = HKQuery.GetPredicateForSamples(ToNSDate(lastNightStart), ToNSDate(lastNightEnd), HKQueryOptions.None);
        var sleep7dPredicate = HKQuery.GetPredicateForSamples(ToNSDate(start7d), ToNSDate(end7d), HKQueryOptions.None);

        var past48hStart = today.Date.AddDays(-1);
        var past48hPredicate = HKQuery.GetPredicateForSamples(ToNSDate(past48hStart), ToNSDate(end7d), HKQueryOptions.None);

        var bpmUnit = HKUnit.Count.UnitDividedBy(HKUnit.Minute);
        var msUnit = HKUnit.CreateSecondUnit(HKMetricPrefix.Milli);

        var taskSteps7d = QuerySumAsync(stepType, steps7dPredicate, HKUnit.Count, cancellationToken);
        var taskStepsYesterday = QuerySumAsync(stepType, stepsYesterdayPredicate, HKUnit.Count, cancellationToken);
        var taskEnergy7d = QuerySumAsync(energyType, energy7dPredicate, HKUnit.Kilocalorie, cancellationToken);
        var taskLastNightSleep = QuerySamplesAsync(sleepType, lastNightSleepPredicate, 0, null, cancellationToken);
        var taskSleep7d = QuerySamplesAsync(sleepType, sleep7dPredicate, 0, null, cancellationToken);
        var taskRhr = QueryMostRecentQuantityAsync(rhrType, past48hPredicate, bpmUnit, cancellationToken);
        var taskHrv = QueryMostRecentQuantityAsync(hrvType, past48hPredicate, msUnit, cancellationToken);

        await Task.WhenAll(
            taskSteps7d,
            taskStepsYesterday,
            taskEnergy7d,
            taskLastNightSleep,
            taskSleep7d,
            taskRhr,
            taskHrv);

        cancellationToken.ThrowIfCancellationRequested();

        var totalSteps7d = await taskSteps7d;
        var totalStepsYesterday = await taskStepsYesterday;
        var totalEnergy7d = await taskEnergy7d;
        var lastNightSamples = await taskLastNightSleep;
        var sleep7dSamples = await taskSleep7d;
        var recentRhr = await taskRhr;
        var recentHrv = await taskHrv;

        int averageSteps = totalSteps7d.HasValue ? (int)Math.Round(totalSteps7d.Value / 7.0) : 0;
        int? yesterdaySteps = totalStepsYesterday.HasValue ? (int)Math.Round(totalStepsYesterday.Value) : null;
        double? averageActiveEnergyKcal = totalEnergy7d.HasValue ? Math.Round(totalEnergy7d.Value / 7.0, 1) : null;

        var lastNightHours = CalculateAsleepHours(lastNightSamples, lastNightStart, lastNightEnd, out var hasLastNightSleep);
        double? lastNightSleepHours = hasLastNightSleep ? Math.Round(lastNightHours, 1) : null;

        var total7dSleep = CalculateAsleepHours(sleep7dSamples, start7d, end7d, out var has7dSleep);
        var total3dSleep = CalculateAsleepHours(sleep7dSamples, start3d, end7d, out var has3dSleep);

        double averageSleepHours = has7dSleep ? Math.Round(total7dSleep / 7.0, 1) : 0.0;
        double? averageSleep3dHours = has3dSleep ? Math.Round(total3dSleep / 3.0, 1) : null;

        int sleepQualityScore = 0;
        if (lastNightSleepHours.HasValue)
        {
            var hours = lastNightSleepHours.Value;
            if (hours >= 8.0) sleepQualityScore = 5;
            else if (hours >= 7.0) sleepQualityScore = 4;
            else if (hours >= 6.0) sleepQualityScore = 3;
            else if (hours >= 4.5) sleepQualityScore = 2;
            else if (hours > 0.0) sleepQualityScore = 1;
            else sleepQualityScore = 0;
        }

        int restingHeartRate = recentRhr.HasValue ? (int)Math.Round(recentRhr.Value) : 0;
        int hrvScore = recentHrv.HasValue ? (int)Math.Round(recentHrv.Value) : 0;

        var hasAnyData = averageSteps > 0 ||
                         yesterdaySteps.HasValue ||
                         averageActiveEnergyKcal.HasValue ||
                         lastNightSleepHours.HasValue ||
                         averageSleepHours > 0 ||
                         averageSleep3dHours.HasValue ||
                         sleepQualityScore > 0 ||
                         restingHeartRate > 0 ||
                         hrvScore > 0;

        if (!hasAnyData)
        {
            return null;
        }

        return new HealthWeeklySummary(
            Source,
            start7d,
            today.Date,
            averageSteps,
            averageSleepHours,
            sleepQualityScore,
            restingHeartRate,
            hrvScore,
            DateTime.UtcNow,
            yesterdaySteps,
            lastNightSleepHours,
            averageActiveEnergyKcal,
            averageSleep3dHours);
    }

    private Task<double?> QuerySumAsync(
        HKQuantityType? quantityType,
        NSPredicate predicate,
        HKUnit unit,
        CancellationToken cancellationToken)
    {
        if (quantityType == null || _store == null)
        {
            return Task.FromResult<double?>(null);
        }

        var tcs = new TaskCompletionSource<double?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var query = new HKStatisticsQuery(quantityType, predicate, HKStatisticsOptions.CumulativeSum, (q, result, error) =>
        {
            var sum = result?.SumQuantity();
            if (error != null || sum == null)
            {
                tcs.TrySetResult(null);
                return;
            }

            try
            {
                var val = sum.GetDoubleValue(unit);
                if (double.IsNaN(val) || double.IsInfinity(val) || val < 0)
                {
                    tcs.TrySetResult(null);
                }
                else
                {
                    tcs.TrySetResult(val);
                }
            }
            catch
            {
                tcs.TrySetResult(null);
            }
        });

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                try { _store?.StopQuery(query); } catch { }
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        try
        {
            _store.ExecuteQuery(query);
        }
        catch
        {
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }

    private Task<HKSample[]?> QuerySamplesAsync(
        HKSampleType? sampleType,
        NSPredicate predicate,
        nuint limit,
        NSSortDescriptor[]? sortDescriptors,
        CancellationToken cancellationToken)
    {
        if (sampleType == null || _store == null)
        {
            return Task.FromResult<HKSample[]?>(null);
        }

        var tcs = new TaskCompletionSource<HKSample[]?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var query = new HKSampleQuery(sampleType, predicate, limit, sortDescriptors, (q, results, error) =>
        {
            if (error != null || results == null)
            {
                tcs.TrySetResult(null);
                return;
            }

            tcs.TrySetResult(results);
        });

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                try { _store?.StopQuery(query); } catch { }
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        try
        {
            _store.ExecuteQuery(query);
        }
        catch
        {
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }

    private Task<double?> QueryMostRecentQuantityAsync(
        HKQuantityType? quantityType,
        NSPredicate predicate,
        HKUnit unit,
        CancellationToken cancellationToken)
    {
        if (quantityType == null || _store == null)
        {
            return Task.FromResult<double?>(null);
        }

        var tcs = new TaskCompletionSource<double?>(TaskCreationOptions.RunContinuationsAsynchronously);

        var sortDescriptor = new NSSortDescriptor(HKSample.SortIdentifierEndDate, false);
        var query = new HKSampleQuery(quantityType, predicate, (nuint)1, new[] { sortDescriptor }, (q, results, error) =>
        {
            if (error != null || results == null || results.Length == 0)
            {
                tcs.TrySetResult(null);
                return;
            }

            try
            {
                if (results[0] is HKQuantitySample qs)
                {
                    var val = qs.Quantity.GetDoubleValue(unit);
                    if (double.IsNaN(val) || double.IsInfinity(val) || val <= 0)
                    {
                        tcs.TrySetResult(null);
                    }
                    else
                    {
                        tcs.TrySetResult(val);
                    }
                }
                else
                {
                    tcs.TrySetResult(null);
                }
            }
            catch
            {
                tcs.TrySetResult(null);
            }
        });

        if (cancellationToken.CanBeCanceled)
        {
            cancellationToken.Register(() =>
            {
                try { _store?.StopQuery(query); } catch { }
                tcs.TrySetCanceled(cancellationToken);
            });
        }

        try
        {
            _store.ExecuteQuery(query);
        }
        catch
        {
            tcs.TrySetResult(null);
        }

        return tcs.Task;
    }

    private static double CalculateAsleepHours(HKSample[]? samples, DateTime windowStart, DateTime windowEnd, out bool hasAnyAsleepSamples)
    {
        hasAnyAsleepSamples = false;
        if (samples == null || samples.Length == 0)
        {
            return 0.0;
        }

        var windowStartUtc = ToUtcDateTime(windowStart);
        var windowEndUtc = ToUtcDateTime(windowEnd);
        double totalSeconds = 0.0;

        foreach (var s in samples)
        {
            if (s is HKCategorySample cs && IsAsleepValue((nint)cs.Value))
            {
                hasAnyAsleepSamples = true;
                var sampleStart = ToDateTimeUtc(cs.StartDate);
                var sampleEnd = ToDateTimeUtc(cs.EndDate);

                var start = sampleStart < windowStartUtc ? windowStartUtc : sampleStart;
                var end = sampleEnd > windowEndUtc ? windowEndUtc : sampleEnd;

                if (end > start)
                {
                    totalSeconds += (end - start).TotalSeconds;
                }
            }
        }

        var hours = totalSeconds / 3600.0;
        return double.IsNaN(hours) || double.IsInfinity(hours) ? 0.0 : hours;
    }

    private static bool IsAsleepValue(nint value)
    {
        return value == (nint)HKCategoryValueSleepAnalysis.Asleep ||
               value == (nint)HKCategoryValueSleepAnalysis.AsleepCore ||
               value == (nint)HKCategoryValueSleepAnalysis.AsleepDeep ||
               value == (nint)HKCategoryValueSleepAnalysis.AsleepREM;
    }

    private static NSDate ToNSDate(DateTime dateTime)
    {
        var utc = ToUtcDateTime(dateTime);
        return (NSDate)utc;
    }

    private static DateTime ToUtcDateTime(DateTime dateTime)
    {
        return dateTime.Kind == DateTimeKind.Utc
            ? dateTime
            : DateTime.SpecifyKind(dateTime, DateTimeKind.Local).ToUniversalTime();
    }

    private static DateTime ToDateTimeUtc(NSDate nsDate)
    {
        return ((DateTime)nsDate).ToUniversalTime();
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _store?.Dispose();
    }
}

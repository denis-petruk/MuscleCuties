using MuscleCuties.Core.Services.Health;

#if IOS
using Foundation;
using HealthKit;
#endif

namespace MuscleCuties.App.Services.Health;

public sealed class AppleHealthDataProvider : IHealthDataProvider, IHealthDataProviderDiagnostics
{
    public HealthDataSource Source => HealthDataSource.AppleHealth;
    public string DisplayName => "Apple Health";
    public string UnavailableMessage => "Apple Health is available on iPhone after HealthKit is enabled for the app and the user allows access.";
    public string EmptyDataMessage => "Apple Health is connected, but it has not shared steps, sleep, resting heart rate, or HRV yet.";

    public Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
#if IOS
        return Task.FromResult(HKHealthStore.IsHealthDataAvailable);
#else
        return Task.FromResult(false);
#endif
    }

    public async Task<HealthWeeklySummary?> ReadWeeklySummaryAsync(
        DateTime today,
        CancellationToken cancellationToken = default)
    {
#if IOS
        try
        {
            if (!HKHealthStore.IsHealthDataAvailable)
                return null;

            var store = new HKHealthStore();
            var stepType = HKQuantityType.Create(HKQuantityTypeIdentifier.StepCount);
            var sleepType = HKCategoryType.Create(HKCategoryTypeIdentifier.SleepAnalysis);
            var restingHeartRateType = HKQuantityType.Create(HKQuantityTypeIdentifier.RestingHeartRate);
            var hrvType = HKQuantityType.Create(HKQuantityTypeIdentifier.HeartRateVariabilitySdnn);
            if (stepType is null || sleepType is null)
                return null;

            var requestedTypes = new List<HKObjectType> { stepType, sleepType };
            if (restingHeartRateType is not null)
                requestedTypes.Add(restingHeartRateType);
            if (hrvType is not null)
                requestedTypes.Add(hrvType);

            var readTypes = new NSSet<HKObjectType>(requestedTypes.ToArray());
            var authorized = await RequestAuthorizationAsync(store, readTypes);
            if (!authorized)
                return null;

            var end = today.Date.AddDays(1);
            var start = end.AddDays(-7);
            var averageSteps = await ReadAverageStepsAsync(store, stepType, start, end);
            var averageSleepHours = await ReadAverageSleepHoursAsync(store, sleepType, start, end);
            var restingHeartRate = restingHeartRateType is null
                ? 0
                : await ReadAverageQuantityAsync(
                    store,
                    restingHeartRateType,
                    HKUnit.Count.UnitDividedBy(HKUnit.Minute),
                    start,
                    end);
            var hrvScore = hrvType is null
                ? 0
                : await ReadAverageQuantityAsync(
                    store,
                    hrvType,
                    HKUnit.CreateSecondUnit(HKMetricPrefix.Milli),
                    start,
                    end);

            if (averageSteps <= 0 && averageSleepHours <= 0 && restingHeartRate <= 0 && hrvScore <= 0)
                return null;

            var sleepScore = averageSleepHours switch
            {
                >= 8.0 => 92,
                >= 7.0 => 84,
                >= 6.0 => 72,
                >= 5.0 => 58,
                _ => 45
            };

            return new HealthWeeklySummary(
                Source,
                start,
                end.AddDays(-1),
                averageSteps,
                averageSleepHours,
                sleepScore,
                restingHeartRate,
                hrvScore,
                DateTime.UtcNow);
        }
        catch
        {
            return null;
        }
#else
        await Task.CompletedTask;
        return null;
#endif
    }

#if IOS
    private static Task<bool> RequestAuthorizationAsync(HKHealthStore store, NSSet<HKObjectType> readTypes)
    {
        var tcs = new TaskCompletionSource<bool>();
        store.RequestAuthorizationToShare(null, readTypes, (success, _) => tcs.TrySetResult(success));
        return tcs.Task;
    }

    private static Task<int> ReadAverageStepsAsync(
        HKHealthStore store,
        HKQuantityType stepType,
        DateTime start,
        DateTime end)
    {
        var tcs = new TaskCompletionSource<int>();
        var predicate = HKQuery.GetPredicateForSamples(ToNSDate(start), ToNSDate(end), HKQueryOptions.StrictStartDate);
        var query = new HKStatisticsQuery(stepType, predicate, HKStatisticsOptions.CumulativeSum, (_, result, error) =>
        {
            if (error is not null)
            {
                tcs.TrySetResult(0);
                return;
            }

            var steps = result?.SumQuantity()?.GetDoubleValue(HKUnit.Count) ?? 0d;
            tcs.TrySetResult((int)Math.Round(steps / 7d));
        });

        store.ExecuteQuery(query);
        return tcs.Task;
    }

    private static Task<double> ReadAverageSleepHoursAsync(
        HKHealthStore store,
        HKCategoryType sleepType,
        DateTime start,
        DateTime end)
    {
        var tcs = new TaskCompletionSource<double>();
        var predicate = HKQuery.GetPredicateForSamples(ToNSDate(start), ToNSDate(end), HKQueryOptions.StrictStartDate);
        var query = new HKSampleQuery(sleepType, predicate, nuint.MaxValue, [], (_, samples, error) =>
        {
            if (error is not null || samples is null)
            {
                tcs.TrySetResult(0d);
                return;
            }

            var hours = samples
                .OfType<HKCategorySample>()
                .Where(IsAsleepSample)
                .Sum(sample => Math.Max(0d, sample.EndDate.SecondsSinceReferenceDate - sample.StartDate.SecondsSinceReferenceDate) / 3600d);

            tcs.TrySetResult(Math.Round(hours / 7d, 1));
        });

        store.ExecuteQuery(query);
        return tcs.Task;
    }

    private static Task<int> ReadAverageQuantityAsync(
        HKHealthStore store,
        HKQuantityType quantityType,
        HKUnit unit,
        DateTime start,
        DateTime end)
    {
        var tcs = new TaskCompletionSource<int>();
        var predicate = HKQuery.GetPredicateForSamples(ToNSDate(start), ToNSDate(end), HKQueryOptions.StrictStartDate);
        var query = new HKStatisticsQuery(quantityType, predicate, HKStatisticsOptions.DiscreteAverage, (_, result, error) =>
        {
            if (error is not null)
            {
                tcs.TrySetResult(0);
                return;
            }

            var average = result?.AverageQuantity()?.GetDoubleValue(unit) ?? 0d;
            tcs.TrySetResult((int)Math.Round(average));
        });

        store.ExecuteQuery(query);
        return tcs.Task;
    }

    private static bool IsAsleepSample(HKCategorySample sample)
    {
        var valueName = Enum.GetName(typeof(HKCategoryValueSleepAnalysis), (int)sample.Value);
        return valueName?.Contains("Asleep", StringComparison.OrdinalIgnoreCase) == true;
    }

    private static NSDate ToNSDate(DateTime date)
    {
        var utcDate = DateTime.SpecifyKind(date, DateTimeKind.Local).ToUniversalTime();
        return NSDate.FromTimeIntervalSince1970(new DateTimeOffset(utcDate).ToUnixTimeSeconds());
    }
#endif
}

using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Cycle;

public sealed record CycleObservation(DateTime StartDate, DateTime? EndDate, int CycleLength);

public sealed class CyclePredictionDomainRule
{
    private const int HistoryCyclesToUse = 6;
    private static readonly DateTime MinValidDate = new(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime MaxValidDate = new(2100, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    public static CyclePredictionEstimate CreatePrediction(
        CycleObservation? latestCycle,
        IReadOnlyCollection<CycleObservation>? history,
        int? profileCycleLength,
        DateTime today,
        CyclePhaseObservation? manualPhaseLog = null)
    {
        var validToday = IsValidDate(today);
        var (cycleLength, source) = PredictCycleLength(history, profileCycleLength, validToday ? today.Date : null);

        if (!validToday || latestCycle is null || !IsValidDate(latestCycle.StartDate) ||
            latestCycle.StartDate.Date > today.Date ||
            (latestCycle.EndDate is { } endDate &&
             (!IsValidDate(endDate) || endDate.Date < latestCycle.StartDate.Date)) ||
            (today.Date - latestCycle.StartDate.Date).Days > CycleLengthLimits.Maximum * 3)
            return ApplyManualPhase(new CyclePredictionEstimate
            {
                HasActiveCycle = false,
                CurrentDay = 0,
                PredictedCycleLength = cycleLength,
                CurrentPhase = CyclePhase.Unknown,
                DaysUntilPeriod = 0,
                PredictionSource = source
            }, manualPhaseLog, today);

        var startDate = latestCycle.StartDate.Date;
        var safeToday = today.Date;
        var daysDiff = (safeToday - startDate).Days;
        var currentDay = Math.Clamp(daysDiff + 1, 1, CycleLengthLimits.Maximum * 3);

        var nextPeriodDate = SafeAddDays(startDate, cycleLength);
        var daysUntilPeriod = Math.Max(0, (nextPeriodDate - safeToday).Days);
        var ovulationDate = SafeAddDays(startDate, Math.Max(0, cycleLength - 14));

        return ApplyManualPhase(new CyclePredictionEstimate
        {
            HasActiveCycle = true,
            CurrentCycleStartDate = startDate,
            CurrentDay = currentDay,
            PredictedCycleLength = cycleLength,
            CurrentPhase = CyclePhaseRule.CalculatePhase(currentDay, cycleLength),
            PredictedNextPeriodDate = nextPeriodDate,
            DaysUntilPeriod = daysUntilPeriod,
            PredictedOvulationDate = ovulationDate,
            FertileWindowStartDate = SafeAddDays(ovulationDate, -5),
            FertileWindowEndDate = SafeAddDays(ovulationDate, 1),
            PredictionSource = source
        }, manualPhaseLog, today);
    }

    private static CyclePredictionEstimate ApplyManualPhase(
        CyclePredictionEstimate prediction, CyclePhaseObservation? manualPhaseLog, DateTime today)
    {
        if (manualPhaseLog is not { } log || !IsValidDate(log.LoggedAt) ||
            log.LoggedAt.Date > today.Date || log.LoggedAt == default ||
            log.Phase == CyclePhase.Unknown || !Enum.IsDefined(log.Phase))
            return prediction;

        var normalizedLength = CyclePhaseRule.NormalizeCycleLength(prediction.PredictedCycleLength);
        if ((today.Date - log.LoggedAt.Date).Days > normalizedLength * 3)
            return prediction;

        var phase = CyclePhaseRule.ProjectPhaseFromLog(log, today, normalizedLength);
        if (phase == CyclePhase.Unknown)
            return prediction;

        return new CyclePredictionEstimate
        {
            HasActiveCycle = prediction.HasActiveCycle,
            CurrentCycleStartDate = prediction.CurrentCycleStartDate,
            CurrentDay = prediction.CurrentDay,
            PredictedCycleLength = prediction.PredictedCycleLength,
            CurrentPhase = phase,
            PredictedNextPeriodDate = prediction.PredictedNextPeriodDate,
            DaysUntilPeriod = prediction.DaysUntilPeriod,
            PredictedOvulationDate = prediction.PredictedOvulationDate,
            FertileWindowStartDate = prediction.FertileWindowStartDate,
            FertileWindowEndDate = prediction.FertileWindowEndDate,
            PredictionSource = "manual phase log"
        };
    }

    private static (int CycleLength, string Source) PredictCycleLength(
        IReadOnlyCollection<CycleObservation>? history,
        int? profileCycleLength,
        DateTime? today)
    {
        if (history != null && history.Count > 0 && today is not null)
        {
            var measuredLengths = history
                .Where(cycle => cycle != null && IsValidDate(cycle.StartDate) &&
                                cycle.StartDate.Date <= today.Value && IsUsableCycleLength(cycle.CycleLength) &&
                                (cycle.EndDate is null || (IsValidDate(cycle.EndDate.Value) &&
                                                           cycle.EndDate.Value.Date >= cycle.StartDate.Date)))
                .OrderByDescending(cycle => cycle.StartDate)
                .Take(HistoryCyclesToUse)
                .Select(cycle => cycle.CycleLength)
                .ToList();

            if (measuredLengths.Count > 0)
                return (ClampCycleLength((int)Math.Round(measuredLengths.Average())), "recent cycles");

            var startDateLengths = CalculateStartDateLengths(history, today.Value)
                .Where(IsUsableCycleLength)
                .TakeLast(HistoryCyclesToUse)
                .ToList();

            if (startDateLengths.Count > 0)
                return (ClampCycleLength((int)Math.Round(startDateLengths.Average())), "cycle start history");
        }

        if (profileCycleLength is not null && IsUsableCycleLength(profileCycleLength.Value))
            return (profileCycleLength.Value, "profile");

        // This source label is surfaced as an estimate, never a reported length.
        return (CycleLengthLimits.Default, "default");
    }

    private static bool IsUsableCycleLength(int cycleLength)
    {
        return cycleLength is >= CycleLengthLimits.Minimum and <= CycleLengthLimits.Maximum;
    }

    private static int ClampCycleLength(int cycleLength)
    {
        return CyclePhaseRule.NormalizeCycleLength(cycleLength);
    }

    private static bool IsValidDate(DateTime date)
    {
        return date >= MinValidDate && date <= MaxValidDate;
    }

    private static DateTime SafeAddDays(DateTime date, double days)
    {
        try
        {
            if (days > 0 && (DateTime.MaxValue - date).TotalDays < days)
                return DateTime.MaxValue.Date;
            if (days < 0 && (date - DateTime.MinValue).TotalDays < Math.Abs(days))
                return DateTime.MinValue.Date;
            return date.AddDays(days);
        }
        catch (ArgumentOutOfRangeException)
        {
            return days > 0 ? DateTime.MaxValue.Date : DateTime.MinValue.Date;
        }
    }

    private static IEnumerable<int> CalculateStartDateLengths(IEnumerable<CycleObservation> history, DateTime today)
    {
        var orderedCycles = history
            .Where(cycle => cycle != null && IsValidDate(cycle.StartDate) &&
                            cycle.StartDate.Date <= today &&
                            (cycle.EndDate is null || (IsValidDate(cycle.EndDate.Value) &&
                                                       cycle.EndDate.Value.Date >= cycle.StartDate.Date)))
            .OrderBy(cycle => cycle.StartDate)
            .ToList();

        for (var i = 1; i < orderedCycles.Count; i++)
        {
            var diff = (orderedCycles[i].StartDate.Date - orderedCycles[i - 1].StartDate.Date).Days;
            if (diff > 0)
                yield return diff;
        }
    }
}

using System.Globalization;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Cycle.Planning;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Cycle.Overview;

namespace MuscleCuties.UI.Services.Notifications;

public sealed class CyclePhaseNotificationService : ICyclePhaseNotificationService
{
    private const string ManualPredictionSource = "manual phase log";

    private static readonly TimeSpan MorningNotificationTime = new(8, 0, 0);

    private readonly ICycleService? _cycleService;
    private readonly ICycleOverviewUseCase? _cycleOverview;
    private readonly ILocalNotificationService _localNotificationService;

    public CyclePhaseNotificationService(
        ICycleService cycleService,
        ILocalNotificationService localNotificationService)
    {
        _cycleService = cycleService;
        _localNotificationService = localNotificationService;
    }

    public CyclePhaseNotificationService(
        ICycleOverviewUseCase cycleOverview,
        ILocalNotificationService localNotificationService)
    {
        _cycleOverview = cycleOverview;
        _localNotificationService = localNotificationService;
    }

    public async Task NotifyIfPhaseChangedAsync(int userId)
    {
        if (userId <= 0)
            return;

        // Remove phase alerts scheduled by older releases before replacing
        // them with discreet copy.
        await _localNotificationService.CancelAsync(BuildTodayNotificationId(userId));
        await _localNotificationService.CancelAsync(BuildNextNotificationId(userId));

        CyclePhase currentPhase;
        CyclePrediction? prediction = null;
        CyclePredictionSummary? predictionSummary = null;

        if (_cycleOverview is not null)
        {
            var todayDate = DateTime.Today;
            var overview = await _cycleOverview.LoadAsync(todayDate.AddDays(-35), todayDate.AddDays(35), CancellationToken.None);
            if (overview is null)
                return;
            predictionSummary = overview.Prediction;
            currentPhase = predictionSummary.CurrentPhase;
        }
        else if (_cycleService is not null)
        {
            prediction = await _cycleService.GetPredictionAsync(userId);
            currentPhase = prediction.CurrentPhase;
        }
        else
        {
            return;
        }

        var lastPhaseKey = BuildLastPhaseKey(userId);
        var previousPhaseValue = await GetSecureMarkerAsync(lastPhaseKey) ?? string.Empty;

        if (!Enum.TryParse<CyclePhase>(previousPhaseValue, out var previousPhase))
        {
            await SetSecureMarkerAsync(lastPhaseKey, currentPhase.ToString());
            await ScheduleNextPhaseChangeReminderAsync(userId, prediction, predictionSummary);
            return;
        }

        if (previousPhase == currentPhase)
        {
            await ScheduleNextPhaseChangeReminderAsync(userId, prediction, predictionSummary);
            return;
        }

        var today = DateTime.Today;
        var dateKey = BuildLastNotificationDateKey(userId);
        var todayValue = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await GetSecureMarkerAsync(dateKey) == todayValue)
        {
            await SetSecureMarkerAsync(lastPhaseKey, currentPhase.ToString());
            await ScheduleNextPhaseChangeReminderAsync(userId, prediction, predictionSummary);
            return;
        }

        var title = UiTextCatalog.Translate("Your plan is ready");
        var body = UiTextCatalog.Translate("Open MuscleCuties to see today's update.");
        var morning = today.Add(MorningNotificationTime);
        var notificationId = BuildTodayNotificationId(userId);

        var notificationHandled = DateTime.Now < morning
            ? await _localNotificationService.ScheduleAsync(notificationId, title, body, morning)
            : await _localNotificationService.ShowAsync(notificationId, title, body);

        if (!notificationHandled)
            return;

        await SetSecureMarkerAsync(lastPhaseKey, currentPhase.ToString());
        await SetSecureMarkerAsync(dateKey, todayValue);

        await ScheduleNextPhaseChangeReminderAsync(userId, prediction, predictionSummary);
    }

    private async Task ScheduleNextPhaseChangeReminderAsync(
        int userId,
        CyclePrediction? prediction,
        CyclePredictionSummary? predictionSummary)
    {
        PhaseChangeReminder? reminder = null;
        if (prediction is not null)
        {
            reminder = await FindNextPhaseChangeReminderAsync(userId, prediction);
        }
        else if (predictionSummary is not null)
        {
            reminder = FindNextPhaseChangeReminderFromSummary(predictionSummary);
        }

        if (reminder is null)
            return;

        var scheduledAt = reminder.Value.Date.Date.Add(MorningNotificationTime);
        if (scheduledAt <= DateTime.Now)
            return;

        await _localNotificationService.ScheduleAsync(
            BuildNextNotificationId(userId),
            UiTextCatalog.Translate("Your plan is ready"),
            UiTextCatalog.Translate("Open MuscleCuties to see today's update."),
            scheduledAt);
    }

    private static PhaseChangeReminder? FindNextPhaseChangeReminderFromSummary(CyclePredictionSummary prediction)
    {
        if (!prediction.HasActiveCycle || prediction.CurrentDay <= 0)
            return null;

        var cycleLength = CyclePhaseRules.NormalizeCycleLength(prediction.PredictedCycleLength);
        var today = DateTime.Today;

        for (var dayOffset = 1; dayOffset <= cycleLength; dayOffset++)
        {
            var date = today.AddDays(dayOffset);
            var projectedCycleDay = (prediction.CurrentDay - 1 + dayOffset) % cycleLength + 1;
            var phase = CyclePhaseRules.CalculatePhase(projectedCycleDay, cycleLength);
            if (phase != prediction.CurrentPhase)
                return new PhaseChangeReminder(date, phase);
        }

        return null;
    }

    private async Task<PhaseChangeReminder?> FindNextPhaseChangeReminderAsync(int userId, CyclePrediction prediction)
    {
        if (!prediction.HasActiveCycle || prediction.CurrentDay <= 0)
            return null;

        var cycleLength = CyclePhaseRules.NormalizeCycleLength(prediction.PredictedCycleLength);
        var today = DateTime.Today;
        var latestPhaseLog = _cycleService is not null && prediction.PredictionSource == ManualPredictionSource
            ? await _cycleService.GetLatestPhaseLogAsync(userId)
            : null;

        for (var dayOffset = 1; dayOffset <= cycleLength; dayOffset++)
        {
            var date = today.AddDays(dayOffset);
            var phase = ResolveFuturePhase(prediction, latestPhaseLog, date, dayOffset, cycleLength);
            if (phase != prediction.CurrentPhase)
                return new PhaseChangeReminder(date, phase);
        }

        return null;
    }

    private static CyclePhase ResolveFuturePhase(
        CyclePrediction prediction,
        CyclePhaseLog? latestPhaseLog,
        DateTime date,
        int dayOffset,
        int cycleLength)
    {
        if (latestPhaseLog is not null)
            return CyclePhaseRules.ProjectPhaseFromLog(
                new CyclePhaseLogProjection(latestPhaseLog.Phase, latestPhaseLog.LoggedAt),
                date,
                cycleLength);

        var projectedCycleDay = (prediction.CurrentDay - 1 + dayOffset) % cycleLength + 1;
        return CyclePhaseRules.CalculatePhase(projectedCycleDay, cycleLength);
    }

    private static string BuildLastPhaseKey(int userId)
    {
        return $"cycle.phase.last.{userId}";
    }

    private static string BuildLastNotificationDateKey(int userId)
    {
        return $"cycle.phase.notificationDate.{userId}";
    }

    private static async Task<string?> GetSecureMarkerAsync(string key)
    {
        try
        {
            return await SecureStorage.Default.GetAsync(key);
        }
        catch
        {
            return null;
        }
    }

    private static async Task SetSecureMarkerAsync(string key, string value)
    {
        try
        {
            await SecureStorage.Default.SetAsync(key, value);
        }
        catch
        {
            // Notification markers are privacy-sensitive and optional. If secure
            // storage is unavailable, skip persistence instead of falling back
            // to unencrypted Preferences.
        }
    }

    private static int BuildTodayNotificationId(int userId)
    {
        return 740_000 + Math.Abs(userId % 10_000);
    }

    private static int BuildNextNotificationId(int userId)
    {
        return 760_000 + Math.Abs(userId % 10_000);
    }

    private readonly record struct PhaseChangeReminder(DateTime Date, CyclePhase Phase);
}

using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;
using FlowIntensity = MuscleCuties.UseCases.Features.Cycle.Contracts.FlowIntensityDto;
using SymptomType = MuscleCuties.UseCases.Features.Cycle.Contracts.SymptomTypeDto;
using MuscleCuties.UseCases.Features.Cycle.Overview;

namespace MuscleCuties.UI.Services.Notifications;

/// <summary>Alerts the person when their cycle phase changes, at most once a day, and schedules the next</summary>
/// change in advance. Copy stays discreet: it never names the phase.</summary>
public sealed class CyclePhaseNotificationService : ICyclePhaseNotificationService
{
    private static readonly TimeSpan MorningNotificationTime = new(8, 0, 0);

    private readonly ICycleOverviewUseCase _cycleOverview;
    private readonly ICyclePhaseProjectionUseCase _phaseProjection;
    private readonly ILocalNotificationService _localNotificationService;

    /// <summary>Takes the use cases and services that schedule phase reminders.</summary>
    public CyclePhaseNotificationService(
        ICycleOverviewUseCase cycleOverview,
        ICyclePhaseProjectionUseCase phaseProjection,
        ILocalNotificationService localNotificationService)
    {
        _cycleOverview = cycleOverview;
        _phaseProjection = phaseProjection;
        _localNotificationService = localNotificationService;
    }

    /// <summary>Cancels yesterday's alerts, compares the current phase with the last one recorded, and only</summary>
    /// notifies on a change that has not already been announced today. Markers live in secure storage.</summary>
    public async Task NotifyIfPhaseChangedAsync(int userId)
    {
        if (userId <= 0)
            return;

        // Remove phase alerts scheduled by older releases before replacing
        // them with discreet copy.
        await _localNotificationService.CancelAsync(BuildTodayNotificationId(userId));
        await _localNotificationService.CancelAsync(BuildNextNotificationId(userId));

        CyclePhase currentPhase;
        CyclePredictionSummary? predictionSummary = null;

        var todayDate = DateTime.Today;
        var overview = await _cycleOverview.LoadAsync(
            todayDate.AddDays(-35), todayDate.AddDays(35), CancellationToken.None);
        if (overview is null)
            return;
        predictionSummary = overview.Prediction;
        currentPhase = predictionSummary.CurrentPhase;

        var lastPhaseKey = BuildLastPhaseKey(userId);
        var previousPhaseValue = await GetSecureMarkerAsync(lastPhaseKey) ?? string.Empty;

        if (!Enum.TryParse<CyclePhase>(previousPhaseValue, out var previousPhase))
        {
            await SetSecureMarkerAsync(lastPhaseKey, currentPhase.ToString());
            await ScheduleNextPhaseChangeReminderAsync(userId, predictionSummary);
            return;
        }

        if (previousPhase == currentPhase)
        {
            await ScheduleNextPhaseChangeReminderAsync(userId, predictionSummary);
            return;
        }

        var today = DateTime.Today;
        var dateKey = BuildLastNotificationDateKey(userId);
        var todayValue = today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (await GetSecureMarkerAsync(dateKey) == todayValue)
        {
            await SetSecureMarkerAsync(lastPhaseKey, currentPhase.ToString());
            await ScheduleNextPhaseChangeReminderAsync(userId, predictionSummary);
            return;
        }

        var title = UiTextCatalog.TranslateRuntimeText("Your plan is ready");
        var body = UiTextCatalog.TranslateRuntimeText("Open MuscleCuties to see today's update.");
        var morning = today.Add(MorningNotificationTime);
        var notificationId = BuildTodayNotificationId(userId);

        var notificationHandled = DateTime.Now < morning
            ? await _localNotificationService.ScheduleAsync(notificationId, title, body, morning)
            : await _localNotificationService.ShowAsync(notificationId, title, body);

        if (!notificationHandled)
            return;

        await SetSecureMarkerAsync(lastPhaseKey, currentPhase.ToString());
        await SetSecureMarkerAsync(dateKey, todayValue);

        await ScheduleNextPhaseChangeReminderAsync(userId, predictionSummary);
    }

    /// <summary>Schedules a reminder for the next phase change.</summary>
    private async Task ScheduleNextPhaseChangeReminderAsync(
        int userId,
        CyclePredictionSummary? predictionSummary)
    {
        var reminder = predictionSummary is null
            ? null
            : FindNextPhaseChangeReminder(predictionSummary);

        if (reminder is null)
            return;

        var scheduledAt = reminder.Value.Date.Date.Add(MorningNotificationTime);
        if (scheduledAt <= DateTime.Now)
            return;

        await _localNotificationService.ScheduleAsync(
            BuildNextNotificationId(userId),
            UiTextCatalog.TranslateRuntimeText("Your plan is ready"),
            UiTextCatalog.TranslateRuntimeText("Open MuscleCuties to see today's update."),
            scheduledAt);
    }

    /// <summary>The first day ahead whose projected phase differs from today's, or null within one cycle.</summary>
    private PhaseChangeReminder? FindNextPhaseChangeReminder(CyclePredictionSummary prediction)
    {
        if (!prediction.HasActiveCycle || prediction.CurrentDay <= 0)
            return null;

        var today = DateTime.Today;
        var cycleLength = _phaseProjection.Project(new CyclePhaseProjectionInputDto(
            today,
            today,
            prediction.PredictedCycleLength,
            CycleDayOverride: prediction.CurrentDay,
            CycleDayOverrideDate: today,
            CurrentPhase: prediction.CurrentPhase,
            HasActiveCycle: prediction.HasActiveCycle)).NormalizedCycleLength;

        for (var dayOffset = 1; dayOffset <= cycleLength; dayOffset++)
        {
            var date = today.AddDays(dayOffset);
            var phase = _phaseProjection.Project(new CyclePhaseProjectionInputDto(
                date,
                today,
                prediction.PredictedCycleLength,
                CycleDayOverride: prediction.CurrentDay,
                CycleDayOverrideDate: today,
                CurrentPhase: prediction.CurrentPhase,
                HasActiveCycle: prediction.HasActiveCycle)).Phase;
            if (phase != prediction.CurrentPhase)
                return new PhaseChangeReminder(date, phase);
        }

        return null;
    }

    /// <summary>Secure-storage key holding the last announced phase, per account.</summary>
    private static string BuildLastPhaseKey(int userId)
    {
        return $"cycle.phase.last.{userId}";
    }

    /// <summary>Secure-storage key holding the date last announced, which enforces one alert per day.</summary>
    private static string BuildLastNotificationDateKey(int userId)
    {
        return $"cycle.phase.notificationDate.{userId}";
    }

    /// <summary>Reads a marker, treating an unavailable keychain as "no marker yet".</summary>
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

    /// <summary>Writes a marker. If secure storage is unavailable the write is skipped rather than falling back</summary>
    /// to plain preferences, because the value reveals cycle state.</summary>
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

    /// <summary>Id of today's alert, derived from the account so two accounts cannot cancel each other.</summary>
    private static int BuildTodayNotificationId(int userId)
    {
        return 740_000 + Math.Abs(userId % 10_000);
    }

    /// <summary>Id of the scheduled next-change alert.</summary>
    private static int BuildNextNotificationId(int userId)
    {
        return 760_000 + Math.Abs(userId % 10_000);
    }

    /// <summary>The date of the next phase change and the phase it moves to.</summary>
    private readonly record struct PhaseChangeReminder(DateTime Date, CyclePhase Phase);
}

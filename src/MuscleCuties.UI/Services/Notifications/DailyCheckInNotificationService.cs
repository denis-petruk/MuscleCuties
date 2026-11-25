using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UI.Services.Notifications;

/// <summary>Schedules the morning check-in reminder, but only when today's check-in is still open.</summary>
public sealed class DailyCheckInNotificationService : IDailyCheckInNotificationService
{
    private const int CheckInReminderId = 9001;
    private const int CheckInReminderHour = 8;

    private readonly ILocalNotificationService _notifications;
    private readonly IDailyCheckInStore _checkInStore;

    /// <summary>Takes the notification service and the check-in store.</summary>
    public DailyCheckInNotificationService(
        ILocalNotificationService notifications,
        IDailyCheckInStore checkInStore)
    {
        _notifications = notifications;
        _checkInStore = checkInStore;
    }

    /// <summary>Asks the check-in store whether today is done, then schedules for 8 a.m. today or tomorrow.</summary>
    public async Task ScheduleCheckInReminderAsync(int userId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var status = await _checkInStore.GetStatusAsync(
            new DailyCheckInStatusRequest(today, TimeZoneInfo.Local.Id), CancellationToken.None).ConfigureAwait(false);
        if (status.IsCompleted)
            return;

        var scheduledTime = DateTime.Today.AddHours(CheckInReminderHour);
        if (scheduledTime <= DateTime.Now)
            scheduledTime = scheduledTime.AddDays(1);

        await _notifications.ScheduleAsync(
            CheckInReminderId,
            UiTextCatalog.TranslateRuntimeText("Time for your daily check-in"),
            UiTextCatalog.TranslateRuntimeText("Log how you slept and how you feel to get your personalized plan."),
            scheduledTime).ConfigureAwait(false);
    }
}

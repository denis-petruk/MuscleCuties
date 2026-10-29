using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UI.Services.Notifications;

public sealed class DailyCheckInNotificationService : IDailyCheckInNotificationService
{
    private const int CheckInReminderId = 9001;
    private const int CheckInReminderHour = 8;

    private readonly ILocalNotificationService _notifications;
    private readonly IDailyCheckInStore _checkInStore;

    public DailyCheckInNotificationService(
        ILocalNotificationService notifications,
        IDailyCheckInStore checkInStore)
    {
        _notifications = notifications;
        _checkInStore = checkInStore;
    }

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
            UiTextCatalog.Translate("Time for your daily check-in"),
            UiTextCatalog.Translate("Log how you slept and how you feel to get your personalized plan."),
            scheduledTime).ConfigureAwait(false);
    }
}

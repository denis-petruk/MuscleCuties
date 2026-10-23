using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Notifications;
using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UI.Services.Notifications;

public sealed class DailyCheckInNotificationService : IDailyCheckInNotificationService
{
    private const int CheckInReminderId = 9001;
    private readonly ILocalNotificationService _notifications;
    private readonly IReadinessRepository? _dailyRepo;
    private readonly IDailyCheckInStore? _checkInStore;

    public DailyCheckInNotificationService(
        ILocalNotificationService notifications,
        IReadinessRepository dailyRepo)
    {
        _notifications = notifications;
        _dailyRepo = dailyRepo;
    }

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
        if (_checkInStore is not null)
        {
            var status = await _checkInStore.GetStatusAsync(new DailyCheckInStatusRequest(today), CancellationToken.None).ConfigureAwait(false);
            if (status.IsCompleted)
                return;
        }
        else if (_dailyRepo is not null)
        {
            var existing = await _dailyRepo.GetInputsForAsync(userId, today).ConfigureAwait(false);
            if (existing is not null)
                return;
        }

        var scheduledTime = DateTime.Today.AddHours(8);
        if (scheduledTime <= DateTime.Now)
            scheduledTime = scheduledTime.AddDays(1);

        await _notifications.ScheduleAsync(
            CheckInReminderId,
            UiTextCatalog.Translate("Time for your daily check-in"),
            UiTextCatalog.Translate("Log how you slept and how you feel to get your personalized plan."),
            scheduledTime).ConfigureAwait(false);
    }
}

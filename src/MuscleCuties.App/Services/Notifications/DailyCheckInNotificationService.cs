using MuscleCuties.Core.Repositories.Workout.Planning;
using MuscleCuties.Core.Services.Notifications;

namespace MuscleCuties.App.Services.Notifications;

public sealed class DailyCheckInNotificationService : IDailyCheckInNotificationService
{
    private const int CheckInReminderId = 9001;

    private readonly ILocalNotificationService _notifications;
    private readonly IReadinessRepository _dailyRepo;

    public DailyCheckInNotificationService(
        ILocalNotificationService notifications,
        IReadinessRepository dailyRepo)
    {
        _notifications = notifications;
        _dailyRepo = dailyRepo;
    }

    public async Task ScheduleCheckInReminderAsync(int userId)
    {
        var today = DateOnly.FromDateTime(DateTime.Today);
        var existing = await _dailyRepo.GetInputsForAsync(userId, today);
        if (existing is not null)
            return;

        var scheduledTime = DateTime.Today.AddHours(8);
        if (scheduledTime <= DateTime.Now)
            scheduledTime = scheduledTime.AddDays(1);

        await _notifications.ScheduleAsync(
            CheckInReminderId,
            "Time for your daily check-in",
            "Log how you slept and how you feel to get your personalized plan.",
            scheduledTime);
    }

}

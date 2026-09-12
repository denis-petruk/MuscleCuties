namespace MuscleCuties.Core.Services.Notifications;

public interface IDailyCheckInNotificationService
{
    Task ScheduleCheckInReminderAsync(int userId);
}

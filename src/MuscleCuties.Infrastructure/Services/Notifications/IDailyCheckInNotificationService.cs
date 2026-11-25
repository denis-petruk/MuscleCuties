namespace MuscleCuties.Core.Services.Notifications;

/// <summary>The daily check-in reminder the app schedules with the system.</summary>
public interface IDailyCheckInNotificationService
{
    Task ScheduleCheckInReminderAsync(int userId);
}

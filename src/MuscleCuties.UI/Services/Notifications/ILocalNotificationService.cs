namespace MuscleCuties.UI.Services.Notifications;

public interface ILocalNotificationService
{
    Task<bool> ShowAsync(int notificationId, string title, string body);
    Task<bool> ScheduleAsync(int notificationId, string title, string body, DateTime scheduledAt);
    Task CancelAsync(int notificationId);
}

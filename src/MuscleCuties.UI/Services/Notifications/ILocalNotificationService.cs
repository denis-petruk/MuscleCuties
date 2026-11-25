namespace MuscleCuties.UI.Services.Notifications;

/// <summary>Platform notifications by numeric id: show now, schedule for later, or cancel, each reporting</summary>
/// whether it succeeded so the caller can avoid recording a marker for an undelivered alert.</summary>
public interface ILocalNotificationService
{
    Task<bool> ShowAsync(int notificationId, string title, string body);
    Task<bool> ScheduleAsync(int notificationId, string title, string body, DateTime scheduledAt);
    Task CancelAsync(int notificationId);
}

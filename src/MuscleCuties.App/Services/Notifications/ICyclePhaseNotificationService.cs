namespace MuscleCuties.App.Services.Notifications;

public interface ICyclePhaseNotificationService
{
    Task NotifyIfPhaseChangedAsync(int userId);
}

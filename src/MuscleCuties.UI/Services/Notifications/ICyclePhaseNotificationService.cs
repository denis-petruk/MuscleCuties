namespace MuscleCuties.UI.Services.Notifications;

public interface ICyclePhaseNotificationService
{
    Task NotifyIfPhaseChangedAsync(int userId);
}

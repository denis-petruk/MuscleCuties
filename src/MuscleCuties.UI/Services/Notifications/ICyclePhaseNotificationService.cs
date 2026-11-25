namespace MuscleCuties.UI.Services.Notifications;

/// <summary>Notifies once when the cycle phase changes, at most once per day.</summary>
public interface ICyclePhaseNotificationService
{
    Task NotifyIfPhaseChangedAsync(int userId);
}

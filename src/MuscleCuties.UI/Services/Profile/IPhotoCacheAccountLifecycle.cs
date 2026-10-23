namespace MuscleCuties.UI.Services.Profile;

public interface IPhotoCacheAccountLifecycle
{
    Task ActivateCurrentAccountAsync(CancellationToken cancellationToken = default);
}

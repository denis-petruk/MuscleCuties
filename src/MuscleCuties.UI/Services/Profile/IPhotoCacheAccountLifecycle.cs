namespace MuscleCuties.UI.Services.Profile;

/// <summary>Points the photo cache at whoever is signed in, called when a session begins.</summary>
public interface IPhotoCacheAccountLifecycle
{
    Task ActivateCurrentAccountAsync(CancellationToken cancellationToken = default);
}

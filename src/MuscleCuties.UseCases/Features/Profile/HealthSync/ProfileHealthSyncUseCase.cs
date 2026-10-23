namespace MuscleCuties.UseCases.Features.Profile.HealthSync;

public sealed class ProfileHealthSyncUseCase(IProfileHealthSyncStore store) : IProfileHealthSyncUseCase
{
    public Task<ProfileHealthSyncDto> LoadAsync(CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(cancellationToken);
    }

    public Task<ProfileHealthConnectResult> ConnectAsync(CancellationToken cancellationToken = default)
    {
        return store.ConnectAsync(cancellationToken);
    }

    public Task<ProfileHealthDisconnectResult> DisconnectAsync(CancellationToken cancellationToken = default)
    {
        return store.DisconnectAsync(cancellationToken);
    }
}

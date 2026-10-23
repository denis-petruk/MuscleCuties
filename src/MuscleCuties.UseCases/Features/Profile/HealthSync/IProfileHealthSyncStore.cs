namespace MuscleCuties.UseCases.Features.Profile.HealthSync;

public interface IProfileHealthSyncStore
{
    Task<ProfileHealthSyncDto> LoadAsync(CancellationToken cancellationToken);
    Task<ProfileHealthConnectResult> ConnectAsync(CancellationToken cancellationToken);
    Task<ProfileHealthDisconnectResult> DisconnectAsync(CancellationToken cancellationToken);
}

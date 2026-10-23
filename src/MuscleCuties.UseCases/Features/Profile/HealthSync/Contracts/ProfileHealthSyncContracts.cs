namespace MuscleCuties.UseCases.Features.Profile.HealthSync;

public sealed record ProfileHealthSyncDto(
    bool IsAppleHealthAvailable,
    string StatusText,
    string ConnectionBadgeText,
    string LastSyncedText,
    int ReadinessScore,
    string ReadinessExplanationText,
    bool IsConnected);

public sealed record ProfileHealthConnectResult(
    bool IsConnected,
    string Message,
    ProfileHealthSyncDto Summary);

public sealed record ProfileHealthDisconnectResult(string Message, ProfileHealthSyncDto Summary);

public interface IProfileHealthSyncUseCase
{
    Task<ProfileHealthSyncDto> LoadAsync(CancellationToken cancellationToken = default);
    Task<ProfileHealthConnectResult> ConnectAsync(CancellationToken cancellationToken = default);
    Task<ProfileHealthDisconnectResult> DisconnectAsync(CancellationToken cancellationToken = default);
}

using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Health;

namespace MuscleCuties.UI.Services.Profile;

public interface IProfileHealthConnection
{
    bool IsAvailable { get; }
    Task<bool> IsConnectedAsync();
    Task<(bool IsConnected, string Message)> ConnectAsync();
}

public sealed class ProfileHealthConnection(
    IServiceScopeFactory scopeFactory,
    IHealthSyncService healthSyncService) : IProfileHealthConnection
{
    public bool IsAvailable => OperatingSystem.IsIOS() && healthSyncService is not DisabledHealthSyncService;

    public async Task<bool> IsConnectedAsync()
    {
        if (!IsAvailable)
            return false;

        await using var scope = scopeFactory.CreateAsyncScope();
        var userId = await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .GetCurrentUserIdAsync().ConfigureAwait(false);
        if (userId <= 0)
            return false;

        var status = await healthSyncService.GetStatusAsync(userId).ConfigureAwait(false);
        return status.IsConnected;
    }

    public async Task<(bool IsConnected, string Message)> ConnectAsync()
    {
        if (!IsAvailable)
            return (false, "Health connection is unavailable.");

        await using var scope = scopeFactory.CreateAsyncScope();
        var userId = await scope.ServiceProvider.GetRequiredService<IAuthService>()
            .GetCurrentUserIdAsync().ConfigureAwait(false);
        if (userId <= 0)
            return (false, "Sign in and complete profile setup before connecting Apple Health.");

        var result = await healthSyncService.SyncAsync(userId, HealthDataSource.AppleHealth)
            .ConfigureAwait(false);
        return (result.IsConnected, result.Message);
    }
}

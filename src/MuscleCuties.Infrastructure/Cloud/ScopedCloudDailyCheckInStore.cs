using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.Infrastructure.Cloud;

/// <summary>Runs each long-lived cloud check-in operation with scoped account dependencies.</summary>
public sealed class ScopedCloudDailyCheckInStore(IServiceScopeFactory scopeFactory) : IDailyCheckInStore
{
    /// <summary>Reports whether the day's check-in is open.</summary>
    public async Task<DailyCheckInStatusResponse> GetStatusAsync(
        DailyCheckInStatusRequest request,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<CloudDailyCheckInStore>();
        return await store.GetStatusAsync(request, ct).ConfigureAwait(false);
    }

    /// <summary>Submits the day's check-in.</summary>
    public async Task<SubmitDailyCheckInResponse> SubmitAsync(
        SubmitDailyCheckInRequest request,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<CloudDailyCheckInStore>();
        return await store.SubmitAsync(request, ct).ConfigureAwait(false);
    }
}

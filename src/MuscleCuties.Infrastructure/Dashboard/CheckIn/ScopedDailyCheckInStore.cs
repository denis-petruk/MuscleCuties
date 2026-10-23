using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.Infrastructure.Dashboard.CheckIn;

public sealed class ScopedDailyCheckInStore(IServiceScopeFactory scopeFactory) : IDailyCheckInStore
{
    public async Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request,
        CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LocalDailyCheckInStore>()
            .GetStatusAsync(request, ct).ConfigureAwait(false);
    }

    public async Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request, CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LocalDailyCheckInStore>()
            .SubmitAsync(request, ct).ConfigureAwait(false);
    }
}

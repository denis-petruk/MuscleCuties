using Microsoft.Extensions.DependencyInjection;
using MuscleCuties.UseCases.Features.Progress;

namespace MuscleCuties.Infrastructure.Progress;

/// <summary>
///     Keeps the long-lived dashboard action from capturing a scoped database context.
/// </summary>
public sealed class ScopedProgressSummaryStore(IServiceScopeFactory scopeFactory) : IProgressSummaryStore
{
    public async Task<ProgressSummaryDto> GetSummaryAsync(
        DateOnly date, int lookbackDays = 90, CancellationToken ct = default)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var store = scope.ServiceProvider.GetRequiredService<LocalProgressSummaryStore>();
        return await store.GetSummaryAsync(date, lookbackDays, ct).ConfigureAwait(false);
    }
}

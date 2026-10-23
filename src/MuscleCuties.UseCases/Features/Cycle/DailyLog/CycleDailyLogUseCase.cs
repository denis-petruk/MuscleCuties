using MuscleCuties.Core.Domain.Cycle;

namespace MuscleCuties.UseCases.Features.Cycle.DailyLog;

public sealed class CycleDailyLogUseCase(ICycleDailyLogStore store) : ICycleDailyLogUseCase
{
    public Task<CycleDailyLogSnapshot?> LoadAsync(DateTime date, CancellationToken cancellationToken = default)
    {
        return store.LoadAsync(date.Date, cancellationToken);
    }

    public Task<bool> SaveAsync(CycleDailyLogRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        CycleQuickLogRule.Validate(new CycleQuickLogInput(request.Date.Date,
            request.IsPeriodStart, request.IsPeriodEnd, request.FlowIntensity,
            request.Symptoms, request.PhaseOverride, request.Note));
        return store.SaveAsync(request with { Date = request.Date.Date }, cancellationToken);
    }
}

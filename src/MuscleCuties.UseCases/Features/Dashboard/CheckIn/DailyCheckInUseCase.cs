using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn;

public sealed class DailyCheckInUseCase(IDailyCheckInStore store) : IDailyCheckInUseCase
{
    public Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.GetStatusAsync(request, ct);
    }

    public Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.SubmitAsync(request, ct);
    }
}

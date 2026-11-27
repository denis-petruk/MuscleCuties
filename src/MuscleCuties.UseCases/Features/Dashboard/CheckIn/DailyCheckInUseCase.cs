using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Dashboard.CheckIn.Contracts;

namespace MuscleCuties.UseCases.Features.Dashboard.CheckIn;

/// <summary>Saves a check-in and returns the derived readiness, marking the plan for regeneration.</summary>
public sealed class DailyCheckInUseCase(IDailyCheckInStore store) : IDailyCheckInUseCase
{
    /// <summary>The presentation settings the check-in screen uses.</summary>
    public DailyCheckInPresentationSettings PresentationSettings { get; } = new(
        TodayCheckInScoreRule.ScoreDisplayEnabled,
        TodayPlanningDecisionRule.ReadinessSuggestionsEnabled);

    /// <summary>Reports whether the day's check-in is still open.</summary>
    public Task<DailyCheckInStatusResponse> GetStatusAsync(DailyCheckInStatusRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.GetStatusAsync(request, ct);
    }

    /// <summary>Submits the day's check-in answers.</summary>
    public Task<SubmitDailyCheckInResponse> SubmitAsync(SubmitDailyCheckInRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        return store.SubmitAsync(request, ct);
    }
}

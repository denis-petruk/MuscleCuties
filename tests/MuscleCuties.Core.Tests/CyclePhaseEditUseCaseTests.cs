using MuscleCuties.UseCases.Features.Cycle.Contracts;
using MuscleCuties.UseCases.Features.Cycle.PhaseEdit;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CyclePhaseEditUseCaseTests
{
    [Fact]
    public async Task SaveAsync_UnsupportedDate_ReturnsInvalidInputWithoutReachingStore()
    {
        var store = new RecordingStore();
        var useCase = new CyclePhaseEditUseCase(store);

        var result = await useCase.SaveAsync(
            new CyclePhaseEditRequest(new DateTime(1900, 1, 2), CyclePhaseDto.Follicular, null));

        Assert.Equal(CyclePhaseEditOutcome.InvalidInput, result.Outcome);
        Assert.Null(store.LastRequest);
    }

    [Fact]
    public async Task SaveAsync_UnknownPhase_ReturnsInvalidInput()
    {
        var store = new RecordingStore();
        var useCase = new CyclePhaseEditUseCase(store);

        var result = await useCase.SaveAsync(
            new CyclePhaseEditRequest(new DateTime(2026, 10, 19), CyclePhaseDto.Unknown, null));

        Assert.Equal(CyclePhaseEditOutcome.InvalidInput, result.Outcome);
        Assert.Null(store.LastRequest);
    }

    [Fact]
    public async Task SaveAsync_ValidRequest_PassesStoreOutcomeBackWithDateOnlyValue()
    {
        var store = new RecordingStore { Result = CyclePhaseEditResult.OrderConflict(CyclePhaseDto.Luteal) };
        var useCase = new CyclePhaseEditUseCase(store);

        var result = await useCase.SaveAsync(new CyclePhaseEditRequest(
            new DateTime(2026, 10, 19, 14, 30, 0), CyclePhaseDto.Luteal, "Calendar phase edit"));

        Assert.Equal(CyclePhaseEditOutcome.OrderConflict, result.Outcome);
        Assert.Equal(CyclePhaseDto.Luteal, result.SuggestedPhase);
        Assert.Equal(new DateTime(2026, 10, 19), store.LastRequest!.Date);
        Assert.Equal(CyclePhaseDto.Luteal, store.LastRequest.Phase);
    }

    private sealed class RecordingStore : ICyclePhaseEditStore
    {
        public CyclePhaseEditResult Result { get; init; } = CyclePhaseEditResult.Saved;
        public CyclePhaseEditRequest? LastRequest { get; private set; }

        public Task<CyclePhaseEditResult> SaveAsync(CyclePhaseEditRequest request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(Result);
        }
    }
}

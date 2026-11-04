using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UseCases.Features.Cycle.History;
using CycleAnchorRule = MuscleCuties.Core.Domain.Cycle.CyclePhaseRule;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class CycleOnboardingAnchorTests
{
    private const int ProfileCycleLength = 28;
    private const int ShortCycleLength = 21;
    private const int LongCycleLength = 35;
    private static readonly DateOnly OnboardingDate = new(2026, 10, 29);

    [Theory]
    [InlineData(CyclePhase.Menstrual, ShortCycleLength)]
    [InlineData(CyclePhase.Menstrual, ProfileCycleLength)]
    [InlineData(CyclePhase.Menstrual, LongCycleLength)]
    [InlineData(CyclePhase.Follicular, ProfileCycleLength)]
    [InlineData(CyclePhase.Follicular, LongCycleLength)]
    [InlineData(CyclePhase.Ovulatory, ShortCycleLength)]
    [InlineData(CyclePhase.Ovulatory, ProfileCycleLength)]
    [InlineData(CyclePhase.Luteal, ShortCycleLength)]
    [InlineData(CyclePhase.Luteal, ProfileCycleLength)]
    [InlineData(CyclePhase.Luteal, LongCycleLength)]
    public void ImpliedCycleStartLandsTheObservationOnItsOwnPhase(CyclePhase phase, int cycleLength)
    {
        var impliedStart = CycleAnchorRule.GetImpliedCycleStart(phase, OnboardingDate, cycleLength);
        var cycleDay = OnboardingDate.DayNumber - impliedStart.DayNumber + 1;

        Assert.True(cycleDay >= 1, $"{phase} on a {cycleLength}-day cycle must start on or before the observation.");
        Assert.Equal(phase, CycleAnchorRule.CalculatePhase(cycleDay, cycleLength));
    }

    [Fact]
    public void OnboardingPhaseLogAloneProducesAnActiveCycle()
    {
        var useCase = new CycleHistoryUseCase(new StubCycleHistoryStore(
            new CyclePredictionInputs(
                [],
                ProfileCycleLength,
                new CycleManualPhaseLog(CyclePhase.Luteal.ToString(), OnboardingDate))));

        var prediction = useCase.ProjectPredictionAsync(Guid.NewGuid(), OnboardingDate).GetAwaiter().GetResult();

        var expectedStart = CycleAnchorRule.GetImpliedCycleStart(
            CyclePhase.Luteal, OnboardingDate, ProfileCycleLength);
        Assert.True(prediction.HasActiveCycle);
        Assert.Equal(expectedStart, prediction.CurrentCycleStartDate);
        Assert.Equal(OnboardingDate.DayNumber - expectedStart.DayNumber + 1, prediction.CurrentDay);
        Assert.Equal(CyclePhase.Luteal.ToString(), prediction.CurrentPhase);
    }

    [Fact]
    public void RecordedCycleHistoryStaysAuthoritativeOverTheOnboardingAnchor()
    {
        var recordedStart = OnboardingDate.AddDays(-9);
        var useCase = new CycleHistoryUseCase(new StubCycleHistoryStore(
            new CyclePredictionInputs(
                [new CycleObservation(Guid.NewGuid(), recordedStart, null, 0)],
                ProfileCycleLength,
                new CycleManualPhaseLog(CyclePhase.Menstrual.ToString(), OnboardingDate))));

        var prediction = useCase.ProjectPredictionAsync(Guid.NewGuid(), OnboardingDate).GetAwaiter().GetResult();

        Assert.True(prediction.HasActiveCycle);
        Assert.Equal(recordedStart, prediction.CurrentCycleStartDate);
        Assert.Equal(OnboardingDate.DayNumber - recordedStart.DayNumber + 1, prediction.CurrentDay);
    }

    [Fact]
    public void UnknownOnboardingPhaseKeepsTheObservationDateAsTheStart()
    {
        var impliedStart = CycleAnchorRule.GetImpliedCycleStart(
            CyclePhase.Unknown, OnboardingDate, ProfileCycleLength);

        Assert.Equal(OnboardingDate, impliedStart);
    }

    private sealed class StubCycleHistoryStore(CyclePredictionInputs inputs) : ICycleHistoryStore
    {
        public Task<CyclePredictionInputs> LoadPredictionInputsAsync(
            Guid userId, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult(inputs);

        public Task<CycleHistorySnapshot> LoadAsync(
            Guid userId, DateOnly from, DateOnly through, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CycleObservation?> SaveCycleAsync(
            SaveCycleObservation request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteCycleAsync(
            Guid userId, Guid cycleId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<CycleSymptom?> SaveSymptomAsync(
            SaveCycleSymptom request, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task<bool> DeleteSymptomAsync(
            Guid userId, Guid symptomId, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }
}

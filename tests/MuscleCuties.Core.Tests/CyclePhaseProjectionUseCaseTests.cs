using MuscleCuties.UseCases.Features.Cycle.Overview;
using Xunit;
using CyclePhase = MuscleCuties.UseCases.Features.Cycle.Contracts.CyclePhaseDto;

namespace MuscleCuties.Core.Tests;

public sealed class CyclePhaseProjectionUseCaseTests
{
    private const int TwentyEightDayCycle = 28;
    private const int FiveCycleDays = 5;
    private const int ThreeCycleDays = 3;
    private const int TwoRemainingDays = 2;
    private const int ShortCycleLength = 18;
    private static readonly DateTime CycleStart = new(2026, 1, 1);

    [Fact]
    public void CycleProjectionReturnsCycleDayAndPhaseFromTheCoreRule()
    {
        var targetDate = CycleStart.AddDays(FiveCycleDays - 1);
        var result = new CyclePhaseProjectionUseCase().Project(new CyclePhaseProjectionInputDto(
            targetDate,
            targetDate,
            TwentyEightDayCycle,
            CycleStart));

        Assert.Equal(FiveCycleDays, result.CycleDay);
        Assert.Equal(CyclePhase.Menstrual, result.Phase);
        Assert.Equal(TwentyEightDayCycle, result.NormalizedCycleLength);
    }

    [Fact]
    public void PriorPhaseLogProjectsThroughTheCoreCyclePhaseRule()
    {
        var loggedAt = CycleStart;
        var targetDate = CycleStart.AddDays(14);
        var result = new CyclePhaseProjectionUseCase().Project(new CyclePhaseProjectionInputDto(
            targetDate,
            targetDate,
            TwentyEightDayCycle,
            CurrentPhase: CyclePhase.Menstrual,
            LoggedPhase: CyclePhase.Menstrual,
            LoggedAt: loggedAt));

        Assert.Equal(CyclePhase.Ovulatory, result.Phase);
    }

    [Fact]
    public void CountdownAndDashboardCalendarUseNamedCycleBounds()
    {
        var useCase = new CyclePhaseProjectionUseCase();
        var countdown = useCase.GetDaysRemaining(new CyclePhaseCountdownInputDto(
            ThreeCycleDays, TwentyEightDayCycle, CyclePhase.Menstrual));
        var calendar = useCase.ProjectDashboardCalendar(new CyclePhaseProjectionInputDto(
            CycleStart,
            CycleStart,
            ShortCycleLength,
            CycleStart));

        Assert.Equal(TwoRemainingDays, countdown.DaysRemaining);
        Assert.Equal(CycleCalendarMinimumLength, calendar.NormalizedCycleLength);
    }

    [Fact]
    public void NextPhaseIsReturnedAsApplicationOutputData()
    {
        var result = new CyclePhaseProjectionUseCase().GetNextPhase(
            new NextCyclePhaseInputDto(CyclePhase.Follicular));

        Assert.Equal(CyclePhase.Ovulatory, result.NextPhase);
    }

    private const int CycleCalendarMinimumLength =
        MuscleCuties.Core.Domain.Cycle.CycleCalendarProjectionRule.MinimumCalendarCycleLength;
}

using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.Overview;

public sealed class CyclePhaseProjectionUseCase : ICyclePhaseProjectionUseCase
{
    public CyclePhaseProjectionOutputDto Project(CyclePhaseProjectionInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var normalizedCycleLength = CyclePhaseRule.NormalizeCycleLength(input.CycleLength);
        var cycleDay = input.CycleDayOverride is { } cycleDayOverride
            ? input.CycleDayOverrideDate is { } overrideDate
                ? CyclePhaseRule.ProjectCycleDay(cycleDayOverride, overrideDate, input.Date, normalizedCycleLength)
                : cycleDayOverride
            : CyclePhaseRule.CalculateCycleDay(input.Date, input.CycleStartDate, normalizedCycleLength);
        var phase = ResolvePhase(input, cycleDay, normalizedCycleLength);
        return new CyclePhaseProjectionOutputDto(normalizedCycleLength, cycleDay, CycleContractMapper.ToDto(phase));
    }

    public CyclePhaseProjectionOutputDto ProjectDashboardCalendar(CyclePhaseProjectionInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var calendarCycleLength = CycleCalendarProjectionRule.NormalizeCycleLength(input.CycleLength);
        return Project(input with { CycleLength = calendarCycleLength });
    }

    public CyclePhaseCountdownOutputDto GetDaysRemaining(CyclePhaseCountdownInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new CyclePhaseCountdownOutputDto(CyclePhaseRule.GetDaysRemainingInPhase(
            input.CycleDay, input.CycleLength, CycleContractMapper.ToCore(input.CurrentPhase)));
    }

    public NextCyclePhaseOutputDto GetNextPhase(NextCyclePhaseInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new NextCyclePhaseOutputDto(
            CycleContractMapper.ToDto(CyclePhaseRule.GetNextPhase(CycleContractMapper.ToCore(input.CurrentPhase))));
    }

    private static CyclePhase ResolvePhase(
        CyclePhaseProjectionInputDto input,
        int cycleDay,
        int cycleLength)
    {
        var currentPhase = CycleContractMapper.ToCore(input.CurrentPhase);
        if (input.LoggedPhase is not { } loggedPhase || input.LoggedAt is not { } loggedAt)
            return cycleDay > 0 ? CyclePhaseRule.CalculatePhase(cycleDay, cycleLength) : currentPhase;

        if (loggedAt.Date == input.Date.Date)
            return CycleContractMapper.ToCore(loggedPhase);
        if (input.Date.Date == input.Today.Date && input.HasActiveCycle)
            return currentPhase;

        return CyclePhaseRule.ProjectPhaseFromLog(
            new CyclePhaseObservation(CycleContractMapper.ToCore(loggedPhase), loggedAt), input.Date, cycleLength);
    }
}

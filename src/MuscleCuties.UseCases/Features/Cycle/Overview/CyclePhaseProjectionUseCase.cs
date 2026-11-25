using MuscleCuties.Core.Domain.Cycle;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Cycle.Overview;

/// <summary>Answers the phase questions from the shared phase rules, so the client never reimplements them.</summary>
public sealed class CyclePhaseProjectionUseCase : ICyclePhaseProjectionUseCase
{
    /// <summary>The phase for a day, using a manual log first and the prediction only as a fallback.</summary>
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

    /// <summary>Phases for the calendar range, so the strip and the detail cannot disagree.</summary>
    public CyclePhaseProjectionOutputDto ProjectDashboardCalendar(CyclePhaseProjectionInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var calendarCycleLength = CycleCalendarProjectionRule.NormalizeCycleLength(input.CycleLength);
        return Project(input with { CycleLength = calendarCycleLength });
    }

    /// <summary>Days until the phase changes, floored at zero once it has.</summary>
    public CyclePhaseCountdownOutputDto GetDaysRemaining(CyclePhaseCountdownInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new CyclePhaseCountdownOutputDto(CyclePhaseRule.GetDaysRemainingInPhase(
            input.CycleDay, input.CycleLength, CycleContractMapper.ToCore(input.CurrentPhase)));
    }

    /// <summary>The phase after the given one, wrapping from the last back to the first.</summary>
    public NextCyclePhaseOutputDto GetNextPhase(NextCyclePhaseInputDto input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return new NextCyclePhaseOutputDto(
            CycleContractMapper.ToDto(CyclePhaseRule.GetNextPhase(CycleContractMapper.ToCore(input.CurrentPhase))));
    }

    /// <summary>Chooses the phase for a date from logs, predictions and the profile's cycle length.</summary>
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

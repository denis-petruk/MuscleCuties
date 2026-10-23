using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Dashboard;

public static class DashboardPhaseSelectionRule
{
    public static CyclePhase Resolve(CyclePhase projectedPhase, CyclePhase profilePhase)
    {
        if (Enum.IsDefined(projectedPhase) && projectedPhase != CyclePhase.Unknown)
            return projectedPhase;

        return Enum.IsDefined(profilePhase) && profilePhase != CyclePhase.Unknown
            ? profilePhase
            : CyclePhase.Unknown;
    }
}

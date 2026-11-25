using MuscleCuties.Core.Models.Enums.Cycle;

namespace MuscleCuties.Core.Domain.Dashboard;

/// <summary>Chooses which phase the dashboard shows, preferring a logged phase over a projection.</summary>
public static class DashboardPhaseSelectionRule
{
    /// <summary>The phase for the day, with whether it was logged or projected.</summary>
    public static CyclePhase Resolve(CyclePhase projectedPhase, CyclePhase profilePhase)
    {
        if (Enum.IsDefined(projectedPhase) && projectedPhase != CyclePhase.Unknown)
            return projectedPhase;

        return Enum.IsDefined(profilePhase) && profilePhase != CyclePhase.Unknown
            ? profilePhase
            : CyclePhase.Unknown;
    }
}

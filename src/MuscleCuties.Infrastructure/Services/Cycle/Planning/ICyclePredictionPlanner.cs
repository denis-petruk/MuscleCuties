using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Services.Cycle.Planning;

public interface ICyclePredictionPlanner
{
    CyclePrediction CreatePrediction(
        CycleLog? latestCycle,
        IReadOnlyCollection<CycleLog> history,
        UserProfile? profile,
        DateTime today);
}

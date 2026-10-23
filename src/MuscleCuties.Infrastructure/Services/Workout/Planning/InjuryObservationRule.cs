using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public sealed record InjuryObservation(int SiteFlag, string? Status, DateOnly Since);

public static class InjuryObservationRule
{
    public static List<Injury> Map(IReadOnlyCollection<InjuryObservation>? observations)
    {
        if (observations is null)
            return [];

        var pureObservations = observations
            .Where(o => o is not null)
            .Select(o => new PureInjuryObservation(o.SiteFlag, o.Status, o.Since))
            .ToList();

        var results = PureInjuryObservationRule.Map(pureObservations);

        return results
            .Select(r => new Injury((InjuryFlag)r.SiteFlag, (InjuryStatus)r.Status, r.Since))
            .ToList();
    }
}

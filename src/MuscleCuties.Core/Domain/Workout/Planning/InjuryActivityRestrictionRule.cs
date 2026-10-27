using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Domain.Workout.Planning;

public static class InjuryActivityRestrictionRule
{
    public static IReadOnlySet<WorkoutActivityType> GetBlockedActivities(IEnumerable<Injury>? injuries)
    {
        var blocked = new HashSet<WorkoutActivityType>();
        if (injuries is null) return blocked;

        foreach (var injury in injuries.Where(injury => injury.Status != InjuryStatus.Cleared))
        {
            if (injury.Site is InjuryFlag.Metatarsal or InjuryFlag.Ankle)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                if (injury.Status == InjuryStatus.Acute)
                {
                    blocked.Add(WorkoutActivityType.Running);
                    blocked.Add(WorkoutActivityType.RockClimbing);
                }
            }

            if (injury.Site == InjuryFlag.Knee)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                blocked.Add(WorkoutActivityType.Running);
                if (injury.Status == InjuryStatus.Acute) blocked.Add(WorkoutActivityType.RockClimbing);
            }

            if (injury.Site == InjuryFlag.Shoulder && injury.Status == InjuryStatus.Acute)
            {
                blocked.Add(WorkoutActivityType.Swimming);
                blocked.Add(WorkoutActivityType.RockClimbing);
            }

            if (injury.Site == InjuryFlag.Hip)
            {
                blocked.Add(WorkoutActivityType.Running);
                if (injury.Status == InjuryStatus.Acute)
                {
                    blocked.Add(WorkoutActivityType.Hiit);
                    blocked.Add(WorkoutActivityType.RockClimbing);
                }
            }

            if (injury.Site == InjuryFlag.Neck && injury.Status == InjuryStatus.Acute)
                blocked.Add(WorkoutActivityType.Swimming);

            if (injury.Site == InjuryFlag.Wrist && injury.Status == InjuryStatus.Acute)
                blocked.Add(WorkoutActivityType.RockClimbing);

            if (injury.Site == InjuryFlag.LowBack && injury.Status == InjuryStatus.Acute)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                blocked.Add(WorkoutActivityType.Running);
                blocked.Add(WorkoutActivityType.RockClimbing);
            }
        }

        return blocked;
    }

}

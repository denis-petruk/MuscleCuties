using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public static class WorkoutInjuryRules
{
    public static InjuryFlag ToFlags(IEnumerable<Injury> injuries)
    {
        var flags = InjuryFlag.None;
        foreach (var injury in injuries.Where(injury => injury.Status != InjuryStatus.Cleared))
        {
            flags |= injury.Site switch
            {
                InjurySite.Metatarsal => InjuryFlag.Metatarsal,
                InjurySite.Knee => InjuryFlag.Knee,
                InjurySite.Ankle => InjuryFlag.Ankle,
                InjurySite.Shoulder => InjuryFlag.Shoulder,
                InjurySite.LowBack => InjuryFlag.LowBack,
                InjurySite.Wrist => InjuryFlag.Wrist,
                _ => InjuryFlag.None
            };
        }

        return flags;
    }

    public static IReadOnlySet<WorkoutActivityType> GetBlockedActivities(IEnumerable<Injury> injuries)
    {
        var blocked = new HashSet<WorkoutActivityType>();
        foreach (var injury in injuries.Where(injury => injury.Status != InjuryStatus.Cleared))
        {
            if (injury.Site is InjurySite.Metatarsal or InjurySite.Ankle)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                if (injury.Status == InjuryStatus.Acute)
                {
                    blocked.Add(WorkoutActivityType.Running);
                    blocked.Add(WorkoutActivityType.RockClimbing);
                }
            }

            if (injury.Site == InjurySite.Knee)
            {
                blocked.Add(WorkoutActivityType.Hiit);
                blocked.Add(WorkoutActivityType.Running);
            }

            if (injury.Site == InjurySite.Shoulder && injury.Status == InjuryStatus.Acute)
            {
                blocked.Add(WorkoutActivityType.Swimming);
                blocked.Add(WorkoutActivityType.RockClimbing);
            }
        }

        return blocked;
    }
}

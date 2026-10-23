using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public static class WorkoutInjuryRules
{
    /// <summary>
    ///     Documented injury classes with movement contraindications and activity gating.
    ///     Note: Algorithmic contraindication rules provide deterministic safety guardrails
    ///     and code evidence, but do not replace qualified medical or physical therapy review of exercise advice.
    /// </summary>
    public static readonly IReadOnlyList<InjuryFlag> DocumentedInjuryClasses =
    [
        InjuryFlag.Metatarsal,
        InjuryFlag.Knee,
        InjuryFlag.Ankle,
        InjuryFlag.Shoulder,
        InjuryFlag.LowBack,
        InjuryFlag.Wrist,
        InjuryFlag.Hip,
        InjuryFlag.Neck
    ];

    public static List<Injury> MapInjuries(IReadOnlyCollection<WorkoutInjuryLog>? injuryLogs)
    {
        var observations = injuryLogs?.Where(log => log is not null)
            .Select(log => new InjuryObservation(log.SiteFlag, log.Status, log.Since))
            .ToArray();
        return InjuryObservationRule.Map(observations);
    }

    public static InjuryFlag ToFlags(IEnumerable<Injury>? injuries)
    {
        if (injuries is null)
            return InjuryFlag.None;

        var flags = InjuryFlag.None;
        foreach (var injury in injuries.Where(injury => injury.Status != InjuryStatus.Cleared))
            flags |= injury.Site;
        return flags;
    }

    /// <summary>
    ///     Evaluates whether an exercise's contraindication bitmask conflicts with any active injuries.
    /// </summary>
    public static bool IsContraindicated(InjuryFlag contraindications, InjuryFlag activeInjuries)
    {
        if (activeInjuries == InjuryFlag.None || contraindications == InjuryFlag.None)
            return false;

        return (contraindications & activeInjuries) != InjuryFlag.None;
    }

    /// <summary>
    ///     Determines whether a specific exercise definition is contraindicated for the user's active injury flags.
    ///     Missing exercises default to clinically conservative rejection (true).
    /// </summary>
    public static bool IsContraindicated(WorkoutExerciseDefinition? exercise, InjuryFlag activeInjuries)
    {
        if (exercise is null)
            return true;

        return IsContraindicated(exercise.Contraindications, activeInjuries);
    }

    /// <summary>
    ///     Determines whether a specific exercise definition is contraindicated for a collection of user injuries.
    ///     Cleared injuries do not cause contraindication.
    /// </summary>
    public static bool IsContraindicated(WorkoutExerciseDefinition? exercise, IEnumerable<Injury>? injuries)
    {
        if (exercise is null)
            return true;

        if (injuries is null)
            return false;

        var flags = ToFlags(injuries);
        return IsContraindicated(exercise.Contraindications, flags);
    }

    /// <summary>
    ///     Filters a collection of exercises, rejecting any movements that are contraindicated by active injuries.
    /// </summary>
    public static List<WorkoutExerciseDefinition> FilterSafeExercises(
        IEnumerable<WorkoutExerciseDefinition>? exercises,
        InjuryFlag activeInjuries)
    {
        if (exercises is null)
            return [];

        if (activeInjuries == InjuryFlag.None)
            return exercises.ToList();

        return exercises
            .Where(e => !IsContraindicated(e, activeInjuries))
            .ToList();
    }

    public static IReadOnlySet<WorkoutActivityType> GetBlockedActivities(IEnumerable<Injury>? injuries)
    {
        var blocked = new HashSet<WorkoutActivityType>();
        if (injuries is null)
            return blocked;

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

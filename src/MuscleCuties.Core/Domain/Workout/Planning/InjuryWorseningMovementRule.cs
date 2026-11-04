using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Domain.Workout.Planning;

/// <summary>
/// One recorded injury safety answer. <see cref="ClinicianAdvisedAvoid"/> is deliberately
/// nullable so an unanswered question never reads as a clinician clearance.
/// </summary>
public sealed record InjurySafetyAnswer(
    InjuryWorseningTrigger WorseningTrigger,
    bool? ClinicianAdvisedAvoid = null,
    InjuryFlag Site = InjuryFlag.None);

/// <summary>Reviewed catalog evidence used to expand an explicit clinician restriction.</summary>
public sealed record ExerciseContraindicationPattern(MovementPattern Pattern, InjuryFlag Contraindications);

/// <summary>
/// Owner decision (2026-11-01): a worsening movement answer and an explicit clinician restriction
/// must change which exercises the planner may select, not only withhold automatic dose changes.
/// The closed mapping keeps the substitution deterministic and keeps free text out of planning.
/// </summary>
public static class InjuryWorseningMovementRule
{
    private static readonly IReadOnlySet<MovementPattern> NoPatterns = new HashSet<MovementPattern>();

    /// <summary>Movement patterns the record says make the injury worse.</summary>
    public static IReadOnlySet<MovementPattern> BlockedPatterns(InjuryWorseningTrigger trigger) => trigger switch
    {
        InjuryWorseningTrigger.OverheadReach => Set(
            MovementPattern.VerticalPush,
            MovementPattern.VerticalPull,
            MovementPattern.LateralRaise),
        InjuryWorseningTrigger.DeepKneeFlexion => Set(
            MovementPattern.SquatPattern,
            MovementPattern.Lunge,
            MovementPattern.KneeFlexion),
        InjuryWorseningTrigger.SpinalLoading => Set(
            MovementPattern.HipHinge,
            MovementPattern.AntiExtension,
            MovementPattern.AntiLateralFlexion),
        InjuryWorseningTrigger.ImpactAndGait => Set(
            MovementPattern.CalfRaise,
            MovementPattern.Lunge),
        InjuryWorseningTrigger.GripAndWristLoad => Set(
            MovementPattern.HorizontalPull,
            MovementPattern.VerticalPull,
            MovementPattern.ElbowFlexion,
            MovementPattern.Climbing),
        _ => NoPatterns
    };

    /// <summary>
    /// Combines every active answer. A trigger blocks its own patterns. An explicit clinician
    /// restriction expands to the patterns of reviewed exercises contraindicated by that site,
    /// so an unanswered trigger cannot silently clear a restriction the clinician recorded.
    /// </summary>
    public static HashSet<MovementPattern> BlockedPatterns(
        IEnumerable<InjurySafetyAnswer>? answers,
        IEnumerable<ExerciseContraindicationPattern>? catalog)
    {
        var blocked = new HashSet<MovementPattern>();
        if (answers is null)
            return blocked;

        var catalogEntries = catalog as IReadOnlyCollection<ExerciseContraindicationPattern>
            ?? (catalog ?? []).ToArray();
        foreach (var answer in answers)
        {
            if (answer is null ||
                !Enum.IsDefined(answer.WorseningTrigger) ||
                !Enum.IsDefined(answer.Site))
                continue;

            foreach (var pattern in BlockedPatterns(answer.WorseningTrigger))
                blocked.Add(pattern);

            if (answer.ClinicianAdvisedAvoid != true || answer.Site == InjuryFlag.None)
                continue;

            foreach (var entry in catalogEntries)
            {
                if (entry is not null && (entry.Contraindications & answer.Site) != InjuryFlag.None)
                    blocked.Add(entry.Pattern);
            }
        }

        return blocked;
    }

    private static HashSet<MovementPattern> Set(params MovementPattern[] patterns) =>
        patterns.ToHashSet();
}

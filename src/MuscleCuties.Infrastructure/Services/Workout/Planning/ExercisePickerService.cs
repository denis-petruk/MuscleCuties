using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IExercisePickerService
{
    Task<PrescribedSession> PickForSessionAsync(
        PlannedSession session,
        EquipmentSet available,
        InjuryFlag activeInjuries,
        double setMultiplier,
        int rpeCap,
        int userId = 0,
        CancellationToken cancellationToken = default);
}

public class ExercisePickerService : IExercisePickerService
{
    private readonly IContributionLookup _contributions;
    private readonly IWorkoutPlanningDataSource _dataSource;

    public ExercisePickerService(
        AppDatabase db,
        IContributionLookup contributions)
        : this(new AppDatabaseWorkoutPlanningDataSource(db), contributions)
    {
    }

    public ExercisePickerService(
        IWorkoutPlanningDataSource dataSource,
        IContributionLookup contributions)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _contributions = contributions ?? throw new ArgumentNullException(nameof(contributions));
    }

    public async Task<PrescribedSession> PickForSessionAsync(
        PlannedSession session,
        EquipmentSet available,
        InjuryFlag activeInjuries,
        double setMultiplier,
        int rpeCap,
        int userId = 0,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var allPatterns = session.Slots
            .SelectMany(slot => slot.AllowedPatterns)
            .Distinct()
            .ToArray();

        var definitionRows = await _dataSource.GetExerciseDefinitionsAsync(allPatterns, cancellationToken)
            .ConfigureAwait(false);
        if (definitionRows is null || definitionRows.Count == 0)
            throw new WorkoutPlanningFailureException("No exercise definitions match the planned movement patterns.");

        var candidates = ExercisePickerRule.FilterCandidates(definitionRows.Select(ToNeutral), available, activeInjuries);
        if (candidates.Count == 0)
            throw new WorkoutPlanningFailureException("No safe exercise can be selected for the planned session.");

        var preferenceRows = userId > 0
            ? await _dataSource.GetUserExercisePreferencesAsync(userId, cancellationToken).ConfigureAwait(false)
            : [];
        var preferences = preferenceRows
            .GroupBy(preference => preference.OriginalExerciseId)
            .ToDictionary(group => group.Key, group => group.First().PreferredExerciseId);

        var contributionIndex = new WorkoutPlanningReferenceIndex(candidates
            .SelectMany(candidate => _contributions.GetContributions(candidate.Id)
                .Select(row => new PlanningMuscleContribution(row.Id, candidate.Id, row.MuscleGroupId, row.Fraction)))
            .ToArray());

        var prescribed = ExercisePickerRule.PrescribeSession(
            session,
            candidates,
            activeInjuries,
            setMultiplier,
            rpeCap,
            preferences,
            contributionIndex);

        if (prescribed is null)
            throw new WorkoutPlanningFailureException("The planned strength session has no usable exercises.");

        return prescribed;
    }

    private static PlanningExerciseDefinition ToNeutral(WorkoutExerciseDefinition definition) => new(
        definition.Id, definition.Name, definition.Pattern, definition.Required, definition.LongLengthLoaded,
        definition.FatigueCost, definition.SkillDemand, definition.SetupSeconds, definition.SecondsPerRep,
        definition.EligibleForHiBlock, definition.UnilateralDoublesTime, definition.IsBodyweight,
        definition.Contraindications, definition.PreferredFor, definition.SupineOrProne, definition.ValsalvaDemand,
        definition.SubstituteGroupId, []);

    public static EquipmentSet MapEquipment(Equipment equipment) =>
        ExercisePickerRule.MapEquipment(equipment);
}

using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IVolumeBudgetResolver
{
    Task<Dictionary<int, double>> ResolveBudgetAsync(
        int daysPerWeek,
        TrainingExperienceLevel experience,
        UserGoal goal,
        int? sessionMinutesTarget = null,
        CancellationToken cancellationToken = default);
}

public class VolumeBudgetResolver : IVolumeBudgetResolver
{
    private const int ShortSessionThreshold = 35;

    private readonly IWorkoutPlanningDataSource _dataSource;

    public VolumeBudgetResolver(AppDatabase db)
        : this(new AppDatabaseWorkoutPlanningDataSource(db))
    {
    }

    public VolumeBudgetResolver(IWorkoutPlanningDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task<Dictionary<int, double>> ResolveBudgetAsync(
        int daysPerWeek,
        TrainingExperienceLevel experience,
        UserGoal goal,
        int? sessionMinutesTarget = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        daysPerWeek = Math.Clamp(daysPerWeek, 2, 6);

        var baseRows = await _dataSource.GetVolumeBudgetRowsAsync(daysPerWeek, cancellationToken).ConfigureAwait(false);

        HashSet<int>? tierCMuscles = null;
        if (sessionMinutesTarget is <= ShortSessionThreshold)
            tierCMuscles = (await _dataSource.GetGoalTierMuscleGroupIdsAsync(
                    goal,
                    tierCOnly: true,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false)).ToHashSet();

        var neutralRows = baseRows.Select(row => new PlanningVolumeBudget(
            row.Id, row.DaysPerWeek, row.MuscleGroupId, row.FractionalSets)).ToArray();
        var indexedRows = WorkoutPlanningReferenceIndex.VolumeRows(neutralRows, daysPerWeek);

        return VolumeBudgetRule.Resolve(indexedRows.Select(row =>
                new VolumeBudgetObservation(row.Key, row.Value)).ToArray(),
            experience, sessionMinutesTarget, tierCMuscles);
    }
}

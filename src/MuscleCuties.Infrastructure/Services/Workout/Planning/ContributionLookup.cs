using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IContributionLookup
{
    double Contribution(int exerciseId, int muscleGroupId);
    IReadOnlyList<ExerciseMuscleContribution> GetContributions(int exerciseId);
    Task LoadAsync(CancellationToken cancellationToken = default);
}

public class ContributionLookup : IContributionLookup
{
    private readonly IWorkoutPlanningDataSource _dataSource;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Dictionary<int, List<ExerciseMuscleContribution>> _byExercise = new();
    private bool _loaded;
    private Dictionary<(int ExerciseId, int MuscleGroupId), double> _map = new();

    public ContributionLookup(AppDatabase db)
        : this(new AppDatabaseWorkoutPlanningDataSource(db))
    {
    }

    public ContributionLookup(IWorkoutPlanningDataSource dataSource)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        if (_loaded)
            return;

        await _loadLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_loaded)
                return;

            var contributions = await _dataSource.GetContributionsAsync(cancellationToken).ConfigureAwait(false);

            var neutralRows = contributions.Select(contribution => new PlanningMuscleContribution(
                contribution.Id, contribution.ExerciseId, contribution.MuscleGroupId, contribution.Fraction)).ToArray();
            var index = new WorkoutPlanningReferenceIndex(neutralRows);
            _map = neutralRows
                .Where(row => index.ContributionsFor(row.ExerciseDefinitionId).Contains(row))
                .GroupBy(row => (row.ExerciseDefinitionId, row.MuscleGroupId))
                .ToDictionary(group => group.Key, group => index.Contribution(group.Key.ExerciseDefinitionId, group.Key.MuscleGroupId));
            _byExercise = index.ExerciseIds().ToDictionary(
                exerciseId => exerciseId,
                exerciseId => index.ContributionsFor(exerciseId).Select(row => new ExerciseMuscleContribution
                {
                    Id = row.Id,
                    ExerciseId = row.ExerciseDefinitionId,
                    MuscleGroupId = row.MuscleGroupId,
                    Fraction = row.Fraction
                }).ToList());

            _loaded = true;
        }
        finally
        {
            _loadLock.Release();
        }
    }

    public double Contribution(int exerciseId, int muscleGroupId)
    {
        EnsureLoaded();
        return _map.GetValueOrDefault((exerciseId, muscleGroupId), 0.0);
    }

    public IReadOnlyList<ExerciseMuscleContribution> GetContributions(int exerciseId)
    {
        EnsureLoaded();
        return _byExercise.GetValueOrDefault(exerciseId, []);
    }

    private void EnsureLoaded()
    {
        if (!_loaded)
            throw new InvalidOperationException(
                "Contribution lookup must be loaded before it is read.");
    }
}

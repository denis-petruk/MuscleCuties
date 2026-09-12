using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IContributionLookup
{
    double Contribution(int exerciseId, int muscleGroupId);
    IReadOnlyList<ExerciseMuscleContribution> GetContributions(int exerciseId);
    Task LoadAsync();
}

public class ContributionLookup : IContributionLookup
{
    private readonly AppDatabase _db;
    private readonly SemaphoreSlim _loadLock = new(1, 1);
    private Dictionary<(int ExerciseId, int MuscleGroupId), double> _map = new();
    private Dictionary<int, List<ExerciseMuscleContribution>> _byExercise = new();
    private bool _loaded;

    public ContributionLookup(AppDatabase db)
    {
        _db = db;
    }

    public async Task LoadAsync()
    {
        if (_loaded)
            return;

        await _loadLock.WaitAsync();
        try
        {
            if (_loaded)
                return;

            var contributions = await _db.ExerciseMuscleContributions
                .AsNoTracking()
                .ToListAsync();

            _map = contributions.ToDictionary(
                contribution => (contribution.ExerciseId, contribution.MuscleGroupId),
                contribution => contribution.Fraction);

            _byExercise = contributions
                .GroupBy(contribution => contribution.ExerciseId)
                .ToDictionary(group => group.Key, group => group.ToList());

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

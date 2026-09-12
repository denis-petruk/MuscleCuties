using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Enums.Users;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IVolumeBudgetResolver
{
    Task<Dictionary<int, double>> ResolveBudgetAsync(
        int daysPerWeek,
        TrainingExperienceLevel experience,
        UserGoal goal,
        int? sessionMinutesTarget = null);
}

public class VolumeBudgetResolver : IVolumeBudgetResolver
{
    private const int GlutesMuscleId = 1;
    private const double GlutesCeiling = 26.0;
    private const double DefaultCeiling = 22.0;
    private const double ShortSessionTierCScale = 0.6;
    private const int ShortSessionThreshold = 35;

    private readonly AppDatabase _db;

    public VolumeBudgetResolver(AppDatabase db)
    {
        _db = db;
    }

    public async Task<Dictionary<int, double>> ResolveBudgetAsync(
        int daysPerWeek,
        TrainingExperienceLevel experience,
        UserGoal goal,
        int? sessionMinutesTarget = null)
    {
        daysPerWeek = Math.Clamp(daysPerWeek, 2, 6);

        var baseRows = await _db.VolumeBudgetRows
            .AsNoTracking()
            .Where(r => r.DaysPerWeek == daysPerWeek)
            .ToListAsync();

        double modifier = ExperienceModifier(experience);

        var budget = new Dictionary<int, double>(baseRows.Count);
        foreach (var row in baseRows)
        {
            double scaled = row.FractionalSets * modifier;
            double ceiling = row.MuscleGroupId == GlutesMuscleId
                ? GlutesCeiling
                : DefaultCeiling;
            budget[row.MuscleGroupId] = Math.Min(scaled, ceiling);
        }

        if (sessionMinutesTarget is <= ShortSessionThreshold)
        {
            var tierCMuscles = (await _db.GoalTierWeights
                .AsNoTracking()
                .Where(t => t.Goal == goal && t.Tier == PriorityTier.C)
                .Select(t => t.MuscleGroupId)
                .ToListAsync()).ToHashSet();

            foreach (var muscleId in tierCMuscles)
            {
                if (budget.TryGetValue(muscleId, out var current))
                    budget[muscleId] = current * ShortSessionTierCScale;
            }
        }

        return budget;
    }

    private static double ExperienceModifier(TrainingExperienceLevel experience) =>
        experience switch
        {
            TrainingExperienceLevel.Beginner => 0.75,
            TrainingExperienceLevel.Intermediate => 1.0,
            TrainingExperienceLevel.Advanced => 1.1,
            _ => 0.75 // Unknown defaults to novice modifier
        };
}

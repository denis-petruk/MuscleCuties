using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Repositories.Workout.Planning;

public interface IWorkoutPlanningConfigRepository
{
    Task<WorkoutPlanningConfig> LoadAsync();
}

public class WorkoutPlanningConfigRepository : IWorkoutPlanningConfigRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly AppDatabase _db;

    public WorkoutPlanningConfigRepository(AppDatabase db)
    {
        _db = db;
    }

    public async Task<WorkoutPlanningConfig> LoadAsync()
    {
        var entries = await _db.WorkoutPlanningConfigEntries
            .AsNoTracking()
            .ToListAsync();

        var lookup = entries
            .GroupBy(e => e.Section)
            .ToDictionary(g => g.Key, g => g.ToDictionary(e => e.Key, e => e.Value));

        var config = new WorkoutPlanningConfig();

        if (lookup.TryGetValue("readiness", out var readiness))
            config.Readiness = BuildReadinessConfig(readiness);

        if (lookup.TryGetValue("gating", out var gating))
            config.Gating = BuildGatingConfig(gating);

        return config;
    }

    private static ReadinessConfig BuildReadinessConfig(Dictionary<string, string> values)
    {
        var config = new ReadinessConfig();

        if (values.TryGetValue("sleepLastNight", out var sleepLastNight))
            config.SleepLastNight = JsonSerializer.Deserialize<double[][]>(sleepLastNight, JsonOptions) ?? [];

        if (values.TryGetValue("sleep3dAvg", out var sleep3dAvg))
            config.Sleep3dAvg = JsonSerializer.Deserialize<double[][]>(sleep3dAvg, JsonOptions) ?? [];

        if (values.TryGetValue("stepsDeltaPct", out var stepsDelta))
            config.StepsDeltaPct = JsonSerializer.Deserialize<StepsDeltaConfig>(stepsDelta, JsonOptions) ?? new();

        if (values.TryGetValue("energyMultiplier", out var energy))
            config.EnergyMultiplier = int.Parse(energy);

        if (values.TryGetValue("pain", out var pain))
            config.Pain = JsonSerializer.Deserialize<int[]>(pain, JsonOptions) ?? [15, 10, 3, 0];

        if (values.TryGetValue("phasePrior", out var phasePrior))
            config.PhasePrior = JsonSerializer.Deserialize<Dictionary<string, int>>(phasePrior, JsonOptions) ?? new();

        if (values.TryGetValue("highTier", out var highTier))
            config.HighTier = int.Parse(highTier);

        if (values.TryGetValue("moderateTier", out var moderateTier))
            config.ModerateTier = int.Parse(moderateTier);

        return config;
    }

    private static GatingConfig BuildGatingConfig(Dictionary<string, string> values)
    {
        var config = new GatingConfig();

        if (values.TryGetValue("hiitMinSleep", out var hiitMinSleep))
            config.HiitMinSleep = double.Parse(hiitMinSleep);

        if (values.TryGetValue("hiitMaxPain", out var hiitMaxPain))
            config.HiitMaxPain = int.Parse(hiitMaxPain);

        if (values.TryGetValue("hiitMenstrualMinEnergy", out var hiitEnergy))
            config.HiitMenstrualMinEnergy = int.Parse(hiitEnergy);

        if (values.TryGetValue("lowReadinessSetMultiplier", out var setMul))
            config.LowReadinessSetMultiplier = double.Parse(setMul);

        if (values.TryGetValue("lowReadinessRpeCap", out var rpeCap))
            config.LowReadinessRpeCap = int.Parse(rpeCap);

        if (values.TryGetValue("consecutiveLowDaysToRest", out var consecutive))
            config.ConsecutiveLowDaysToRest = int.Parse(consecutive);

        return config;
    }
}

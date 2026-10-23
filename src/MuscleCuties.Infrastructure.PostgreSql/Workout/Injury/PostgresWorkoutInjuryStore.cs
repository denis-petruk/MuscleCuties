using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Injury;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Injury;

public sealed class PostgresWorkoutInjuryStore(PostgresDbContext dbContext) : IServerWorkoutInjuryStore
{
    private const int MaxSwapCandidates = 8;
    private const double PrimaryMuscleMinFraction = 0.2;
    private const double SignificantMuscleFraction = 0.3;
    private const int MaxDisplayMuscles = 3;
    private const int MatchScoreMultiplier = 100;
    private const double MatchDifferenceDivisor = 2.0;

    public async Task<WorkoutInjuryImpactDto> GetImpactAsync(
        Guid userId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<WorkoutInjuryImpactDto>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var logs = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .Where(l => l.UserId == userId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var activeLogs = logs
                .Where(l => !string.Equals(l.Status, "Cleared", StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (activeLogs.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new WorkoutInjuryImpactDto(false, string.Empty, [], [], []);
            }

            var activeFlags = (InjuryFlag)activeLogs.Aggregate(0, (acc, l) => acc | l.SiteFlag);
            var injuries = activeLogs
                .Select(l => new MuscleCuties.Core.Models.Workout.Planning.Injury(
                    (InjuryFlag)l.SiteFlag,
                    Enum.TryParse<InjuryStatus>(l.Status, true, out var s) ? s : InjuryStatus.Acute,
                    l.Since))
                .ToList();

            var blockedActivities = FourWeekPlanRule.GetBlockedActivities(injuries)
                .Select(a => a.ToString())
                .OrderBy(a => a)
                .ToList();

            var allExercises = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var blocked = allExercises
                .Where(e => (e.Contraindications & activeFlags) != InjuryFlag.None)
                .Select(e => e.Name)
                .OrderBy(n => n)
                .ToList();

            var rehab = allExercises
                .Where(e => (e.PreferredFor & activeFlags) != InjuryFlag.None)
                .Select(e => e.Name)
                .OrderBy(n => n)
                .ToList();

            var sites = activeLogs.Select(l => $"{GetSiteName((InjuryFlag)l.SiteFlag)} ({l.Status})");
            var summary = string.Join(", ", sites);
            if (blocked.Count > 0)
                summary += $" -- {blocked.Count} exercises blocked";

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new WorkoutInjuryImpactDto(true, summary, blocked, rehab, blockedActivities);
        }).ConfigureAwait(false);

    public async Task<ExerciseContraindicationCheckDto> CheckContraindicationAsync(
        Guid userId,
        int exerciseDefinitionId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<ExerciseContraindicationCheckDto>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var exercise = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == exerciseDefinitionId, cancellationToken)
                .ConfigureAwait(false);

            if (exercise is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new ExerciseContraindicationCheckDto(
                    exerciseDefinitionId,
                    "Unknown Exercise",
                    true,
                    [],
                    "Exercise not found (clinically conservative rejection).");
            }

            var activeLogs = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.Status != "Cleared")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var activeFlags = (InjuryFlag)activeLogs.Aggregate(0, (acc, l) => acc | l.SiteFlag);
            var isContraindicated = (exercise.Contraindications & activeFlags) != InjuryFlag.None;
            var activeSites = activeLogs.Select(l => GetSiteName((InjuryFlag)l.SiteFlag)).Distinct().ToList();
            var reason = isContraindicated
                ? $"Exercise '{exercise.Name}' conflicts with active injury: {string.Join(", ", activeSites)}"
                : null;

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new ExerciseContraindicationCheckDto(
                exercise.Id,
                exercise.Name,
                isContraindicated,
                activeSites,
                reason);
        }).ConfigureAwait(false);

    public async Task<IReadOnlyList<WorkoutSwapCandidateDto>> GetSafeSwapCandidatesAsync(
        Guid userId,
        int exerciseDefinitionId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<IReadOnlyList<WorkoutSwapCandidateDto>>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var target = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == exerciseDefinitionId, cancellationToken)
                .ConfigureAwait(false);

            if (target is null)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Array.Empty<WorkoutSwapCandidateDto>();
            }

            var targetContribs = await dbContext.ExerciseMuscleContributions.AsNoTracking()
                .Where(c => c.ExerciseId == exerciseDefinitionId)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (targetContribs.Count == 0)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return Array.Empty<WorkoutSwapCandidateDto>();
            }

            var primaryMuscleId = targetContribs.OrderByDescending(c => c.Fraction).First().MuscleGroupId;

            var activeLogs = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.Status != "Cleared")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var activeFlags = (InjuryFlag)activeLogs.Aggregate(0, (acc, l) => acc | l.SiteFlag);

            var profile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var equipment = ResolveEquipment(profile?.EquipmentLevel);

            var allExercises = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var allContribs = await dbContext.ExerciseMuscleContributions.AsNoTracking()
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var contribsByExercise = allContribs
                .GroupBy(c => c.ExerciseId)
                .ToDictionary(g => g.Key, g => (IReadOnlyList<ExerciseMuscleContribution>)g.ToList());

            var muscleGroups = await dbContext.WorkoutMuscleGroups.AsNoTracking()
                .ToDictionaryAsync(mg => mg.Id, mg => mg.Name, cancellationToken)
                .ConfigureAwait(false);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            var candidates = allExercises
                .Where(e => e.Id != exerciseDefinitionId)
                .Where(e => (e.Contraindications & activeFlags) == InjuryFlag.None)
                .Where(e => e.IsBodyweight || (e.Required & equipment) == e.Required)
                .Select(e =>
                {
                    var contribs = contribsByExercise.TryGetValue(e.Id, out var c) ? c : [];
                    var score = ComputeMuscleMatchScore(targetContribs, contribs, primaryMuscleId);
                    return (Exercise: e, Score: score, Contribs: contribs);
                })
                .Where(x => x.Score > 0)
                .OrderByDescending(x => x.Score)
                .Take(MaxSwapCandidates)
                .Select(x => new WorkoutSwapCandidateDto(
                    x.Exercise.Id,
                    x.Exercise.Name,
                    $"{(int)(x.Score * MatchScoreMultiplier)}% muscle match",
                    x.Exercise.IsBodyweight ? "Bodyweight" : x.Exercise.Required.ToString(),
                    string.Join(", ", x.Contribs
                        .Where(c => (double)c.Fraction >= SignificantMuscleFraction)
                        .OrderByDescending(c => c.Fraction)
                        .Take(MaxDisplayMuscles)
                        .Select(c => muscleGroups.GetValueOrDefault(c.MuscleGroupId, string.Empty)))))
                .ToList();

            return candidates;
        }).ConfigureAwait(false);

    public async Task<bool> SwapExerciseAsync(
        Guid userId,
        Guid workoutDayExerciseId,
        int newExerciseDefinitionId,
        bool savePreference,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var dayExercise = await dbContext.WorkoutDayExercises
                .SingleOrDefaultAsync(e => e.UserId == userId && e.Id == workoutDayExerciseId, cancellationToken)
                .ConfigureAwait(false);

            if (dayExercise is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var newDef = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .SingleOrDefaultAsync(e => e.Id == newExerciseDefinitionId, cancellationToken)
                .ConfigureAwait(false);

            if (newDef is null)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var activeLogs = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .Where(l => l.UserId == userId && l.Status != "Cleared")
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            var activeFlags = (InjuryFlag)activeLogs.Aggregate(0, (acc, l) => acc | l.SiteFlag);
            if ((newDef.Contraindications & activeFlags) != InjuryFlag.None)
                throw new InvalidOperationException($"Exercise '{newDef.Name}' is contraindicated by active injuries.");

            var profile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken)
                .ConfigureAwait(false);

            var equipment = ResolveEquipment(profile?.EquipmentLevel);
            if (!newDef.IsBodyweight && (newDef.Required & equipment) != newDef.Required)
                throw new InvalidOperationException($"Exercise '{newDef.Name}' requires equipment not available in current profile.");

            var originalExerciseId = dayExercise.ExerciseId;
            dayExercise.ExerciseId = newExerciseDefinitionId;

            if (savePreference)
            {
                var existingPref = await dbContext.UserExercisePreferences
                    .SingleOrDefaultAsync(p => p.UserId == userId && p.OriginalExerciseId == originalExerciseId, cancellationToken)
                    .ConfigureAwait(false);

                if (existingPref is not null)
                {
                    existingPref.PreferredExerciseId = newExerciseDefinitionId;
                }
                else
                {
                    dbContext.UserExercisePreferences.Add(new UserExercisePreferenceRecord
                    {
                        Id = Guid.NewGuid(),
                        UserId = userId,
                        OriginalExerciseId = originalExerciseId,
                        PreferredExerciseId = newExerciseDefinitionId,
                        CreatedAt = DateTime.UtcNow
                    });
                }
            }

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return true;
        }).ConfigureAwait(false);

    private static string GetSiteName(InjuryFlag site) => site switch
    {
        InjuryFlag.Metatarsal => "Foot",
        InjuryFlag.LowBack => "Low back",
        _ => site.ToString()
    };

    private static EquipmentSet ResolveEquipment(string? equipmentLevel)
    {
        if (string.IsNullOrWhiteSpace(equipmentLevel))
            return ExercisePickerRule.MapEquipment(Equipment.FullGym);

        return Enum.TryParse<Equipment>(equipmentLevel, true, out var eq)
            ? ExercisePickerRule.MapEquipment(eq)
            : ExercisePickerRule.MapEquipment(Equipment.FullGym);
    }

    private static double ComputeMuscleMatchScore(
        IReadOnlyList<ExerciseMuscleContribution> original,
        IReadOnlyList<ExerciseMuscleContribution> candidate,
        int primaryMuscleId)
    {
        if (candidate.Count == 0)
            return 0.0;

        var candidatePrimary = candidate.FirstOrDefault(c => c.MuscleGroupId == primaryMuscleId);
        if (candidatePrimary is null || (double)candidatePrimary.Fraction < PrimaryMuscleMinFraction)
            return 0.0;

        var originalMap = original.ToDictionary(c => c.MuscleGroupId, c => (double)c.Fraction);
        var candidateMap = candidate.ToDictionary(c => c.MuscleGroupId, c => (double)c.Fraction);

        var allMuscles = originalMap.Keys.Union(candidateMap.Keys).ToList();
        if (allMuscles.Count == 0)
            return 0.0;

        double totalDifference = 0;
        foreach (var muscleId in allMuscles)
        {
            var origFraction = originalMap.GetValueOrDefault(muscleId, 0.0);
            var candFraction = candidateMap.GetValueOrDefault(muscleId, 0.0);
            totalDifference += Math.Abs(origFraction - candFraction);
        }

        return Math.Max(0.0, 1.0 - totalDifference / MatchDifferenceDivisor);
    }
}

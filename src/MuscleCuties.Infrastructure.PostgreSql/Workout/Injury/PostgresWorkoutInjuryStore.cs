using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Injury;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Injury;

/// <summary>Injuries as the workout side reports them, with the status the planner reads to withhold load.</summary>
public sealed class PostgresWorkoutInjuryStore(PostgresDbContext dbContext) : IServerWorkoutInjuryStore
{
    /// <summary>The sessions and exercises an injury affects.</summary>
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

            var blockedActivities = InjuryActivityRestrictionRule.GetBlockedActivities(injuries)
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

    /// <summary>The readable name of an injury site.</summary>
    private static string GetSiteName(InjuryFlag site) => site switch
    {
        InjuryFlag.Metatarsal => "Foot",
        InjuryFlag.LowBack => "Low back",
        _ => site.ToString()
    };
}

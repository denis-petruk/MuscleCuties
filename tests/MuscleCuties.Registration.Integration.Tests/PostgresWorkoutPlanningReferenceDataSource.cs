using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Maps PostgreSQL planning reference rows to persistence-neutral planner inputs.</summary>
public sealed class PostgresWorkoutPlanningReferenceDataSource(PostgresDbContext database)
    : IWorkoutPlanningReferenceDataSource
{
    public async Task<IReadOnlyList<PlanningExercise>> GetExercisesAsync(
        CancellationToken cancellationToken = default) =>
        await database.Exercises.AsNoTracking()
            .OrderBy(exercise => exercise.Id)
            .Select(exercise => new PlanningExercise(
                exercise.Id,
                exercise.Code,
                exercise.Name,
                exercise.Description,
                exercise.PrimaryMuscle,
                exercise.SecondaryMuscles,
                exercise.JointAreas,
                exercise.IsInjuryFriendly))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PlanningExerciseDefinition>> GetExerciseDefinitionsAsync(
        IReadOnlyCollection<MovementPattern>? patterns = null,
        CancellationToken cancellationToken = default)
    {
        var query = database.WorkoutExerciseDefinitions.AsNoTracking().AsQueryable();
        if (patterns is { Count: > 0 })
            query = query.Where(definition => patterns.Contains(definition.Pattern));

        var rows = await query.Include(definition => definition.MuscleContributions)
            .OrderBy(definition => definition.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(definition => new PlanningExerciseDefinition(
                definition.Id,
                definition.Name,
                definition.Pattern,
                definition.Required,
                definition.LongLengthLoaded,
                definition.FatigueCost,
                definition.SkillDemand,
                definition.SetupSeconds,
                definition.SecondsPerRep,
                definition.EligibleForHiBlock,
                definition.UnilateralDoublesTime,
                definition.IsBodyweight,
                definition.Contraindications,
                definition.PreferredFor,
                definition.SupineOrProne,
                definition.ValsalvaDemand,
                definition.SubstituteGroupId,
                definition.MuscleContributions.OrderBy(contribution => contribution.MuscleGroupId)
                    .Select(contribution => new PlanningMuscleContribution(
                        contribution.Id,
                        contribution.ExerciseId,
                        contribution.MuscleGroupId,
                        (double)contribution.Fraction))
                    .ToArray()))
            .ToArray();
    }

    public async Task<IReadOnlyList<PlanningArchetype>> GetArchetypesAsync(
        IReadOnlyCollection<int> archetypeIds,
        CancellationToken cancellationToken = default)
    {
        if (archetypeIds.Count == 0)
            return [];

        var rows = await database.SessionArchetypes.AsNoTracking()
            .Where(archetype => archetypeIds.Contains(archetype.Id))
            .Include(archetype => archetype.SlotTemplates)
            .OrderBy(archetype => archetype.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        return rows.Select(archetype => new PlanningArchetype(
                archetype.Id,
                archetype.Code,
                archetype.DisplayName,
                archetype.IsLowerDominant,
                archetype.ContainsHeavyHinge,
                archetype.SlotTemplates.OrderBy(slot => slot.Order)
                    .Select(slot => new PlanningSlot(
                        slot.Id,
                        slot.ArchetypeId,
                        slot.Order,
                        slot.Block,
                        NormalizeAllowedPatterns(slot.AllowedPatternsJson),
                        slot.PrimaryMuscleId,
                        slot.SetsMin,
                        slot.SetsMax,
                        slot.RepsMin,
                        slot.RepsMax,
                        slot.TargetRir,
                        slot.Droppable,
                        slot.SupersetGroup))
                    .ToArray()))
            .ToArray();
    }

    public async Task<PlanningWeekTemplate?> FindWeekTemplateAsync(
        int daysPerWeek,
        CancellationToken cancellationToken = default)
    {
        var row = await database.WeekTemplates.AsNoTracking()
            .Where(template => template.DaysPerWeek == daysPerWeek)
            .OrderBy(template => template.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        return row is null ? null : new PlanningWeekTemplate(
            row.Id, row.DaysPerWeek, NormalizeArchetypeSequence(row.ArchetypeSequenceJson));
    }

    private static string NormalizeArchetypeSequence(string sequence)
    {
        if (!sequence.StartsWith("[", StringComparison.Ordinal))
            return sequence;

        try
        {
            var ids = JsonSerializer.Deserialize<int[]>(sequence);
            return ids is null ? sequence : string.Join(',', ids);
        }
        catch (JsonException)
        {
            return sequence;
        }
    }

    private static string NormalizeAllowedPatterns(string patterns)
    {
        if (!patterns.StartsWith("[", StringComparison.Ordinal))
            return patterns;

        try
        {
            var names = JsonSerializer.Deserialize<string[]>(patterns);
            return names is null ? patterns : string.Join(',', names);
        }
        catch (JsonException)
        {
            return patterns;
        }
    }

    public async Task<IReadOnlyList<PlanningVolumeBudget>> GetVolumeBudgetsAsync(
        int daysPerWeek,
        CancellationToken cancellationToken = default) =>
        await database.VolumeBudgetRows.AsNoTracking()
            .Where(row => row.DaysPerWeek == daysPerWeek)
            .OrderBy(row => row.MuscleGroupId)
            .ThenBy(row => row.Id)
            .Select(row => new PlanningVolumeBudget(row.Id, row.DaysPerWeek, row.MuscleGroupId, (double)row.FractionalSets))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PlanningGoalTierWeight>> GetGoalTierWeightsAsync(
        UserGoal goal,
        bool tierCOnly,
        CancellationToken cancellationToken = default) =>
        await database.GoalTierWeights.AsNoTracking()
            .Where(row => row.Goal == goal && (!tierCOnly || row.Tier == PriorityTier.C))
            .OrderBy(row => row.MuscleGroupId)
            .ThenBy(row => row.Id)
            .Select(row => new PlanningGoalTierWeight(row.Id, row.Goal, row.MuscleGroupId, row.Tier))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<PlanningExercisePreference>> GetExercisePreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return [];

        if (database.Database.CurrentTransaction is not null)
            return await QueryExercisePreferencesAsync(userId, cancellationToken).ConfigureAwait(false);

        return await database.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            database.ChangeTracker.Clear();
            await using var transaction = await database.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(database, userId, cancellationToken)
                .ConfigureAwait(false);
            var preferences = await QueryExercisePreferencesAsync(userId, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return preferences;
        }).ConfigureAwait(false);
    }

    private async Task<IReadOnlyList<PlanningExercisePreference>> QueryExercisePreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken) =>
        await database.UserExercisePreferences.AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .OrderBy(preference => preference.OriginalExerciseId)
            .Select(preference => new PlanningExercisePreference(
                preference.Id,
                preference.UserId,
                preference.OriginalExerciseId,
                preference.PreferredExerciseId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}

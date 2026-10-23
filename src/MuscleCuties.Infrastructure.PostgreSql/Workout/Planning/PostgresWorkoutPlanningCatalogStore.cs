using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public sealed class PostgresWorkoutPlanningCatalogStore(PostgresDbContext database) : IWorkoutPlanningCatalogStore
{
    public async Task<IReadOnlyList<WorkoutExercisePreference>> GetUserExercisePreferencesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        if (userId == Guid.Empty)
            return [];

        return await database.UserExercisePreferences.AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .OrderBy(preference => preference.OriginalExerciseId)
            .Select(preference => new WorkoutExercisePreference(
                preference.OriginalExerciseId,
                preference.PreferredExerciseId))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<WorkoutPlanningCatalogResponse> GetCatalogAsync(CancellationToken cancellationToken = default)
    {
        var exercises = await database.Exercises.AsNoTracking()
            .OrderBy(exercise => exercise.Id)
            .Select(exercise => new WorkoutCatalogExercise(
                exercise.Id,
                exercise.Code,
                exercise.Name,
                exercise.Description,
                exercise.PrimaryMuscle,
                exercise.SecondaryMuscles,
                exercise.JointAreas,
                exercise.IsInjuryFriendly))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var muscleGroups = await database.WorkoutMuscleGroups.AsNoTracking()
            .OrderBy(group => group.Id)
            .Select(group => new WorkoutCatalogMuscleGroup(group.Id, group.Name))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var definitionRows = await database.WorkoutExerciseDefinitions.AsNoTracking()
            .Include(definition => definition.MuscleContributions)
            .OrderBy(definition => definition.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var definitions = definitionRows.Select(definition => new WorkoutCatalogExerciseDefinition(
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
                    .Select(contribution => new WorkoutCatalogContribution(contribution.MuscleGroupId, contribution.Fraction))
                    .ToArray()))
            .ToArray();

        var archetypeRows = await database.SessionArchetypes.AsNoTracking()
            .Include(archetype => archetype.SlotTemplates)
            .OrderBy(archetype => archetype.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var archetypes = archetypeRows.Select(archetype => new WorkoutCatalogArchetype(
                archetype.Id,
                archetype.Code,
                archetype.DisplayName,
                archetype.IsLowerDominant,
                archetype.ContainsHeavyHinge,
                archetype.SlotTemplates.OrderBy(slot => slot.Order)
                    .Select(slot => new WorkoutCatalogSlot(
                        slot.Id,
                        slot.Order,
                        slot.Block,
                        slot.AllowedPatternsJson,
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

        var weekTemplates = await database.WeekTemplates.AsNoTracking()
            .OrderBy(template => template.DaysPerWeek)
            .Select(template => new WorkoutCatalogWeekTemplate(template.DaysPerWeek, template.ArchetypeSequenceJson))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var volumeBudgets = await database.VolumeBudgetRows.AsNoTracking()
            .OrderBy(row => row.DaysPerWeek)
            .ThenBy(row => row.MuscleGroupId)
            .Select(row => new WorkoutCatalogVolumeBudget(row.DaysPerWeek, row.MuscleGroupId, row.FractionalSets))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var goalTierWeights = await database.GoalTierWeights.AsNoTracking()
            .OrderBy(row => row.Goal)
            .ThenBy(row => row.MuscleGroupId)
            .Select(row => new WorkoutCatalogGoalTierWeight(row.Goal, row.MuscleGroupId, row.Tier))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var planningConfigEntries = await database.WorkoutPlanningConfigEntries.AsNoTracking()
            .OrderBy(row => row.Section)
            .ThenBy(row => row.Key)
            .Select(row => new WorkoutCatalogConfigEntry(row.Section, row.Key, row.Value))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new WorkoutPlanningCatalogResponse(
            exercises,
            muscleGroups,
            definitions,
            archetypes,
            weekTemplates,
            volumeBudgets,
            goalTierWeights,
            planningConfigEntries);
    }
}

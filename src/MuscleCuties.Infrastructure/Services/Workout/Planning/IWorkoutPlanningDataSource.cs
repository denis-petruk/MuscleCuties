using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Workout;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

/// <summary>Provides the reference data consumed by the deterministic workout planner.</summary>
public interface IWorkoutPlanningDataSource
{
    Task<WorkoutPlanningSnapshot> GetPlanningSnapshotAsync(
        int localUserId,
        CancellationToken cancellationToken = default);
    Task EnsurePlanningCatalogAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ExerciseMuscleContribution>> GetContributionsAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Exercise>> GetExerciseLibraryAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<WorkoutExerciseDefinition>> GetExerciseDefinitionsAsync(
        IReadOnlyCollection<MovementPattern>? patterns = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<UserExercisePreference>> GetUserExercisePreferencesAsync(
        int userId,
        CancellationToken cancellationToken = default);
    Task<WeekTemplate?> FindWeekTemplateAsync(int daysPerWeek, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SessionArchetype>> GetSessionArchetypesAsync(
        IReadOnlyCollection<int> archetypeIds,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetGoalTierMuscleGroupIdsAsync(
        UserGoal goal,
        bool tierCOnly,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VolumeBudgetRow>> GetVolumeBudgetRowsAsync(
        int daysPerWeek,
        CancellationToken cancellationToken = default);
}

/// <summary>Signals that planning cannot safely produce a replacement workout plan.</summary>
public class WorkoutPlanningFailureException(string message) : InvalidOperationException(message)
{
    public const string SafeFailureMessage = "A safe workout plan could not be generated from the available planning data.";

    public WorkoutPlanningFailureException()
        : this(SafeFailureMessage)
    {
    }
}

/// <summary>Signals that workout reference data is unsafe for generating a replacement plan.</summary>
public sealed class WorkoutPlanningCatalogException(string message) : WorkoutPlanningFailureException(message)
{
    public const string ValidationMessagePrefix = "Workout planning catalog failure:";
    public new const string SafeFailureMessage = "Workout planning reference data is incomplete or malformed.";

    public static bool IsCatalogFailure(PlannedWeek week) => week.Validation.Entries.Any(entry =>
        entry.Severity == ValidationSeverity.Error &&
        entry.Message.StartsWith(ValidationMessagePrefix, StringComparison.Ordinal));

    public static string CreateValidationMessage(string detail) => $"{ValidationMessagePrefix} {detail}";

    public WorkoutPlanningCatalogException()
        : this(SafeFailureMessage)
    {
    }
}

/// <summary>Maintains the existing local database path while planner algorithms migrate to a data source.</summary>
public sealed class AppDatabaseWorkoutPlanningDataSource(AppDatabase database) : IWorkoutPlanningDataSource
{
    public async Task<WorkoutPlanningSnapshot> GetPlanningSnapshotAsync(
        int localUserId,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var exercises = await GetExerciseLibraryAsync(cancellationToken).ConfigureAwait(false);
        var definitions = await GetExerciseDefinitionsAsync(cancellationToken: cancellationToken).ConfigureAwait(false);
        var contributions = await GetContributionsAsync(cancellationToken).ConfigureAwait(false);
        var archetypes = await GetSessionArchetypesAsync(
            await database.SessionArchetypes.AsNoTracking().Select(row => row.Id).ToListAsync(cancellationToken)
                .ConfigureAwait(false), cancellationToken).ConfigureAwait(false);
        var weekTemplates = await database.WeekTemplates.AsNoTracking().ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var allVolumeBudgets = await database.VolumeBudgetRows.AsNoTracking().ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var goalWeights = await database.GoalTierWeights.AsNoTracking().ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        var preferences = await GetUserExercisePreferencesAsync(localUserId, cancellationToken).ConfigureAwait(false);

        var contributionMap = contributions.GroupBy(row => row.ExerciseId)
            .ToDictionary(group => group.Key, group => (IReadOnlyList<PlanningMuscleContribution>)group
                .Select(row => new PlanningMuscleContribution(row.Id, row.ExerciseId, row.MuscleGroupId, row.Fraction))
                .ToArray());
        var definitionRecords = definitions.Select(row => new PlanningExerciseDefinition(
            row.Id, row.Name, row.Pattern, row.Required, row.LongLengthLoaded, row.FatigueCost, row.SkillDemand,
            row.SetupSeconds, row.SecondsPerRep, row.EligibleForHiBlock, row.UnilateralDoublesTime, row.IsBodyweight,
            row.Contraindications, row.PreferredFor, row.SupineOrProne, row.ValsalvaDemand, row.SubstituteGroupId,
            contributionMap.GetValueOrDefault(row.Id, []))).ToArray();
        var archetypeRecords = archetypes.Select(row => new PlanningArchetype(
            row.Id, row.Code, row.DisplayName, row.IsLowerDominant, row.ContainsHeavyHinge,
            row.SlotTemplates.OrderBy(slot => slot.Order).Select(slot => new PlanningSlot(
                slot.Id, slot.ArchetypeId, slot.Order, slot.Block, slot.AllowedPatternsJson, slot.PrimaryMuscleId,
                slot.SetsMin, slot.SetsMax, slot.RepsMin, slot.RepsMax, slot.TargetRir, slot.Droppable,
                slot.SupersetGroup)).ToArray())).ToArray();

        return new WorkoutPlanningSnapshot(
            exercises.Select(row => new PlanningExercise(row.Id, row.Code, row.Name, row.Description,
                row.PrimaryMuscle, row.SecondaryMuscles, row.JointAreas, row.IsInjuryFriendly)).ToArray(),
            definitionRecords,
            archetypeRecords,
            weekTemplates.Select(row => new PlanningWeekTemplate(row.Id, row.DaysPerWeek,
                row.ArchetypeSequenceJson)).ToArray(),
            allVolumeBudgets.Select(row => new PlanningVolumeBudget(row.Id, row.DaysPerWeek, row.MuscleGroupId,
                row.FractionalSets)).ToArray(),
            goalWeights.Select(row => new PlanningGoalTierWeight(row.Id, row.Goal, row.MuscleGroupId, row.Tier))
                .ToArray(),
            preferences.Select(row => new PlanningExerciseSubstitution(row.OriginalExerciseId,
                row.PreferredExerciseId)).ToArray());
    }

    public async Task EnsurePlanningCatalogAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await database.SeedWorkoutPlanningDataAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<ExerciseMuscleContribution>> GetContributionsAsync(
        CancellationToken cancellationToken = default) =>
        await database.ExerciseMuscleContributions.AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Exercise>> GetExerciseLibraryAsync(
        CancellationToken cancellationToken = default) =>
        await database.Exercises.AsNoTracking().ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<WorkoutExerciseDefinition>> GetExerciseDefinitionsAsync(
        IReadOnlyCollection<MovementPattern>? patterns = null,
        CancellationToken cancellationToken = default)
    {
        IQueryable<WorkoutExerciseDefinition> query = database.WorkoutExerciseDefinitions.AsNoTracking();
        if (patterns is { Count: 0 })
            return [];
        if (patterns is not null)
            query = query.Where(exercise => patterns.Contains(exercise.Pattern));
        return await query.ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserExercisePreference>> GetUserExercisePreferencesAsync(
        int userId,
        CancellationToken cancellationToken = default) =>
        await database.UserExercisePreferences.AsNoTracking()
            .Where(preference => preference.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

    public async Task<WeekTemplate?> FindWeekTemplateAsync(
        int daysPerWeek,
        CancellationToken cancellationToken = default) =>
        await database.WeekTemplates.AsNoTracking()
            .FirstOrDefaultAsync(template => template.DaysPerWeek == daysPerWeek, cancellationToken)
            .ConfigureAwait(false);

    public async Task<IReadOnlyList<SessionArchetype>> GetSessionArchetypesAsync(
        IReadOnlyCollection<int> archetypeIds,
        CancellationToken cancellationToken = default)
    {
        if (archetypeIds.Count == 0)
            return [];

        return await database.SessionArchetypes.AsNoTracking()
            .Include(archetype => archetype.SlotTemplates)
            .Where(archetype => archetypeIds.Contains(archetype.Id))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<int>> GetGoalTierMuscleGroupIdsAsync(
        UserGoal goal,
        bool tierCOnly,
        CancellationToken cancellationToken = default)
    {
        var query = database.GoalTierWeights.AsNoTracking().Where(weight => weight.Goal == goal);
        query = tierCOnly
            ? query.Where(weight => weight.Tier == PriorityTier.C)
            : query.Where(weight => weight.Tier != PriorityTier.C);
        return await query.Select(weight => weight.MuscleGroupId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<VolumeBudgetRow>> GetVolumeBudgetRowsAsync(
        int daysPerWeek,
        CancellationToken cancellationToken = default) =>
        await database.VolumeBudgetRows.AsNoTracking()
            .Where(row => row.DaysPerWeek == daysPerWeek)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
}

using MuscleCuties.Core.Data;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public interface IWeekPlanGenerator
{
    Task<PlannedWeek> GenerateWeekAsync(WeekGenerationInput input, CancellationToken cancellationToken = default);
}

public class WeekPlanGenerator : IWeekPlanGenerator
{
    private readonly IVolumeBudgetResolver _budgetResolver;
    private readonly IContributionLookup _contributions;
    private readonly IWorkoutPlanningDataSource _dataSource;

    public WeekPlanGenerator(
        AppDatabase db,
        IContributionLookup contributions,
        IVolumeBudgetResolver budgetResolver)
        : this(new AppDatabaseWorkoutPlanningDataSource(db), contributions, budgetResolver)
    {
    }

    public WeekPlanGenerator(
        IWorkoutPlanningDataSource dataSource,
        IContributionLookup contributions,
        IVolumeBudgetResolver budgetResolver)
    {
        _dataSource = dataSource ?? throw new ArgumentNullException(nameof(dataSource));
        _contributions = contributions ?? throw new ArgumentNullException(nameof(contributions));
        _budgetResolver = budgetResolver ?? throw new ArgumentNullException(nameof(budgetResolver));
    }

    public async Task<PlannedWeek> GenerateWeekAsync(
        WeekGenerationInput input,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(input);
        cancellationToken.ThrowIfCancellationRequested();
        await _contributions.LoadAsync(cancellationToken).ConfigureAwait(false);

        var daysPerWeek = WeekPlanRule.ClampDaysPerWeek(input.DaysPerWeek);

        var template = await _dataSource.FindWeekTemplateAsync(daysPerWeek, cancellationToken).ConfigureAwait(false);
        var neutralTemplate = template is null
            ? null
            : new PlanningWeekTemplate(template.Id, template.DaysPerWeek, template.ArchetypeSequenceJson);
        if (!WeekPlanRule.TryValidateTemplate(neutralTemplate, daysPerWeek, out var archetypeSequence, out var templateFailure))
            return templateFailure!;

        var strengthIds = archetypeSequence.Where(id => id > 0).Distinct().ToArray();

        var archetypeRows = await _dataSource.GetSessionArchetypesAsync(strengthIds, cancellationToken)
            .ConfigureAwait(false);
        var neutralArchetypes = archetypeRows?.Select(ToNeutralArchetype).ToArray();
        if (!WeekPlanRule.TryValidateArchetypes(neutralArchetypes, strengthIds, daysPerWeek, out var archetypes, out var archetypeFailure))
            return archetypeFailure!;

        var tierAB = (await _dataSource.GetGoalTierMuscleGroupIdsAsync(
                    input.Goal,
                    tierCOnly: false,
                    cancellationToken: cancellationToken)
                .ConfigureAwait(false))
            .ToHashSet();

        var budget = await _budgetResolver.ResolveBudgetAsync(
                daysPerWeek, input.Experience, input.Goal, input.SessionMinutesTarget, cancellationToken)
            .ConfigureAwait(false);

        var exercises = await _dataSource.GetExerciseDefinitionsAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);
        if (exercises is null || exercises.Count == 0)
            throw new WorkoutPlanningCatalogException(WorkoutPlanningCatalogException.SafeFailureMessage);

        var neutralExercises = exercises.Select(ToNeutralExerciseDefinition).ToArray();
        var contributionIndex = new WorkoutPlanningReferenceIndex(neutralExercises
            .SelectMany(exercise => exercise.MuscleContributions)
            .ToArray());
        var exposureMap = WeekPlanRule.ComputeArchetypeExposures(
            archetypes,
            neutralExercises,
            contributionIndex,
            out var patternMuscleMap);

        return await Task.Run(() => WeekPlanRule.BuildWeek(
            input,
            daysPerWeek,
            archetypeSequence,
            archetypes,
            tierAB,
            budget,
            exposureMap,
            patternMuscleMap), cancellationToken).ConfigureAwait(false);
    }

    private static PlanningArchetype? ToNeutralArchetype(SessionArchetype? archetype) =>
        archetype is null
            ? null
            : new PlanningArchetype(
                archetype.Id,
                archetype.Code,
                archetype.DisplayName,
                archetype.IsLowerDominant,
                archetype.ContainsHeavyHinge,
                archetype.SlotTemplates is null
                    ? null!
                    : archetype.SlotTemplates
                        .Select(slot => slot is null
                            ? null!
                            : new PlanningSlot(
                                slot.Id,
                                slot.ArchetypeId,
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
                        .ToArray());

    private PlanningExerciseDefinition ToNeutralExerciseDefinition(WorkoutExerciseDefinition definition) =>
        new(
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
            _contributions.GetContributions(definition.Id)
                .Select(row => new PlanningMuscleContribution(row.Id, definition.Id, row.MuscleGroupId, row.Fraction))
                .ToArray());
}

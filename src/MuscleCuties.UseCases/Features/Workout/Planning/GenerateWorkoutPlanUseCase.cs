using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning.Contracts;

namespace MuscleCuties.UseCases.Features.Workout.Planning;

/// <summary>Executes the persistence-neutral four-week workout planner on the server using the reference data port.</summary>
public sealed class GenerateWorkoutPlanUseCase(IWorkoutPlanningReferenceDataSource referenceDataSource)
{
    private const int DefaultDaysPerWeek = 3;
    private const int DefaultSessionMinutesCap = 60;
    private const int DefaultBaselineSteps = 8000;

    public async Task<GenerateWorkoutPlanResponse> ExecuteAsync(
        Guid userId,
        GenerateWorkoutPlanRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();
        if (!WorkoutTrainingPolicyV010.CanGeneratePlans)
            throw new InvalidOperationException(WorkoutTrainingPolicyV010.GenerationUnavailableMessage);

        var daysPerWeek = WeekPlanRule.ClampDaysPerWeek(
            request.DaysPerWeek <= 0 ? DefaultDaysPerWeek : request.DaysPerWeek);
        var template = await referenceDataSource.FindWeekTemplateAsync(daysPerWeek, cancellationToken)
            .ConfigureAwait(false);
        if (!WeekPlanRule.TryValidateTemplate(template, daysPerWeek, out var archetypeSequence, out _))
            throw new InvalidOperationException("Workout planning reference data is incomplete or malformed.");

        var strengthIds = archetypeSequence.Where(id => id > 0).Distinct().ToArray();
        var archetypes = await referenceDataSource.GetArchetypesAsync(strengthIds, cancellationToken)
            .ConfigureAwait(false);
        var exercises = await referenceDataSource.GetExercisesAsync(cancellationToken).ConfigureAwait(false);
        var definitions = await referenceDataSource.GetExerciseDefinitionsAsync(null, cancellationToken)
            .ConfigureAwait(false);
        var volumeBudgets = await referenceDataSource.GetVolumeBudgetsAsync(daysPerWeek, cancellationToken)
            .ConfigureAwait(false);
        var goalTierWeights = await referenceDataSource.GetGoalTierWeightsAsync(
                request.Goal,
                tierCOnly: false,
                cancellationToken)
            .ConfigureAwait(false);
        var tierCWeights = await referenceDataSource.GetGoalTierWeightsAsync(
                request.Goal,
                tierCOnly: true,
                cancellationToken)
            .ConfigureAwait(false);
        var preferences = await referenceDataSource.GetExercisePreferencesAsync(userId, cancellationToken)
            .ConfigureAwait(false);

        var snapshot = new WorkoutPlanningSnapshot(
            exercises,
            definitions,
            archetypes,
            template is null ? [] : [template],
            volumeBudgets,
            goalTierWeights.Concat(tierCWeights).DistinctBy(weight => weight.Id).ToArray(),
            preferences.Select(preference =>
                    new PlanningExerciseSubstitution(preference.OriginalExerciseId, preference.PreferredExerciseId))
                .ToArray());

        var selectedActivities = (request.SelectedActivities ?? [])
            .Append(WorkoutActivityType.HighVolumeStrength)
            .ToHashSet();
        var adaptiveProfile = new AdaptiveProfile(
            UserId: 0,
            Goal: request.Goal,
            Experience: request.Experience,
            DaysPerWeek: daysPerWeek,
            SessionMinutesCap: request.SessionMinutesCap > 0 ? request.SessionMinutesCap : DefaultSessionMinutesCap,
            Selected: selectedActivities,
            Style: request.Style,
            Equipment: request.Equipment,
            Injuries: request.Injuries?.ToList() ?? [],
            BaselineSteps: request.BaselineSteps > 0 ? request.BaselineSteps : DefaultBaselineSteps,
            Baselines: request.Baselines);

        var generated = FourWeekPlanRule.GeneratePlan(
            snapshot,
            adaptiveProfile,
            request.Phase,
            request.AllowsCyclePhaseAdjustments,
            request.CreatedAtUtc,
            request.Today,
            request.TodayInputs,
            request.ConsecutiveLowDays);

        return new GenerateWorkoutPlanResponse(
            generated.Name,
            generated.CyclePhaseTarget,
            generated.CreatedAt,
            generated.WorkoutDays.Select(day => new GeneratedWorkoutDayResponse(
                day.DayOfWeek,
                day.WeekIndex,
                day.Date,
                day.WorkoutType,
                day.Name,
                day.Exercises.Select(exercise => new GeneratedWorkoutDayExerciseResponse(
                    exercise.ExerciseId,
                    exercise.ExerciseCode,
                    exercise.ExerciseName,
                    exercise.Sets,
                    exercise.Reps,
                    exercise.DurationSeconds)).ToArray())).ToArray());
    }
}

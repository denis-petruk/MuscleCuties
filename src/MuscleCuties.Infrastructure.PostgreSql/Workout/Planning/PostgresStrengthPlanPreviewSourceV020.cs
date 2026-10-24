using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

public sealed class PostgresStrengthPlanPreviewSourceV020(PostgresDbContext dbContext)
    : IStrengthPlanPreviewSourceV020
{
    public Task<StrengthPlanPreviewSourceV020?> LoadAsync(Guid userId,
        CancellationToken cancellationToken) =>
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);

            var profile = await dbContext.UserProfiles.AsNoTracking()
                .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken)
                .ConfigureAwait(false);
            if (profile is null || !Enum.TryParse<UserGoal>(profile.Goal, true, out var goal) ||
                !Enum.IsDefined(goal) ||
                !Enum.TryParse<Equipment>(profile.EquipmentLevel, true, out var equipment) ||
                !Enum.IsDefined(equipment))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            var definitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
                .Include(row => row.MuscleContributions)
                .ThenInclude(row => row.MuscleGroup)
                .OrderBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
            var goalTierWeights = await dbContext.GoalTierWeights.AsNoTracking()
                .Where(row => row.Goal == goal)
                .OrderBy(row => row.MuscleGroupId).ThenBy(row => row.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var goalPriorityByExerciseId = GoalSpecificAccessoryPriorityRule.Resolve(
                goal,
                definitions.SelectMany(definition => definition.MuscleContributions.Select(contribution =>
                    new PlanningMuscleContribution(contribution.Id, definition.Id,
                        contribution.MuscleGroupId, (double)contribution.Fraction))).ToArray(),
                goalTierWeights.Select(weight => new PlanningGoalTierWeight(
                    weight.Id, weight.Goal, weight.MuscleGroupId, weight.Tier)).ToArray());
            var reviewedSlots = ReviewedExerciseCatalogV020ReviewMap.MapSlots(definitions);
            var preferences = await dbContext.UserExercisePreferences.AsNoTracking()
                .Where(row => row.UserId == userId)
                .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id)
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var injuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.Status != "Cleared")
                .Select(row => new { row.SiteFlag, row.Pain })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var logs = await dbContext.WorkoutExerciseLogs.AsNoTracking()
                .Where(row => row.UserId == userId && row.CompletedSets > 0 && row.CompletedReps > 0)
                .Select(row => new { row.ExerciseId, row.WorkoutLogId, row.WeightKg })
                .ToListAsync(cancellationToken).ConfigureAwait(false);
            var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
                .Where(row => row.UserId == userId && row.IsActive)
                .OrderByDescending(row => row.CreatedAt)
                .Select(row => (Guid?)row.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var acceptedExerciseIds = new HashSet<int>();
            if (activePlanId is { } planId)
            {
                var dayIds = await dbContext.WorkoutDays.AsNoTracking()
                    .Where(row => row.UserId == userId && row.WorkoutPlanId == planId)
                    .Select(row => row.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
                acceptedExerciseIds = (await dbContext.WorkoutDayExercises.AsNoTracking()
                    .Where(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId))
                    .Select(row => row.ExerciseId).ToListAsync(cancellationToken).ConfigureAwait(false))
                    .ToHashSet();
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new StrengthPlanPreviewSourceV020(
                goal, equipment,
                injuries.Aggregate(InjuryFlag.None, (flags, injury) =>
                    flags | (InjuryFlag)injury.SiteFlag),
                injuries.Any(injury => injury.Pain > 0),
                definitions.Select(WorkoutExerciseV020CatalogMapper.Map).ToArray(),
                preferences.GroupBy(row => row.OriginalExerciseId)
                    .ToDictionary(group => group.Key, group => group.Last().PreferredExerciseId),
                acceptedExerciseIds,
                logs.GroupBy(row => row.ExerciseId)
                    .ToDictionary(group => group.Key,
                        group => group.Select(row => row.WorkoutLogId).Distinct().Count()),
                logs.Where(row => row.WeightKg is > 0)
                    .Select(row => row.ExerciseId).ToHashSet(),
                definitions.ToDictionary(row => row.Id, row => (int)row.SecondsPerRep),
                definitions.Where(row => row.MuscleContributions.Any())
                    .ToDictionary(row => row.Id, row => row.MuscleContributions
                        .OrderByDescending(contribution => contribution.Fraction)
                        .ThenBy(contribution => contribution.MuscleGroupId)
                        .First().MuscleGroup?.Name ?? string.Empty),
                reviewedSlots.AllowedIds,
                reviewedSlots.Conditions)
            {
                SelectedActivityTypes = ParseActivityTypes(profile.PreferredWorkoutActivityTypes),
                ExcludedExerciseIds = ParseExcludedExerciseIds(profile.ExcludedStrengthExerciseIdsJson),
                AvailableWeekdays = ParseAvailableWeekdays(profile.AvailableWorkoutWeekdaysJson),
                ActivitySessionDurationsMinutes = ParseActivityDurations(profile.ActivitySessionDurationsJson),
                RequestedSessionsPerWeek = ParseActivityFrequencies(
                    profile.RequestedActivitySessionsPerWeekJson),
                ProfileTrainingExperience = Enum.TryParse<TrainingExperienceLevel>(
                    profile.TrainingExperienceLevel, true, out var trainingExperience) &&
                    Enum.IsDefined(trainingExperience) ? trainingExperience : null,
                GoalPriorityByExerciseId = goalPriorityByExerciseId
            };
        });

    private static IReadOnlySet<int> ParseExcludedExerciseIds(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new HashSet<int>();
            return document.RootElement.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Number &&
                                value.TryGetInt32(out var exerciseId) && exerciseId > 0)
                .Select(value => value.GetInt32())
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<int>();
        }
    }

    private static IReadOnlySet<int> ParseActivityTypes(string? value) =>
        (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .Select(item => Enum.TryParse<WorkoutActivityType>(item, true, out var activity) && Enum.IsDefined(activity)
            ? (int?)activity
            : null)
        .Where(activity => activity.HasValue)
        .Select(activity => activity!.Value)
        .ToHashSet();

    private static IReadOnlySet<DayOfWeek> ParseAvailableWeekdays(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new HashSet<DayOfWeek>();
            return document.RootElement.EnumerateArray()
                .Select(value => value.ValueKind switch
                {
                    JsonValueKind.String when Enum.TryParse<DayOfWeek>(value.GetString(), true, out var day) && Enum.IsDefined(day) => (DayOfWeek?)day,
                    JsonValueKind.Number when value.TryGetInt32(out var dayValue) && Enum.IsDefined(typeof(DayOfWeek), dayValue) => (DayOfWeek?)dayValue,
                    _ => null
                })
                .Where(day => day.HasValue)
                .Select(day => day!.Value)
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<DayOfWeek>();
        }
    }

    private static IReadOnlyDictionary<int, int> ParseActivityDurations(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<int, int>();
            var result = new Dictionary<int, int>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (Enum.TryParse<WorkoutActivityType>(property.Name, true, out var activity) &&
                    Enum.IsDefined(activity) && property.Value.TryGetInt32(out var minutes) &&
                    WorkoutPreferencesRule.IsValidSessionDuration(activity, minutes))
                    result[(int)activity] = minutes;
            }
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<int, int>();
        }
    }

    private static IReadOnlyDictionary<int, int> ParseActivityFrequencies(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                return new Dictionary<int, int>();
            var result = new Dictionary<int, int>();
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (Enum.TryParse<WorkoutActivityType>(property.Name, true, out var activity) &&
                    Enum.IsDefined(activity) && property.Value.TryGetInt32(out var sessionsPerWeek) &&
                    WorkoutPreferencesRule.RequiresExplicitWeeklyFrequency(activity))
                    result[(int)activity] = sessionsPerWeek;
            }
            return result;
        }
        catch (JsonException)
        {
            return new Dictionary<int, int>();
        }
    }

}

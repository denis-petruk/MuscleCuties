using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
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
        dbContext.Database.CreateExecutionStrategy().ExecuteAsync<StrengthPlanPreviewSourceV020?>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await WorkoutSessionAccountLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await PlanningCatalogFingerprint.LockCatalogAsync(dbContext, cancellationToken).ConfigureAwait(false);

            var result = await LoadInCurrentTransactionAsync(userId, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return result;
        });

    // Caller owns the tenant transaction and account/catalog locks.
    internal async Task<StrengthPlanPreviewSourceV020?> LoadInCurrentTransactionAsync(Guid userId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken)
            .ConfigureAwait(false);
        var planningState = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
        var goal = Enum.TryParse<UserGoal>(profile?.Goal, true, out var parsedGoal) &&
            Enum.IsDefined(parsedGoal)
            ? parsedGoal
            : UserGoal.MuscleTone;
        var equipment = Enum.TryParse<Equipment>(profile?.EquipmentLevel, true, out var parsedEquipment) &&
            Enum.IsDefined(parsedEquipment)
            ? parsedEquipment
            : Equipment.FullGym;

        var definitions = await dbContext.WorkoutExerciseDefinitions.AsNoTracking()
            .Include(row => row.MuscleContributions)
            .ThenInclude(row => row.MuscleGroup)
            .OrderBy(row => row.Id).ToListAsync(cancellationToken).ConfigureAwait(false);
        var goalTierWeights = await dbContext.GoalTierWeights.AsNoTracking()
            .OrderBy(row => row.MuscleGroupId).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var goalPriorityByExerciseId = GoalSpecificAccessoryPriorityRule.Resolve(
            goal,
            definitions.SelectMany(definition => definition.MuscleContributions.Select(contribution =>
                new PlanningMuscleContribution(contribution.Id, definition.Id,
                    contribution.MuscleGroupId, (double)contribution.Fraction))).ToArray(),
            goalTierWeights.Where(weight => weight.Goal == goal).Select(weight => new PlanningGoalTierWeight(
                weight.Id, weight.Goal, weight.MuscleGroupId, weight.Tier)).ToArray());
        var floorRows = await ReviewedTemplateFloorSource.LoadAsync(dbContext, cancellationToken).ConfigureAwait(false);
        var reviewedSlots = ReviewedExerciseCatalogV020ReviewMap.MapSlots(definitions);
        var preferences = await dbContext.UserExercisePreferences.AsNoTracking()
            .Where(row => row.UserId == userId)
            .OrderBy(row => row.CreatedAt).ThenBy(row => row.Id)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var injuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
            .Where(row => row.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        DateOnly? today = null;
        if (planningState?.TimeZoneId is { } zoneId)
        {
            try { today = WorkoutAccountCalendarRule.LocalDate(DateTimeOffset.UtcNow, TimeZoneInfo.FindSystemTimeZoneById(zoneId)); }
            catch (TimeZoneNotFoundException) { }
            catch (InvalidTimeZoneException) { }
        }
        var relevantInjuries = injuries.Where(injury => !string.Equals(injury.Status, "Cleared", StringComparison.OrdinalIgnoreCase) ||
            injury.ClearedDate is null || today is null ||
            WorkoutInjuryClearanceRule.IsRestoredExerciseOptional(injury.ClearedDate.Value, today.Value)).ToArray();
        var logs = await dbContext.WorkoutExerciseLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.CompletedSets > 0 && row.CompletedReps > 0)
            .Select(row => new { row.ExerciseId, row.WorkoutLogId, row.WeightKg })
            .OrderBy(row => row.WorkoutLogId).ThenBy(row => row.ExerciseId).ThenBy(row => row.WeightKg)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(row => row.UserId == userId && row.IsActive)
            .OrderByDescending(row => row.CreatedAt)
            .Select(row => (Guid?)row.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var acceptedExerciseIds = new HashSet<int>();
        Guid? activeRevisionId = null;
        if (activePlanId is { } planId)
        {
            activeRevisionId = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
                .Where(row => row.UserId == userId && row.PlanId == planId)
                .OrderByDescending(row => row.RevisionNumber).Select(row => (Guid?)row.Id)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            var dayIds = await dbContext.WorkoutDays.AsNoTracking()
                .Where(row => row.UserId == userId && row.WorkoutPlanId == planId)
                .Select(row => row.Id).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            acceptedExerciseIds = (await dbContext.WorkoutDayExercises.AsNoTracking()
                .Where(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId))
                .Select(row => row.ExerciseId).ToListAsync(cancellationToken).ConfigureAwait(false))
                .ToHashSet();
        }

        return new StrengthPlanPreviewSourceV020(
            goal, equipment,
            relevantInjuries.Aggregate(InjuryFlag.None, (flags, injury) =>
                flags | (InjuryFlag)injury.SiteFlag),
            relevantInjuries.Any(injury => injury.Pain > 0),
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
            SelectedActivityTypes = ParseActivityTypes(profile?.PreferredWorkoutActivityTypes),
            ExcludedExerciseIds = ParseExcludedExerciseIds(profile?.ExcludedStrengthExerciseIdsJson),
            AvailableWeekdays = ParseAvailableWeekdays(profile?.AvailableWorkoutWeekdaysJson),
            ActivitySessionDurationsMinutes = ParseActivityDurations(profile?.ActivitySessionDurationsJson),
            RequestedSessionsPerWeek = ParseActivityFrequencies(
                profile?.RequestedActivitySessionsPerWeekJson),
            ProfileTrainingExperience = planningState?.EffectiveExperience ?? (Enum.TryParse<TrainingExperienceLevel>(
                profile?.TrainingExperienceLevel, true, out var trainingExperience) &&
                Enum.IsDefined(trainingExperience) ? trainingExperience : null),
            MuscleGroupsByExerciseId = definitions
                .Where(row => row.MuscleContributions.Any(contribution => contribution.Fraction > 0) &&
                    row.MuscleContributions.Where(contribution => contribution.Fraction > 0)
                        .All(contribution => !string.IsNullOrWhiteSpace(contribution.MuscleGroup?.Name)))
                .ToDictionary(row => row.Id, row => (IReadOnlySet<string>)row.MuscleContributions
                    .Where(contribution => contribution.Fraction > 0)
                    .Select(contribution => contribution.MuscleGroup!.Name)
                    .ToHashSet(StringComparer.OrdinalIgnoreCase)),
            GoalPriorityByExerciseId = goalPriorityByExerciseId,
            TemplateVolumeFloors = ReviewedTemplateFloorSource.Map(floorRows),
            ProfileVersion = profile?.ConcurrencyVersion,
            CatalogFingerprint = PlanningCatalogFingerprint.FromInputs(definitions, goalTierWeights, preferences, floorRows),
            InjuryFingerprint = PlanningInputFingerprint.Injuries(injuries),
            ActivePlanId = activePlanId,
            ActiveRevisionId = activeRevisionId,
            HistoryFingerprint = PlanningInputFingerprint.Hash(logs),
            TimeZoneVersion = planningState?.TimeZoneVersion ?? WorkoutAccountCalendarRule.InitialTimeZoneVersion,
            ExperienceVersion = planningState?.ExperienceVersion ?? 0,
            AnswerVersion = planningState?.AnswerVersion ?? 0,
            SavedAnswers = string.IsNullOrWhiteSpace(planningState?.AnswersJson)
                ? new SavedActivityAnswersDto(0, [], null)
                : JsonSerializer.Deserialize<SavedActivityAnswersDto>(planningState.AnswersJson,
                    new JsonSerializerOptions(JsonSerializerDefaults.Web))
        };
    }

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

    private static IReadOnlySet<int> ParseActivityTypes(string? value)
    {
        var set = (value ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Select(item => Enum.TryParse<WorkoutActivityType>(item, true, out var activity) && Enum.IsDefined(activity)
                ? (int?)activity
                : null)
            .Where(activity => activity.HasValue)
            .Select(activity => activity!.Value)
            .ToHashSet();
        if (set.Count == 0)
            set.Add((int)WorkoutActivityType.GeneralStrength);
        return set;
    }

    private static IReadOnlySet<DayOfWeek> ParseAvailableWeekdays(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new HashSet<DayOfWeek>();
            var set = document.RootElement.EnumerateArray()
                .Select(value => value.ValueKind switch
                {
                    JsonValueKind.String when Enum.TryParse<DayOfWeek>(value.GetString(), true, out var day) && Enum.IsDefined(day) => (DayOfWeek?)day,
                    JsonValueKind.Number when value.TryGetInt32(out var dayValue) && Enum.IsDefined(typeof(DayOfWeek), dayValue) => (DayOfWeek?)dayValue,
                    _ => null
                })
                .Where(day => day.HasValue)
                .Select(day => day!.Value)
                .ToHashSet();
            return set;
        }
        catch (JsonException)
        {
            return new HashSet<DayOfWeek>();
        }
    }

    private static IReadOnlyDictionary<int, int> ParseActivityDurations(string? json)
    {
        var result = new Dictionary<int, int>();
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "{}" : json);
            if (document.RootElement.ValueKind == JsonValueKind.Object)
            {
                foreach (var property in document.RootElement.EnumerateObject())
                {
                    if (Enum.TryParse<WorkoutActivityType>(property.Name, true, out var activity) &&
                        Enum.IsDefined(activity) && property.Value.TryGetInt32(out var minutes) &&
                        ActivitySessionDurationRule.ForUserPreference(activity) is not null)
                        result[(int)activity] = minutes;
                }
            }
        }
        catch (JsonException)
        {
        }

        return result;
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

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>
/// Loads the planner's eligibility facts for an in-session swap: saved equipment, injury answers,
/// blocked movement patterns, exclusions and the experience the accepted plan was built with.
/// </summary>
internal static class ExerciseSwapEligibilitySource
{
    private static readonly JsonSerializerOptions JsonOptions = WorkoutPlanningJson.Create();

    // Caller owns the tenant transaction.
    internal static async Task<ExerciseSwapEligibilityResult> EvaluateAsync(
        PostgresDbContext dbContext,
        Guid userId,
        int originalExerciseId,
        IReadOnlyCollection<WorkoutExerciseDefinition> definitions,
        int? workoutActivityTypeId,
        CancellationToken cancellationToken)
    {
        var profile = await dbContext.UserProfiles.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
        var planningState = await dbContext.WorkoutAccountPlanningStates.AsNoTracking()
            .SingleOrDefaultAsync(row => row.UserId == userId, cancellationToken).ConfigureAwait(false);
        var injuries = await dbContext.WorkoutInjuryLogs.AsNoTracking()
            .Where(row => row.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var episodes = await dbContext.WorkoutInjuryEpisodes.AsNoTracking()
            .Where(row => row.UserId == userId).ToListAsync(cancellationToken).ConfigureAwait(false);
        var today = PlanningSafetyInputs.AccountToday(planningState?.TimeZoneId);
        var relevantInjuries = PlanningSafetyInputs.RelevantInjuries(injuries, episodes, today);
        var acceptedRequest = await LoadAcceptedRequestAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

        var strengthActivity = workoutActivityTypeId is { } activityTypeId &&
                               Enum.IsDefined(typeof(WorkoutActivityType), activityTypeId) &&
                               WorkoutPreferencesRule.IsStrengthStyle((WorkoutActivityType)activityTypeId)
            ? (WorkoutActivityType)activityTypeId
            : acceptedRequest is not null && Enum.IsDefined(acceptedRequest.Activity)
                ? WorkoutActivityContractMapper.ToCore(acceptedRequest.Activity)
                : WorkoutActivityType.GeneralStrength;
        var history = acceptedRequest?.ActivityHistory is { } input
            ? new ActivityTrainingHistory(input.ActivityId, input.ConsistentParticipationWeeks,
                input.AverageSessionsPerWeekInRecentFourWeeks, input.LastCompletedSessionDate, input.IsSelfReported)
            : null;
        // The current account date only ages history; it can lower, never raise, the planner's tier.
        var localDate = today ?? acceptedRequest?.LocalDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var experience = ExerciseSwapEligibilityRule.Experience(strengthActivity, history, localDate,
            PlanningSafetyInputs.ProfileTrainingExperience(planningState, profile));

        var excluded = new HashSet<int>(PlanningSafetyInputs.ParseExcludedExerciseIds(
            profile?.ExcludedStrengthExerciseIdsJson));
        if (acceptedRequest?.ExcludedExerciseIds is { } acceptedExclusions)
            excluded.UnionWith(acceptedExclusions.Where(id => id > 0));

        return ExerciseSwapEligibilityRule.Evaluate(new ExerciseSwapEligibilityInput(
            originalExerciseId,
            definitions.Select(WorkoutExerciseV020CatalogMapper.Map).ToArray(),
            PlanningSafetyInputs.ParseEquipment(profile?.EquipmentLevel),
            experience,
            excluded,
            PlanningSafetyInputs.BlockedMovementPatterns(relevantInjuries, definitions),
            PlanningSafetyInputs.ActiveInjuryFlags(relevantInjuries),
            acceptedRequest?.UnknownExperienceAssessmentCompleted == true));
    }

    private static async Task<StrengthPlanPreviewRequestV020?> LoadAcceptedRequestAsync(
        PostgresDbContext dbContext, Guid userId, CancellationToken cancellationToken)
    {
        var activePlanId = await dbContext.WorkoutPlans.AsNoTracking()
            .Where(row => row.UserId == userId && row.IsActive)
            .OrderByDescending(row => row.CreatedAt)
            .Select(row => (Guid?)row.Id)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (activePlanId is not { } planId)
            return null;
        var snapshotJson = await dbContext.WorkoutPlanRevisionsV02.AsNoTracking()
            .Where(row => row.UserId == userId && row.PlanId == planId)
            .OrderByDescending(row => row.RevisionNumber)
            .Select(row => row.InputSnapshotJson)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(snapshotJson))
            return null;
        try
        {
            return JsonSerializer.Deserialize<AcceptedInputSnapshot>(snapshotJson, JsonOptions)?.Request;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed record AcceptedInputSnapshot(StrengthPlanPreviewRequestV020? Request);
}

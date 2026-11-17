using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>Reads the immutable accepted prescription, including retired plans with open sessions.</summary>
internal static class AcceptedGenerationPlanV2
{
    private static readonly JsonSerializerOptions JsonOptions = WorkoutPlanningJson.Create();

    internal static GenerationSessionV2? ApplyOccurrenceDose(GenerationSessionV2? generation,
        IReadOnlyList<PlannedExerciseSnapshot> exercises)
    {
        if (generation is null) return null;
        if (generation.Exercises.Count != exercises.Count ||
            exercises.Select(exercise => exercise.ExerciseId).Distinct().Count() != exercises.Count)
            throw new InvalidOperationException("The V2 occurrence dose does not match its accepted prescription.");
        var doses = exercises.ToDictionary(exercise => exercise.ExerciseId);
        return generation with { Exercises = generation.Exercises.Select(exercise =>
        {
            if (!doses.TryGetValue(exercise.ExerciseId, out var dose) || dose.Sets <= 0)
                throw new InvalidOperationException("The V2 occurrence dose is unavailable.");
            return exercise with { WorkingSets = dose.Sets,
                TargetRir = dose.TargetRepetitionsInReserve ?? exercise.TargetRir };
        }).ToArray() };
    }

    internal static async Task<GenerationSessionV2?> LoadSessionAsync(PostgresDbContext db, Guid userId,
        WorkoutDayRecord day, CancellationToken ct)
    {
        var json = await db.WorkoutPlanRevisionsV02.AsNoTracking()
            .Where(revision => revision.UserId == userId && revision.PlanId == day.WorkoutPlanId)
            .OrderByDescending(revision => revision.RevisionNumber)
            .Select(revision => revision.PlanJson).FirstOrDefaultAsync(ct).ConfigureAwait(false);
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("proposal", out var proposal) ||
            !proposal.TryGetProperty("generationV2", out var generation) || generation.ValueKind == JsonValueKind.Null)
            return null; // Historical plans have no V2 prescription.
        var plan = generation.Deserialize<GenerationPlanV2>(JsonOptions)
            ?? throw new InvalidOperationException("The accepted V2 prescription is unavailable.");
        var sessions = plan.Sessions.Where(session => (int)session.Weekday == day.DayOfWeek &&
            session.ActivityTypeId == day.ActivityTypeId && plan.Sessions.Count(other =>
                other.Weekday == session.Weekday && other.SessionIndex <= session.SessionIndex) == day.DayOrder).ToArray();
        if (sessions.Length != 1 || plan.Sessions.Count != plan.SessionCount)
            throw new InvalidOperationException("The accepted V2 prescription does not match this session.");
        var state = await db.WorkoutAccountPlanningStates.AsNoTracking().SingleOrDefaultAsync(
            row => row.UserId == userId, ct).ConfigureAwait(false);
        var observed = await V2ObservedHistory.LoadAsync(db, userId, state?.TimeZoneId ?? "UTC", ct).ConfigureAwait(false);
        var returns = await V2PainReturnSource.LoadAsync(db, userId, observed, ct).ConfigureAwait(false);
        return sessions[0] with
        {
            LocalDate = day.Date ?? sessions[0].LocalDate,
            Exercises = sessions[0].Exercises.Select(exercise =>
            {
                if (returns.Any(item => item.ExerciseId == exercise.ExerciseId && item.RequiresReturnCalibration &&
                    item.ConservativeReturnSessionsCompleted < WorkoutInjuryClearanceRule.ConservativeReturnSessionCount))
                    return exercise with { LoadStatus = "CalibrationRequired", PrescribedLoadKg = null };
                var related = returns.Where(item => item.ExerciseId == exercise.ExerciseId &&
                    item.ReturnBaselineLoadKg is > 0m).ToArray();
                var caps = related.Where(item => item.ConservativeReturnSessionsCompleted <
                    WorkoutInjuryClearanceRule.ConservativeReturnSessionCount).Select(item =>
                    WorkoutInjuryClearanceRule.ComputeConservativeReturnLoad(item.ReturnBaselineLoadKg!.Value,
                        item.ConservativeReturnSessionsCompleted)).ToArray();
                if (caps.Length > 0)
                    return exercise with { LoadStatus = "ConservativeReturnLoad", PrescribedLoadKg =
                        Math.Min(exercise.PrescribedLoadKg ?? caps.Min(), caps.Min()) };
                if (exercise.LoadStatus == "ConservativeReturnLoad" && related.Length > 0)
                    return exercise with { LoadStatus = "PrescribedLoad", PrescribedLoadKg =
                        related.Min(item => item.ReturnBaselineLoadKg!.Value) };
                return exercise;
            }).ToArray()
        };
    }
}

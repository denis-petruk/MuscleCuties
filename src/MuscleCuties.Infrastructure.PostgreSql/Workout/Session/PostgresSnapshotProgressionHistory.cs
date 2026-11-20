using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

internal static class PostgresSnapshotProgressionHistory
{
    internal static async Task<Dictionary<int, IReadOnlyList<SnapshotProgressionExposure>>> LoadAsync(
        PostgresDbContext db, Guid userId, int[] exerciseIds, Guid? currentSessionId, CancellationToken ct,
        IReadOnlySet<int>? unilateralExerciseIds = null)
    {
        if (exerciseIds.Length == 0) return [];
        var now = DateTime.UtcNow;
        var sessions = await db.WorkoutSessionLogs.AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted && s.CompletedAt != null && s.CompletedAt <= now && s.Id != currentSessionId)
            .OrderByDescending(s => s.CompletedAt).ThenByDescending(s => s.Id).Take(500).ToListAsync(ct);
        var sessionIds = sessions.Select(s => s.Id).ToArray();
        var keys = exerciseIds.Select(id => id.ToString(CultureInfo.InvariantCulture)).ToArray();
        var sets = await db.ExerciseSetLogs.AsNoTracking()
            .Where(s => s.UserId == userId && !s.IsDeleted && s.IsObserved && !s.IsWarmup &&
                sessionIds.Contains(s.WorkoutSessionLogId) && s.ExerciseId != null && keys.Contains(s.ExerciseId))
            .ToListAsync(ct);
        var dayIds = sessions.Where(s => s.PlannedWorkoutDayId.HasValue).Select(s => s.PlannedWorkoutDayId!.Value).Distinct().ToArray();
        var days = await db.WorkoutDays.AsNoTracking().Where(d => d.UserId == userId && dayIds.Contains(d.Id)).ToListAsync(ct);
        var definitionNamesById = await db.WorkoutExerciseDefinitions.AsNoTracking()
            .Where(row => exerciseIds.Contains(row.Id))
            .ToDictionaryAsync(row => row.Id, row => row.Name, ct);
        var result = new Dictionary<int, IReadOnlyList<SnapshotProgressionExposure>>();
        foreach (var exerciseId in exerciseIds)
        {
            var key = exerciseId.ToString(CultureInfo.InvariantCulture);
            var exposures = new List<SnapshotProgressionExposure>();
            foreach (var session in sessions)
            {
                var matchingSets = sets.Where(s => s.WorkoutSessionLogId == session.Id && s.ExerciseId == key).ToArray();
                var snapshot = PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson);
                if (snapshot is null || snapshot.Exercises is null || snapshot.Exercises.Any(e => e is null) || session.PlannedWorkoutDayId != snapshot.DayId)
                {
                    // Explicit non-strength activity with no matching exercise cannot be this exposure.
                    var day = days.FirstOrDefault(d => d.Id == session.PlannedWorkoutDayId);
                    var activityId = session.UnplannedActivityType ?? day?.ActivityTypeId;
                    if (matchingSets.Length == 0 && activityId is { } activity &&
                        Enum.IsDefined((WorkoutActivityType)activity) &&
                        !StrengthStyleSelectionRule.IsStrengthStyle((WorkoutActivityType)activity)) continue;
                    exposures.Add(new(session.CompletedAt!.Value, 0, 0, null, []));
                    break;
                }
                var prescribed = snapshot.Exercises.Where(e => e.ExerciseId == exerciseId).ToArray();
                if (prescribed.Length == 0)
                {
                    if (matchingSets.Length == 0) continue;
                    // A performed substitute lacks a verified prescription and blocks older successes.
                    exposures.Add(new(session.CompletedAt!.Value, 0, 0, null, []));
                    break;
                }
                var one = prescribed.Length == 1 ? prescribed[0] : null;
                var generationExercise = snapshot.GenerationV2?.Exercises.SingleOrDefault(item => item.ExerciseId == exerciseId);
                var usesV2Definition = generationExercise is not null ||
                    prescribed.Any(e => e.PlanningSlotId?.StartsWith("v2-", StringComparison.Ordinal) == true);
                if (usesV2Definition && matchingSets.Length > 0 &&
                    (!definitionNamesById.TryGetValue(exerciseId, out var expectedName) ||
                     matchingSets.Any(s => !string.Equals(s.ExerciseName, expectedName, StringComparison.Ordinal))))
                {
                    exposures.Add(new(session.CompletedAt!.Value, 0, 0, null, []));
                    break;
                }
                var isUnilateral = unilateralExerciseIds?.Contains(exerciseId) == true || generationExercise?.IsUnilateral == true;
                exposures.Add(new(session.CompletedAt!.Value,
                    generationExercise?.WorkingSets ?? one?.Sets ?? 0,
                    generationExercise?.MinRepetitions ?? one?.Reps ?? 0,
                    generationExercise?.TargetRir ?? one?.TargetRepetitionsInReserve,
                    matchingSets.Select(s => new SnapshotProgressionSet(
                        s.SetNumber, s.RepsCompleted, s.WeightKg, s.RirActual, s.TechniqueAcceptable,
                        s.LeftSideReps, s.RightSideReps)).ToArray(),
                    isUnilateral,
                    generationExercise?.MaxRepetitions ?? one?.Reps));
                if (exposures.Count == 2) break;
            }
            result.Add(exerciseId, exposures);
        }
        return result;
    }
}

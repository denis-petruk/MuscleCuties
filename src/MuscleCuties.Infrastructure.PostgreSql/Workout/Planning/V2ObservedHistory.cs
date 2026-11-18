using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

internal static class V2ObservedHistory
{
    internal static async Task<IReadOnlyList<ObservedSetRecordV2>> LoadAsync(PostgresDbContext db,
        Guid userId, string timeZoneId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var sessions = await db.WorkoutSessionLogs.AsNoTracking().Where(session => session.UserId == userId &&
                !session.IsDeleted && session.CompletedAt != null && session.CompletedAt <= now)
            .OrderByDescending(session => session.CompletedAt).ThenByDescending(session => session.Id).Take(500).ToListAsync(ct).ConfigureAwait(false);
        var ids = sessions.Select(session => session.Id).ToArray();
        var sets = await db.ExerciseSetLogs.AsNoTracking().Where(set => set.UserId == userId && !set.IsDeleted &&
            set.IsObserved && !set.IsWarmup && set.RepsCompleted > 0 && ids.Contains(set.WorkoutSessionLogId))
            .ToListAsync(ct).ConfigureAwait(false);
        var parsedExerciseIds = sets
            .Select(set => int.TryParse(set.ExerciseId, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id) ? (int?)id : null)
            .OfType<int>()
            .Distinct()
            .ToArray();
        var definitionNamesById = parsedExerciseIds.Length == 0
            ? new Dictionary<int, string>()
            : await db.WorkoutExerciseDefinitions.AsNoTracking()
                .Where(row => parsedExerciseIds.Contains(row.Id))
                .ToDictionaryAsync(row => row.Id, row => row.Name, ct)
                .ConfigureAwait(false);
        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var setsBySessionId = sets
            .Where(set => int.TryParse(set.ExerciseId, NumberStyles.Integer, CultureInfo.InvariantCulture, out _))
            .ToLookup(set => set.WorkoutSessionLogId);
        var blockedExerciseIds = new HashSet<int>();
        var accepted = new List<ObservedSetRecordV2>();
        foreach (var session in sessions)
        {
            var sessionSets = setsBySessionId[session.Id].ToArray();
            if (sessionSets.Length == 0)
                continue;
            var snapshot = PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson);
            var completedAt = new DateTimeOffset(DateTime.SpecifyKind(session.CompletedAt!.Value, DateTimeKind.Utc));
            var localDate = session.UnplannedCompletionLocalDate ?? WorkoutAccountCalendarRule.LocalDate(completedAt, zone);
            foreach (var group in sessionSets.GroupBy(set => int.Parse(set.ExerciseId!, CultureInfo.InvariantCulture)))
            {
                var exerciseId = group.Key;
                if (blockedExerciseIds.Contains(exerciseId))
                    continue;
                var usesV2Definition = snapshot?.GenerationV2?.Exercises.Any(item => item.ExerciseId == exerciseId) == true ||
                    snapshot?.Exercises?.Any(item => item is not null && item.ExerciseId == exerciseId &&
                        item.PlanningSlotId?.StartsWith("v2-", StringComparison.Ordinal) == true) == true;
                if (usesV2Definition &&
                    (!definitionNamesById.TryGetValue(exerciseId, out var expectedName) ||
                     group.Any(set => !string.Equals(set.ExerciseName, expectedName, StringComparison.Ordinal))))
                {
                    blockedExerciseIds.Add(exerciseId);
                    continue;
                }

                foreach (var set in group)
                {
                    accepted.Add(new ObservedSetRecordV2(session.Id, exerciseId, localDate, completedAt,
                        set.SetNumber, SetKindV2.Working, set.RepsCompleted, set.WeightKg,
                        set.RirActual, set.TechniqueAcceptable, set.LeftSideReps, set.RightSideReps));
                }
            }
        }

        return accepted.OrderByDescending(set => set.CompletedAtUtc).ThenBy(set => set.SessionId).ThenBy(set => set.ExerciseId)
            .ThenBy(set => set.SetNumber).ToArray();
    }

    internal static async Task<IReadOnlyList<CardioActualRecordV2>> LoadCardioAsync(PostgresDbContext db,
        Guid userId, string timeZoneId, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var sessions = await db.WorkoutSessionLogs.AsNoTracking().Where(session => session.UserId == userId &&
                !session.IsDeleted && session.CompletedAt != null && session.CompletedAt <= now &&
                (session.TotalDurationSeconds > 0 || session.DistanceKm > 0))
            .OrderBy(session => session.CompletedAt).Take(200).ToListAsync(ct).ConfigureAwait(false);
        if (sessions.Count == 0)
            return [];

        var plannedDayIds = sessions.Where(s => s.PlannedWorkoutDayId.HasValue)
            .Select(s => s.PlannedWorkoutDayId!.Value).Distinct().ToArray();
        var dayActivities = plannedDayIds.Length == 0
            ? new Dictionary<Guid, int?>()
            : await db.WorkoutDays.AsNoTracking()
                .Where(d => d.UserId == userId && plannedDayIds.Contains(d.Id))
                .ToDictionaryAsync(d => d.Id, d => d.ActivityTypeId, ct).ConfigureAwait(false);

        var zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
        var result = new List<CardioActualRecordV2>();
        foreach (var session in sessions)
        {
            var activityId = session.UnplannedActivityType ??
                (session.PlannedWorkoutDayId is { } dayId && dayActivities.TryGetValue(dayId, out var plannedAct) ? plannedAct : null);
            if (activityId is not { } act || !Enum.IsDefined((WorkoutActivityType)act))
                continue;
            var activity = (WorkoutActivityType)act;
            if (!WorkoutPolicyV2.IsCardioActivitySupportedInV2(activity))
                continue;

            var completedAt = new DateTimeOffset(DateTime.SpecifyKind(session.CompletedAt!.Value, DateTimeKind.Utc));
            var localDate = session.UnplannedCompletionLocalDate ?? session.PlannedLocalDate ??
                WorkoutAccountCalendarRule.LocalDate(completedAt, zone);
            var durationMinutes = session.TotalDurationSeconds > 0
                ? (int?)Math.Max(1, (int)Math.Round(session.TotalDurationSeconds / 60.0, MidpointRounding.AwayFromZero))
                : null;

            result.Add(new CardioActualRecordV2(
                Activity: activity,
                LocalDate: localDate,
                DurationMinutes: durationMinutes,
                DistanceKm: session.DistanceKm,
                Steps: null,
                Rpe: session.RpeRating,
                HeartRateBpm: null,
                CyclingPowerWatts: null));
        }

        return result;
    }
}

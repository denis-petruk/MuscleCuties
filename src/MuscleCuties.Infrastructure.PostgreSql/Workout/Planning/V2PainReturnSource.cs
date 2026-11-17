using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

internal static class V2PainReturnSource
{
    internal static async Task<IReadOnlyList<PainRestrictionV2>> LoadAsync(PostgresDbContext db,
        Guid userId, IReadOnlyList<ObservedSetRecordV2> observed, CancellationToken ct)
    {
        var injuries = await db.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId)
            .ToListAsync(ct).ConfigureAwait(false);
        var episodes = await db.WorkoutInjuryEpisodes.AsNoTracking().Where(row => row.UserId == userId)
            .ToListAsync(ct).ConfigureAwait(false);
        var state = await db.WorkoutAccountPlanningStates.AsNoTracking().SingleOrDefaultAsync(
            row => row.UserId == userId, ct).ConfigureAwait(false);
        var today = PlanningSafetyInputs.AccountToday(state?.TimeZoneId);
        var relevant = PlanningSafetyInputs.RelevantInjuries(injuries, episodes, today).Select(row => row.Id).ToHashSet();
        var definitions = await db.WorkoutExerciseDefinitions.AsNoTracking().ToListAsync(ct).ConfigureAwait(false);
        var sessionIds = observed.Select(set => set.SessionId).Distinct().ToArray();
        var sessions = (await db.WorkoutSessionLogs.AsNoTracking().Where(row => row.UserId == userId &&
                sessionIds.Contains(row.Id)).ToListAsync(ct).ConfigureAwait(false))
            .ToDictionary(row => row.Id);
        var result = new List<PainRestrictionV2>();
        foreach (var injury in injuries.Where(row => relevant.Contains(row.Id)))
        {
            var reported = injury.ReportedAtUtc is { } timestamp
                ? new DateTimeOffset(DateTime.SpecifyKind(timestamp, DateTimeKind.Utc)) : (DateTimeOffset?)null;
            result.Add(new PainRestrictionV2(null, null, (InjuryFlag)injury.SiteFlag, reported, false));
            foreach (var pattern in PlanningSafetyInputs.BlockedMovementPatterns([injury], definitions))
                result.Add(new PainRestrictionV2(null, pattern, InjuryFlag.None, reported, false));
        }
        foreach (var injury in injuries.Where(row => !relevant.Contains(row.Id) && row.ReportedAtUtc is not null &&
                     row.ClearedAtUtc is not null && row.ClearedAtUtc <= DateTime.UtcNow))
        {
            var flags = (InjuryFlag)injury.SiteFlag;
            var patterns = PlanningSafetyInputs.BlockedMovementPatterns([injury], definitions);
            foreach (var definition in definitions.Where(row => (row.Contraindications & flags) != InjuryFlag.None ||
                         patterns.Contains(row.Pattern)))
            {
                var usesNonKgLoading = (definition.Required & ~(EquipmentSet.Sliders |
                    EquipmentSet.Bands | EquipmentSet.BandAnchor)) == EquipmentSet.None;
                var before = observed.Where(set => set.ExerciseId == definition.Id &&
                        set.CompletedAtUtc.UtcDateTime < injury.ReportedAtUtc &&
                        (usesNonKgLoading ? set.RepsCompleted > 0 : set.LoadKg > 0m))
                    .OrderByDescending(set => set.CompletedAtUtc).FirstOrDefault();
                var cap = before is null || usesNonKgLoading ? (decimal?)null :
                    WorkoutInjuryClearanceRule.ComputeConservativeReturnLoad(before.LoadKg, 0);
                var completed = observed.Where(set => set.ExerciseId == definition.Id &&
                        set.CompletedAtUtc.UtcDateTime >= injury.ClearedAtUtc)
                    .GroupBy(set => set.SessionId)
                    .Select(group =>
                    {
                        if (!sessions.TryGetValue(group.Key, out var session) ||
                            session.PlannedWorkoutDayId is not { } plannedDayId)
                            return (Guid?)null;
                        var snapshot = PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson);
                        if (snapshot?.DayId != plannedDayId || snapshot.Exercises is null)
                            return null;
                        var prescriptions = snapshot.Exercises.Where(exercise => exercise.ExerciseId == definition.Id).ToArray();
                        if (prescriptions.Length != 1) return null;
                        if (!usesNonKgLoading && before is null && snapshot.GenerationV2?.Exercises.SingleOrDefault(
                            exercise => exercise.ExerciseId == definition.Id)?.LoadStatus != "CalibrationRequired")
                            return null;
                        var sets = group.OrderBy(set => set.SetNumber).ToArray();
                        var completeObservedPrescription = sets.Length == prescriptions[0].Sets &&
                            sets.Select(set => set.SetNumber).SequenceEqual(Enumerable.Range(1, prescriptions[0].Sets)) &&
                            sets.All(set => (usesNonKgLoading || set.LoadKg > 0m) && (cap is null || set.LoadKg <= cap) && set.RepsCompleted > 0 &&
                                (!definition.UnilateralDoublesTime || set.LeftSideReps is > 0 && set.RightSideReps is > 0));
                        return completeObservedPrescription ? plannedDayId : (Guid?)null;
                    })
                    .Where(dayId => dayId.HasValue)
                    .Select(dayId => dayId!.Value)
                    .Distinct()
                    .Count();
                result.Add(new PainRestrictionV2(definition.Id, null, InjuryFlag.None,
                    new DateTimeOffset(DateTime.SpecifyKind(injury.ReportedAtUtc!.Value, DateTimeKind.Utc)), true,
                    completed, before?.LoadKg, !usesNonKgLoading && before is null));
            }
        }
        return result;
    }
}

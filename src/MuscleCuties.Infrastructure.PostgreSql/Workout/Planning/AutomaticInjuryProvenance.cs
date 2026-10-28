using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

// Only active, explicitly recorded episodes are supported. Clearance and legacy histories stay blocked.
internal static class AutomaticInjuryProvenance
{
    internal sealed record Slot(InjuryRestorationSlotIdentity Identity, int OriginalExerciseId, int AlternativeExerciseId,
        Guid[] EpisodeIds, IReadOnlyDictionary<DateOnly, Guid> OriginsByDate, int Sets, int OptionalSets, int Reps, int? DurationSeconds,
        bool IsOptional, bool IsRemovedByReadiness);
    internal sealed record Evidence(string Fingerprint, IReadOnlyList<Slot> Slots);

    internal static async Task<Evidence?> LoadAsync(PostgresDbContext db, Guid userId, Guid planId,
        DateOnly today, IReadOnlyList<StrengthPlanPreviewSessionV020> proposalSessions,
        StrengthPlanPreviewSourceV020 source, CancellationToken ct)
    {
        var logs = await db.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId).ToListAsync(ct).ConfigureAwait(false);
        var episodes = await db.WorkoutInjuryEpisodes.AsNoTracking().Where(row => row.UserId == userId).ToListAsync(ct).ConfigureAwait(false);
        if (logs.Count == 0 || episodes.Count == 0 || episodes.Any(row => row.SourceDeleted || row.ClearedOn != null ||
                row.StartedOn > today || row.SiteFlag == (int)InjuryFlag.None || !Enum.IsDefined(typeof(InjuryFlag), row.SiteFlag)) ||
            logs.Any(row => !string.Equals(row.Status, "Acute", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(row.Status, "Recovering", StringComparison.OrdinalIgnoreCase)) ||
            logs.Any(log => episodes.Count(episode => episode.InjuryLogId == log.Id && episode.SiteFlag == log.SiteFlag) != 1) ||
            episodes.Any(episode => logs.Count(log => log.Id == episode.InjuryLogId && log.SiteFlag == episode.SiteFlag) != 1)) return null;

        var futureDays = await db.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId && day.WorkoutPlanId == planId &&
            (day.Date == null || day.Date >= today)).ToListAsync(ct).ConfigureAwait(false);
        if (futureDays.Any(day => day.Date is null || day.SafetyActivityTypeId != null ||
                (day.IsRemovedBySafety && (day.SafetyInjuryId is null || day.WorkoutType != nameof(WorkoutType.Strength))) ||
                (!day.IsRemovedBySafety && day.SafetyInjuryId is not null))) return null;
        var days = futureDays.Where(day => day.WorkoutType == nameof(WorkoutType.Strength)).ToList();
        var dayIds = days.Select(day => day.Id).ToArray();
        // Never reconstruct a prescription from a day with any recorded session or log.
        if (await db.WorkoutLogs.AsNoTracking().AnyAsync(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId), ct).ConfigureAwait(false) ||
            await db.WorkoutSessionLogs.AsNoTracking().AnyAsync(row => row.UserId == userId &&
                row.PlannedWorkoutDayId != null && dayIds.Contains(row.PlannedWorkoutDayId.Value), ct).ConfigureAwait(false))
            return null;
        var exercises = await db.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId))
            .ToListAsync(ct).ConfigureAwait(false);
        if (exercises.Count == 0 || days.Any(day => !day.IsRemovedBySafety || day.SafetyActivityTypeId != null ||
            day.SafetyInjuryId is null || episodes.All(episode => episode.InjuryLogId != day.SafetyInjuryId))) return null;
        var exerciseIds = exercises.Select(row => row.Id).ToArray();
        var links = await db.WorkoutInjuryPrescriptionLinks.AsNoTracking().Where(row => row.UserId == userId && exerciseIds.Contains(row.WorkoutDayExerciseId))
            .ToListAsync(ct).ConfigureAwait(false);
        var snapshots = await db.WorkoutInjuryPrescriptions.AsNoTracking()
            .Where(row => row.UserId == userId && exerciseIds.Contains(row.OriginalWorkoutDayExerciseId))
            .ToListAsync(ct).ConfigureAwait(false);
        var originalIds = exercises.Select(row => row.InjuryOriginalExerciseId ?? row.ExerciseId).Distinct().ToArray();
        var definitions = await db.WorkoutExerciseDefinitions.AsNoTracking().Where(row => originalIds.Contains(row.Id)).ToListAsync(ct).ConfigureAwait(false);
        if (definitions.Count != originalIds.Length || definitions.Any(row => WorkoutExerciseV020CatalogMapper.Map(row).MetadataGaps.Count != 0)) return null;
        var dayMap = days.ToDictionary(day => day.Id);
        var rows = new List<(InjuryRestorationSlotIdentity Identity, DateOnly Date, int Original, Guid[] Causes,
            Models.App.WorkoutInjuryPrescriptionRecord Snapshot, Guid Origin)>();
        foreach (var exercise in exercises)
        {
            if (exercise.PlanningTemplateId is null || exercise.PlanningTemplateVersion is null || exercise.PlanningSlotId is null ||
                exercise.PlanningSessionIndex is null || !dayMap.TryGetValue(exercise.WorkoutDayId, out var day)) return null;
            var original = exercise.InjuryOriginalExerciseId ?? exercise.ExerciseId;
            var definition = definitions.Single(row => row.Id == original);
            var expected = episodes.Where(episode => (definition.Contraindications & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)
                .Select(episode => episode.Id).OrderBy(id => id).ToArray();
            var actual = links.Where(link => link.WorkoutDayExerciseId == exercise.Id).ToArray();
            if (actual.Any(link => link.OriginalExerciseId != original) || !expected.SequenceEqual(actual.Select(link => link.InjuryEpisodeId).OrderBy(id => id)) ||
                (expected.Length > 0 && exercise.InjuryOriginalExerciseId is null)) return null;
            var captured = snapshots.Where(row => row.OriginalWorkoutDayExerciseId == exercise.Id).ToArray();
            if (expected.Length == 0)
            {
                if (captured.Length != 0 || exercise.InjuryOriginalExerciseId is not null || exercise.OriginWorkoutDayExerciseId is not null) return null;
                continue;
            }
            if (captured.Length != expected.Length || expected.Any(id => captured.Count(row => row.EpisodeId == id && row.OriginalExerciseId == original &&
                    row.WorkoutDayId == day.Id && row.OriginalPlanningSlotId == exercise.PlanningSlotId) != 1))
                return null;
            var first = captured.First(row => row.EpisodeId == expected[0]);
            if (captured.Where(row => expected.Contains(row.EpisodeId)).Any(row =>
                    row.OriginalSets != first.OriginalSets || row.OriginalOptionalSets != first.OriginalOptionalSets ||
                    row.OriginalReps != first.OriginalReps || row.OriginalDurationSeconds != first.OriginalDurationSeconds ||
                    row.OriginalIsOptional != first.OriginalIsOptional ||
                    row.OriginalIsRemovedByReadiness != first.OriginalIsRemovedByReadiness ||
                    row.OriginalOriginWorkoutDayExerciseId != first.OriginalOriginWorkoutDayExerciseId))
                return null;
            var identity = new InjuryRestorationSlotIdentity((DayOfWeek)day.DayOfWeek, exercise.PlanningTemplateId,
                exercise.PlanningTemplateVersion, exercise.PlanningSessionIndex.Value, exercise.PlanningSlotId);
            rows.Add((identity, day.Date!.Value, original, expected, first, first.OriginalOriginWorkoutDayExerciseId ?? exercise.Id));
        }
        // Suppression is applied at day level as a precaution, even when no reviewed
        // exercise on that day is contraindicated by the episode. Prove every
        // selected prescription safe before lifting that precaution.
        foreach (var selectedId in proposalSessions.SelectMany(session => session.Slots)
                     .Where(slot => slot.ExerciseId.HasValue).Select(slot => slot.ExerciseId!.Value).Distinct())
        {
            var candidates = source.Catalog.Where(entry => entry.Candidate.ExerciseId == selectedId).ToArray();
            if (candidates.Length != 1 || candidates[0].MetadataGaps.Count != 0 ||
                candidates[0].Candidate.Contraindications is not { } flags ||
                episodes.Any(episode => (flags & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)) return null;
        }
        var slots = new List<Slot>();
        foreach (var group in rows.GroupBy(row => row.Identity))
        {
            var match = InjuryRestorationSlotRule.Match(group.Key, group.Select(row =>
                new InjuryRestorationSlotEvidence(row.Identity, row.Original, row.Causes)).ToArray());
            if (match is null) return null;
            var sessions = proposalSessions.Where(session => session.PlannedWeekday == group.Key.Weekday &&
                session.TemplateId == group.Key.TemplateId && session.TemplateVersion == group.Key.TemplateVersion &&
                session.SessionIndex == group.Key.SessionIndex).ToArray();
            var selected = sessions.Length == 1 ? sessions[0].Slots.Where(slot => slot.SlotId == group.Key.SlotId).ToArray() : [];
            if (selected.Length != 1 || selected[0].ExerciseId is not { } alternative || alternative == match.OriginalExerciseId) return null;
            var candidates = source.Catalog.Where(entry => entry.Candidate.ExerciseId == alternative).ToArray();
            if (candidates.Length != 1 || candidates[0].MetadataGaps.Count != 0 ||
                candidates[0].Candidate.Contraindications is not { } flags ||
                episodes.Any(episode => (flags & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)) return null;
            var first = group.First();
            if (group.Select(row => row.Date).Distinct().Count() != group.Count() ||
                group.Any(row => row.Snapshot.OriginalSets != first.Snapshot.OriginalSets ||
                row.Snapshot.OriginalOptionalSets != first.Snapshot.OriginalOptionalSets || row.Snapshot.OriginalReps != first.Snapshot.OriginalReps ||
                row.Snapshot.OriginalDurationSeconds != first.Snapshot.OriginalDurationSeconds ||
                row.Snapshot.OriginalIsOptional != first.Snapshot.OriginalIsOptional ||
                row.Snapshot.OriginalIsRemovedByReadiness != first.Snapshot.OriginalIsRemovedByReadiness)) return null;
            slots.Add(new Slot(group.Key, match.OriginalExerciseId!.Value, alternative, match.CausalEpisodeIds.OrderBy(id => id).ToArray(),
                group.ToDictionary(row => row.Date, row => row.Origin), first.Snapshot.OriginalSets, first.Snapshot.OriginalOptionalSets, first.Snapshot.OriginalReps,
                first.Snapshot.OriginalDurationSeconds, first.Snapshot.OriginalIsOptional, first.Snapshot.OriginalIsRemovedByReadiness));
        }
        var sorted = slots.OrderBy(slot => slot.Identity.Weekday).ThenBy(slot => slot.Identity.TemplateId, StringComparer.Ordinal)
            .ThenBy(slot => slot.Identity.TemplateVersion, StringComparer.Ordinal).ThenBy(slot => slot.Identity.SessionIndex)
            .ThenBy(slot => slot.Identity.SlotId, StringComparer.Ordinal).ToArray();
        var provenance = snapshots.OrderBy(row => row.Id).Select(row => new
        {
            row.Id, row.EpisodeId, row.WorkoutDayId, row.OriginalWorkoutDayExerciseId,
            row.OriginalExerciseId, row.OriginalPlanningSlotId, row.OriginalOriginWorkoutDayExerciseId,
            row.OriginalSets, row.OriginalOptionalSets, row.OriginalReps, row.OriginalDurationSeconds,
            row.OriginalIsOptional, row.OriginalIsRemovedByReadiness
        }).ToArray();
        return new Evidence(PlanningInputFingerprint.Hash(new { Slots = sorted, Snapshots = provenance }), sorted);
    }
}

using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

// Preserve every proven causal episode, including a cleared episode overlapping
// a still-active restriction on the same original prescription.
internal static class AutomaticInjuryProvenance
{
    internal sealed record Slot(InjuryRestorationSlotIdentity Identity, int OriginalExerciseId, int? AlternativeExerciseId,
        Guid[] EpisodeIds, IReadOnlyDictionary<DateOnly, Guid> OriginsByDate, int Sets, int OptionalSets, int Reps, int? DurationSeconds,
        bool IsOptional, bool IsRemovedByReadiness, int? AlternativeSets, int? AlternativeReps, bool AlternativeIsOptional,
        bool IsRestoredOriginal);
    internal sealed record Evidence(string Fingerprint, IReadOnlyList<Slot> Slots);

    internal static async Task<Evidence?> LoadAsync(PostgresDbContext db, Guid userId, Guid planId,
        DateOnly today, IReadOnlyList<StrengthPlanPreviewSessionV020> proposalSessions,
        StrengthPlanPreviewSourceV020 source, bool strengthAbsent, CancellationToken ct)
    {
        var logs = await db.WorkoutInjuryLogs.AsNoTracking().Where(row => row.UserId == userId).ToListAsync(ct).ConfigureAwait(false);
        var episodes = await db.WorkoutInjuryEpisodes.AsNoTracking().Where(row => row.UserId == userId).ToListAsync(ct).ConfigureAwait(false);
        var activeEpisodes = episodes.Where(row => row.ClearedOn is null).ToArray();
        if (logs.Count == 0 || activeEpisodes.Length == 0 || episodes.Any(row => row.SourceDeleted ||
                row.StartedOn > today || row.ClearedOn > today || row.ClearedOn < row.StartedOn ||
                (row.ClearedOn is not null && (string.IsNullOrWhiteSpace(row.ClearanceTimeZoneId) ||
                    row.ClearanceTimeZoneVersion is null)) || row.SiteFlag == (int)InjuryFlag.None ||
                !Enum.IsDefined(typeof(InjuryFlag), row.SiteFlag)) ||
            logs.Any(log =>
                (string.Equals(log.Status, "Acute", StringComparison.OrdinalIgnoreCase) ||
                 string.Equals(log.Status, "Recovering", StringComparison.OrdinalIgnoreCase))
                    ? episodes.Count(episode => episode.InjuryLogId == log.Id && episode.SiteFlag == log.SiteFlag &&
                        episode.ClearedOn is null) != 1
                    : !string.Equals(log.Status, "Cleared", StringComparison.OrdinalIgnoreCase) ||
                      log.ClearedDate is null || episodes.Count(episode => episode.InjuryLogId == log.Id &&
                          episode.SiteFlag == log.SiteFlag && episode.ClearedOn == log.ClearedDate) != 1 ||
                      episodes.Any(episode => episode.InjuryLogId == log.Id && episode.ClearedOn is null)) ||
            episodes.Any(episode => logs.Count(log => log.Id == episode.InjuryLogId &&
                log.SiteFlag == episode.SiteFlag) != 1)) return null;

        var futureDays = await db.WorkoutDays.AsNoTracking().Where(day => day.UserId == userId && day.WorkoutPlanId == planId &&
            (day.Date == null || day.Date >= today)).ToListAsync(ct).ConfigureAwait(false);
        if (futureDays.Any(day => day.Date is null || day.SafetyActivityTypeId != null ||
                (day.IsRemovedBySafety && (day.SafetyInjuryId is null || day.WorkoutType != nameof(WorkoutType.Strength))) ||
                (!day.IsRemovedBySafety && day.SafetyInjuryId is not null))) return null;
        var days = futureDays.Where(day => day.WorkoutType == nameof(WorkoutType.Strength)).ToList();
        var omitted = await db.Set<Models.App.WorkoutInjuryOmittedSlotRecord>().AsNoTracking().Where(row =>
            row.UserId == userId && row.PlanId == planId && row.ScheduledDate >= today).ToListAsync(ct).ConfigureAwait(false);
        var dayIds = days.Select(day => day.Id).ToArray();
        // Never reconstruct a prescription from a day with any recorded session or log.
        if (await db.WorkoutLogs.AsNoTracking().AnyAsync(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId), ct).ConfigureAwait(false) ||
            await db.WorkoutSessionLogs.AsNoTracking().AnyAsync(row => row.UserId == userId &&
                row.PlannedWorkoutDayId != null && dayIds.Contains(row.PlannedWorkoutDayId.Value), ct).ConfigureAwait(false))
            return null;
        var exercises = await db.WorkoutDayExercises.AsNoTracking().Where(row => row.UserId == userId && dayIds.Contains(row.WorkoutDayId))
            .ToListAsync(ct).ConfigureAwait(false);
        if ((exercises.Count == 0 && omitted.Count == 0) || days.Any(day => !day.IsRemovedBySafety || day.SafetyActivityTypeId != null ||
            day.SafetyInjuryId is null || episodes.All(episode => episode.InjuryLogId != day.SafetyInjuryId))) return null;
        var exerciseIds = exercises.Select(row => row.Id).ToArray();
        var links = await db.WorkoutInjuryPrescriptionLinks.AsNoTracking().Where(row => row.UserId == userId && exerciseIds.Contains(row.WorkoutDayExerciseId))
            .ToListAsync(ct).ConfigureAwait(false);
        var snapshots = await db.WorkoutInjuryPrescriptions.AsNoTracking()
            .Where(row => row.UserId == userId && exerciseIds.Contains(row.OriginalWorkoutDayExerciseId))
            .ToListAsync(ct).ConfigureAwait(false);
        var originalIds = exercises.Select(row => row.InjuryOriginalExerciseId ?? row.ExerciseId)
            .Concat(omitted.Select(row => row.OriginalExerciseId)).Distinct().ToArray();
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
            var requiredActive = activeEpisodes.Where(episode => (definition.Contraindications & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)
                .Select(episode => episode.Id).OrderBy(id => id).ToArray();
            var actual = links.Where(link => link.WorkoutDayExerciseId == exercise.Id).ToArray();
            var expected = actual.Select(link => link.InjuryEpisodeId).OrderBy(id => id).ToArray();
            if (actual.Any(link => link.OriginalExerciseId != original ||
                    episodes.All(episode => episode.Id != link.InjuryEpisodeId ||
                        (definition.Contraindications & (InjuryFlag)episode.SiteFlag) == InjuryFlag.None)) ||
                expected.Distinct().Count() != expected.Length || requiredActive.Except(expected).Any() ||
                (expected.Length > 0 && exercise.InjuryOriginalExerciseId is null)) return null;
            var captured = snapshots.Where(row => row.OriginalWorkoutDayExerciseId == exercise.Id &&
                (expected.Contains(row.EpisodeId) || activeEpisodes.Any(episode => episode.Id == row.EpisodeId))).ToArray();
            var capturedEpisodeIds = expected.Concat(activeEpisodes.Select(episode => episode.Id)).Distinct().ToArray();
            if (captured.Length != capturedEpisodeIds.Length || capturedEpisodeIds.Any(episodeId =>
                captured.Count(row => row.EpisodeId == episodeId && row.WorkoutDayId == day.Id &&
                    row.OriginalExerciseId == original && row.OriginalPlanningSlotId == exercise.PlanningSlotId) != 1)) return null;
            var first = captured[0];
            if (captured.Any(row => row.OriginalSets != first.OriginalSets ||
                    row.OriginalOptionalSets != first.OriginalOptionalSets || row.OriginalReps != first.OriginalReps ||
                    row.OriginalDurationSeconds != first.OriginalDurationSeconds || row.OriginalIsOptional != first.OriginalIsOptional ||
                    row.OriginalIsRemovedByReadiness != first.OriginalIsRemovedByReadiness ||
                    row.OriginalOriginWorkoutDayExerciseId != first.OriginalOriginWorkoutDayExerciseId)) return null;
            if (expected.Length == 0)
            {
                if (strengthAbsent) return null;
                if (exercise.InjuryOriginalExerciseId is not null || exercise.OriginWorkoutDayExerciseId is not null ||
                    first.OriginalSets != exercise.Sets || first.OriginalOptionalSets != exercise.OptionalSets ||
                    first.OriginalReps != exercise.Reps || first.OriginalDurationSeconds != exercise.DurationSeconds ||
                    first.OriginalIsOptional != exercise.IsOptional ||
                    first.OriginalIsRemovedByReadiness != exercise.IsRemovedByReadiness) return null;
                continue;
            }
            var identity = new InjuryRestorationSlotIdentity((DayOfWeek)day.DayOfWeek, exercise.PlanningTemplateId,
                exercise.PlanningTemplateVersion, exercise.PlanningSessionIndex.Value, exercise.PlanningSlotId);
            rows.Add((identity, day.Date!.Value, original, expected, first, first.OriginalOriginWorkoutDayExerciseId ?? exercise.Id));
        }
        foreach (var group in omitted.GroupBy(row => (row.ScheduledDate, row.TemplateId, row.TemplateVersion, row.SessionIndex, row.SlotId)))
        {
            var first = group.First();
            var priorCauses = group.Select(row => row.EpisodeId).Order().ToArray();
            var definition = definitions.Single(row => row.Id == first.OriginalExerciseId);
            if (priorCauses.Length == 0 || priorCauses.Distinct().Count() != priorCauses.Length ||
                group.Any(row => row.OriginalExerciseId != first.OriginalExerciseId ||
                    row.OriginWorkoutDayExerciseId != first.OriginWorkoutDayExerciseId || row.OriginalSets != first.OriginalSets ||
                    row.OriginalOptionalSets != first.OriginalOptionalSets || row.OriginalReps != first.OriginalReps ||
                    row.OriginalDurationSeconds != first.OriginalDurationSeconds || row.OriginalIsOptional != first.OriginalIsOptional ||
                    row.OriginalIsRemovedByReadiness != first.OriginalIsRemovedByReadiness) ||
                priorCauses.Any(id => episodes.All(episode => episode.Id != id ||
                    (definition.Contraindications & (InjuryFlag)episode.SiteFlag) == InjuryFlag.None))) return null;
            var matchingDays = days.Where(day => day.Date == first.ScheduledDate).ToArray();
            if (matchingDays.Length > 1 || (matchingDays.Length == 1 && exercises.Any(row => row.WorkoutDayId == matchingDays[0].Id &&
                row.PlanningTemplateId == first.TemplateId && row.PlanningTemplateVersion == first.TemplateVersion &&
                row.PlanningSessionIndex == first.SessionIndex && row.PlanningSlotId == first.SlotId))) return null;
            var causes = priorCauses.Concat(activeEpisodes.Where(episode =>
                    (definition.Contraindications & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None).Select(episode => episode.Id))
                .Distinct().Order().ToArray();
            var snapshot = new Models.App.WorkoutInjuryPrescriptionRecord
            {
                OriginalExerciseId = first.OriginalExerciseId, OriginalSets = first.OriginalSets,
                OriginalOptionalSets = first.OriginalOptionalSets, OriginalReps = first.OriginalReps,
                OriginalDurationSeconds = first.OriginalDurationSeconds, OriginalIsOptional = first.OriginalIsOptional,
                OriginalIsRemovedByReadiness = first.OriginalIsRemovedByReadiness
            };
            rows.Add((new InjuryRestorationSlotIdentity(first.ScheduledDate.DayOfWeek, first.TemplateId,
                first.TemplateVersion, first.SessionIndex, first.SlotId), first.ScheduledDate,
                first.OriginalExerciseId, causes, snapshot, first.OriginWorkoutDayExerciseId));
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
                episodes.Any(episode => WorkoutInjuryClearanceRule.RequiresConservativeRestriction(
                    episode.ClearedOn is not null, episode.ClearedOn, today) &&
                    (flags & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)) return null;
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
            if (selected.Length != 1 && !(strengthAbsent && sessions.Length == 0)) return null;
            var selectedSlot = selected.SingleOrDefault();
            var alternative = selectedSlot?.ExerciseId;
            var alternativeSets = selectedSlot?.Sets;
            var alternativeReps = selectedSlot?.MinimumRepetitions;
            var isRestoredOriginal = alternative == match.OriginalExerciseId &&
                match.CausalEpisodeIds.All(id => episodes.Single(episode => episode.Id == id).ClearedOn is not null);
            if (alternative is null && selectedSlot is not null &&
                (!selectedSlot.LimitationCodes.Contains(nameof(ExerciseEligibilityReason.ExplicitRestrictionConflict)) ||
                 selectedSlot.LimitationCodes.Contains(nameof(ExerciseEligibilityReason.MissingCatalogMetadata)) ||
                 selectedSlot.LimitationCodes.Contains(nameof(ExerciseEligibilityReason.UnreviewedCatalogMetadata)))) return null;
            if (alternative is { } alternativeId)
            {
                if ((alternativeId == match.OriginalExerciseId && !isRestoredOriginal) || alternativeSets is null or <= 0 || alternativeReps is null or <= 0) return null;
                var candidates = source.Catalog.Where(entry => entry.Candidate.ExerciseId == alternativeId).ToArray();
                if (candidates.Length != 1 || candidates[0].MetadataGaps.Count != 0 ||
                    candidates[0].Candidate.Contraindications is not { } flags ||
                    episodes.Any(episode => WorkoutInjuryClearanceRule.RequiresConservativeRestriction(
                        episode.ClearedOn is not null, episode.ClearedOn, today) &&
                        (flags & (InjuryFlag)episode.SiteFlag) != InjuryFlag.None)) return null;
            }
            var first = group.First();
            if (isRestoredOriginal && (alternativeSets != first.Snapshot.OriginalSets ||
                alternativeReps != first.Snapshot.OriginalReps || first.Snapshot.OriginalOptionalSets != 0 ||
                first.Snapshot.OriginalDurationSeconds is not null || first.Snapshot.OriginalIsRemovedByReadiness)) return null;
            if (group.Select(row => row.Date).Distinct().Count() != group.Count() ||
                group.Any(row => row.Snapshot.OriginalSets != first.Snapshot.OriginalSets ||
                row.Snapshot.OriginalOptionalSets != first.Snapshot.OriginalOptionalSets || row.Snapshot.OriginalReps != first.Snapshot.OriginalReps ||
                row.Snapshot.OriginalDurationSeconds != first.Snapshot.OriginalDurationSeconds ||
                row.Snapshot.OriginalIsOptional != first.Snapshot.OriginalIsOptional ||
                row.Snapshot.OriginalIsRemovedByReadiness != first.Snapshot.OriginalIsRemovedByReadiness)) return null;
            slots.Add(new Slot(group.Key, match.OriginalExerciseId!.Value, alternative, match.CausalEpisodeIds.OrderBy(id => id).ToArray(),
                group.ToDictionary(row => row.Date, row => row.Origin), first.Snapshot.OriginalSets, first.Snapshot.OriginalOptionalSets, first.Snapshot.OriginalReps,
                first.Snapshot.OriginalDurationSeconds, first.Snapshot.OriginalIsOptional, first.Snapshot.OriginalIsRemovedByReadiness,
                alternativeSets, alternativeReps, selectedSlot is not null && !selectedSlot.IsRequired, isRestoredOriginal));
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
        return new Evidence(PlanningInputFingerprint.Hash(new { Slots = sorted, Snapshots = provenance, Omitted = omitted }), sorted);
    }
}

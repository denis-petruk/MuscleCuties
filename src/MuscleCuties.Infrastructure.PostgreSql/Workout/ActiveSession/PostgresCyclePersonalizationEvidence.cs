using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;

/// <summary>Loads linked, dated check-ins and completed app sessions without inferring symptoms or performance.</summary>
internal static class PostgresCyclePersonalizationEvidence
{
    internal static async Task<IReadOnlyList<CycleReadinessPatternObservation>> LoadAsync(
        PostgresDbContext db, Guid userId, DateOnly today, string timeZoneId, CancellationToken cancellationToken)
    {
        TimeZoneInfo zone;
        try { zone = TimeZoneInfo.FindSystemTimeZoneById(timeZoneId); }
        catch (Exception exception) when (exception is TimeZoneNotFoundException or InvalidTimeZoneException or ArgumentException)
        { return []; }
        var window = CycleReadinessPrescriptionRule.PatternObservationWindowDays;
        if (today.DayNumber < window) return [];
        var start = today.AddDays(-window);
        var credits = await db.WorkoutCompletionDayCredits.AsNoTracking()
            .Where(x => x.UserId == userId && x.LocalDate >= start && x.LocalDate < today &&
                x.TimeZoneId == timeZoneId && x.SourceSessionId != null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (credits.Count == 0) return [];
        var sessionIds = credits.Select(x => x.SourceSessionId!.Value).ToArray();
        var sessions = await db.WorkoutSessionLogs.AsNoTracking()
            .Where(x => x.UserId == userId && sessionIds.Contains(x.Id) && !x.IsDeleted &&
                x.CompletedAt != null && x.PlannedWorkoutDayId != null && x.PlannedPrescriptionSnapshotJson != null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var dates = credits.Select(x => x.LocalDate).ToArray();
        var checks = await db.DailyReadinessLogs.AsNoTracking()
            .Where(x => x.UserId == userId && dates.Contains(x.Date) && x.PlanningTimeZoneId == timeZoneId &&
                x.PlanningRecordedAtUtc != null).ToListAsync(cancellationToken).ConfigureAwait(false);
        var phases = await db.CyclePhaseLogs.AsNoTracking()
            .Where(x => x.UserId == userId && dates.Contains(x.LocalDate) && x.CycleLogId != null)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var cycles = await db.CycleLogs.AsNoTracking().Where(x => x.UserId == userId && x.StartDate < today)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var cycleIds = phases.Select(x => x.CycleLogId!.Value).Distinct().ToArray();
        var anchors = await db.CyclePhaseLogs.AsNoTracking()
            .Where(x => x.UserId == userId && x.CycleLogId != null && cycleIds.Contains(x.CycleLogId.Value))
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        var result = new List<CycleReadinessPatternObservation>();
        foreach (var credit in credits)
        {
            var session = sessions.SingleOrDefault(x => x.Id == credit.SourceSessionId);
            if (session is null || session.PlannedLocalDate != credit.LocalDate || session.PlannedTimeZoneId != timeZoneId ||
                DateOnly.FromDateTime(TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(session.CompletedAt!.Value, DateTimeKind.Utc), zone)) != credit.LocalDate ||
                PlannedPrescriptionSnapshot.Deserialize(session.PlannedPrescriptionSnapshotJson) is not { } snapshot ||
                snapshot.DayId != session.PlannedWorkoutDayId || snapshot.Exercises is null || snapshot.Exercises.Count == 0) continue;
            var matchingPhases = phases.Where(x => x.LocalDate == credit.LocalDate).ToArray();
            var matchingChecks = checks.Where(x => x.Date == credit.LocalDate).ToArray();
            if (matchingPhases.Length != 1 || matchingChecks.Length != 1) continue;
            var phase = matchingPhases[0];
            var check = matchingChecks[0];
            if (check.PlanningRecordedAtUtc is not { } recorded ||
                recorded > session.StartedAt || phase.CreatedAt > session.StartedAt ||
                !Enum.TryParse<CyclePhase>(phase.Phase, true, out var parsed) || parsed == CyclePhase.Unknown || !Enum.IsDefined(parsed)) continue;
            var cycle = cycles.SingleOrDefault(x => x.Id == phase.CycleLogId);
            if (cycle is null || cycle.StartDate > credit.LocalDate ||
                cycles.Any(x => x.StartDate > cycle.StartDate && x.StartDate <= credit.LocalDate) ||
                !anchors.Any(x => x.CycleLogId == cycle.Id && x.LocalDate == cycle.StartDate &&
                    string.Equals(x.Phase, nameof(CyclePhase.Menstrual), StringComparison.OrdinalIgnoreCase) &&
                    x.CreatedAt <= session.StartedAt)) continue;
            var recordedUtc = new DateTimeOffset(DateTime.SpecifyKind(recorded, DateTimeKind.Utc));
            var observation = new TodayCheckInObservation(check.Date, timeZoneId, recordedUtc, check.PlanningRevision,
                check.PlanningSleepQuality, check.Energy, check.PlanningSoreness, check.PlanningStress);
            var score = TodayCheckInScoreRule.Evaluate(observation, recordedUtc, timeZoneId);
            if (score.Status != CheckInScoreStatus.Available || score.Band is not
                (CheckInScoreBand.ConsiderLighterSession or CheckInScoreBand.ConsiderEasierDay)) continue;
            result.Add(new(parsed, cycle.Id.ToString("D"), check.Id.ToString("D"), credit.LocalDate, true, true));
        }
        return result;
    }
}

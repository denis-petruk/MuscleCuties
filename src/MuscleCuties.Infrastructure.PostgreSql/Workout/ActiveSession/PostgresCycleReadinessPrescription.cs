using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Health;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;

internal static class PostgresCycleReadinessPrescription
{
    private const int RequiredWorkingSets = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<PlannedPrescriptionSnapshot> ApplyAsync(PostgresDbContext db,
        Guid userId, DateOnly localDate, string timeZoneId, DateTimeOffset nowUtc,
        PlannedPrescriptionSnapshot prescription, CancellationToken cancellationToken)
    {
        DateOnly observedThrough;
        try { observedThrough = WorkoutAccountCalendarRule.LocalDate(nowUtc,
            TimeZoneInfo.FindSystemTimeZoneById(timeZoneId)); }
        catch (TimeZoneNotFoundException) { observedThrough = DateOnly.MinValue; }
        catch (InvalidTimeZoneException) { observedThrough = DateOnly.MinValue; }
        var isCurrentDay = localDate == observedThrough;
        var checkIn = isCurrentDay ? await db.DailyReadinessLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.Date == localDate)
            .OrderByDescending(row => row.PlanningRevision)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false) : null;
        var observation = checkIn is null ? null : new TodayCheckInObservation(
            checkIn.Date, checkIn.PlanningTimeZoneId ?? string.Empty,
            checkIn.PlanningRecordedAtUtc is { } recordedAt
                ? new DateTimeOffset(DateTime.SpecifyKind(recordedAt, DateTimeKind.Utc))
                : DateTimeOffset.MinValue,
            checkIn.PlanningRevision, checkIn.PlanningSleepQuality, checkIn.Energy,
            checkIn.PlanningSoreness, checkIn.PlanningStress);
        var score = TodayCheckInScoreRule.Evaluate(observation, nowUtc, timeZoneId);

        var phaseLog = isCurrentDay ? await db.CyclePhaseLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.LocalDate == localDate)
            .OrderByDescending(row => row.LoggedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false) : null;
        // Registration also writes an onboarding phase log, without a cycle link.
        // Only a same-day phase entry linked by the explicit phase logging path is
        // evidence of a current confirmation.
        var phase = phaseLog?.CycleLogId is not null &&
            Enum.TryParse<CyclePhase>(phaseLog.Phase, true, out var parsedPhase) &&
            parsedPhase is CyclePhase.Menstrual or CyclePhase.Follicular or
                CyclePhase.Ovulatory or CyclePhase.Luteal
            ? parsedPhase : CyclePhase.Unknown;
        var phaseProvenance = phase == CyclePhase.Unknown
            ? PhaseReviewProvenance.Unknown : PhaseReviewProvenance.UserConfirmed;
        if (phase != CyclePhase.Unknown)
        {
            var cycle = await db.CycleLogs.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId &&
                row.Id == phaseLog!.CycleLogId && row.StartDate <= localDate &&
                (row.EndDate == null || localDate < row.EndDate), cancellationToken).ConfigureAwait(false);
            var hasMenstrualAnchor = cycle is not null && await db.CyclePhaseLogs.AsNoTracking()
                .AnyAsync(row => row.UserId == userId && row.CycleLogId == cycle.Id &&
                    row.LocalDate == cycle.StartDate && row.Phase == nameof(CyclePhase.Menstrual), cancellationToken)
                .ConfigureAwait(false);
            if (!hasMenstrualAnchor)
            {
                phase = CyclePhase.Unknown;
                phaseProvenance = PhaseReviewProvenance.Unknown;
            }
        }
        if (phase == CyclePhase.Unknown && observedThrough != DateOnly.MinValue && localDate >= observedThrough)
        {
            var cycles = await db.CycleLogs.AsNoTracking().Where(row => row.UserId == userId &&
                row.StartDate <= observedThrough).OrderByDescending(row => row.StartDate)
                .ThenByDescending(row => row.Id).Take(WorkoutEstimatedCyclePhaseRule.RequiredRecordedStarts)
                .Select(row => new { row.Id, row.StartDate, row.EndDate }).ToArrayAsync(cancellationToken).ConfigureAwait(false);
            if (cycles.Length == WorkoutEstimatedCyclePhaseRule.RequiredRecordedStarts &&
                (cycles[0].EndDate is null || localDate < cycles[0].EndDate))
            {
                var ids = cycles.Select(row => row.Id).ToArray();
                var anchors = await db.CyclePhaseLogs.AsNoTracking().Where(row => row.UserId == userId &&
                    row.CycleLogId != null && ids.Contains(row.CycleLogId.Value) &&
                    row.Phase == nameof(CyclePhase.Menstrual))
                    .Select(row => new { row.CycleLogId, row.LocalDate })
                    .ToArrayAsync(cancellationToken).ConfigureAwait(false);
                if (cycles.All(cycle => anchors.Any(anchor => anchor.CycleLogId == cycle.Id &&
                    anchor.LocalDate == cycle.StartDate)))
                {
                    phase = WorkoutEstimatedCyclePhaseRule.Estimate(localDate, observedThrough,
                        cycles.Select(cycle => cycle.StartDate).ToArray());
                    if (phase != CyclePhase.Unknown)
                        phaseProvenance = PhaseReviewProvenance.Estimated;
                }
            }
        }
        PhaseBaseline? baseline = null;
        if (phase != CyclePhase.Unknown)
        {
            var profile = await db.UserProfiles.AsNoTracking()
                .Where(row => row.UserId == userId)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (!ReproductiveContextCodec.Parse(profile?.ReproductiveContextJson)
                    .AllowsCyclePhaseTrainingAdjustments)
            {
                phase = CyclePhase.Unknown;
                phaseProvenance = PhaseReviewProvenance.Unknown;
            }
            try
            {
                if (phase != CyclePhase.Unknown && !string.IsNullOrWhiteSpace(profile?.PhaseBaselinesJson))
                    baseline = JsonSerializer.Deserialize<CyclePhaseBaselines>(profile.PhaseBaselinesJson, JsonOptions)
                        ?.ForPhase(phase);
            }
            catch (JsonException) { /* Missing personal evidence retains normal dosage. */ }
        }

        var fromDate = localDate.AddDays(-CycleReadinessPrescriptionRule.PatternObservationWindowDays);
        var symptoms = await db.SymptomLogs.AsNoTracking().Where(row => row.UserId == userId &&
            row.Date >= fromDate && row.Date <= localDate)
            .Select(row => new CycleSafetySymptom(row.Date, row.SymptomType, row.Severity))
            .ToArrayAsync(cancellationToken).ConfigureAwait(false);
        var previousPain = await db.DailyReadinessLogs.AsNoTracking().Where(row => row.UserId == userId &&
            row.Date >= fromDate && row.Date < localDate && row.Pain != null)
            .OrderByDescending(row => row.Date).Select(row => row.Pain).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var changedPainDate = await db.WorkoutAccountPlanningStates.AsNoTracking().Where(row => row.UserId == userId)
            .Select(row => row.CycleReadinessPainReviewDate).SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var safety = CycleReadinessSafetyRule.Evaluate(localDate, phase, phase != CyclePhase.Unknown,
            checkIn?.Pain, previousPain, symptoms);
        var hasSafetyConcern = safety.RequiresReview || changedPainDate == localDate;
        var exerciseRecords = await db.WorkoutDayExercises.AsNoTracking()
            .Where(row => row.UserId == userId && row.WorkoutDayId == prescription.DayId &&
                !row.IsRemovedByReadiness)
            .ToDictionaryAsync(row => row.Id, cancellationToken).ConfigureAwait(false);
        var decision = CycleReadinessPrescriptionRule.Evaluate(new CycleReadinessPrescriptionInput(
            phase, phaseProvenance,
            baseline, score, hasSafetyConcern,
            prescription.Exercises.Select(exercise => new CycleReadinessExercise(
                exercise.Id, exercise.Sets, exercise.OptionalSets, exercise.IsOptional,
                exerciseRecords.TryGetValue(exercise.Id, out var record) &&
                record.PlanningSlotId is not null)).ToArray(),
            PatternObservations: phase == CyclePhase.Unknown ? null :
                await PostgresCyclePersonalizationEvidence.LoadAsync(db, userId, localDate, timeZoneId, cancellationToken).ConfigureAwait(false),
            EvaluationDate: localDate, SleepHours: checkIn?.SleepHours, RoutineSymptoms: safety.RoutineSymptoms,
            PhaseEstimateReliable: phaseProvenance == PhaseReviewProvenance.Estimated));
        return prescription with
        {
            Exercises = prescription.Exercises.Select(exercise => exercise.Id == decision.AdjustedPrescriptionId &&
                decision.AdjustedSets is { } adjustedSets
                ? exercise with { Sets = adjustedSets, OptionalSets = Math.Min(exercise.OptionalSets, adjustedSets - RequiredWorkingSets) }
                : exercise).ToArray(),
            DoseAdjustmentReasonCode = decision.ReasonCode,
            DoseAdjustmentPolicyVersion = CycleReadinessPrescriptionRule.PolicyVersion,
            DoseAdjustmentCheckInRevision = decision.CheckInRevision,
            DoseAdjustmentCyclePhase = decision.EvidencedPhase?.ToString(),
            DoseAdjustmentPhaseProvenance = decision.EvidencedPhase is null ? null : phaseProvenance.ToString(),
            DoseAdjustmentConfidence = decision.Confidence.ToString()
        };
    }
}

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
        var checkIn = await db.DailyReadinessLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.Date == localDate)
            .OrderByDescending(row => row.PlanningRevision)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        var observation = checkIn is null ? null : new TodayCheckInObservation(
            checkIn.Date, checkIn.PlanningTimeZoneId ?? string.Empty,
            checkIn.PlanningRecordedAtUtc is { } recordedAt
                ? new DateTimeOffset(DateTime.SpecifyKind(recordedAt, DateTimeKind.Utc))
                : DateTimeOffset.MinValue,
            checkIn.PlanningRevision, checkIn.PlanningSleepQuality, checkIn.Energy,
            checkIn.PlanningSoreness, checkIn.PlanningStress);
        var score = TodayCheckInScoreRule.Evaluate(observation, nowUtc, timeZoneId);

        var phaseLog = await db.CyclePhaseLogs.AsNoTracking()
            .Where(row => row.UserId == userId && row.LocalDate == localDate)
            .OrderByDescending(row => row.LoggedAt)
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        // Registration also writes an onboarding phase log, without a cycle link.
        // Only a same-day phase entry linked by the explicit phase logging path is
        // evidence of a current confirmation.
        var phase = phaseLog?.CycleLogId is not null &&
            Enum.TryParse<CyclePhase>(phaseLog.Phase, true, out var parsedPhase) &&
            parsedPhase is CyclePhase.Menstrual or CyclePhase.Follicular or
                CyclePhase.Ovulatory or CyclePhase.Luteal
            ? parsedPhase : CyclePhase.Unknown;
        if (phase != CyclePhase.Unknown)
        {
            var cycle = await db.CycleLogs.AsNoTracking().SingleOrDefaultAsync(row => row.UserId == userId &&
                row.Id == phaseLog!.CycleLogId && row.StartDate <= localDate &&
                (row.EndDate == null || localDate < row.EndDate), cancellationToken).ConfigureAwait(false);
            var hasMenstrualAnchor = cycle is not null && await db.CyclePhaseLogs.AsNoTracking()
                .AnyAsync(row => row.UserId == userId && row.CycleLogId == cycle.Id &&
                    row.LocalDate == cycle.StartDate && row.Phase == nameof(CyclePhase.Menstrual), cancellationToken)
                .ConfigureAwait(false);
            if (!hasMenstrualAnchor) phase = CyclePhase.Unknown;
        }
        PhaseBaseline? baseline = null;
        if (phase != CyclePhase.Unknown)
        {
            var profile = await db.UserProfiles.AsNoTracking()
                .Where(row => row.UserId == userId)
                .SingleOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            if (!ReproductiveContextCodec.Parse(profile?.ReproductiveContextJson)
                    .AllowsCyclePhaseTrainingAdjustments)
                phase = CyclePhase.Unknown;
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
            phase, phase == CyclePhase.Unknown ? PhaseReviewProvenance.Unknown :
                PhaseReviewProvenance.UserConfirmed,
            baseline, score, hasSafetyConcern,
            prescription.Exercises.Select(exercise => new CycleReadinessExercise(
                exercise.Id, exercise.Sets, exercise.OptionalSets, exercise.IsOptional,
                exerciseRecords.TryGetValue(exercise.Id, out var record) &&
                record.PlanningSlotId is not null)).ToArray(),
            PatternObservations: phase == CyclePhase.Unknown ? null :
                await PostgresCyclePersonalizationEvidence.LoadAsync(db, userId, localDate, timeZoneId, cancellationToken).ConfigureAwait(false),
            EvaluationDate: localDate, SleepHours: checkIn?.SleepHours, RoutineSymptoms: safety.RoutineSymptoms));
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
            DoseAdjustmentConfidence = decision.Confidence.ToString()
        };
    }
}

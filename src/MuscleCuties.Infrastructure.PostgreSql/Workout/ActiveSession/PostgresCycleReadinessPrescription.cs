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
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static async Task<PlannedPrescriptionSnapshot> ApplyAsync(PostgresDbContext db,
        Guid userId, DateOnly localDate, string timeZoneId, DateTimeOffset nowUtc,
        PlannedPrescriptionSnapshot prescription, CancellationToken cancellationToken)
    {
        if (prescription.Exercises.Count == 0)
            return prescription;

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
        var phase = phaseLog is not null &&
            Enum.TryParse<CyclePhase>(phaseLog.Phase, true, out var parsedPhase) &&
            parsedPhase is CyclePhase.Menstrual or CyclePhase.Follicular or
                CyclePhase.Ovulatory or CyclePhase.Luteal
            ? parsedPhase : CyclePhase.Unknown;
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

        var hasSafetyConcern = await db.SymptomLogs.AsNoTracking()
            .AnyAsync(row => row.UserId == userId && row.Date == localDate, cancellationToken)
            .ConfigureAwait(false) ||
            await db.WorkoutInjuryLogs.AsNoTracking()
                .AnyAsync(row => row.UserId == userId && row.Status != "Cleared", cancellationToken)
                .ConfigureAwait(false);
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
                record.PlanningSlotId is not null)).ToArray()));
        if (decision.AdjustedPrescriptionId is not { } adjustedId ||
            decision.AdjustedSets is not { } adjustedSets)
            return prescription;

        return prescription with
        {
            Exercises = prescription.Exercises.Select(exercise => exercise.Id == adjustedId
                ? exercise with { Sets = adjustedSets }
                : exercise).ToArray(),
            DoseAdjustmentReasonCode = decision.ReasonCode,
            DoseAdjustmentPolicyVersion = CycleReadinessPrescriptionRule.PolicyVersion,
            DoseAdjustmentCheckInRevision = decision.CheckInRevision,
            DoseAdjustmentCyclePhase = decision.EvidencedPhase?.ToString()
        };
    }
}

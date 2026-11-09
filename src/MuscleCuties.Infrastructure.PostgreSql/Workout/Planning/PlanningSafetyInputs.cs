using System.Text.Json;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;

/// <summary>
/// One owner for the saved safety facts every planning path reads: which injuries still count,
/// which movement patterns they block, and the parsed equipment and goal answers. Missing or
/// unparseable answers stay missing; nothing here supplies a default.
/// </summary>
internal static class PlanningSafetyInputs
{
    private const string ClearedStatus = "Cleared";

    internal static DateOnly? AccountToday(string? timeZoneId)
    {
        if (string.IsNullOrWhiteSpace(timeZoneId))
            return null;
        try
        {
            return WorkoutAccountCalendarRule.LocalDate(DateTimeOffset.UtcNow,
                TimeZoneInfo.FindSystemTimeZoneById(timeZoneId));
        }
        catch (TimeZoneNotFoundException) { return null; }
        catch (InvalidTimeZoneException) { return null; }
    }

    /// <summary>
    /// An injury stops counting only when it is cleared on or before the account's known date and
    /// every clearance episode is verified. An unknown date or unverified clearance keeps it active.
    /// </summary>
    internal static WorkoutInjuryLogRecord[] RelevantInjuries(
        IReadOnlyCollection<WorkoutInjuryLogRecord> injuries,
        IReadOnlyCollection<WorkoutInjuryEpisodeRecord> episodes,
        DateOnly? today) =>
        injuries.Where(injury =>
            !string.Equals(injury.Status, ClearedStatus, StringComparison.OrdinalIgnoreCase) || today is null ||
            injury.ClearedDate is null || injury.ClearedDate > today ||
            !episodes.Any(episode => episode.InjuryLogId == injury.Id && !episode.SourceDeleted &&
                episode.ClearedOn == injury.ClearedDate && !string.IsNullOrWhiteSpace(episode.ClearanceTimeZoneId) &&
                episode.ClearanceTimeZoneVersion is not null) ||
            episodes.Any(episode => episode.InjuryLogId == injury.Id && (episode.SourceDeleted ||
                episode.ClearedOn is null || episode.ClearedOn > today ||
                string.IsNullOrWhiteSpace(episode.ClearanceTimeZoneId) || episode.ClearanceTimeZoneVersion is null)))
            .ToArray();

    /// <summary>
    /// Safety answers drive exercise substitution, not only the cycle/readiness gate. The trigger
    /// mapping is closed; an explicit clinician restriction expands through reviewed catalog
    /// contraindications so it still applies when the trigger question was left unanswered.
    /// </summary>
    internal static HashSet<MovementPattern> BlockedMovementPatterns(
        IEnumerable<WorkoutInjuryLogRecord> relevantInjuries,
        IEnumerable<WorkoutExerciseDefinition> definitions) =>
        InjuryWorseningMovementRule.BlockedPatterns(
            relevantInjuries.Select(injury => new InjurySafetyAnswer(
                InjuryObservationRule.TryNormalizeWorseningTrigger(injury.WorseningTrigger, out var worsening)
                    ? worsening
                    : InjuryWorseningTrigger.NotAnswered,
                injury.ClinicianAdvisedAvoid,
                (InjuryFlag)injury.SiteFlag)),
            definitions.Select(definition => new ExerciseContraindicationPattern(
                definition.Pattern, definition.Contraindications)));

    internal static InjuryFlag ActiveInjuryFlags(IEnumerable<WorkoutInjuryLogRecord> relevantInjuries) =>
        relevantInjuries.Aggregate(InjuryFlag.None, (flags, injury) => flags | (InjuryFlag)injury.SiteFlag);

    internal static Equipment? ParseEquipment(string? value) =>
        Enum.TryParse<Equipment>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;

    internal static UserGoal? ParseGoal(string? value) =>
        Enum.TryParse<UserGoal>(value, true, out var parsed) && Enum.IsDefined(parsed) ? parsed : null;

    internal static TrainingExperienceLevel? ProfileTrainingExperience(
        WorkoutAccountPlanningStateRecord? planningState, UserProfileRecord? profile) =>
        planningState?.EffectiveExperience ?? (Enum.TryParse<TrainingExperienceLevel>(
            profile?.TrainingExperienceLevel, true, out var level) && Enum.IsDefined(level) ? level : null);

    internal static IReadOnlySet<int> ParseExcludedExerciseIds(string? json)
    {
        try
        {
            using var document = JsonDocument.Parse(string.IsNullOrWhiteSpace(json) ? "[]" : json);
            if (document.RootElement.ValueKind != JsonValueKind.Array)
                return new HashSet<int>();
            return document.RootElement.EnumerateArray()
                .Where(value => value.ValueKind == JsonValueKind.Number &&
                                value.TryGetInt32(out var exerciseId) && exerciseId > 0)
                .Select(value => value.GetInt32())
                .ToHashSet();
        }
        catch (JsonException)
        {
            return new HashSet<int>();
        }
    }
}

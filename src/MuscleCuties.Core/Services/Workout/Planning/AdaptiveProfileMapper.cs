using System.Text.Json;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout;

namespace MuscleCuties.Core.Services.Workout.Planning;

public static class AdaptiveProfileMapper
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static AdaptiveProfile FromUserProfile(
        UserProfile profile,
        IReadOnlyCollection<WorkoutInjuryLog>? injuryLogs = null)
    {
        var selectedActivities = WorkoutActivityPreferences
            .EnsureRequired(WorkoutActivityPreferences.Parse(profile.PreferredWorkoutActivityTypes))
            .ToHashSet();

        var sessionMinutes = profile.SessionDurationMinutes > 0 ? profile.SessionDurationMinutes : 60;
        var equipment = ParseEquipment(profile.EquipmentLevel);
        var baselines = ParseBaselines(profile.PhaseBaselinesJson);

        return new AdaptiveProfile(
            UserId: profile.UserId,
            Goal: profile.Goal,
            Experience: profile.TrainingExperienceLevel,
            DaysPerWeek: Math.Clamp(profile.WorkoutDaysPerWeek <= 0 ? 3 : profile.WorkoutDaysPerWeek, 2, 6),
            SessionMinutesCap: sessionMinutes,
            Selected: selectedActivities,
            Style: WorkoutActivityPreferences.ParseStrengthStyle(profile.PreferredWorkoutActivityTypes),
            Equipment: equipment,
            Injuries: MapInjuries(injuryLogs),
            BaselineSteps: 8000,
            Baselines: baselines);
    }

    private static Equipment ParseEquipment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Equipment.FullGym;

        return Enum.TryParse<Equipment>(value, true, out var equipment)
            ? equipment
            : Equipment.FullGym;
    }

    internal static CyclePhaseBaselines? ParseBaselines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CyclePhaseBaselines>(json, JsonOptions);
        }
        catch
        {
            return null;
        }
    }

    internal static string SerializeBaselines(CyclePhaseBaselines baselines)
    {
        return JsonSerializer.Serialize(baselines, JsonOptions);
    }

    private static List<Injury> MapInjuries(IReadOnlyCollection<WorkoutInjuryLog>? injuryLogs)
    {
        if (injuryLogs is null)
            return [];

        return injuryLogs
            .Where(log => Enum.TryParse<InjurySite>(log.Site, true, out _) &&
                          Enum.TryParse<InjuryStatus>(log.Status, true, out _))
            .Select(log => new Injury(
                Enum.Parse<InjurySite>(log.Site, true),
                Enum.Parse<InjuryStatus>(log.Status, true),
                log.Since))
            .ToList();
    }
}

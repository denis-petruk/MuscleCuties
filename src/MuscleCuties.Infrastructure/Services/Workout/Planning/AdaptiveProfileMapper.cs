using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Workout.Planning;

namespace MuscleCuties.Core.Services.Workout.Planning;

public static class AdaptiveProfileMapper
{
    private const int DefaultDaysPerWeek = 3;
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
            profile.UserId,
            profile.Goal,
            profile.TrainingExperienceLevel,
            WeekPlanRule.ClampDaysPerWeek(profile.WorkoutDaysPerWeek <= 0
                ? DefaultDaysPerWeek : profile.WorkoutDaysPerWeek),
            sessionMinutes,
            selectedActivities,
            WorkoutActivityPreferences.ParseStrengthStyle(profile.PreferredWorkoutActivityTypes),
            equipment,
            MapInjuries(injuryLogs),
            8000,
            baselines);
    }

    private static Equipment ParseEquipment(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return Equipment.FullGym;

        return Enum.TryParse<Equipment>(value, true, out var equipment)
            ? equipment
            : Equipment.FullGym;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    internal static CyclePhaseBaselines? ParseBaselines(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return null;

        try
        {
            return JsonSerializer.Deserialize<CyclePhaseBaselines>(json, JsonOptions);
        }
        catch (JsonException ex)
        {
            Trace.WriteLine(
                $"[{nameof(AdaptiveProfileMapper)}] Ignoring invalid cycle baselines ({ex.GetType().Name}).");
            return null;
        }
    }

    [UnconditionalSuppressMessage("Trimming", "IL2026",
        Justification = "EF Core blocks full trimming; reflection-based JSON is safe.")]
    internal static string SerializeBaselines(CyclePhaseBaselines baselines)
    {
        return JsonSerializer.Serialize(baselines, JsonOptions);
    }

    private static List<Injury> MapInjuries(IReadOnlyCollection<WorkoutInjuryLog>? injuryLogs)
    {
        return WorkoutInjuryRules.MapInjuries(injuryLogs);
    }
}

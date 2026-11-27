using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.UseCases.Features.Profile.ServerProfile;

/// <summary>The profile as the server stores it.</summary>
public sealed record ServerProfileSnapshot(
    string Name,
    DateOnly? DateOfBirth,
    decimal Height,
    decimal Weight,
    string Goal,
    string WeightGoalPace,
    string TrainingExperienceLevel,
    int WorkoutDaysPerWeek,
    string AvailableWorkoutWeekdaysJson,
    int CycleLength,
    string DietaryTags,
    string ActivitySessionDurationsJson,
    string EquipmentLevel,
    string PreferredWorkoutActivityTypes,
    string UnitSystem,
    string BodyWeightUnit,
    string FoodMassUnit,
    string HeightUnit,
    string DistanceUnit,
    string EnergyUnit,
    string NutritionGoalsJson,
    int Version,
    string Email = "",
    string ReproductiveContextJson = "",
    DateTime? CreatedAtUtc = null,
    string ProfileImagePath = "",
    string RequestedActivitySessionsPerWeekJson = "{}",
    string ExcludedStrengthExerciseIdsJson = "[]",
    bool GluteFocus = false,
    EquipmentCapabilities? EquipmentCapabilities = null);

/// <summary>A profile change, applied against the version it was based on.</summary>
public sealed record UpdateServerProfileRequest(
    Guid UserId,
    int ExpectedVersion,
    string? Name,
    DateOnly? DateOfBirth,
    bool SetDateOfBirth,
    decimal? Height,
    decimal? Weight,
    string? Goal,
    string? WeightGoalPace,
    string? TrainingExperienceLevel,
    int? CycleLength,
    string? DietaryTags,
    string? EquipmentLevel,
    string? ReproductiveContextJson = null,
    string? ProfileImagePath = null);

/// <summary>A change to the nutrition preferences.</summary>
public sealed record UpdateServerNutritionPreferencesRequest(
    Guid UserId,
    int ExpectedVersion,
    string Goal,
    string WeightGoalPace,
    string DietaryTags);

/// <summary>A change to the workout preferences.</summary>
public sealed record UpdateServerWorkoutPreferencesRequest(
    Guid UserId,
    int ExpectedVersion,
    IReadOnlyList<int> ActivityTypes,
    IReadOnlyList<int> AvailableWeekdays,
    IReadOnlyDictionary<int, int> ActivitySessionDurationsMinutes,
    string? EquipmentLevel = null,
    IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek = null,
    IReadOnlySet<int>? ExcludedExerciseIds = null,
    bool? GluteFocus = null,
    EquipmentCapabilities? EquipmentCapabilities = null);

/// <summary>A change to the nutrition targets.</summary>
public sealed record UpdateServerNutritionTargetsRequest(
    Guid UserId,
    int ExpectedVersion,
    IReadOnlyDictionary<string, string> Targets);

/// <summary>How a profile write ended.</summary>
public enum ServerProfileWriteStatus
{
    Saved,
    ProfileMissing,
    VersionConflict,
    InvalidRequest,
    InvalidCatalogReference
}

/// <summary>The outcome of a profile write, with the new version when it applied.</summary>
public sealed record ServerProfileWriteResult(ServerProfileWriteStatus Status, int? Version);

/// <summary>The server profile persistence.</summary>
public interface IServerProfileDataStore
{
    Task<ServerProfileSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateProfileAsync(UpdateServerProfileRequest request, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateNutritionPreferencesAsync(UpdateServerNutritionPreferencesRequest request, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateWorkoutPreferencesAsync(UpdateServerWorkoutPreferencesRequest request, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateNutritionTargetsAsync(UpdateServerNutritionTargetsRequest request, CancellationToken cancellationToken);
}

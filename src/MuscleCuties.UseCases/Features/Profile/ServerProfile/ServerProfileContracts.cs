namespace MuscleCuties.UseCases.Features.Profile.ServerProfile;

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
    string ExcludedStrengthExerciseIdsJson = "[]");

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

public sealed record ServerProfileHistoryEntry(Guid Id, string Reason, string ProfileJson, DateTime CreatedAtUtc);

public sealed record ServerQuizResponse(int QuestionId, int AnswerId, DateTime AnsweredAtUtc);

public sealed record CorrectServerQuizResponseRequest(
    Guid UserId,
    int ExpectedVersion,
    int QuestionId,
    int AnswerId);

public sealed record UpdateServerNutritionPreferencesRequest(
    Guid UserId,
    int ExpectedVersion,
    string Goal,
    string WeightGoalPace,
    string DietaryTags);

public sealed record UpdateServerWorkoutPreferencesRequest(
    Guid UserId,
    int ExpectedVersion,
    IReadOnlyList<int> ActivityTypes,
    IReadOnlyList<int> AvailableWeekdays,
    IReadOnlyDictionary<int, int> ActivitySessionDurationsMinutes,
    string? EquipmentLevel = null,
    IReadOnlyDictionary<int, int>? RequestedSessionsPerWeek = null,
    IReadOnlySet<int>? ExcludedExerciseIds = null);

public sealed record UpdateServerNutritionTargetsRequest(
    Guid UserId,
    int ExpectedVersion,
    IReadOnlyDictionary<string, string> Targets);

public enum ServerProfileWriteStatus
{
    Saved,
    ProfileMissing,
    VersionConflict,
    InvalidRequest,
    InvalidCatalogReference
}

public sealed record ServerProfileWriteResult(ServerProfileWriteStatus Status, int? Version);

public interface IServerProfileDataStore
{
    Task<ServerProfileSnapshot?> LoadAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ServerProfileHistoryEntry>> LoadHistoryAsync(Guid userId, CancellationToken cancellationToken);
    Task<IReadOnlyList<ServerQuizResponse>> LoadQuizResponsesAsync(Guid userId, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateProfileAsync(UpdateServerProfileRequest request, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> CorrectQuizResponseAsync(CorrectServerQuizResponseRequest request, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateNutritionPreferencesAsync(UpdateServerNutritionPreferencesRequest request, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateWorkoutPreferencesAsync(UpdateServerWorkoutPreferencesRequest request, CancellationToken cancellationToken);
    Task<ServerProfileWriteResult> UpdateNutritionTargetsAsync(UpdateServerNutritionTargetsRequest request, CancellationToken cancellationToken);
}

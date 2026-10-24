using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.UseCases.Features.Cycle.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.Overview;

public sealed record ProfileOverviewDto(
    string Email,
    DateTime? MemberSince,
    string Name,
    UserGoal Goal,
    WeightGoalPace WeightGoalPace,
    int CycleDays,
    string ProfileImagePath,
    string UnitSystem,
    string BodyWeightUnit,
    string HeightUnit,
    int AvailableWeekdaysCount,
    int CompletedSessions,
    int WorkoutStreakDays,
    int NutritionStreakDays,
    CyclePhaseDto CurrentPhase,
    int CyclesTracked,
    bool? IsHealthConnected,
    int ActiveInjuryCount,
    int VaultPhotoCount,
    string LatestVaultPhotoPath);

public sealed record ProfileOverviewSource(
    ProfileOverviewDto Overview,
    CyclePhase PredictedPhase,
    CyclePhase? StoredPhase);

public sealed record ProfilePurgeResult(bool Succeeded, bool AccountFound);

public interface IProfileOverviewUseCase
{
    Task<ProfileOverviewDto> LoadAsync(CancellationToken cancellationToken = default);
    Task LogoutAsync(CancellationToken cancellationToken = default);
    Task<string?> UpdateProfileImageAsync(string? imagePath, CancellationToken cancellationToken = default);
    Task<ProfilePurgeResult> PurgeCurrentAccountAsync(CancellationToken cancellationToken = default);
}

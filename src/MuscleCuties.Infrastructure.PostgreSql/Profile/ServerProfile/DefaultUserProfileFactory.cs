using MuscleCuties.Infrastructure.PostgreSql.Models.App;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.ServerProfile;

/// <summary>
/// Builds an initial, unfilled <see cref="UserProfileRecord"/> for authenticated accounts that were
/// created without completing the email onboarding quiz (for example first-time Sign in with Apple).
/// Safety-critical workout and health fields remain unset so workout planning still requires explicit
/// user input before generation runs.
/// </summary>
internal static class DefaultUserProfileFactory
{
    private const string EmptyJsonObject = "{}";
    private const string EmptyJsonArray = "[]";
    private const int InitialConcurrencyVersion = 1;

    internal static UserProfileRecord CreateInitial(Guid userId, string? displayName, DateTime nowUtc) => new()
    {
        Id = Guid.CreateVersion7(),
        UserId = userId,
        Name = string.IsNullOrWhiteSpace(displayName) ? string.Empty : displayName.Trim(),
        DateOfBirth = null,
        Height = 0m,
        Weight = 0m,
        Goal = string.Empty,
        WeightGoalPace = string.Empty,
        TrainingExperienceLevel = string.Empty,
        CurrentCyclePhase = null,
        WorkoutDaysPerWeek = 0,
        AvailableWorkoutWeekdaysJson = EmptyJsonArray,
        CycleLength = 0,
        DietaryTags = string.Empty,
        ActivitySessionDurationsJson = EmptyJsonObject,
        RequestedActivitySessionsPerWeekJson = EmptyJsonObject,
        ExcludedStrengthExerciseIdsJson = EmptyJsonArray,
        EquipmentLevel = string.Empty,
        PhaseBaselinesJson = EmptyJsonObject,
        PreferredWorkoutActivityTypes = string.Empty,
        UnitSystem = string.Empty,
        BodyWeightUnit = string.Empty,
        FoodMassUnit = string.Empty,
        HeightUnit = string.Empty,
        DistanceUnit = string.Empty,
        EnergyUnit = string.Empty,
        NutritionGoalsJson = EmptyJsonObject,
        ReproductiveContextJson = EmptyJsonObject,
        ProfileImagePath = string.Empty,
        GluteFocus = false,
        ConcurrencyVersion = InitialConcurrencyVersion,
        UpdatedAt = nowUtc
    };
}

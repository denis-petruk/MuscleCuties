using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

// PostgreSQL persistence rows use server-issued UUIDv7 identifiers for tenant data.
public interface ITenantRecord
{
    Guid Id { get; set; }
    Guid UserId { get; set; }
}

public sealed class UserRecord
{
    public Guid Id { get; set; }
    public string? Username { get; set; }
    public string Email { get; set; } = string.Empty;
    public string NormalizedEmail { get; private set; } = string.Empty;
    public string? AppleUserId { get; set; }
    public string CredentialType { get; set; } = "Password";
    public string? PasswordHash { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsOnboardingComplete { get; set; }
    public string? PrivacyNoticeVersion { get; set; }
    public DateTime? PrivacyNoticeAcceptedAtUtc { get; set; }
}

public sealed class EmailChallengeRecord
{
    public string Id { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public DateTime RedeemedAtUtc { get; set; }
}

public sealed class RefreshSessionRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid FamilyId { get; set; }
    public string TokenHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public DateTime? ReplacedAtUtc { get; set; }
}

public sealed class AppleSignInNonceRecord
{
    public Guid Id { get; set; }
    public string NonceHash { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RedeemedAtUtc { get; set; }
}

public sealed class RevokedAccessTokenRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string TokenId { get; set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime RevokedAtUtc { get; set; }
}

public sealed class EmailChallengeSessionRecord
{
    public Guid Id { get; set; }
    public string NormalizedEmail { get; set; } = string.Empty;
    public int Purpose { get; set; }
    public string HmacKeyId { get; set; } = string.Empty;
    public string CodeMac { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime ResendAvailableAtUtc { get; set; }
    public DateTime? ConsumedAtUtc { get; set; }
    public DateTime? SupersededAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public int FailedAttempts { get; set; }
    public string? ReceiptId { get; set; }
    public string? PrivacyNoticeVersion { get; set; }
    public DateTime? PrivacyNoticeAcceptedAtUtc { get; set; }
}

public sealed class SourceAccountClaimNonceRecord
{
    public Guid Id { get; set; }
    public Guid DestinationUserId { get; set; }
    public string SourceInstallationKeyHash { get; set; } = string.Empty;
    public string SourceAccountKeyHash { get; set; } = string.Empty;
    public string NonceHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RedeemedAtUtc { get; set; }
}

public sealed class SourceAccountMappingRecord
{
    public Guid Id { get; set; }
    public Guid ClaimId { get; set; }
    public Guid DestinationUserId { get; set; }
    public string SourceInstallationKeyHash { get; set; } = string.Empty;
    public string SourceAccountKeyHash { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class UserProfileRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    public decimal Height { get; set; }
    public decimal Weight { get; set; }
    public string Goal { get; set; } = string.Empty;
    public string WeightGoalPace { get; set; } = string.Empty;
    public string TrainingExperienceLevel { get; set; } = string.Empty;
    public string CycleTrackingMode { get; set; } = string.Empty;
    public string? CurrentCyclePhase { get; set; }
    public int WorkoutDaysPerWeek { get; set; }
    public int CycleLength { get; set; }
    public string DietaryTags { get; set; } = string.Empty;
    public int SessionDurationMinutes { get; set; }
    public string EquipmentLevel { get; set; } = string.Empty;
    public string PhaseBaselinesJson { get; set; } = string.Empty;
    public string PreferredWorkoutActivityTypes { get; set; } = string.Empty;
    public string UnitSystem { get; set; } = string.Empty;
    public string BodyWeightUnit { get; set; } = string.Empty;
    public string FoodMassUnit { get; set; } = string.Empty;
    public string HeightUnit { get; set; } = string.Empty;
    public string DistanceUnit { get; set; } = string.Empty;
    public string EnergyUnit { get; set; } = string.Empty;
    public string NutritionGoalsJson { get; set; } = string.Empty;
    public string ReproductiveContextJson { get; set; } = string.Empty;
    public string ProfileImagePath { get; set; } = string.Empty;
    public int ConcurrencyVersion { get; set; } = 1;
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Atomic owner-scoped replay journal for externally submitted database commands.</summary>
public sealed class CommandIdempotencyRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string CommandKey { get; set; } = string.Empty;
    public string RequestHash { get; set; } = string.Empty;
    public string? ResponseJson { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public sealed class NutritionDiaryEventCursorRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long LastCursor { get; set; }
}

public sealed class NutritionDiaryCommandEventRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public long Cursor { get; set; }
    public DateOnly DiaryDate { get; set; }
    public string CommandId { get; set; } = string.Empty;
}

public sealed class UserProfileSnapshotRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string SnapshotReason { get; set; } = string.Empty;
    public string ProfileJson { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class UserQuizResponseRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int QuizQuestionId { get; set; }
    public int QuizAnswerId { get; set; }
    public DateTime AnsweredAt { get; set; }
    public Guid? UserProfileSnapshotId { get; set; }
}

public sealed class CycleLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    public int CycleLength { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class CyclePhaseLogRecord : ITenantRecord
{
    private DateOnly _localDate;
    private DateTime _loggedAt;

    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid? CycleLogId { get; set; }
    public string Phase { get; set; } = string.Empty;
    public string? FlowIntensity { get; set; }
    public DateOnly LocalDate
    {
        get => _localDate != default ? _localDate : (_loggedAt != default ? DateOnly.FromDateTime(_loggedAt) : default);
        set => _localDate = value;
    }
    public DateTime LoggedAt
    {
        get => _loggedAt;
        set
        {
            _loggedAt = value;
            if (_localDate == default && value != default)
                _localDate = DateOnly.FromDateTime(value);
        }
    }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SymptomLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid CycleLogId { get; set; }
    public DateOnly Date { get; set; }
    public string SymptomType { get; set; } = string.Empty;
    public int Severity { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class DailyReadinessLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public decimal? SleepHours { get; set; }
    public decimal? Sleep3dAvg { get; set; }
    public int? StepsYesterday { get; set; }
    public int? Steps7dAvg { get; set; }
    public int? Energy { get; set; }
    public int? Pain { get; set; }
    public bool? Bloating { get; set; }
    public decimal? WeightKg { get; set; }
    public int? SleepQuality { get; set; }
    public int? Soreness { get; set; }
    public int? PlanningSleepQuality { get; set; }
    public int? PlanningSoreness { get; set; }
    public int? PlanningStress { get; set; }
    public string? PlanningTimeZoneId { get; set; }
    public DateTime? PlanningRecordedAtUtc { get; set; }
    public long PlanningRevision { get; set; }
    public decimal? PreviousDayLoad { get; set; }
    public int ReadinessScore { get; set; }
    public ReadinessTier ReadinessTier { get; set; }
    public string Phase { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class WorkoutInjuryLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int SiteFlag { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateOnly Since { get; set; }
    public int Pain { get; set; }
    public DateOnly Date { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WorkoutPlanRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public string? CyclePhaseTarget { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>Immutable v0.2 proposal and input snapshot; acceptance state changes only inside the API.</summary>
public sealed class WorkoutPlanProposalV02Record : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public Guid ExpectedRevisionId { get; set; }
    public string RulesetVersion { get; set; } = string.Empty;
    public string TrainingPolicyVersion { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string InputSnapshotJson { get; set; } = "{}";
    public string ProposalJson { get; set; } = "{}";
    public string ReasonsJson { get; set; } = "[]";
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? DecidedAtUtc { get; set; }
    public Guid? AcceptedRevisionId { get; set; }
    public int ConcurrencyVersion { get; set; } = 1;
}

/// <summary>Append-only accepted v0.2 plan revision; prior revisions remain queryable.</summary>
public sealed class WorkoutPlanRevisionV02Record : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid PlanId { get; set; }
    public long RevisionNumber { get; set; }
    public Guid SourceProposalId { get; set; }
    public string RulesetVersion { get; set; } = string.Empty;
    public string TrainingPolicyVersion { get; set; } = string.Empty;
    public string PlanJson { get; set; } = "{}";
    public string InputSnapshotJson { get; set; } = "{}";
    public DateTime AcceptedAtUtc { get; set; }
}

public sealed class WorkoutDayRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkoutPlanId { get; set; }
    public int DayOfWeek { get; set; }
    public string WorkoutType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int WeekIndex { get; set; }
    public DateOnly? Date { get; set; }
    public bool IsOptional { get; set; }
    public bool IsEasierAlternative { get; set; }
    public bool IsRemovedByReadiness { get; set; }
}

public sealed class WorkoutDayExerciseRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkoutDayId { get; set; }
    public int ExerciseId { get; set; }
    public int Sets { get; set; }
    public int OptionalSets { get; set; }
    public int Reps { get; set; }
    public int? DurationSeconds { get; set; }
    public bool IsOptional { get; set; }
    public bool IsRemovedByReadiness { get; set; }
}

public sealed class WorkoutSessionLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string WorkoutName { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public int TotalDurationSeconds { get; set; }
    public decimal TotalVolumeKg { get; set; }
    public string? Notes { get; set; }
    public int? RpeRating { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class ExerciseSetLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkoutSessionLogId { get; set; }
    public string? ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public int SetNumber { get; set; }
    public decimal WeightKg { get; set; }
    public int RepsCompleted { get; set; }
    public decimal? Rpe { get; set; }
    public bool IsWarmup { get; set; }
    public int RestTimeSeconds { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class WorkoutLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkoutDayId { get; set; }
    public DateOnly Date { get; set; }
    public int CompletionPercent { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class WorkoutExerciseLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid WorkoutLogId { get; set; }
    public Guid WorkoutDayExerciseId { get; set; }
    public int ExerciseId { get; set; }
    public int CompletedSets { get; set; }
    public int CompletedReps { get; set; }
    public decimal? WeightKg { get; set; }
    public int? CompletedDurationSeconds { get; set; }
    public decimal? DistanceKm { get; set; }
    public int? AverageHeartRateBpm { get; set; }
    public int? PaceSecondsPerKm { get; set; }
    public int? PowerWatts { get; set; }
    public int? CadenceRpm { get; set; }
    public int? EffortRating { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserExercisePreferenceRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int OriginalExerciseId { get; set; }
    public int PreferredExerciseId { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserMovementNoteRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public int ExerciseId { get; set; }
    public string ExerciseName { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class DailyNutritionLogRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly LocalDate { get; set; }
    public int WaterIntakeMl { get; set; }
    public decimal TargetCalories { get; set; }
    public decimal TargetProtein { get; set; }
    public decimal TargetCarbs { get; set; }
    public decimal TargetFat { get; set; }
    public decimal TotalCalories { get; set; }
    public decimal TotalProtein { get; set; }
    public decimal TotalCarbs { get; set; }
    public decimal TotalFat { get; set; }
    public string? Notes { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class FoodItemEntryRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid DailyLogId { get; set; }
    public DateOnly LocalDate { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal ServingSize { get; set; }
    public string ServingUnit { get; set; } = string.Empty;
    public decimal ServingsCount { get; set; }
    public decimal Calories { get; set; }
    public decimal Protein { get; set; }
    public decimal Carbs { get; set; }
    public decimal Fat { get; set; }
    public DateTime LoggedAtTimestamp { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UserFoodItemRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal? Calories { get; set; }
    public decimal? Protein { get; set; }
    public decimal? Carbs { get; set; }
    public decimal? Fats { get; set; }
    public decimal? Fiber { get; set; }
    public decimal? Iron { get; set; }
    public decimal? VitaminB12 { get; set; }
    public decimal? VitaminC { get; set; }
    public decimal? VitaminD { get; set; }
    public decimal? VitaminA { get; set; }
    public decimal? VitaminB6 { get; set; }
    public decimal? Folate { get; set; }
    public decimal? Calcium { get; set; }
    public decimal? Magnesium { get; set; }
    public decimal? Zinc { get; set; }
    public decimal? Potassium { get; set; }
    public int? FdcId { get; set; }
    public string? DataType { get; set; }
    public string? BrandOwner { get; set; }
    public string? BrandName { get; set; }
    public string? GtinUpc { get; set; }
    public string? Ingredients { get; set; }
    public decimal? ServingSize { get; set; }
    public string? ServingSizeUnit { get; set; }
    public string? ServingOptionsJson { get; set; }
    public DateTime? LastSyncedAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public sealed class UserMealTemplateRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string DietaryTags { get; set; } = string.Empty;
    public string PhaseTags { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class UserMealTemplateEntryRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid MealTemplateId { get; set; }
    public int? CatalogFoodItemId { get; set; }
    public Guid? UserFoodItemId { get; set; }
    public decimal Grams { get; set; }
}

public sealed class LoggedMealRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public DateTime LoggedAt { get; set; }
    public string MealType { get; set; } = string.Empty;
    public string? AcceptedSuggestionName { get; set; }
    public string? AcceptedSuggestionPhase { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class LoggedMealEntryRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid LoggedMealId { get; set; }
    public int? CatalogFoodItemId { get; set; }
    public Guid? UserFoodItemId { get; set; }
    public decimal Grams { get; set; }
}

public sealed class RecipeRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class SavedMealTemplateRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string MealType { get; set; } = string.Empty;
    public string SnapshotJson { get; set; } = string.Empty;
    public DateTime CreatedAtUtc { get; set; }
}

public sealed class PhotoJourneyEntryRecord : ITenantRecord
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public DateOnly Date { get; set; }
    public string ImagePath { get; set; } = string.Empty;
    public decimal? WeightKg { get; set; }
    public string Notes { get; set; } = string.Empty;
    public string Phase { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

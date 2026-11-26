
namespace MuscleCuties.Infrastructure.PostgreSql.Models.App;

// PostgreSQL persistence rows use server-issued UUIDv7 identifiers for tenant data.
/// <summary>Marks a row that belongs to one account, which is what the per-tenant query filter is applied through.</summary>
public interface ITenantRecord
{
    Guid Id { get; set; }
    Guid UserId { get; set; }
}

/// <summary>An account and how it signs in. It is not tenant-filtered: it is the row every other app table points at.</summary>
public sealed class UserRecord
{
    /// <summary>The account id; every tenant row references it.</summary>
    public Guid Id { get; set; }
    /// <summary>Optional display handle; accounts created through Apple may never choose one.</summary>
    public string? Username { get; set; }
    /// <summary>The address as typed, kept for display.</summary>
    public string Email { get; set; } = string.Empty;
    /// <summary>The lower-cased address uniqueness is enforced on, so casing cannot create a second account.</summary>
    public string NormalizedEmail { get; private set; } = string.Empty;
    /// <summary>The Apple subject identifier, set only for Apple accounts.</summary>
    public string? AppleUserId { get; set; }
    /// <summary>How the account signs in, stored as text.</summary>
    public string CredentialType { get; set; } = "Password";
    /// <summary>The hashed password; null for platform sign-ins.</summary>
    public string? PasswordHash { get; set; }
    /// <summary>When the account was created.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When the account last changed.</summary>
    public DateTime UpdatedAt { get; set; }
    /// <summary>Whether sign-in leads to the dashboard or back into onboarding.</summary>
    public bool IsOnboardingComplete { get; set; }
    /// <summary>The privacy notice version she accepted, so a newer notice can be re-consented.</summary>
    public string? PrivacyNoticeVersion { get; set; }
    /// <summary>When that notice was accepted.</summary>
    public DateTime? PrivacyNoticeAcceptedAtUtc { get; set; }
}

/// <summary>A one-time verification code's redemption, keyed by the challenge id the email carried.</summary>
public sealed class EmailChallengeRecord
{
    /// <summary>The challenge id the email carried, which is what the redemption is looked up by.</summary>
    public string Id { get; set; } = string.Empty;
    /// <summary>What the code was for, so a sign-in code cannot redeem a deletion.</summary>
    public string Purpose { get; set; } = string.Empty;
    /// <summary>When the code was used; a second redemption is refused.</summary>
    public DateTime RedeemedAtUtc { get; set; }
}

/// <summary>One issued refresh token. Rotation writes a new row and marks the old one replaced, so a replayed token is detectable.</summary>
public sealed class RefreshSessionRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The rotation family, so one stolen token revokes its whole lineage.</summary>
    public Guid FamilyId { get; set; }
    /// <summary>A hash of the token; the token itself is never stored.</summary>
    public string TokenHash { get; set; } = string.Empty;
    /// <summary>When the token was issued.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>After this it cannot be exchanged, whatever else the row says.</summary>
    public DateTime ExpiresAtUtc { get; set; }
    /// <summary>When it was revoked outside rotation.</summary>
    public DateTime? RevokedAtUtc { get; set; }
    /// <summary>When rotation replaced it, which is what makes a replay of the old token suspicious.</summary>
    public DateTime? ReplacedAtUtc { get; set; }
}

/// <summary>A one-time nonce an Apple identity token must carry, which is what stops a token being replayed.</summary>
public sealed class AppleSignInNonceRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>A hash of the nonce the client signed; the nonce itself is never stored.</summary>
    public string NonceHash { get; set; } = string.Empty;
    /// <summary>After this the nonce is useless, which bounds a replay window.</summary>
    public DateTime ExpiresAtUtc { get; set; }
    /// <summary>When it was consumed; null while still valid.</summary>
    public DateTime? RedeemedAtUtc { get; set; }
}

/// <summary>An access token revoked before its expiry, so a signed-out token stops working immediately.</summary>
public sealed class RevokedAccessTokenRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The token's own id, which is what an incoming token is matched by.</summary>
    public string TokenId { get; set; } = string.Empty;
    /// <summary>The token's original expiry, after which the row is redundant.</summary>
    public DateTime ExpiresAtUtc { get; set; }
    /// <summary>When it was revoked.</summary>
    public DateTime RevokedAtUtc { get; set; }
}

/// <summary>A verification code challenge. Only a keyed hash of the code is stored, with the attempt and resend limits enforced here.</summary>
public sealed class EmailChallengeSessionRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The address the code was sent to, normalised so casing cannot fork a challenge.</summary>
    public string NormalizedEmail { get; set; } = string.Empty;
    /// <summary>What the code authorises, stored as a number so an unknown value is not mistaken for a known one.</summary>
    public int Purpose { get; set; }
    /// <summary>Which key signed the code, so a key rotation can still verify older challenges.</summary>
    public string HmacKeyId { get; set; } = string.Empty;
    /// <summary>The keyed hash of the code; the code itself is never stored.</summary>
    public string CodeMac { get; set; } = string.Empty;
    /// <summary>When the challenge was created.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>When the code stops working.</summary>
    public DateTime ExpiresAtUtc { get; set; }
    /// <summary>Earliest resend, which is what rate-limits the flow.</summary>
    public DateTime ResendAvailableAtUtc { get; set; }
    /// <summary>When it was redeemed successfully.</summary>
    public DateTime? ConsumedAtUtc { get; set; }
    /// <summary>When a resend replaced it, so an older code cannot still be used.</summary>
    public DateTime? SupersededAtUtc { get; set; }
    /// <summary>When too many wrong attempts locked it.</summary>
    public DateTime? LockedAtUtc { get; set; }
    /// <summary>Wrong attempts so far, compared against the lock threshold.</summary>
    public int FailedAttempts { get; set; }
    /// <summary>The delivery receipt, kept so a send can be traced without storing the address twice.</summary>
    public string? ReceiptId { get; set; }
    /// <summary>The notice version accepted as part of this challenge.</summary>
    public string? PrivacyNoticeVersion { get; set; }
    /// <summary>When that version was accepted.</summary>
    public DateTime? PrivacyNoticeAcceptedAtUtc { get; set; }
}

/// <summary>A one-time nonce that authorises claiming an account that predates this backend.</summary>
public sealed class SourceAccountClaimNonceRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The account the claim will apply to.</summary>
    public Guid DestinationUserId { get; set; }
    /// <summary>Hash of the installation identifier the claim came from.</summary>
    public string SourceInstallationKeyHash { get; set; } = string.Empty;
    /// <summary>Hash of the source account identifier being claimed.</summary>
    public string SourceAccountKeyHash { get; set; } = string.Empty;
    /// <summary>Hash of the nonce proving the claim was issued, not guessed.</summary>
    public string NonceHash { get; set; } = string.Empty;
    /// <summary>When the claim nonce was issued.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>When it stops being usable.</summary>
    public DateTime ExpiresAtUtc { get; set; }
    /// <summary>When the claim was completed.</summary>
    public DateTime? RedeemedAtUtc { get; set; }
}

/// <summary>The permanent record that a source account became a given account, so an import is never applied twice.</summary>
public sealed class SourceAccountMappingRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The claim that established the mapping.</summary>
    public Guid ClaimId { get; set; }
    /// <summary>The account the source account now belongs to.</summary>
    public Guid DestinationUserId { get; set; }
    /// <summary>Hash of the installation identifier, so the same install can be recognised.</summary>
    public string SourceInstallationKeyHash { get; set; } = string.Empty;
    /// <summary>Hash of the source account identifier, which is what makes the mapping unique.</summary>
    public string SourceAccountKeyHash { get; set; } = string.Empty;
    /// <summary>When the mapping was made.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>The account's profile: onboarding answers, units and nutrition targets, with the version a save must send back.</summary>
public sealed class UserProfileRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The name she chose to be called.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Used for age in the calorie and safety calculations; null until onboarding asks.</summary>
    public DateOnly? DateOfBirth { get; set; }
    /// <summary>Stored in centimetres; converted for display only.</summary>
    public decimal Height { get; set; }
    /// <summary>Stored in kilograms; converted for display only.</summary>
    public decimal Weight { get; set; }
    /// <summary>The training goal, stored as text so a renamed enum member cannot rewrite stored rows.</summary>
    public string Goal { get; set; } = string.Empty;
    /// <summary>How fast she wants to change weight, which caps the calorie deficit or surplus.</summary>
    public string WeightGoalPace { get; set; } = string.Empty;
    /// <summary>The level chosen at setup, which bounds what the planner may prescribe.</summary>
    public string TrainingExperienceLevel { get; set; } = string.Empty;
    /// <summary>The phase in force, or null when she has not recorded cycles.</summary>
    public string? CurrentCyclePhase { get; set; }
    /// <summary>How many days she trains, which selects the week template.</summary>
    public int WorkoutDaysPerWeek { get; set; }
    /// <summary>The weekdays she can train, as JSON.</summary>
    public string AvailableWorkoutWeekdaysJson { get; set; } = "[]";
    /// <summary>Her cycle length in days, which the phase projection counts with.</summary>
    public int CycleLength { get; set; }
    /// <summary>Comma-separated dietary preferences the meal planner filters on.</summary>
    public string DietaryTags { get; set; } = string.Empty;
    /// <summary>Minutes per activity type, as JSON.</summary>
    public string ActivitySessionDurationsJson { get; set; } = "{}";
    /// <summary>Wanted sessions per activity type, as JSON.</summary>
    public string RequestedActivitySessionsPerWeekJson { get; set; } = "{}";
    /// <summary>Exercises she excluded, as JSON; the planner never prescribes them.</summary>
    public string ExcludedStrengthExerciseIdsJson { get; set; } = "[]";
    /// <summary>The equipment she has, which limits the exercises available.</summary>
    public string EquipmentLevel { get; set; } = string.Empty;
    /// <summary>Per-phase starting values, as JSON, so a change of phase is not treated as a fresh start.</summary>
    public string PhaseBaselinesJson { get; set; } = string.Empty;
    /// <summary>Comma-separated activity types she prefers.</summary>
    public string PreferredWorkoutActivityTypes { get; set; } = string.Empty;
    /// <summary>Metric or imperial, which every other unit column is interpreted under.</summary>
    public string UnitSystem { get; set; } = string.Empty;
    /// <summary>Display unit for body weight.</summary>
    public string BodyWeightUnit { get; set; } = string.Empty;
    /// <summary>Display unit for food mass.</summary>
    public string FoodMassUnit { get; set; } = string.Empty;
    /// <summary>Display unit for height.</summary>
    public string HeightUnit { get; set; } = string.Empty;
    /// <summary>Display unit for distance.</summary>
    public string DistanceUnit { get; set; } = string.Empty;
    /// <summary>Display unit for energy.</summary>
    public string EnergyUnit { get; set; } = string.Empty;
    /// <summary>The nutrition targets as JSON, so a new target needs no migration.</summary>
    public string NutritionGoalsJson { get; set; } = string.Empty;
    /// <summary>The reproductive context answers as JSON; they gate cycle features only.</summary>
    public string ReproductiveContextJson { get; set; } = string.Empty;
    /// <summary>Path of the stored avatar, on the device or in the photo store.</summary>
    public string ProfileImagePath { get; set; } = string.Empty;
    /// <summary>User-selected glute focus. Stored on the profile so planning never has to infer it.</summary>
    public bool GluteFocus { get; set; }
    /// <summary>Bumped on every save; a writer holding an older value is refused instead of overwriting.</summary>
    public int ConcurrencyVersion { get; set; } = 1;
    /// <summary>When the profile last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>Atomic owner-scoped replay journal for externally submitted database commands.</summary>
public sealed class CommandIdempotencyRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The caller's key for the command; unique per account, so a retry finds this row.</summary>
    public string CommandKey { get; set; } = string.Empty;
    /// <summary>Hash of the request, so the same key with different content is rejected rather than replayed.</summary>
    public string RequestHash { get; set; } = string.Empty;
    /// <summary>The first run's answer, replayed to later attempts with the same key.</summary>
    public string? ResponseJson { get; set; }
    /// <summary>When the command finished; null while it is still in flight.</summary>
    public DateTime? CompletedAt { get; set; }
}

/// <summary>How far a device has consumed one account's diary event stream.</summary>
public sealed class NutritionDiaryEventCursorRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The highest event cursor this device has applied, so a reload resumes rather than replays.</summary>
    public long LastCursor { get; set; }
}

/// <summary>One appended diary command, the event the cursor reads forward through.</summary>
public sealed class NutritionDiaryCommandEventRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>Monotonic position in the account's stream, which is what the cursor advances over.</summary>
    public long Cursor { get; set; }
    /// <summary>The day the command affected, so a client can resync one day.</summary>
    public DateOnly DiaryDate { get; set; }
    /// <summary>The idempotency key of the command that produced the event.</summary>
    public string CommandId { get; set; } = string.Empty;
}

/// <summary>A profile frozen for a reason (plan generation, import), so the inputs a decision used stay readable.</summary>
public sealed class UserProfileSnapshotRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>Why the copy was taken, so a plan can be traced to its inputs.</summary>
    public string SnapshotReason { get; set; } = string.Empty;
    /// <summary>The profile as it stood, frozen.</summary>
    public string ProfileJson { get; set; } = string.Empty;
    /// <summary>When the copy was taken.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>One onboarding answer, kept apart from the profile it was folded into.</summary>
public sealed class UserQuizResponseRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The question answered.</summary>
    public int QuizQuestionId { get; set; }
    /// <summary>The option chosen.</summary>
    public int QuizAnswerId { get; set; }
    /// <summary>When it was answered; the latest answer per question wins.</summary>
    public DateTime AnsweredAt { get; set; }
    /// <summary>The snapshot the answer was applied to, so a re-run can tell what it already folded in.</summary>
    public Guid? UserProfileSnapshotId { get; set; }
}

/// <summary>A recorded cycle, the anchor the phase projection counts from.</summary>
public sealed class CycleLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>First day of the cycle, in her local calendar.</summary>
    public DateOnly StartDate { get; set; }
    /// <summary>Last day; null while the cycle is still running.</summary>
    public DateOnly? EndDate { get; set; }
    /// <summary>Days the cycle lasted, which the next prediction counts from.</summary>
    public int CycleLength { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>One day's logged phase, which overrides the projection for that day.</summary>
public sealed class CyclePhaseLogRecord : ITenantRecord
{
    private DateOnly _localDate;
    private DateTime _loggedAt;

    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The cycle it belongs to; null for a log made before any cycle was recorded.</summary>
    public Guid? CycleLogId { get; set; }
    /// <summary>The phase recorded for that day, stored as text.</summary>
    public string Phase { get; set; } = string.Empty;
    /// <summary>The flow she recorded; null when she recorded none.</summary>
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
    /// <summary>Her own note for the day.</summary>
    public string? Note { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>One symptom on one day of a cycle.</summary>
public sealed class SymptomLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The cycle the symptom belongs to; a symptom row always has one.</summary>
    public Guid CycleLogId { get; set; }
    /// <summary>The day it was felt.</summary>
    public DateOnly Date { get; set; }
    /// <summary>Which symptom, stored as text.</summary>
    public string SymptomType { get; set; } = string.Empty;
    /// <summary>How strong it was on the app's fixed scale.</summary>
    public int Severity { get; set; }
    /// <summary>Her own note.</summary>
    public string? Notes { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>One day's readiness: what she reported and what the planner derived from it.</summary>
public sealed class DailyReadinessLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The local day the answers are about.</summary>
    public DateOnly Date { get; set; }
    /// <summary>Hours slept, as reported.</summary>
    public decimal? SleepHours { get; set; }
    /// <summary>Three-day average at the time of logging, kept because it cannot be recomputed after later edits.</summary>
    public decimal? Sleep3dAvg { get; set; }
    /// <summary>Steps the day before, from the device.</summary>
    public int? StepsYesterday { get; set; }
    /// <summary>Seven-day step average at the time of logging.</summary>
    public int? Steps7dAvg { get; set; }
    /// <summary>Self-reported energy, on the app's fixed scale.</summary>
    public int? Energy { get; set; }
    /// <summary>Self-reported pain; any pain is what withholds a strong session.</summary>
    public int? Pain { get; set; }
    /// <summary>Whether she reported bloating.</summary>
    public bool? Bloating { get; set; }
    /// <summary>Body weight that day in kilograms, which the trend is computed from.</summary>
    public decimal? WeightKg { get; set; }
    /// <summary>Sleep quality she reported, on the app's fixed scale.</summary>
    public int? SleepQuality { get; set; }
    /// <summary>Soreness she reported, on the app's fixed scale.</summary>
    public int? Soreness { get; set; }
    /// <summary>The sleep quality the plan actually used, kept separate from what she edited later.</summary>
    public int? PlanningSleepQuality { get; set; }
    /// <summary>The fatigue value the plan used.</summary>
    public int? PlanningFatigue { get; set; }
    /// <summary>The soreness value the plan used.</summary>
    public int? PlanningSoreness { get; set; }
    /// <summary>The stress value the plan used.</summary>
    public int? PlanningStress { get; set; }
    /// <summary>The zone the planning values were computed in.</summary>
    public string? PlanningTimeZoneId { get; set; }
    /// <summary>When the planning values were captured.</summary>
    public DateTime? PlanningRecordedAtUtc { get; set; }
    /// <summary>Bumped whenever the planning values change, so a plan built on an older revision is detectable.</summary>
    public long PlanningRevision { get; set; }
    /// <summary>The load the previous day imposed, which the readiness score weighs.</summary>
    public decimal? PreviousDayLoad { get; set; }
    /// <summary>The derived score the tier is chosen from.</summary>
    public int ReadinessScore { get; set; }
    /// <summary>The tier the plan acts on.</summary>
    public ReadinessTier ReadinessTier { get; set; }
    /// <summary>The cycle phase on that day, frozen with the answers.</summary>
    public string Phase { get; set; } = string.Empty;
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>One reported injury and whether it is still restricting planning.</summary>
public sealed class WorkoutInjuryLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The body site as a flag, which is what the planning rules match against.</summary>
    public int SiteFlag { get; set; }
    /// <summary>Whether the injury is active, clearing or cleared; only an active injury restricts a plan.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>The date it started, in her local calendar.</summary>
    public DateOnly Since { get; set; }
    /// <summary>The pain level reported, which the review thresholds read.</summary>
    public int Pain { get; set; }

    /// <summary>Closed-set answer to which movements worsen the injury; stored by enum name.</summary>
    public string WorseningTrigger { get; set; } =
        nameof(MuscleCuties.Core.Domain.Workout.InjuryWorseningTrigger.NotAnswered);

    /// <summary>Null when unanswered, so an unanswered question is never read as "no restriction".</summary>
    public bool? ClinicianAdvisedAvoid { get; set; }

    /// <summary>The date of the report.</summary>
    public DateOnly Date { get; set; }
    /// <summary>The date it was cleared; null while active.</summary>
    public DateOnly? ClearedDate { get; set; }
    /// <summary>When the report was written.</summary>
    public DateTime? ReportedAtUtc { get; set; }
    /// <summary>When the clearance was recorded.</summary>
    public DateTime? ClearedAtUtc { get; set; }
    /// <summary>When the row was created.</summary>
    public DateTime CreatedAt { get; set; }
}

// An episode keeps its identity across clearance and survives deletion of its source log.
/// <summary>The link between a suppressed exercise row and the injury episode that suppressed it.</summary>
public sealed class WorkoutInjuryPrescriptionLinkRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The exercise row that was suppressed or replaced.</summary>
    public Guid WorkoutDayExerciseId { get; set; }
    /// <summary>The episode responsible; restoring the episode restores every row it links.</summary>
    public Guid InjuryEpisodeId { get; set; }
    /// <summary>The exercise as prescribed, copied because the row may later be deleted.</summary>
    public int OriginalExerciseId { get; set; }
}

/// <summary>The plan in force for an account. Only one row is active, and replacing it is the one write that changes what she trains.</summary>
public sealed class WorkoutPlanRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The plan's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Whether this is the plan in force; at most one row per account is active.</summary>
    public bool IsActive { get; set; }
    /// <summary>The phase the plan was built for, or null when it is phase-agnostic.</summary>
    public string? CyclePhaseTarget { get; set; }
    /// <summary>When the plan was created.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>Immutable v0.2 proposal and input snapshot; acceptance state changes only inside the API.</summary>
public sealed class WorkoutPlanProposalV02Record : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The plan the proposal would replace.</summary>
    public Guid PlanId { get; set; }
    /// <summary>The revision the proposal was built on; applying it against a newer revision is refused.</summary>
    public Guid ExpectedRevisionId { get; set; }
    /// <summary>The rule set that produced it, so an outdated proposal can be rejected after a rules change.</summary>
    public string RulesetVersion { get; set; } = string.Empty;
    /// <summary>The training policy version it assumes.</summary>
    public string TrainingPolicyVersion { get; set; } = string.Empty;
    /// <summary>Where the proposal stands: pending, accepted, rejected or superseded.</summary>
    public string Status { get; set; } = string.Empty;
    /// <summary>The inputs it was generated from, frozen.</summary>
    public string InputSnapshotJson { get; set; } = "{}";
    /// <summary>The proposed plan itself, as JSON.</summary>
    public string ProposalJson { get; set; } = "{}";
    /// <summary>Why the planner changed what it changed, as JSON.</summary>
    public string ReasonsJson { get; set; } = "[]";
    /// <summary>When it was generated.</summary>
    public DateTime CreatedAtUtc { get; set; }
    /// <summary>When it was accepted or rejected.</summary>
    public DateTime? DecidedAtUtc { get; set; }
    /// <summary>The revision acceptance produced, so a replayed decision finds it.</summary>
    public Guid? AcceptedRevisionId { get; set; }
    /// <summary>Bumped on each decision, so two devices cannot both decide.</summary>
    public int ConcurrencyVersion { get; set; } = 1;
    /// <summary>The holder of the planning lease, which is what serialises generation per account.</summary>
    public Guid? LeaseId { get; set; }
    /// <summary>Fencing token that invalidates a lease holder whose lease expired.</summary>
    public long LeaseFence { get; set; }
    /// <summary>When the lease lapses and another holder may take over.</summary>
    public DateTime? LeaseExpiresAtUtc { get; set; }
}

/// <summary>Append-only accepted v0.2 plan revision; prior revisions remain queryable.</summary>
public sealed class WorkoutPlanRevisionV02Record : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The plan this revision belongs to.</summary>
    public Guid PlanId { get; set; }
    /// <summary>Monotonic within the plan, which is how "newer" is decided.</summary>
    public long RevisionNumber { get; set; }
    /// <summary>The proposal that produced it, or null for an initial revision.</summary>
    public Guid SourceProposalId { get; set; }
    /// <summary>The rule set in force when it was applied.</summary>
    public string RulesetVersion { get; set; } = string.Empty;
    /// <summary>The training policy in force when it was applied.</summary>
    public string TrainingPolicyVersion { get; set; } = string.Empty;
    /// <summary>The plan as applied, immutable.</summary>
    public string PlanJson { get; set; } = "{}";
    /// <summary>The inputs it was applied against, frozen.</summary>
    public string InputSnapshotJson { get; set; } = "{}";
    /// <summary>When it was applied.</summary>
    public DateTime AcceptedAtUtc { get; set; }
}

/// <summary>One day of a plan, with the flags that record why it differs from the generated version.</summary>
public sealed class WorkoutDayRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The plan the day belongs to.</summary>
    public Guid WorkoutPlanId { get; set; }
    /// <summary>The weekday it is scheduled for; unused for optional days.</summary>
    public int DayOfWeek { get; set; }
    /// <summary>Strength or an activity type, which decides which prescription fields apply.</summary>
    public string WorkoutType { get; set; } = string.Empty;
    /// <summary>The day's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Which week of the plan it falls in.</summary>
    public int WeekIndex { get; set; }
    /// <summary>The date it is scheduled on, or null for a day without a fixed date.</summary>
    public DateOnly? Date { get; set; }
    /// <summary>Whether the day is a bonus session that the weekly count does not require.</summary>
    public bool IsOptional { get; set; }
    /// <summary>Whether the day was deliberately made easier rather than being the generated session.</summary>
    public bool IsEasierAlternative { get; set; }
    /// <summary>Whether readiness removed it; such a day is not treated as missed.</summary>
    public bool IsRemovedByReadiness { get; set; }
    /// <summary>Whether she removed it herself.</summary>
    public bool IsRemovedByUserAdjustment { get; set; }
    /// <summary>Whether an injury or review removed it, which is why a reason is stored alongside.</summary>
    public bool IsRemovedBySafety { get; set; }
    /// <summary>The injury that removed it, so the reason can be shown and revisited.</summary>
    public Guid? SafetyInjuryId { get; set; }
    /// <summary>The activity type a safety removal applied to, for non-strength days.</summary>
    public int? SafetyActivityTypeId { get; set; }
    /// <summary>The activity type for an activity day.</summary>
    public int? ActivityTypeId { get; set; }
    /// <summary>Position within its week, which keeps the display order stable.</summary>
    public int? DayOrder { get; set; }
    /// <summary>Planned duration in minutes, which the time budget was fitted to.</summary>
    public decimal? PlannedMainDurationMinutes { get; set; }
    /// <summary>The activity day's prescription as JSON.</summary>
    public string? PlannedActivityPrescriptionJson { get; set; }
}

/// <summary>One prescribed exercise of a day, carrying the injury provenance that lets a suppressed slot be restored.</summary>
public sealed class WorkoutDayExerciseRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The day it belongs to.</summary>
    public Guid WorkoutDayId { get; set; }
    /// <summary>The exercise prescribed.</summary>
    public int ExerciseId { get; set; }
    /// <summary>The template the slot came from, kept so a regeneration can match slots.</summary>
    public string? PlanningTemplateId { get; set; }
    /// <summary>The template version used, so a template change is detectable.</summary>
    public string? PlanningTemplateVersion { get; set; }
    /// <summary>The slot it fills, which is what a substitute is placed into.</summary>
    public string? PlanningSlotId { get; set; }
    /// <summary>Which session of the day it belongs to when a day holds more than one.</summary>
    public int? PlanningSessionIndex { get; set; }
    // Null for legacy or non-injury prescriptions; never infer provenance from a replacement.
    /// <summary>The exercise it replaced for an injury, or null when it is the original.</summary>
    public int? InjuryOriginalExerciseId { get; set; }
    /// <summary>The date from which restoration is due, so a cleared injury restores the slot.</summary>
    public DateOnly? InjuryRestorationRequiredFromDate { get; set; }
    /// <summary>For a derived row, the row it came from.</summary>
    public Guid? OriginWorkoutDayExerciseId { get; set; }
    /// <summary>Number of working sets the prescription asks her to complete.</summary>
    public int Sets { get; set; }
    /// <summary>Sets that may be skipped without the session counting as incomplete.</summary>
    public int OptionalSets { get; set; }
    /// <summary>Reps per set prescribed.</summary>
    public int Reps { get; set; }
    /// <summary>Effort target in reps in reserve; null when the work is time-based.</summary>
    public int? TargetRepetitionsInReserve { get; set; }
    /// <summary>Duration for time-based work, in seconds.</summary>
    public int? DurationSeconds { get; set; }
    /// <summary>Whether the exercise itself is optional.</summary>
    public bool IsOptional { get; set; }
    /// <summary>Whether readiness removed it, so the day does not count it as missed.</summary>
    public bool IsRemovedByReadiness { get; set; }
}

/// <summary>A session that happened, planned or not, with the prescription it was performed against.</summary>
public sealed class WorkoutSessionLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The planned day it fulfils; null for an unplanned session.</summary>
    public Guid? PlannedWorkoutDayId { get; set; }
    /// <summary>The activity type for a session that was not planned.</summary>
    public int? UnplannedActivityType { get; set; }
    /// <summary>The local day an unplanned session was completed on.</summary>
    public DateOnly? UnplannedCompletionLocalDate { get; set; }
    /// <summary>The zone that date was recorded in.</summary>
    public string? UnplannedCompletionTimeZoneId { get; set; }
    /// <summary>The tz version used to resolve that zone.</summary>
    public long? UnplannedCompletionTimeZoneVersion { get; set; }
    /// <summary>The zone the planned day's date was interpreted in.</summary>
    public string? PlannedTimeZoneId { get; set; }
    /// <summary>The tz version used for the planned date.</summary>
    public long? PlannedTimeZoneVersion { get; set; }
    /// <summary>The planned local day, frozen so a later zone change does not move the session.</summary>
    public DateOnly? PlannedLocalDate { get; set; }
    /// <summary>Seconds of activity she confirmed, which is what earns the day's credit.</summary>
    public int ConfirmedActiveSeconds { get; set; }
    /// <summary>The timer interval this session is being recorded under; null when it was logged after the fact.</summary>
    public Guid? TimingIntervalId { get; set; }
    /// <summary>When that interval started.</summary>
    public DateTime? TimingIntervalStartedAtUtc { get; set; }
    /// <summary>The last confirmation the client sent, which bounds the credited time.</summary>
    public DateTime? TimingIntervalLastConfirmedAtUtc { get; set; }
    /// <summary>Seconds confirmed so far, kept separately so a lost client cannot inflate the total.</summary>
    public int TimingIntervalConfirmedSeconds { get; set; }
    /// <summary>The prescription as planned, frozen so the session is judged against what was prescribed.</summary>
    public string? PlannedPrescriptionSnapshotJson { get; set; }
    /// <summary>The session's display name.</summary>
    public string WorkoutName { get; set; } = string.Empty;
    /// <summary>When the session started.</summary>
    public DateTime StartedAt { get; set; }
    /// <summary>When it was finished; null while in progress.</summary>
    public DateTime? CompletedAt { get; set; }
    /// <summary>Elapsed seconds, which may exceed the active seconds.</summary>
    public int TotalDurationSeconds { get; set; }
    /// <summary>Total volume in kilograms, summed over the working sets.</summary>
    public decimal TotalVolumeKg { get; set; }
    /// <summary>Her notes about the session.</summary>
    public string? Notes { get; set; }
    /// <summary>Session effort, on the app's fixed scale.</summary>
    public int? RpeRating { get; set; }
    /// <summary>Distance for a distance-based session, in kilometres.</summary>
    public decimal? DistanceKm { get; set; }
    /// <summary>Whether the row is hidden without being erased.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>When it was soft-deleted; rows are kept so history stays readable.</summary>
    public DateTime? DeletedAt { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When it last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>One set performed, the smallest unit progress is judged on.</summary>
public sealed class ExerciseSetLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The session the set belongs to.</summary>
    public Guid WorkoutSessionLogId { get; set; }
    /// <summary>The exercise performed.</summary>
    public string? ExerciseId { get; set; }
    /// <summary>The name as it was then, copied so history survives a catalog rename.</summary>
    public string ExerciseName { get; set; } = string.Empty;
    /// <summary>Position in the exercise's sets, which is how a set is matched to its prescription.</summary>
    public int SetNumber { get; set; }
    /// <summary>Load in kilograms; null for bodyweight work.</summary>
    public decimal WeightKg { get; set; }
    /// <summary>Reps she actually completed, against the prescription.</summary>
    public int RepsCompleted { get; set; }
    /// <summary>Effort reported, on the app's fixed scale.</summary>
    public decimal? Rpe { get; set; }
    /// <summary>Reps in reserve she reported, which is what progression is judged on.</summary>
    public int? RirActual { get; set; }
    /// <summary>Whether the technique was judged acceptable; a false value blocks a load increase.</summary>
    public bool TechniqueAcceptable { get; set; } = true;
    /// <summary>Whether the set was actually seen by the app rather than entered after the fact.</summary>
    public bool IsObserved { get; set; }
    /// <summary>Whether it was a warm-up set, which never counts as volume.</summary>
    public bool IsWarmup { get; set; }
    /// <summary>Reps on the left for unilateral work.</summary>
    public int? LeftSideReps { get; set; }
    /// <summary>Reps on the right for unilateral work.</summary>
    public int? RightSideReps { get; set; }
    /// <summary>Rest taken after the set, in seconds.</summary>
    public int RestTimeSeconds { get; set; }
    /// <summary>Whether the row is hidden without being erased.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>When it was soft-deleted; rows are kept so history stays readable.</summary>
    public DateTime? DeletedAt { get; set; }
    /// <summary>When the set was recorded.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When it last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>The legacy per-workout log row, kept readable beside the session logs.</summary>
public sealed class WorkoutLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The planned day it logged.</summary>
    public Guid WorkoutDayId { get; set; }
    /// <summary>The session it was logged against, when one exists.</summary>
    public Guid? PlannedSessionId { get; set; }
    /// <summary>The local day it was logged for.</summary>
    public DateOnly Date { get; set; }
    /// <summary>How much of the day was completed, kept because the exercises may since have changed.</summary>
    public int CompletionPercent { get; set; }
    /// <summary>Notes she added about the session.</summary>
    public string? Notes { get; set; }
    /// <summary>When the log was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>A legacy per-exercise log row belonging to a workout log.</summary>
public sealed class WorkoutExerciseLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The workout log it belongs to.</summary>
    public Guid WorkoutLogId { get; set; }
    /// <summary>The prescribed exercise it fulfilled.</summary>
    public Guid WorkoutDayExerciseId { get; set; }
    /// <summary>The exercise performed.</summary>
    public int ExerciseId { get; set; }
    /// <summary>Sets she completed out of the prescription.</summary>
    public int CompletedSets { get; set; }
    /// <summary>Reps completed in total.</summary>
    public int CompletedReps { get; set; }
    /// <summary>Load in kilograms.</summary>
    public decimal? WeightKg { get; set; }
    /// <summary>Duration for time-based work.</summary>
    public int? CompletedDurationSeconds { get; set; }
    /// <summary>Distance covered in kilometres.</summary>
    public decimal? DistanceKm { get; set; }
    /// <summary>Average heart rate, from the device or a manual entry.</summary>
    public int? AverageHeartRateBpm { get; set; }
    /// <summary>Pace in seconds per kilometre.</summary>
    public int? PaceSecondsPerKm { get; set; }
    /// <summary>Power output in watts.</summary>
    public int? PowerWatts { get; set; }
    /// <summary>Cadence in revolutions per minute.</summary>
    public int? CadenceRpm { get; set; }
    /// <summary>Effort on the app's fixed scale.</summary>
    public int? EffortRating { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>An account's swap: the exercise she prefers in place of another.</summary>
public sealed class UserExercisePreferenceRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The exercise she replaced.</summary>
    public int OriginalExerciseId { get; set; }
    /// <summary>The exercise she prefers instead; the planner substitutes it where the pattern allows.</summary>
    public int PreferredExerciseId { get; set; }
    /// <summary>When the preference was set.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>Her own note about a movement, shown beside the prescription.</summary>
public sealed class UserMovementNoteRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The movement the note is about.</summary>
    public int ExerciseId { get; set; }
    /// <summary>The name at the time of writing, so the note reads correctly after a rename.</summary>
    public string ExerciseName { get; set; } = string.Empty;
    /// <summary>The note itself.</summary>
    public string Notes { get; set; } = string.Empty;
    /// <summary>When it was written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When it last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>One day of nutrition: the targets frozen for that day and the totals rolled up from its entries.</summary>
public sealed class DailyNutritionLogRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The local day the row covers; one row per account and day.</summary>
    public DateOnly LocalDate { get; set; }
    /// <summary>Water drunk, in millilitres.</summary>
    public int WaterIntakeMl { get; set; }
    /// <summary>Calorie target frozen for the day, so a later target change does not rewrite history.</summary>
    public decimal TargetCalories { get; set; }
    /// <summary>Protein target for the day, in grams.</summary>
    public decimal TargetProtein { get; set; }
    /// <summary>Carbohydrate target for the day, in grams.</summary>
    public decimal TargetCarbs { get; set; }
    /// <summary>Fat target for the day, in grams.</summary>
    public decimal TargetFat { get; set; }
    /// <summary>Calories rolled up from the day's entries.</summary>
    public decimal TotalCalories { get; set; }
    /// <summary>Protein rolled up, in grams.</summary>
    public decimal TotalProtein { get; set; }
    /// <summary>Carbohydrate rolled up, in grams.</summary>
    public decimal TotalCarbs { get; set; }
    /// <summary>Fat rolled up, in grams.</summary>
    public decimal TotalFat { get; set; }
    /// <summary>Her notes for the day.</summary>
    public string? Notes { get; set; }
    /// <summary>Whether the row is hidden without being erased.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>When it was soft-deleted; rows are kept so history stays readable.</summary>
    public DateTime? DeletedAt { get; set; }
    /// <summary>When the day was first written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When it last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>One food logged on a day, with the nutrients copied at logging time.</summary>
public sealed class FoodItemEntryRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The day it belongs to.</summary>
    public Guid DailyLogId { get; set; }
    /// <summary>The local day, repeated so a day can be queried without the parent row.</summary>
    public DateOnly LocalDate { get; set; }
    /// <summary>Which meal it was eaten as, stored as text.</summary>
    public string MealType { get; set; } = string.Empty;
    /// <summary>The food's name as logged, copied so the entry survives a catalog change.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Size of one serving, in the unit stored beside it.</summary>
    public decimal ServingSize { get; set; }
    /// <summary>The unit that serving size is expressed in.</summary>
    public string ServingUnit { get; set; } = string.Empty;
    /// <summary>How many servings were eaten; the nutrients below are the product of this and one serving.</summary>
    public decimal ServingsCount { get; set; }
    /// <summary>Calories for the amount eaten, copied at logging time.</summary>
    public decimal Calories { get; set; }
    /// <summary>Protein for the amount eaten, in grams.</summary>
    public decimal Protein { get; set; }
    /// <summary>Carbohydrate for the amount eaten, in grams.</summary>
    public decimal Carbs { get; set; }
    /// <summary>Fat for the amount eaten, in grams.</summary>
    public decimal Fat { get; set; }
    /// <summary>When it was logged, which orders the day's entries.</summary>
    public DateTime LoggedAtTimestamp { get; set; }
    /// <summary>Whether the row is hidden without being erased.</summary>
    public bool IsDeleted { get; set; }
    /// <summary>When it was soft-deleted; rows are kept so history stays readable.</summary>
    public DateTime? DeletedAt { get; set; }
    /// <summary>When the entry was written.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When it last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>A food the account created; it never enters the shared catalog.</summary>
public sealed class UserFoodItemRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The food's display name.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Energy per 100 g.</summary>
    public decimal? Calories { get; set; }
    /// <summary>Protein per 100 g.</summary>
    public decimal? Protein { get; set; }
    /// <summary>Carbohydrate per 100 g.</summary>
    public decimal? Carbs { get; set; }
    /// <summary>Fat per 100 g.</summary>
    public decimal? Fats { get; set; }
    /// <summary>Fibre per 100 g.</summary>
    public decimal? Fiber { get; set; }
    /// <summary>Iron per 100 g.</summary>
    public decimal? Iron { get; set; }
    /// <summary>Vitamin B12 per 100 g.</summary>
    public decimal? VitaminB12 { get; set; }
    /// <summary>Vitamin C per 100 g.</summary>
    public decimal? VitaminC { get; set; }
    /// <summary>Vitamin D per 100 g.</summary>
    public decimal? VitaminD { get; set; }
    /// <summary>Vitamin A per 100 g.</summary>
    public decimal? VitaminA { get; set; }
    /// <summary>Vitamin B6 per 100 g.</summary>
    public decimal? VitaminB6 { get; set; }
    /// <summary>Folate per 100 g.</summary>
    public decimal? Folate { get; set; }
    /// <summary>Calcium per 100 g.</summary>
    public decimal? Calcium { get; set; }
    /// <summary>Magnesium per 100 g.</summary>
    public decimal? Magnesium { get; set; }
    /// <summary>Zinc per 100 g.</summary>
    public decimal? Zinc { get; set; }
    /// <summary>Potassium per 100 g.</summary>
    public decimal? Potassium { get; set; }
    /// <summary>The source record id when the food came from a lookup; null for one typed by hand.</summary>
    public int? FdcId { get; set; }
    /// <summary>The source's record kind, copied with the lookup.</summary>
    public string? DataType { get; set; }
    /// <summary>The brand owner, when the lookup had one.</summary>
    public string? BrandOwner { get; set; }
    /// <summary>The brand name, when the lookup had one.</summary>
    public string? BrandName { get; set; }
    /// <summary>The barcode, when the lookup had one.</summary>
    public string? GtinUpc { get; set; }
    /// <summary>The ingredient text, when the lookup had one.</summary>
    public string? Ingredients { get; set; }
    /// <summary>The stated serving amount.</summary>
    public decimal? ServingSize { get; set; }
    /// <summary>The unit that amount is in.</summary>
    public string? ServingSizeUnit { get; set; }
    /// <summary>The serving options as JSON, so the diary can offer measures.</summary>
    public string? ServingOptionsJson { get; set; }
    /// <summary>When a lookup last refreshed the row; null for a hand-typed food.</summary>
    public DateTime? LastSyncedAt { get; set; }
    /// <summary>When she created it.</summary>
    public DateTime CreatedAt { get; set; }
    /// <summary>When it last changed.</summary>
    public DateTime UpdatedAt { get; set; }
}

/// <summary>One of the account's own meal templates.</summary>
public sealed class UserMealTemplateRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>Name shown when she picks this template.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Optional description shown under the template name.</summary>
    public string? Description { get; set; }
    /// <summary>Which meal it is meant to be logged as, stored as text.</summary>
    public string MealType { get; set; } = string.Empty;
    /// <summary>Dietary tags stored comma-separated for filtering.</summary>
    public string DietaryTags { get; set; } = string.Empty;
    /// <summary>Comma-separated cycle phases it suits.</summary>
    public string PhaseTags { get; set; } = string.Empty;
    /// <summary>Position in her template list.</summary>
    public int SortOrder { get; set; }
    /// <summary>When she saved it.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>A food inside one of her meal templates, pointing at either a catalog or her own food.</summary>
public sealed class UserMealTemplateEntryRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The template the food belongs to.</summary>
    public Guid MealTemplateId { get; set; }
    /// <summary>The shared catalog food used; null when her own food is used instead.</summary>
    public int? CatalogFoodItemId { get; set; }
    /// <summary>Her own food used; null when a catalog food is used instead.</summary>
    public Guid? UserFoodItemId { get; set; }
    /// <summary>Weight of the food in the template, which the nutrients are scaled from.</summary>
    public decimal Grams { get; set; }
}

/// <summary>A meal logged on a day, the parent of its entries.</summary>
public sealed class LoggedMealRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The local day the meal was logged on.</summary>
    public DateOnly Date { get; set; }
    /// <summary>When it was logged, which orders the day's meals.</summary>
    public DateTime LoggedAt { get; set; }
    /// <summary>Which meal of the day it is, stored as text.</summary>
    public string MealType { get; set; } = string.Empty;
    /// <summary>The suggestion she accepted, kept so a suggested meal can be told from one she built.</summary>
    public string? AcceptedSuggestionName { get; set; }
    /// <summary>The phase the suggestion was made for, so the plan learns what worked per phase.</summary>
    public string? AcceptedSuggestionPhase { get; set; }
    /// <summary>When the row was written.</summary>
    public DateTime CreatedAt { get; set; }
}

/// <summary>A food inside a logged meal, pointing at either a catalog or her own food.</summary>
public sealed class LoggedMealEntryRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The meal the food belongs to.</summary>
    public Guid LoggedMealId { get; set; }
    /// <summary>The shared catalog food used; null when her own food is used.</summary>
    public int? CatalogFoodItemId { get; set; }
    /// <summary>Her own food used; null when a catalog food is used.</summary>
    public Guid? UserFoodItemId { get; set; }
    /// <summary>Weight eaten, which every nutrient is scaled from.</summary>
    public decimal Grams { get; set; }
}

/// <summary>A saved recipe, stored as a JSON snapshot so its shape can grow without a migration.</summary>
public sealed class RecipeRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The name she gave the recipe.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>The recipe as JSON, so a recipe can gain fields without a migration.</summary>
    public string SnapshotJson { get; set; } = string.Empty;
    /// <summary>When it was saved.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A saved meal template she can log again with one tap.</summary>
public sealed class SavedMealTemplateRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The name she gave the template.</summary>
    public string Name { get; set; } = string.Empty;
    /// <summary>Which meal it is meant to be logged as.</summary>
    public string MealType { get; set; } = string.Empty;
    /// <summary>The template's contents as JSON.</summary>
    public string SnapshotJson { get; set; } = string.Empty;
    /// <summary>When it was saved.</summary>
    public DateTime CreatedAtUtc { get; set; }
}

/// <summary>A progress photo by date, holding the image path rather than the image.</summary>
public sealed class PhotoJourneyEntryRecord : ITenantRecord
{
    /// <summary>Primary key of the row.</summary>
    public Guid Id { get; set; }
    /// <summary>The owning account; every read is filtered by it.</summary>
    public Guid UserId { get; set; }
    /// <summary>The local day the photo is filed under.</summary>
    public DateOnly Date { get; set; }
    /// <summary>Where the image is stored; the image itself never lives in the database.</summary>
    public string ImagePath { get; set; } = string.Empty;
    /// <summary>Weight recorded with the photo, in kilograms.</summary>
    public decimal? WeightKg { get; set; }
    /// <summary>Her notes about the photo.</summary>
    public string Notes { get; set; } = string.Empty;
    /// <summary>The cycle phase when it was taken, frozen.</summary>
    public string Phase { get; set; } = string.Empty;
    /// <summary>When the entry was written.</summary>
    public DateTime CreatedAt { get; set; }
}

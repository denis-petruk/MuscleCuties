using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

internal static class AppModelConfiguration
{
    private static readonly Regex WordBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);

    public static void ConfigureTenant<TEntity>(EntityTypeBuilder<TEntity> builder, string table)
        where TEntity : class, ITenantRecord
    {
        builder.ToTable(table, "app");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetColumnName(ToSnakeCase(property.Name));
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                property.SetColumnType("numeric");
            if (property.Name.EndsWith("Json", StringComparison.Ordinal))
            {
                property.SetColumnType("jsonb");
                property.SetValueConverter(JsonTextValueConverter.Instance);
            }
        }

        builder.HasAlternateKey(x => new { x.UserId, x.Id }).HasName($"uq_{table}_user_id_id");
        builder.HasIndex(x => x.UserId).HasDatabaseName($"ix_{table}_user_id");
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(x => x.UserId)
            .OnDelete(DeleteBehavior.Cascade);
    }

    public static void ConfigureGlobal<TEntity>(EntityTypeBuilder<TEntity> builder, string table)
        where TEntity : class
    {
        builder.ToTable(table, "app");
        foreach (var property in builder.Metadata.GetProperties())
        {
            property.SetColumnName(ToSnakeCase(property.Name));
            if (property.ClrType == typeof(decimal) || property.ClrType == typeof(decimal?))
                property.SetColumnType("numeric");
            if (property.Name.EndsWith("Json", StringComparison.Ordinal))
            {
                property.SetColumnType("jsonb");
                property.SetValueConverter(JsonTextValueConverter.Instance);
            }
        }
    }

    public static string ToSnakeCase(string value) => WordBoundary.Replace(value, "$1_$2").ToLowerInvariant();

}

public sealed class UserConfiguration : IEntityTypeConfiguration<UserRecord>
{
    public void Configure(EntityTypeBuilder<UserRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "users");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.Email).HasColumnName("email").UseCollation("C").IsRequired();
        builder.Property(x => x.NormalizedEmail)
            .HasColumnName("normalized_email")
            .HasComputedColumnSql("lower(btrim(email))", stored: true);
        builder.Property(x => x.PasswordHash).HasColumnName("password_hash");
        builder.Property(x => x.CredentialType).HasColumnName("credential_type").IsRequired();
        builder.Property(x => x.PrivacyNoticeVersion).HasColumnName("privacy_notice_version").HasMaxLength(64);
        builder.Property(x => x.PrivacyNoticeAcceptedAtUtc).HasColumnName("privacy_notice_accepted_at_utc");
        builder.HasIndex(x => x.NormalizedEmail).IsUnique().HasDatabaseName("uq_users_normalized_email");
        builder.HasIndex(x => x.AppleUserId).IsUnique().HasFilter("apple_user_id IS NOT NULL").HasDatabaseName("uq_users_apple_user_id");
    }
}

public sealed class PhotoObjectReconcileTargetConfiguration : IEntityTypeConfiguration<PhotoObjectReconcileTargetRecord>
{
    public void Configure(EntityTypeBuilder<PhotoObjectReconcileTargetRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "photo_object_reconcile_targets");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.TargetKey).HasColumnName("target_key").HasMaxLength(512).IsRequired();
        builder.Property(x => x.NotBeforeUtc).HasColumnName("not_before_utc").IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.LeaseToken).HasColumnName("lease_token");
        builder.Property(x => x.LeaseGeneration).HasColumnName("lease_generation");
        builder.Property(x => x.LeaseExpiresAtUtc).HasColumnName("lease_expires_at_utc");
        builder.HasIndex(x => x.TargetKey).IsUnique().HasDatabaseName("uq_photo_object_reconcile_targets_key");
        builder.HasIndex(x => new { x.NotBeforeUtc, x.Id }).HasDatabaseName("ix_photo_object_reconcile_targets_due");
        builder.ToTable("photo_object_reconcile_targets", "app", table =>
            table.HasCheckConstraint("ck_photo_object_reconcile_targets_lease", "(lease_token IS NULL AND lease_expires_at_utc IS NULL) OR (lease_token IS NOT NULL AND lease_expires_at_utc IS NOT NULL)"));
    }
}

public sealed class EmailChallengeConfiguration : IEntityTypeConfiguration<EmailChallengeRecord>
{
    public void Configure(EntityTypeBuilder<EmailChallengeRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "email_challenges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(128).ValueGeneratedNever();
        builder.Property(x => x.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(x => x.RedeemedAtUtc).HasColumnName("redeemed_at_utc").HasColumnType("timestamp with time zone");
    }
}

public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSessionRecord>
{
    public void Configure(EntityTypeBuilder<RefreshSessionRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "refresh_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.FamilyId).HasColumnName("family_id").IsRequired();
        builder.Property(x => x.TokenHash).HasColumnName("token_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc");
        builder.Property(x => x.ReplacedAtUtc).HasColumnName("replaced_at_utc");
        builder.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("uq_refresh_sessions_token_hash");
        builder.HasIndex(x => new { x.FamilyId, x.RevokedAtUtc }).HasDatabaseName("ix_refresh_sessions_family_revoked");
    }
}

public sealed class AppleSignInNonceConfiguration : IEntityTypeConfiguration<AppleSignInNonceRecord>
{
    public void Configure(EntityTypeBuilder<AppleSignInNonceRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "apple_sign_in_nonces");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.NonceHash).HasColumnName("nonce_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.RedeemedAtUtc).HasColumnName("redeemed_at_utc");
        builder.HasIndex(x => x.NonceHash).IsUnique().HasDatabaseName("uq_apple_sign_in_nonces_hash");
    }
}

public sealed class RevokedAccessTokenConfiguration : IEntityTypeConfiguration<RevokedAccessTokenRecord>
{
    public void Configure(EntityTypeBuilder<RevokedAccessTokenRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "revoked_access_tokens");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id").IsRequired();
        builder.Property(x => x.TokenId).HasColumnName("token_id").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.RevokedAtUtc).HasColumnName("revoked_at_utc").IsRequired();
        builder.HasIndex(x => x.TokenId).IsUnique().HasDatabaseName("uq_revoked_access_tokens_token_id");
        builder.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("ix_revoked_access_tokens_expiry");
    }
}

public sealed class EmailChallengeSessionConfiguration : IEntityTypeConfiguration<EmailChallengeSessionRecord>
{
    public void Configure(EntityTypeBuilder<EmailChallengeSessionRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "email_challenge_sessions");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.NormalizedEmail).HasColumnName("normalized_email").HasMaxLength(320).IsRequired();
        builder.Property(x => x.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(x => x.HmacKeyId).HasColumnName("hmac_key_id").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CodeMac).HasColumnName("code_mac").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.ResendAvailableAtUtc).HasColumnName("resend_available_at_utc").IsRequired();
        builder.Property(x => x.ConsumedAtUtc).HasColumnName("consumed_at_utc");
        builder.Property(x => x.SupersededAtUtc).HasColumnName("superseded_at_utc");
        builder.Property(x => x.LockedAtUtc).HasColumnName("locked_at_utc");
        builder.Property(x => x.FailedAttempts).HasColumnName("failed_attempts").IsRequired();
        builder.Property(x => x.ReceiptId).HasColumnName("receipt_id").HasMaxLength(128);
        builder.Property(x => x.PrivacyNoticeVersion).HasColumnName("privacy_notice_version").HasMaxLength(64);
        builder.Property(x => x.PrivacyNoticeAcceptedAtUtc).HasColumnName("privacy_notice_accepted_at_utc");
        builder.HasIndex(x => x.ReceiptId).IsUnique().HasFilter("receipt_id IS NOT NULL")
            .HasDatabaseName("uq_email_challenge_sessions_receipt_id");
        builder.HasIndex(x => new { x.NormalizedEmail, x.CreatedAtUtc }).HasDatabaseName("ix_email_challenge_sessions_email_created");
        builder.ToTable("email_challenge_sessions", "app", table =>
        {
            table.HasCheckConstraint("ck_email_challenge_session_attempts", "failed_attempts >= 0");
            table.HasCheckConstraint("ck_email_challenge_session_expiry", "expires_at_utc > created_at_utc");
            table.HasCheckConstraint("ck_email_challenge_session_purpose", "purpose IN (1, 2)");
        });
    }
}

public sealed class SourceAccountClaimNonceConfiguration : IEntityTypeConfiguration<SourceAccountClaimNonceRecord>
{
    public void Configure(EntityTypeBuilder<SourceAccountClaimNonceRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "source_account_claim_nonces");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.DestinationUserId).HasColumnName("destination_user_id").IsRequired();
        builder.Property(x => x.SourceInstallationKeyHash).HasColumnName("source_installation_key_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceAccountKeyHash).HasColumnName("source_account_key_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.NonceHash).HasColumnName("nonce_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.RedeemedAtUtc).HasColumnName("redeemed_at_utc");
        builder.HasIndex(x => x.NonceHash).IsUnique().HasDatabaseName("uq_source_account_claim_nonces_hash");
        builder.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("ix_source_account_claim_nonces_expiry");
        builder.ToTable("source_account_claim_nonces", "app", table =>
            table.HasCheckConstraint("ck_source_account_claim_expiry", "expires_at_utc > created_at_utc"));
    }
}

public sealed class SourceAccountMappingConfiguration : IEntityTypeConfiguration<SourceAccountMappingRecord>
{
    public void Configure(EntityTypeBuilder<SourceAccountMappingRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "source_account_mappings");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.ClaimId).HasColumnName("claim_id").IsRequired();
        builder.Property(x => x.DestinationUserId).HasColumnName("destination_user_id").IsRequired();
        builder.Property(x => x.SourceInstallationKeyHash).HasColumnName("source_installation_key_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceAccountKeyHash).HasColumnName("source_account_key_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.HasIndex(x => new { x.SourceInstallationKeyHash, x.SourceAccountKeyHash }).IsUnique()
            .HasDatabaseName("uq_source_account_mappings_source");
        builder.HasIndex(x => x.ClaimId).IsUnique().HasDatabaseName("uq_source_account_mappings_claim_id");
        builder.HasIndex(x => x.DestinationUserId).HasDatabaseName("ix_source_account_mappings_destination_user_id");
    }
}

public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfileRecord>
{
    public void Configure(EntityTypeBuilder<UserProfileRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "user_profiles");
        builder.HasIndex(x => x.UserId).IsUnique().HasDatabaseName("uq_user_profiles_user_id");
        builder.Property(x => x.ConcurrencyVersion).HasDefaultValue(1).IsConcurrencyToken();
        builder.Property(x => x.AvailableWorkoutWeekdaysJson).HasColumnName("available_workout_weekdays_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.ActivitySessionDurationsJson).HasColumnName("activity_session_durations_json")
            .HasColumnType("jsonb").IsRequired();
        builder.Property(x => x.RequestedActivitySessionsPerWeekJson)
            .HasColumnName("requested_activity_sessions_per_week_json")
            .HasColumnType("jsonb").HasDefaultValueSql("'{}'::jsonb").IsRequired();
        builder.Property(x => x.ExcludedStrengthExerciseIdsJson)
            .HasColumnName("excluded_strength_exercise_ids_json")
            .HasColumnType("jsonb").HasDefaultValueSql("'[]'::jsonb").IsRequired();
        builder.ToTable("user_profiles", "app", table =>
        {
            table.HasCheckConstraint("ck_user_profiles_available_workout_weekdays_array",
                "jsonb_typeof(available_workout_weekdays_json) = 'array'");
            table.HasCheckConstraint("ck_user_profiles_activity_session_durations_object",
                "jsonb_typeof(activity_session_durations_json) = 'object'");
            table.HasCheckConstraint("ck_user_profiles_requested_activity_sessions_per_week_object",
                "jsonb_typeof(requested_activity_sessions_per_week_json) = 'object'");
            table.HasCheckConstraint("ck_user_profiles_excluded_strength_exercise_ids_array",
                "jsonb_typeof(excluded_strength_exercise_ids_json) = 'array'");
        });
    }
}

public sealed class CommandIdempotencyConfiguration : IEntityTypeConfiguration<CommandIdempotencyRecord>
{
    public void Configure(EntityTypeBuilder<CommandIdempotencyRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "command_idempotency");
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.CommandKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequestHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ResponseJson).HasColumnType("jsonb");
        builder.Property(x => x.CompletedAt).HasColumnType("timestamp with time zone");
        builder.HasIndex(x => new { x.UserId, x.CommandKey }).IsUnique().HasDatabaseName("uq_command_idempotency_user_key");
        builder.ToTable("command_idempotency", "app", table => table.HasCheckConstraint(
            "ck_command_idempotency_completion_pair",
            "(response_json IS NULL AND completed_at IS NULL) OR (response_json IS NOT NULL AND completed_at IS NOT NULL)"));
    }
}

public sealed class NutritionDiaryEventCursorConfiguration : IEntityTypeConfiguration<NutritionDiaryEventCursorRecord>
{
    public void Configure(EntityTypeBuilder<NutritionDiaryEventCursorRecord> builder)
    {
        builder.ToTable("nutrition_diary_event_cursors", "app");
        builder.HasKey(x => x.UserId);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.UserId).HasColumnName("user_id");
        builder.Property(x => x.LastCursor).HasColumnName("last_cursor");
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class NutritionDiaryCommandEventConfiguration : IEntityTypeConfiguration<NutritionDiaryCommandEventRecord>
{
    public void Configure(EntityTypeBuilder<NutritionDiaryCommandEventRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "nutrition_diary_command_events");
        builder.Property(x => x.Cursor).HasColumnName("cursor");
        builder.Property(x => x.DiaryDate).HasColumnName("diary_date");
        builder.Property(x => x.CommandId).HasColumnName("command_id").HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Cursor }).IsUnique().HasDatabaseName("uq_nutrition_diary_command_events_owner_cursor");
    }
}

public sealed class UserProfileSnapshotConfiguration : IEntityTypeConfiguration<UserProfileSnapshotRecord>
{
    public void Configure(EntityTypeBuilder<UserProfileSnapshotRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "user_profile_snapshots");
}

public sealed class UserQuizResponseConfiguration : IEntityTypeConfiguration<UserQuizResponseRecord>
{
    public void Configure(EntityTypeBuilder<UserQuizResponseRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "user_quiz_responses");
        builder.HasOne<QuizAnswer>().WithMany()
            .HasForeignKey(x => new { x.QuizAnswerId, x.QuizQuestionId })
            .HasPrincipalKey(x => new { x.Id, x.QuestionId }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserProfileSnapshotRecord>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.UserProfileSnapshotId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class CycleLogConfiguration : IEntityTypeConfiguration<CycleLogRecord>
{
    public void Configure(EntityTypeBuilder<CycleLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "cycle_logs");
        builder.HasIndex(x => new { x.UserId, x.StartDate }).IsDescending(false, true).HasDatabaseName("ix_cycle_logs_owner_start_date_desc");
    }
}

public sealed class CyclePhaseLogConfiguration : IEntityTypeConfiguration<CyclePhaseLogRecord>
{
    public void Configure(EntityTypeBuilder<CyclePhaseLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "cycle_phase_logs");
        builder.HasIndex(x => new { x.UserId, x.LocalDate }).IsUnique().HasDatabaseName("uq_cycle_phase_logs_owner_local_date");
        builder.HasIndex(x => new { x.UserId, x.LoggedAt }).IsDescending(false, true).HasDatabaseName("ix_cycle_phase_logs_owner_logged_at_desc");
        builder.HasOne<CycleLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.CycleLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class SymptomLogConfiguration : IEntityTypeConfiguration<SymptomLogRecord>
{
    public void Configure(EntityTypeBuilder<SymptomLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "symptom_logs");
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_symptom_logs_owner_date_desc");
        builder.HasOne<CycleLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.CycleLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DailyReadinessLogConfiguration : IEntityTypeConfiguration<DailyReadinessLogRecord>
{
    private const int MinimumPlanningScaleValue = 1;
    private const int MaximumPlanningScaleValue = 5;
    private const long InitialPlanningRevision = 0;
    private static readonly string PlanningScaleConstraint = $"(planning_sleep_quality IS NULL OR planning_sleep_quality BETWEEN {MinimumPlanningScaleValue} AND {MaximumPlanningScaleValue}) AND (planning_soreness IS NULL OR planning_soreness BETWEEN {MinimumPlanningScaleValue} AND {MaximumPlanningScaleValue}) AND (planning_stress IS NULL OR planning_stress BETWEEN {MinimumPlanningScaleValue} AND {MaximumPlanningScaleValue})";
    private static readonly string PlanningRevisionConstraint = $"planning_revision >= {InitialPlanningRevision}";

    public void Configure(EntityTypeBuilder<DailyReadinessLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "daily_readiness_logs");
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_daily_readiness_logs_owner_date_desc");
        builder.Property(x => x.ReadinessTier).HasConversion<string>();
        builder.Property(x => x.PlanningRevision).HasDefaultValue(InitialPlanningRevision);
        builder.ToTable("daily_readiness_logs", "app", table =>
        {
            table.HasCheckConstraint("ck_daily_readiness_logs_planning_scales", PlanningScaleConstraint);
            table.HasCheckConstraint("ck_daily_readiness_logs_planning_revision", PlanningRevisionConstraint);
        });
    }
}

public sealed class WorkoutInjuryLogConfiguration : IEntityTypeConfiguration<WorkoutInjuryLogRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutInjuryLogRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "workout_injury_logs");
}

public sealed class WorkoutInjuryEpisodeConfiguration : IEntityTypeConfiguration<WorkoutInjuryEpisodeRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutInjuryEpisodeRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_episodes");
        builder.HasIndex(x => new { x.UserId, x.SourceInjuryId, x.ClearedDate, x.SourceDeleted })
            .HasDatabaseName("ix_injury_episodes_source_state");
    }
}

public sealed class WorkoutInjuryPrescriptionLinkConfiguration : IEntityTypeConfiguration<WorkoutInjuryPrescriptionLinkRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutInjuryPrescriptionLinkRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_prescription_links");
        builder.HasIndex(x => new { x.UserId, x.WorkoutDayExerciseId, x.InjuryEpisodeId }).IsUnique()
            .HasDatabaseName("ux_injury_links_prescription_episode");
        builder.HasIndex(x => new { x.UserId, x.InjuryEpisodeId }).HasDatabaseName("ix_injury_links_episode");
        builder.HasOne<WorkoutDayExerciseRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutDayExerciseId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkoutInjuryEpisodeRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.InjuryEpisodeId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkoutPlanConfiguration : IEntityTypeConfiguration<WorkoutPlanRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "workout_plans");
}

public sealed class WorkoutPlanProposalV02Configuration : IEntityTypeConfiguration<WorkoutPlanProposalV02Record>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanProposalV02Record> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_plan_proposals_v02");
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.RulesetVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TrainingPolicyVersion).HasMaxLength(32).IsRequired();
        builder.Property(x => x.ConcurrencyVersion).IsConcurrencyToken().HasDefaultValue(1);
        builder.HasIndex(x => new { x.UserId, x.PlanId, x.CreatedAtUtc })
            .IsDescending(false, false, true).HasDatabaseName("ix_workout_plan_proposals_v02_owner_plan_created");
        builder.ToTable("workout_plan_proposals_v02", "app", table =>
        {
            table.HasCheckConstraint("ck_workout_plan_proposals_v02_status",
                "status IN ('Proposed', 'Accepted', 'Rejected', 'Expired')");
            table.HasCheckConstraint("ck_workout_plan_proposals_v02_decision_pair",
                "(status = 'Proposed' AND decided_at_utc IS NULL AND accepted_revision_id IS NULL) OR " +
                "(status = 'Rejected' AND decided_at_utc IS NOT NULL AND accepted_revision_id IS NULL) OR " +
                "(status = 'Accepted' AND decided_at_utc IS NOT NULL AND accepted_revision_id IS NOT NULL) OR " +
                "(status = 'Expired' AND decided_at_utc IS NOT NULL AND accepted_revision_id IS NULL)");
            table.HasCheckConstraint("ck_workout_plan_proposals_v02_concurrency", "concurrency_version > 0");
        });
    }
}

public sealed class WorkoutPlanRevisionV02Configuration : IEntityTypeConfiguration<WorkoutPlanRevisionV02Record>
{
    public void Configure(EntityTypeBuilder<WorkoutPlanRevisionV02Record> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_plan_revisions_v02");
        builder.Property(x => x.RulesetVersion).HasMaxLength(64).IsRequired();
        builder.Property(x => x.TrainingPolicyVersion).HasMaxLength(32).IsRequired();
        builder.HasAlternateKey(x => new { x.UserId, x.PlanId, x.RevisionNumber })
            .HasName("uq_workout_plan_revisions_v02_owner_plan_number");
        builder.HasIndex(x => new { x.UserId, x.PlanId, x.AcceptedAtUtc })
            .IsDescending(false, false, true).HasDatabaseName("ix_workout_plan_revisions_v02_owner_plan_accepted");
        builder.HasOne<WorkoutPlanProposalV02Record>().WithMany()
            .HasForeignKey(x => new { x.UserId, x.SourceProposalId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("workout_plan_revisions_v02", "app", table =>
            table.HasCheckConstraint("ck_workout_plan_revisions_v02_number", "revision_number > 0"));
    }
}

public sealed class WorkoutDayConfiguration : IEntityTypeConfiguration<WorkoutDayRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutDayRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_days");
        builder.Property(x => x.IsOptional).HasDefaultValue(false);
        builder.Property(x => x.IsEasierAlternative).HasDefaultValue(false);
        builder.Property(x => x.IsRemovedByReadiness).HasDefaultValue(false);
        builder.Property(x => x.IsRemovedByUserAdjustment).HasDefaultValue(false);
        builder.ToTable("workout_days", "app", table =>
            table.HasCheckConstraint("ck_workout_days_planned_main_duration_nonnegative",
                "planned_main_duration_minutes IS NULL OR planned_main_duration_minutes >= 0"));
        builder.HasOne<WorkoutPlanRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutPlanId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutDayExerciseConfiguration : IEntityTypeConfiguration<WorkoutDayExerciseRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutDayExerciseRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_day_exercises");
        builder.Property(x => x.OptionalSets).HasDefaultValue(0);
        builder.Property(x => x.IsOptional).HasDefaultValue(false);
        builder.Property(x => x.IsRemovedByReadiness).HasDefaultValue(false);
        builder.ToTable("workout_day_exercises", "app", table =>
            table.HasCheckConstraint("ck_workout_day_exercises_optional_sets",
                "optional_sets >= 0 AND (optional_sets = 0 OR optional_sets < sets)"));
        builder.HasOne<WorkoutDayRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutDayId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkoutSessionLogConfiguration : IEntityTypeConfiguration<WorkoutSessionLogRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutSessionLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_session_logs");
        builder.HasIndex(x => new { x.UserId, x.StartedAt }).IsDescending(false, true).HasDatabaseName("ix_workout_session_logs_owner_started_at_desc");
    }
}

public sealed class ExerciseSetLogConfiguration : IEntityTypeConfiguration<ExerciseSetLogRecord>
{
    public void Configure(EntityTypeBuilder<ExerciseSetLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "exercise_set_logs");
        builder.HasOne<WorkoutSessionLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutSessionLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class WorkoutLogConfiguration : IEntityTypeConfiguration<WorkoutLogRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_logs");
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_workout_logs_owner_date_desc");
        builder.HasOne<WorkoutDayRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutDayId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class WorkoutExerciseLogConfiguration : IEntityTypeConfiguration<WorkoutExerciseLogRecord>
{
    public void Configure(EntityTypeBuilder<WorkoutExerciseLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_exercise_logs");
        builder.HasOne<WorkoutLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<WorkoutDayExerciseRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutDayExerciseId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class UserExercisePreferenceConfiguration : IEntityTypeConfiguration<UserExercisePreferenceRecord>
{
    public void Configure(EntityTypeBuilder<UserExercisePreferenceRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "user_exercise_preferences");
        builder.HasOne<WorkoutExerciseDefinition>().WithMany().HasForeignKey(x => x.OriginalExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkoutExerciseDefinition>().WithMany().HasForeignKey(x => x.PreferredExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.OriginalExerciseId }).IsUnique().HasDatabaseName("uq_user_exercise_preferences_owner_original");
    }
}

public sealed class UserMovementNoteConfiguration : IEntityTypeConfiguration<UserMovementNoteRecord>
{
    public void Configure(EntityTypeBuilder<UserMovementNoteRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "user_movement_notes");
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class DailyNutritionLogConfiguration : IEntityTypeConfiguration<DailyNutritionLogRecord>
{
    public void Configure(EntityTypeBuilder<DailyNutritionLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "daily_nutrition_logs");
        builder.HasIndex(x => new { x.UserId, x.LocalDate }).IsDescending(false, true).HasDatabaseName("ix_daily_nutrition_logs_owner_local_date_desc");
    }
}

public sealed class FoodItemEntryConfiguration : IEntityTypeConfiguration<FoodItemEntryRecord>
{
    public void Configure(EntityTypeBuilder<FoodItemEntryRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "food_item_entries");
        builder.HasOne<DailyNutritionLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.DailyLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class UserFoodItemConfiguration : IEntityTypeConfiguration<UserFoodItemRecord>
{
    public void Configure(EntityTypeBuilder<UserFoodItemRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "user_food_items");
}

public sealed class UserMealTemplateConfiguration : IEntityTypeConfiguration<UserMealTemplateRecord>
{
    public void Configure(EntityTypeBuilder<UserMealTemplateRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "user_meal_templates");
}

public sealed class UserMealTemplateEntryConfiguration : IEntityTypeConfiguration<UserMealTemplateEntryRecord>
{
    public void Configure(EntityTypeBuilder<UserMealTemplateEntryRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "user_meal_template_entries");
        builder.HasOne<UserMealTemplateRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.MealTemplateId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(x => x.CatalogFoodItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserFoodItemRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.UserFoodItemId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("user_meal_template_entries", "app", table => table.HasCheckConstraint(
            "ck_user_meal_template_entries_exactly_one_food_ref",
            "(catalog_food_item_id IS NOT NULL AND user_food_item_id IS NULL) OR (catalog_food_item_id IS NULL AND user_food_item_id IS NOT NULL)"));
    }
}

public sealed class LoggedMealConfiguration : IEntityTypeConfiguration<LoggedMealRecord>
{
    public void Configure(EntityTypeBuilder<LoggedMealRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "logged_meals");
}

public sealed class LoggedMealEntryConfiguration : IEntityTypeConfiguration<LoggedMealEntryRecord>
{
    public void Configure(EntityTypeBuilder<LoggedMealEntryRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "logged_meal_entries");
        builder.HasOne<LoggedMealRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.LoggedMealId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne<FoodItem>().WithMany().HasForeignKey(x => x.CatalogFoodItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserFoodItemRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.UserFoodItemId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
        builder.ToTable("logged_meal_entries", "app", table => table.HasCheckConstraint(
            "ck_logged_meal_entries_exactly_one_food_ref",
            "(catalog_food_item_id IS NOT NULL AND user_food_item_id IS NULL) OR (catalog_food_item_id IS NULL AND user_food_item_id IS NOT NULL)"));
    }
}

public sealed class RecipeConfiguration : IEntityTypeConfiguration<RecipeRecord>
{
    public void Configure(EntityTypeBuilder<RecipeRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "recipes");
}

public sealed class SavedMealTemplateConfiguration : IEntityTypeConfiguration<SavedMealTemplateRecord>
{
    public void Configure(EntityTypeBuilder<SavedMealTemplateRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "saved_meal_templates");
}

public sealed class PhotoJourneyEntryConfiguration : IEntityTypeConfiguration<PhotoJourneyEntryRecord>
{
    public void Configure(EntityTypeBuilder<PhotoJourneyEntryRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "photo_journey_entries");
        builder.Property(x => x.ImagePath).HasColumnName("image_path").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_photo_journey_entries_owner_date_desc");
    }
}

public sealed class PhotoUploadIntentConfiguration : IEntityTypeConfiguration<PhotoUploadIntentRecord>
{
    public void Configure(EntityTypeBuilder<PhotoUploadIntentRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "photo_upload_intents");
        builder.ToTable("photo_upload_intents", "app", table =>
        {
            table.HasCheckConstraint("ck_photo_upload_intents_size_positive", "declared_size_bytes > 0");
            table.HasCheckConstraint("ck_photo_upload_intents_expiry", "expires_at_utc > created_at_utc");
            table.HasCheckConstraint("ck_photo_upload_intents_state", "state IN ('Pending', 'Committed', 'Cancelled', 'Expired', 'Deleted')");
        });
        builder.Property(x => x.IdempotencyKey).HasMaxLength(128).IsRequired();
        builder.Property(x => x.S3Key).HasMaxLength(512).IsRequired();
        builder.Property(x => x.ContentType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.DeclaredSizeBytes).IsRequired();
        builder.Property(x => x.Sha256Checksum).HasMaxLength(64).IsRequired();
        builder.Property(x => x.State).HasMaxLength(16).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.IdempotencyKey }).IsUnique()
            .HasDatabaseName("uq_photo_upload_intents_owner_idempotency");
        builder.HasIndex(x => x.S3Key).IsUnique()
            .HasDatabaseName("uq_photo_upload_intents_s3_key");
        builder.HasIndex(x => new { x.UserId, x.ExpiresAtUtc })
            .HasDatabaseName("ix_photo_upload_intents_owner_expiry");
    }
}

public sealed class PhotoObjectCleanupConfiguration : IEntityTypeConfiguration<PhotoObjectCleanupRecord>
{
    public void Configure(EntityTypeBuilder<PhotoObjectCleanupRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "photo_object_cleanups");
        builder.ToTable("photo_object_cleanups", "app", table =>
        {
            table.HasCheckConstraint("ck_photo_object_cleanups_state", "state IN ('Pending', 'Leased', 'Retryable', 'Completed')");
            table.HasCheckConstraint("ck_photo_object_cleanups_reason", "reason IN ('CancelledUpload', 'ExpiredUpload', 'CommittedPhoto')");
            table.HasCheckConstraint("ck_photo_object_cleanups_failure_category", "last_failure_category IS NULL OR last_failure_category IN ('Transient', 'Permission', 'Configuration', 'Provider', 'Unknown')");
            table.HasCheckConstraint("ck_photo_object_cleanups_attempt_count", "attempt_count >= 0");
            table.HasCheckConstraint("ck_photo_object_cleanups_lease", "(state = 'Leased' AND lease_token IS NOT NULL AND lease_expires_at_utc IS NOT NULL) OR (state <> 'Leased' AND lease_token IS NULL AND lease_expires_at_utc IS NULL)");
        });
        builder.Property(x => x.S3Key).HasMaxLength(512).IsRequired();
        builder.Property(x => x.Reason).HasMaxLength(32).IsRequired();
        builder.Property(x => x.State).HasMaxLength(16).IsRequired();
        builder.Property(x => x.LastFailureCategory).HasMaxLength(32);
        builder.HasIndex(x => new { x.UserId, x.PhotoId, x.Reason }).IsUnique()
            .HasDatabaseName("uq_photo_object_cleanups_owner_photo_reason");
        builder.HasIndex(x => new { x.NotBeforeUtc, x.CreatedAtUtc })
            .HasDatabaseName("ix_photo_object_cleanups_due");
    }
}

internal sealed class JsonTextValueConverter : ValueConverter<string, string>
{
    public static readonly JsonTextValueConverter Instance = new();

    private JsonTextValueConverter()
        : base(value => value.Length == 0 ? "\"\"" : value,
            value => value == "\"\"" ? string.Empty : value)
    {
    }
}

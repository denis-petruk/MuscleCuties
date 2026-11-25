using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;

namespace MuscleCuties.Infrastructure.PostgreSql.Configurations.App;

/// <summary>The conventions every app table shares: snake_case columns, numeric decimals, jsonb for columns named *Json, and the tenant key that scopes a row to one account.</summary>
internal static class AppModelConfiguration
{
    private static readonly Regex WordBoundary = new("([a-z0-9])([A-Z])", RegexOptions.Compiled);

    /// <summary>Applies the table's conventions and the tenant machinery: a never-generated id, the (user_id, id) alternate key child rows reference, a user index, and a cascade from the account.</summary>
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

    /// <summary>Applies the conventions to a table that is not account-scoped, so it gets no tenant key and no cascade from an account.</summary>
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

    /// <summary>Snake case name of a column or table.</summary>
    public static string ToSnakeCase(string value) => WordBoundary.Replace(value, "$1_$2").ToLowerInvariant();

}

/// <summary>A day's readiness overlay; one row per account and plan day, so the overlay cannot be applied twice.</summary>
public sealed class WorkoutCycleReadinessOccurrenceConfiguration : IEntityTypeConfiguration<WorkoutCycleReadinessOccurrenceRecord>
{
    /// <summary>Reaches its day through the (user_id, id) key, so an overlay cannot point at another account's day.</summary>
    public void Configure(EntityTypeBuilder<WorkoutCycleReadinessOccurrenceRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_cycle_readiness_occurrences");
        builder.HasIndex(row => new { row.UserId, row.WorkoutDayId }).IsUnique();
        builder.HasOne<WorkoutDayRecord>().WithMany().HasForeignKey(row => new { row.UserId, row.WorkoutDayId })
            .HasPrincipalKey(row => new { row.UserId, row.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Accounts. Uniqueness is on the normalised address, and the Apple id is unique only where it is present.</summary>
public sealed class UserConfiguration : IEntityTypeConfiguration<UserRecord>
{
    /// <summary>Stores the address with a binary collation and derives the normalised form in the database, so uniqueness is decided by one expression rather than by the writer.</summary>
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

/// <summary>Objects the reconciler must confirm. Ownership is deliberately absent: the row carries only an opaque key.</summary>
public sealed class PhotoObjectReconcileTargetConfiguration : IEntityTypeConfiguration<PhotoObjectReconcileTargetRecord>
{
    /// <summary>Adds the due-order index the reconciler reads and a constraint that a lease token and its expiry appear together.</summary>
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

/// <summary>Redeemed verification codes, keyed by the challenge id the email carried.</summary>
public sealed class EmailChallengeConfiguration : IEntityTypeConfiguration<EmailChallengeRecord>
{
    /// <summary>Keeps the challenge id as the key, so the emailed identifier is what the redemption is looked up by.</summary>
    public void Configure(EntityTypeBuilder<EmailChallengeRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "email_challenges");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").HasMaxLength(128).ValueGeneratedNever();
        builder.Property(x => x.Purpose).HasColumnName("purpose").IsRequired();
        builder.Property(x => x.RedeemedAtUtc).HasColumnName("redeemed_at_utc").HasColumnType("timestamp with time zone");
    }
}

/// <summary>Issued refresh tokens, unique by token hash so a token can only exist once.</summary>
public sealed class RefreshSessionConfiguration : IEntityTypeConfiguration<RefreshSessionRecord>
{
    /// <summary>Indexes the family so a whole rotation lineage can be revoked at once, and requires the expiry to follow the issue.</summary>
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
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable("refresh_sessions", "app", table =>
            table.HasCheckConstraint("ck_refresh_sessions_expiry", "expires_at_utc > created_at_utc"));
    }
}

/// <summary>One-time Apple sign-in nonces, unique by hash and indexed by expiry for pruning.</summary>
public sealed class AppleSignInNonceConfiguration : IEntityTypeConfiguration<AppleSignInNonceRecord>
{
    /// <summary>Unique by nonce hash and indexed by expiry, which is how expired rows are pruned.</summary>
    public void Configure(EntityTypeBuilder<AppleSignInNonceRecord> builder)
    {
        AppModelConfiguration.ConfigureGlobal(builder, "apple_sign_in_nonces");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
        builder.Property(x => x.NonceHash).HasColumnName("nonce_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ExpiresAtUtc).HasColumnName("expires_at_utc").IsRequired();
        builder.Property(x => x.RedeemedAtUtc).HasColumnName("redeemed_at_utc");
        builder.HasIndex(x => x.NonceHash).IsUnique().HasDatabaseName("uq_apple_sign_in_nonces_hash");
        builder.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("ix_apple_sign_in_nonces_expiry");
    }
}

/// <summary>Revoked access tokens, unique by token id so a revocation is recorded once.</summary>
public sealed class RevokedAccessTokenConfiguration : IEntityTypeConfiguration<RevokedAccessTokenRecord>
{
    /// <summary>Unique by token id and indexed by expiry, so the denial list stays small.</summary>
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
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Verification code challenges, with the attempt, expiry and purpose limits expressed as constraints.</summary>
public sealed class EmailChallengeSessionConfiguration : IEntityTypeConfiguration<EmailChallengeSessionRecord>
{
    /// <summary>Constrains purpose to the two supported values, keeps attempts non-negative, and indexes the newest challenge per address.</summary>
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
        builder.HasIndex(x => new { x.NormalizedEmail, x.CreatedAtUtc }).IsDescending(false, true)
            .HasDatabaseName("ix_email_challenge_sessions_email_created");
        builder.HasIndex(x => x.ExpiresAtUtc).HasDatabaseName("ix_email_challenge_sessions_expiry");
        builder.ToTable("email_challenge_sessions", "app", table =>
        {
            table.HasCheckConstraint("ck_email_challenge_session_attempts", "failed_attempts >= 0");
            table.HasCheckConstraint("ck_email_challenge_session_expiry", "expires_at_utc > created_at_utc");
            table.HasCheckConstraint("ck_email_challenge_session_purpose", "purpose IN (1, 2)");
        });
    }
}

/// <summary>Claim nonces for pre-existing accounts, unique by hash.</summary>
public sealed class SourceAccountClaimNonceConfiguration : IEntityTypeConfiguration<SourceAccountClaimNonceRecord>
{
    /// <summary>Unique by nonce hash and indexed by expiry; the destination account must exist.</summary>
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
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(x => x.DestinationUserId).OnDelete(DeleteBehavior.Cascade);
        builder.ToTable("source_account_claim_nonces", "app", table =>
            table.HasCheckConstraint("ck_source_account_claim_expiry", "expires_at_utc > created_at_utc"));
    }
}

/// <summary>Source-account mappings, unique per source account so an import cannot run twice.</summary>
public sealed class SourceAccountMappingConfiguration : IEntityTypeConfiguration<SourceAccountMappingRecord>
{
    /// <summary>Makes the source pair and the claim id unique, so one source account and one claim each map exactly once.</summary>
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
        builder.HasOne<SourceAccountClaimNonceRecord>().WithMany().HasForeignKey(x => x.ClaimId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<UserRecord>().WithMany().HasForeignKey(x => x.DestinationUserId).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>The profile: one row per account, JSON columns constrained to the shape the app stores, and the concurrency token a save must match.</summary>
public sealed class UserProfileConfiguration : IEntityTypeConfiguration<UserProfileRecord>
{
    /// <summary>Makes the row unique per account, marks the version as a concurrency token, and constrains each JSON column to the type the app writes: arrays for id lists, objects for maps.</summary>
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
        builder.Property(x => x.GluteFocus).HasColumnName("glute_focus").HasDefaultValue(false).IsRequired();
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

/// <summary>Recorded command keys, unique per account, with the answer required to appear together with its completion time.</summary>
public sealed class CommandIdempotencyConfiguration : IEntityTypeConfiguration<CommandIdempotencyRecord>
{
    /// <summary>Makes the key unique per account and requires the stored answer and its completion time to appear together.</summary>
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

/// <summary>The diary event cursor, keyed by account rather than by a surrogate id: there is exactly one per account.</summary>
public sealed class NutritionDiaryEventCursorConfiguration : IEntityTypeConfiguration<NutritionDiaryEventCursorRecord>
{
    /// <summary>Keys on the account instead of a surrogate id, because there is exactly one cursor per account.</summary>
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

/// <summary>The diary's append-only command log, unique per account and cursor position.</summary>
public sealed class NutritionDiaryCommandEventConfiguration : IEntityTypeConfiguration<NutritionDiaryCommandEventRecord>
{
    /// <summary>Makes the cursor unique per account, so two events cannot claim one position.</summary>
    public void Configure(EntityTypeBuilder<NutritionDiaryCommandEventRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "nutrition_diary_command_events");
        builder.Property(x => x.Cursor).HasColumnName("cursor");
        builder.Property(x => x.DiaryDate).HasColumnName("diary_date");
        builder.Property(x => x.CommandId).HasColumnName("command_id").HasMaxLength(128).IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Cursor }).IsUnique().HasDatabaseName("uq_nutrition_diary_command_events_owner_cursor");
    }
}

/// <summary>Profile copies. Only the tenant conventions apply; a snapshot is never joined to.</summary>
public sealed class UserProfileSnapshotConfiguration : IEntityTypeConfiguration<UserProfileSnapshotRecord>
{
    /// <summary>Applies only the tenant conventions; a snapshot is written and read whole, never joined.</summary>
    public void Configure(EntityTypeBuilder<UserProfileSnapshotRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "user_profile_snapshots");
}

/// <summary>Onboarding answers, tied to the exact answer option and to the snapshot they were folded into.</summary>
public sealed class UserQuizResponseConfiguration : IEntityTypeConfiguration<UserQuizResponseRecord>
{
    /// <summary>Reaches the answer option through the catalog's alternate key, so an answer cannot be attached to another question.</summary>
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

/// <summary>Recorded cycles, indexed by account and start date descending for the newest-first read.</summary>
public sealed class CycleLogConfiguration : IEntityTypeConfiguration<CycleLogRecord>
{
    /// <summary>Indexed by account and start date descending, which is the newest-first read the overview makes.</summary>
    public void Configure(EntityTypeBuilder<CycleLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "cycle_logs");
        builder.HasIndex(x => new { x.UserId, x.StartDate }).IsDescending(false, true).HasDatabaseName("ix_cycle_logs_owner_start_date_desc");
    }
}

/// <summary>Per-day phase logs, one per account and local date, with the cycle link restricted rather than cascaded.</summary>
public sealed class CyclePhaseLogConfiguration : IEntityTypeConfiguration<CyclePhaseLogRecord>
{
    /// <summary>One row per account and local date, and its cycle link is restricted so deleting a cycle cannot silently drop logged days.</summary>
    public void Configure(EntityTypeBuilder<CyclePhaseLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "cycle_phase_logs");
        builder.HasIndex(x => new { x.UserId, x.LocalDate }).IsUnique().HasDatabaseName("uq_cycle_phase_logs_owner_local_date");
        builder.HasIndex(x => new { x.UserId, x.LoggedAt }).IsDescending(false, true).HasDatabaseName("ix_cycle_phase_logs_owner_logged_at_desc");
        builder.HasOne<CycleLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.CycleLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Symptoms per day, indexed newest-first and tied to their cycle by a restricted key.</summary>
public sealed class SymptomLogConfiguration : IEntityTypeConfiguration<SymptomLogRecord>
{
    /// <summary>Indexed newest-first and linked to its cycle with a restricted key.</summary>
    public void Configure(EntityTypeBuilder<SymptomLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "symptom_logs");
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_symptom_logs_owner_date_desc");
        builder.HasOne<CycleLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.CycleLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Readiness answers, with the planning scales pinned to 1–5 and the revision kept non-negative.</summary>
public sealed class DailyReadinessLogConfiguration : IEntityTypeConfiguration<DailyReadinessLogRecord>
{
    private const int MinimumPlanningScaleValue = 1;
    private const int MaximumPlanningScaleValue = 5;
    private const long InitialPlanningRevision = 0;
    private static readonly string PlanningScaleConstraint = $"(planning_sleep_quality IS NULL OR planning_sleep_quality BETWEEN {MinimumPlanningScaleValue} AND {MaximumPlanningScaleValue}) AND (planning_soreness IS NULL OR planning_soreness BETWEEN {MinimumPlanningScaleValue} AND {MaximumPlanningScaleValue}) AND (planning_stress IS NULL OR planning_stress BETWEEN {MinimumPlanningScaleValue} AND {MaximumPlanningScaleValue})";
    private static readonly string PlanningRevisionConstraint = $"planning_revision >= {InitialPlanningRevision}";

    /// <summary>Pins the planning scales to 1–5 and the revision to non-negative, so a plan can trust any stored value.</summary>
    public void Configure(EntityTypeBuilder<DailyReadinessLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "daily_readiness_logs");
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_daily_readiness_logs_owner_date_desc");
        builder.Property(x => x.ReadinessTier).HasConversion<string>();
        builder.Property(x => x.PlanningRevision).HasDefaultValue(InitialPlanningRevision);
        builder.ToTable("daily_readiness_logs", "app", table =>
        {
            table.HasCheckConstraint("ck_daily_readiness_logs_planning_scales", PlanningScaleConstraint);
            table.HasCheckConstraint("ck_daily_readiness_logs_planning_fatigue",
                "planning_fatigue IS NULL OR planning_fatigue BETWEEN 1 AND 5");
            table.HasCheckConstraint("ck_daily_readiness_logs_planning_revision", PlanningRevisionConstraint);
        });
    }
}

/// <summary>Reported injuries; the column limits keep a free-text trigger short enough to index later.</summary>
public sealed class WorkoutInjuryLogConfiguration : IEntityTypeConfiguration<WorkoutInjuryLogRecord>
{
    private const int WorseningTriggerMaximumLength = 32;

    /// <summary>Limits the free-text trigger column, which is descriptive only and never parsed by the planner.</summary>
    public void Configure(EntityTypeBuilder<WorkoutInjuryLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_injury_logs");
        builder.Property(record => record.WorseningTrigger).HasMaxLength(WorseningTriggerMaximumLength);
    }
}

/// <summary>The link from a suppressed exercise row to its injury episode, unique per pair.</summary>
public sealed class WorkoutInjuryPrescriptionLinkConfiguration : IEntityTypeConfiguration<WorkoutInjuryPrescriptionLinkRecord>
{
    /// <summary>Unique per prescription and episode, so restoring an episode cannot restore one row twice.</summary>
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

/// <summary>The plan in force. Only the tenant conventions apply; the active plan is chosen by the row's own flag.</summary>
public sealed class WorkoutPlanConfiguration : IEntityTypeConfiguration<WorkoutPlanRecord>
{
    /// <summary>Applies only the tenant conventions; which plan is in force is carried by the row itself.</summary>
    public void Configure(EntityTypeBuilder<WorkoutPlanRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "workout_plans");
}

/// <summary>Version-2 proposals, whose status and decision columns are constrained to the four states the planner can be in.</summary>
public sealed class WorkoutPlanProposalV02Configuration : IEntityTypeConfiguration<WorkoutPlanProposalV02Record>
{
    /// <summary>Constrains status to the four decision states and pins the decision columns to that state, so a half-decided proposal cannot exist.</summary>
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

/// <summary>Applied version-2 revisions, unique per plan and revision number so a revision cannot be applied twice.</summary>
public sealed class WorkoutPlanRevisionV02Configuration : IEntityTypeConfiguration<WorkoutPlanRevisionV02Record>
{
    /// <summary>Makes the revision number unique per plan and requires it positive, which is what makes "newer" decidable.</summary>
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

/// <summary>A plan's days, with the removal flags defaulted to false and the duration constrained non-negative.</summary>
public sealed class WorkoutDayConfiguration : IEntityTypeConfiguration<WorkoutDayRecord>
{
    /// <summary>Defaults the removal flags to false and constrains the planned duration non-negative.</summary>
    public void Configure(EntityTypeBuilder<WorkoutDayRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_days");
        builder.Property(x => x.PlannedActivityPrescriptionJson).HasColumnType("jsonb");
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

/// <summary>A day's exercises, with optional sets required to be a strict subset of the working sets.</summary>
public sealed class WorkoutDayExerciseConfiguration : IEntityTypeConfiguration<WorkoutDayExerciseRecord>
{
    /// <summary>Requires optional sets to be a strict subset of the working sets, so skipping them is always possible.</summary>
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

/// <summary>Performed sessions, indexed newest-first; distance is stored to two decimal places of a kilometre.</summary>
public sealed class WorkoutSessionLogConfiguration : IEntityTypeConfiguration<WorkoutSessionLogRecord>
{
    /// <summary>Indexes newest-first and fixes distance to two decimals, which is the precision the app displays.</summary>
    public void Configure(EntityTypeBuilder<WorkoutSessionLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_session_logs");
        builder.HasIndex(x => new { x.UserId, x.StartedAt }).IsDescending(false, true).HasDatabaseName("ix_workout_session_logs_owner_started_at_desc");
        builder.Property(x => x.DistanceKm).HasPrecision(8, 2);
    }
}

/// <summary>Performed sets. Unilateral reps must be given for both sides or for neither.</summary>
public sealed class ExerciseSetLogConfiguration : IEntityTypeConfiguration<ExerciseSetLogRecord>
{
    /// <summary>Requires both unilateral rep counts or neither, keeping the per-side data trustworthy.</summary>
    public void Configure(EntityTypeBuilder<ExerciseSetLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "exercise_set_logs");
        builder.Property(x => x.TechniqueAcceptable).HasDefaultValue(true);
        builder.Property(x => x.IsObserved).HasDefaultValue(false);
        builder.Property(x => x.LeftSideReps).HasColumnName("left_side_reps");
        builder.Property(x => x.RightSideReps).HasColumnName("right_side_reps");
        builder.ToTable("exercise_set_logs", "app", table => table.HasCheckConstraint(
            "ck_exercise_set_logs_unilateral_reps",
            "(left_side_reps IS NULL AND right_side_reps IS NULL) OR " +
            "(left_side_reps IS NOT NULL AND right_side_reps IS NOT NULL AND " +
            "left_side_reps BETWEEN 1 AND 1000 AND right_side_reps BETWEEN 1 AND 1000)"));
        builder.HasOne<WorkoutSessionLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutSessionLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Legacy workout logs, indexed newest-first; the day they log is restricted so history cannot lose its plan.</summary>
public sealed class WorkoutLogConfiguration : IEntityTypeConfiguration<WorkoutLogRecord>
{
    /// <summary>Indexed newest-first, and its day link is restricted so deleting a plan cannot erase history.</summary>
    public void Configure(EntityTypeBuilder<WorkoutLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "workout_logs");
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_workout_logs_owner_date_desc");
        builder.HasOne<WorkoutDayRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.WorkoutDayId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Legacy per-exercise rows, cascaded from their log and restricted against the day's prescription.</summary>
public sealed class WorkoutExerciseLogConfiguration : IEntityTypeConfiguration<WorkoutExerciseLogRecord>
{
    /// <summary>Cascades from its log but restricts against the day's prescription, so a prescription change cannot delete logged work.</summary>
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

/// <summary>Exercise swaps, one per account and original exercise.</summary>
public sealed class UserExercisePreferenceConfiguration : IEntityTypeConfiguration<UserExercisePreferenceRecord>
{
    /// <summary>One swap per account and original exercise, with both exercises restricted from deletion.</summary>
    public void Configure(EntityTypeBuilder<UserExercisePreferenceRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "user_exercise_preferences");
        builder.HasOne<WorkoutExerciseDefinition>().WithMany().HasForeignKey(x => x.OriginalExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<WorkoutExerciseDefinition>().WithMany().HasForeignKey(x => x.PreferredExerciseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.UserId, x.OriginalExerciseId }).IsUnique().HasDatabaseName("uq_user_exercise_preferences_owner_original");
    }
}

/// <summary>Her notes on a movement; the exercise is restricted so a note never dangles.</summary>
public sealed class UserMovementNoteConfiguration : IEntityTypeConfiguration<UserMovementNoteRecord>
{
    /// <summary>Restricts the exercise reference so a note never outlives the movement it describes.</summary>
    public void Configure(EntityTypeBuilder<UserMovementNoteRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "user_movement_notes");
        builder.HasOne<Exercise>().WithMany().HasForeignKey(x => x.ExerciseId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Nutrition days, indexed by account and local date descending.</summary>
public sealed class DailyNutritionLogConfiguration : IEntityTypeConfiguration<DailyNutritionLogRecord>
{
    /// <summary>Indexed by account and local date descending, which is the diary's read.</summary>
    public void Configure(EntityTypeBuilder<DailyNutritionLogRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "daily_nutrition_logs");
        builder.HasIndex(x => new { x.UserId, x.LocalDate }).IsDescending(false, true).HasDatabaseName("ix_daily_nutrition_logs_owner_local_date_desc");
    }
}

/// <summary>Foods logged on a day, cascaded from the day's row.</summary>
public sealed class FoodItemEntryConfiguration : IEntityTypeConfiguration<FoodItemEntryRecord>
{
    /// <summary>Cascades from its day, so deleting a day removes its entries and nothing else.</summary>
    public void Configure(EntityTypeBuilder<FoodItemEntryRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "food_item_entries");
        builder.HasOne<DailyNutritionLogRecord>().WithMany().HasForeignKey(x => new { x.UserId, x.DailyLogId })
            .HasPrincipalKey(x => new { x.UserId, x.Id }).OnDelete(DeleteBehavior.Cascade);
    }
}

/// <summary>Foods she created. Only the tenant conventions apply; they never join the shared catalog.</summary>
public sealed class UserFoodItemConfiguration : IEntityTypeConfiguration<UserFoodItemRecord>
{
    /// <summary>Applies only the tenant conventions: her foods are kept out of the shared catalog by scope, not by a flag.</summary>
    public void Configure(EntityTypeBuilder<UserFoodItemRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "user_food_items");
}

/// <summary>Her meal templates. Only the tenant conventions apply.</summary>
public sealed class UserMealTemplateConfiguration : IEntityTypeConfiguration<UserMealTemplateRecord>
{
    /// <summary>Applies only the tenant conventions.</summary>
    public void Configure(EntityTypeBuilder<UserMealTemplateRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "user_meal_templates");
}

/// <summary>Foods inside her templates; exactly one of the catalog and her own food may be set.</summary>
public sealed class UserMealTemplateEntryConfiguration : IEntityTypeConfiguration<UserMealTemplateEntryRecord>
{
    /// <summary>Requires exactly one food reference, so an entry is never ambiguous between the catalog and her own foods.</summary>
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

/// <summary>Meals logged on a day. Only the tenant conventions apply.</summary>
public sealed class LoggedMealConfiguration : IEntityTypeConfiguration<LoggedMealRecord>
{
    /// <summary>Applies only the tenant conventions.</summary>
    public void Configure(EntityTypeBuilder<LoggedMealRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "logged_meals");
}

/// <summary>Foods inside logged meals; exactly one food reference must be set.</summary>
public sealed class LoggedMealEntryConfiguration : IEntityTypeConfiguration<LoggedMealEntryRecord>
{
    /// <summary>Requires exactly one food reference and cascades from its meal.</summary>
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

/// <summary>Saved recipes. Only the tenant conventions apply; the contents are a JSON snapshot.</summary>
public sealed class RecipeConfiguration : IEntityTypeConfiguration<RecipeRecord>
{
    /// <summary>Applies only the tenant conventions; the recipe body is a JSON snapshot.</summary>
    public void Configure(EntityTypeBuilder<RecipeRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "recipes");
}

/// <summary>Saved meal templates. Only the tenant conventions apply.</summary>
public sealed class SavedMealTemplateConfiguration : IEntityTypeConfiguration<SavedMealTemplateRecord>
{
    /// <summary>Applies only the tenant conventions.</summary>
    public void Configure(EntityTypeBuilder<SavedMealTemplateRecord> builder) => AppModelConfiguration.ConfigureTenant(builder, "saved_meal_templates");
}

/// <summary>Progress photos, indexed by account and date descending; the image path is required.</summary>
public sealed class PhotoJourneyEntryConfiguration : IEntityTypeConfiguration<PhotoJourneyEntryRecord>
{
    /// <summary>Requires the image path and indexes newest-first by account.</summary>
    public void Configure(EntityTypeBuilder<PhotoJourneyEntryRecord> builder)
    {
        AppModelConfiguration.ConfigureTenant(builder, "photo_journey_entries");
        builder.Property(x => x.ImagePath).HasColumnName("image_path").IsRequired();
        builder.HasIndex(x => new { x.UserId, x.Date }).IsDescending(false, true).HasDatabaseName("ix_photo_journey_entries_owner_date_desc");
    }
}

/// <summary>Authorised uploads: state limited to the five lifecycle values, size positive, expiry after issue, and the key unique per account.</summary>
public sealed class PhotoUploadIntentConfiguration : IEntityTypeConfiguration<PhotoUploadIntentRecord>
{
    /// <summary>Limits state to the five lifecycle values, keeps the size positive and the expiry after issue, and makes the key unique per account.</summary>
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

/// <summary>The deletion queue: state, reason and failure category are constrained, and a lease exists only while leased.</summary>
public sealed class PhotoObjectCleanupConfiguration : IEntityTypeConfiguration<PhotoObjectCleanupRecord>
{
    /// <summary>Constrains state, reason and failure category to the known values, and requires a lease token exactly while leased.</summary>
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

/// <summary>Stores an empty string as an empty JSON string, because a jsonb column rejects the empty text the domain model uses for absent.</summary>
internal sealed class JsonTextValueConverter : ValueConverter<string, string>
{
    public static readonly JsonTextValueConverter Instance = new();

    /// <summary>Maps the empty string to an empty JSON string and back, which is the one translation a jsonb column needs.</summary>
    private JsonTextValueConverter()
        : base(value => value.Length == 0 ? "\"\"" : value,
            value => value == "\"\"" ? string.Empty : value)
    {
    }
}

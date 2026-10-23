using System.Data;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed class PostgresPhotoUploadIntentStore(PostgresDbContext dbContext) : IPhotoUploadIntentStore
{
    // The signer caps POST grants at 15 minutes; this five-minute margin covers issuer/worker clock skew.
    private static readonly TimeSpan UploadGrantClockSkewMargin = TimeSpan.FromMinutes(5);
    private const long MaximumDeclaredSizeBytes = 15 * 1024 * 1024;
    private static readonly Regex Sha256Pattern = new("\\A[0-9a-fA-F]{64}\\z", RegexOptions.Compiled);

    public async Task<PhotoUploadIntentDto> CreateOrGetAsync(
        Guid userId,
        CreatePhotoUploadIntentRequest request,
        DateTime createdAtUtc,
        DateTime expiresAtUtc,
        CancellationToken cancellationToken = default)
    {
        Validate(userId, request, createdAtUtc, expiresAtUtc);
        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await dbContext.Database.BeginTransactionAsync(IsolationLevel.ReadCommitted, cancellationToken);
        await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken);
        await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

        var photoId = Guid.NewGuid();
        var s3Key = $"photos/{userId:D}/{photoId:N}";
        await using (var command = new NpgsqlCommand("""
            INSERT INTO app.photo_upload_intents
                (id, user_id, idempotency_key, s3_key, content_type, declared_size_bytes,
                 sha256_checksum, created_at_utc, expires_at_utc, state)
            VALUES (@id, @user_id, @idempotency_key, @s3_key, @content_type, @declared_size_bytes,
                    @sha256_checksum, @created_at_utc, @expires_at_utc, 'Pending')
            ON CONFLICT (user_id, idempotency_key) DO NOTHING
            RETURNING id
            """, (NpgsqlConnection)dbContext.Database.GetDbConnection(), (NpgsqlTransaction)transaction.GetDbTransaction()))
        {
            command.Parameters.AddWithValue("id", photoId);
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("idempotency_key", request.IdempotencyKey);
            command.Parameters.AddWithValue("s3_key", s3Key);
            command.Parameters.AddWithValue("content_type", request.ContentType);
            command.Parameters.AddWithValue("declared_size_bytes", request.DeclaredSizeBytes);
            command.Parameters.AddWithValue("sha256_checksum", request.Sha256Checksum.ToLowerInvariant());
            command.Parameters.AddWithValue("created_at_utc", createdAtUtc);
            command.Parameters.AddWithValue("expires_at_utc", expiresAtUtc);
            var insertedId = await command.ExecuteScalarAsync(cancellationToken);
            if (insertedId is null)
            {
                var existing = await dbContext.PhotoUploadIntents.SingleAsync(
                    intent => intent.UserId == userId && intent.IdempotencyKey == request.IdempotencyKey,
                    cancellationToken);
                if (!SameRequest(existing, request))
                    throw new InvalidOperationException("The idempotency key was already used with different upload metadata.");

                existing = await ExpireIfNeededAsync(existing, createdAtUtc, cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ToDto(existing);
            }
        }

        var created = await dbContext.PhotoUploadIntents.SingleAsync(
            intent => intent.UserId == userId && intent.Id == photoId,
            cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(created);
        }).ConfigureAwait(false);
    }

    public async Task<PhotoUploadIntentDto?> GetAsync(
        Guid userId,
        Guid photoId,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        dbContext.ChangeTracker.Clear();
        await using var transaction = await BeginTenantTransactionAsync(userId, cancellationToken);
        var record = await dbContext.PhotoUploadIntents.SingleOrDefaultAsync(
            intent => intent.UserId == userId && intent.Id == photoId,
            cancellationToken);
        if (record is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        record = await ExpireIfNeededAsync(record, nowUtc, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(record);
    }).ConfigureAwait(false);

    public async Task<PhotoUploadIntentDto?> TransitionAsync(
        Guid userId,
        Guid photoId,
        string expectedS3Key,
        PhotoUploadIntentState targetState,
        DateTime nowUtc,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
    {
        if (targetState is not (PhotoUploadIntentState.Committed or PhotoUploadIntentState.Cancelled))
            throw new ArgumentOutOfRangeException(nameof(targetState), "Only commit and cancel transitions can be requested.");

        dbContext.ChangeTracker.Clear();
        await using var transaction = await BeginTenantTransactionAsync(userId, cancellationToken);
        var record = await dbContext.PhotoUploadIntents.SingleOrDefaultAsync(
            intent => intent.UserId == userId && intent.Id == photoId,
            cancellationToken);
        if (record is null || !string.Equals(record.S3Key, expectedS3Key, StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        await using (var command = new NpgsqlCommand("""
            UPDATE app.photo_upload_intents
            SET state = 'Expired'
            WHERE user_id = @user_id AND id = @id AND state = 'Pending' AND expires_at_utc <= @now_utc
            """, (NpgsqlConnection)dbContext.Database.GetDbConnection(), (NpgsqlTransaction)transaction.GetDbTransaction()))
        {
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("id", photoId);
            command.Parameters.AddWithValue("now_utc", nowUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var command = new NpgsqlCommand("""
            UPDATE app.photo_upload_intents
            SET state = @target_state
            WHERE user_id = @user_id AND id = @id AND state = 'Pending' AND expires_at_utc > @now_utc
            """, (NpgsqlConnection)dbContext.Database.GetDbConnection(), (NpgsqlTransaction)transaction.GetDbTransaction()))
        {
            command.Parameters.AddWithValue("target_state", targetState.ToString());
            command.Parameters.AddWithValue("user_id", userId);
            command.Parameters.AddWithValue("id", photoId);
            command.Parameters.AddWithValue("now_utc", nowUtc);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        dbContext.ChangeTracker.Clear();
        record = await dbContext.PhotoUploadIntents.SingleAsync(
            intent => intent.UserId == userId && intent.Id == photoId,
            cancellationToken);
        if (!string.Equals(record.State, targetState.ToString(), StringComparison.Ordinal))
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        if (targetState == PhotoUploadIntentState.Cancelled)
            await EnqueueCleanupAsync(record, "CancelledUpload", record.ExpiresAtUtc + UploadGrantClockSkewMargin, nowUtc, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return ToDto(record);
    }).ConfigureAwait(false);

    private async Task<Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction> BeginTenantTransactionAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        if (userId == Guid.Empty)
            throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(userId));
        var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken);
            await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            return transaction;
        }
        catch
        {
            await transaction.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    private async Task<PhotoUploadIntentRecord> ExpireIfNeededAsync(
        PhotoUploadIntentRecord record,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        if (record.State != "Pending" || record.ExpiresAtUtc > nowUtc)
            return record;

        var expired = await dbContext.PhotoUploadIntents
            .Where(intent => intent.UserId == record.UserId
                && intent.Id == record.Id
                && intent.State == "Pending"
                && intent.ExpiresAtUtc <= nowUtc)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(intent => intent.State, PhotoUploadIntentState.Expired.ToString()),
                cancellationToken);

        if (expired != 0)
            await EnqueueCleanupAsync(record, "ExpiredUpload", record.ExpiresAtUtc + UploadGrantClockSkewMargin, nowUtc, cancellationToken);

        dbContext.ChangeTracker.Clear();
        return await dbContext.PhotoUploadIntents.SingleAsync(
            intent => intent.UserId == record.UserId && intent.Id == record.Id,
            cancellationToken);
    }

    private async Task EnqueueCleanupAsync(
        PhotoUploadIntentRecord intent,
        string reason,
        DateTime notBeforeUtc,
        DateTime nowUtc,
        CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            INSERT INTO app.photo_object_cleanups
                (id, user_id, photo_id, s3_key, reason, not_before_utc, state, attempt_count,
                 lease_generation, created_at_utc)
            VALUES ({Guid.NewGuid()}, {intent.UserId}, {intent.Id}, {intent.S3Key}, {reason},
                    {notBeforeUtc}, 'Pending', 0, 0, {nowUtc})
            ON CONFLICT (user_id, photo_id, reason) DO NOTHING
            """, cancellationToken);
    }

    private static PhotoUploadIntentDto ToDto(PhotoUploadIntentRecord record) => new(
        record.Id,
        record.S3Key,
        record.IdempotencyKey,
        record.ContentType,
        record.DeclaredSizeBytes,
        record.Sha256Checksum,
        record.CreatedAtUtc,
        record.ExpiresAtUtc,
        Enum.Parse<PhotoUploadIntentState>(record.State, ignoreCase: false));

    private static bool SameRequest(PhotoUploadIntentRecord record, CreatePhotoUploadIntentRequest request) =>
        string.Equals(record.ContentType, request.ContentType, StringComparison.Ordinal)
        && record.DeclaredSizeBytes == request.DeclaredSizeBytes
        && CryptographicOperations.FixedTimeEquals(
            Convert.FromHexString(record.Sha256Checksum),
            Convert.FromHexString(request.Sha256Checksum));

    private static void Validate(
        Guid userId,
        CreatePhotoUploadIntentRequest request,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (userId == Guid.Empty)
            throw new ArgumentException("A non-empty authenticated user ID is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(request.IdempotencyKey) || request.IdempotencyKey.Length > 128)
            throw new ArgumentException("A non-empty idempotency key of at most 128 characters is required.", nameof(request));
        if (string.IsNullOrWhiteSpace(request.ContentType) || request.ContentType.Length > 128)
            throw new ArgumentException("A content type of at most 128 characters is required.", nameof(request));
        if (request.DeclaredSizeBytes <= 0)
            throw new ArgumentOutOfRangeException(nameof(request), "Declared upload size must be positive.");
        if (request.DeclaredSizeBytes > MaximumDeclaredSizeBytes)
            throw new ArgumentOutOfRangeException(nameof(request), $"Declared upload size must not exceed {MaximumDeclaredSizeBytes} bytes.");
        if (!Sha256Pattern.IsMatch(request.Sha256Checksum))
            throw new ArgumentException("A SHA-256 checksum must be 64 hexadecimal characters.", nameof(request));
        if (createdAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc.Kind != DateTimeKind.Utc || expiresAtUtc <= createdAtUtc)
            throw new ArgumentException("Upload intent timestamps must be UTC and expire after creation.", nameof(expiresAtUtc));
    }
}

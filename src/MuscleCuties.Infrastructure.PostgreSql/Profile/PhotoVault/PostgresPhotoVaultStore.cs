using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Configuration;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using MuscleCuties.UseCases.Features.Profile.PhotoVault.Contracts;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed class PostgresPhotoVaultStore(
    PostgresDbContext dbContext,
    IS3PresignedUrlService presignedUrlService,
    IPhotoUploadIntentStore uploadIntentStore,
    IS3PhotoObjectInspector objectInspector,
    IConfiguration configuration,
    IPhotoByteSanitizer? photoByteSanitizer = null) : IServerPhotoVaultStore
{
    private const int DefaultPresignedExpirationMinutes = 15;
    private const int MinimumPresignedExpirationMinutes = 1;
    private static readonly TimeSpan UploadIntentLifetime = TimeSpan.FromMinutes(DefaultPresignedExpirationMinutes);
    private const int MaximumObjectInspectionAttempts = 3;
    private const long MaximumPhotoBytes = 15 * 1024 * 1024;
    private const int MaximumNotesCharacters = 2000;
    private const int MaximumPhaseCharacters = 64;
    private const int InspectionRetryDelayMilliseconds = 100;
    // Do not delete while a still-valid POST grant might create or replace bytes at this key.
    private static readonly TimeSpan UploadGrantClockSkewMargin = TimeSpan.FromMinutes(5);

    public async Task<PhotoUploadIssueResponse> RequestUploadUrlAsync(
        Guid userId,
        PhotoUploadUrlRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.ContentType?.Trim().ToLowerInvariant() is not ("image/jpeg" or "image/png"))
            throw new ArgumentException("Only still JPEG and PNG photos are supported.", nameof(request));
        var nowUtc = DateTime.UtcNow;
        var contentType = request.ContentType.Trim().ToLowerInvariant();
        var intent = await uploadIntentStore.CreateOrGetAsync(
            userId,
            new CreatePhotoUploadIntentRequest(
                request.IdempotencyKey!,
                contentType,
                request.FileSizeBytes,
                request.Sha256Checksum!),
            nowUtc,
            nowUtc.Add(UploadIntentLifetime),
            cancellationToken).ConfigureAwait(false);

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            // A cancellation that wins before this lock prevents issuance; one that follows waits until signing commits.
            await using (var command = new NpgsqlCommand("""
                SELECT id FROM app.photo_upload_intents
                WHERE user_id = @user_id AND id = @photo_id
                FOR UPDATE
                """, (NpgsqlConnection)dbContext.Database.GetDbConnection(),
                (NpgsqlTransaction)transaction.GetDbTransaction()))
            {
                command.Parameters.AddWithValue("user_id", userId);
                command.Parameters.AddWithValue("photo_id", intent.PhotoId);
                if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
                    throw new InvalidOperationException("The upload intent is no longer available.");
            }

            var current = await dbContext.PhotoUploadIntents.AsNoTracking()
                .SingleAsync(i => i.UserId == userId && i.Id == intent.PhotoId, cancellationToken).ConfigureAwait(false);
            if (current.State != PhotoUploadIntentState.Pending.ToString())
                throw new InvalidOperationException("The upload intent is no longer available.");
            var issueTimeUtc = DateTime.UtcNow;
            var remainingLifetime = current.ExpiresAtUtc - issueTimeUtc;
            if (remainingLifetime < TimeSpan.FromMinutes(MinimumPresignedExpirationMinutes))
                throw new InvalidOperationException("The upload intent has expired.");

            var post = presignedUrlService.GeneratePresignedUploadPost(
                current.S3Key,
                current.ContentType,
                current.Id.ToString("D"),
                current.Sha256Checksum,
                remainingLifetime);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
            return new PhotoUploadIssueResponse(current.Id, current.Id, current.S3Key, post.Url, post.Fields, post.ExpiresAtUtc);
        }).ConfigureAwait(false);
    }

    public async Task<CommitPhotoMetadataResponse> CommitPhotoMetadataAsync(
        Guid userId,
        CommitPhotoMetadataRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        PhotoObjectKeys.RequireUpload(userId, request.PhotoId, request.S3Key);
        if (request.Date == default || request.Date > DateOnly.FromDateTime(DateTime.UtcNow) ||
            request.WeightKg is <= 0 || request.Notes?.Length > MaximumNotesCharacters ||
            request.Phase?.Length > MaximumPhaseCharacters)
            throw new ArgumentException("Photo metadata is outside supported bounds.", nameof(request));
        var nowUtc = DateTime.UtcNow;
        var intent = await uploadIntentStore.GetAsync(userId, request.PhotoId, nowUtc, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The photo upload intent is unavailable.");
        if (!string.Equals(intent.S3Key, request.S3Key, StringComparison.Ordinal))
            throw new ArgumentException("The photo key does not match the persisted upload intent.", nameof(request));
        if (intent.State == PhotoUploadIntentState.Committed)
        {
            return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
            {
                dbContext.ChangeTracker.Clear();
                await using var replayTransaction = await dbContext.Database.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
                await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
                await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
                await LockVisiblePhotoRowsAsync(userId, [request.PhotoId], cancellationToken).ConfigureAwait(false);
                var receipt = await PostgresPhotoPublicationReceiptStore.LoadAsync(dbContext, userId, request.PhotoId, cancellationToken).ConfigureAwait(false);
                var committed = await dbContext.PhotoJourneyEntries.AsNoTracking()
                    .SingleOrDefaultAsync(p => p.UserId == userId && p.Id == request.PhotoId, cancellationToken)
                    .ConfigureAwait(false);
                if (receipt is not null && committed is not null && committed.ImagePath == receipt.Key && committed.Date == request.Date &&
                    committed.WeightKg == request.WeightKg && committed.Notes == (request.Notes ?? string.Empty) &&
                    committed.Phase == (request.Phase ?? string.Empty))
                {
                    await replayTransaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                    return new CommitPhotoMetadataResponse(true, request.PhotoId, "Photo committed successfully.");
                }
                throw new InvalidOperationException("The committed photo metadata does not match this replay.");
            }).ConfigureAwait(false);
        }
        if (intent.State != PhotoUploadIntentState.Pending || intent.ExpiresAtUtc <= nowUtc)
            throw new InvalidOperationException("The photo upload intent is no longer available.");

        var bucket = configuration["Storage:S3:BucketName"] ?? "musclecuties-media";
        var context = new PhotoUploadObjectContext(userId, intent.PhotoId, bucket, intent.S3Key,
            intent.Sha256Checksum, intent.DeclaredSizeBytes, intent.ContentType);
        S3PhotoObjectInspection inspection;
        try
        {
            inspection = await InspectUntilAvailableAsync(context, intent.ExpiresAtUtc, cancellationToken).ConfigureAwait(false);
        }
        catch (S3PhotoObjectInspectionException exception)
        {
            throw new InvalidOperationException("The uploaded photo could not be confirmed by storage.", exception);
        }
        if (inspection.ContentLength != intent.DeclaredSizeBytes || inspection.ContentLength is < 1 or > MaximumPhotoBytes)
            throw new InvalidOperationException("The uploaded photo size does not match its upload intent.");
        if (!string.Equals(inspection.ContentType, intent.ContentType, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The uploaded photo content type does not match its upload intent.");
        if (!string.Equals(inspection.UploadIntentId, intent.PhotoId.ToString("D"), StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("The uploaded photo does not match its upload intent.");
        byte[] measuredChecksum;
        try { measuredChecksum = Convert.FromBase64String(inspection.Sha256ChecksumBase64); }
        catch (FormatException exception) { throw new InvalidOperationException("S3 returned an invalid checksum.", exception); }
        var expectedChecksum = Convert.FromHexString(intent.Sha256Checksum);
        if (measuredChecksum.Length != expectedChecksum.Length ||
            !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(measuredChecksum, expectedChecksum))
            throw new InvalidOperationException("The uploaded photo checksum does not match its upload intent.");
        if (string.IsNullOrWhiteSpace(inspection.VersionId) || inspection.VersionId == "null")
            throw new InvalidOperationException("The uploaded photo must have an S3 version ID.");
        SanitizedPhotoBytes sanitized;
        try
        {
            var rawBytes = await objectInspector.ReadVersionAsync(context, inspection.VersionId, cancellationToken).ConfigureAwait(false);
            if (rawBytes.Length != intent.DeclaredSizeBytes ||
                !System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(
                    System.Security.Cryptography.SHA256.HashData(rawBytes.Span), expectedChecksum))
                throw new InvalidOperationException("The measured photo bytes do not match the persisted upload intent.");
            sanitized = (photoByteSanitizer ?? new SkiaPhotoByteSanitizer()).Sanitize(rawBytes, cancellationToken);
        }
        catch (Exception exception) when (exception is S3PhotoObjectInspectionException or InvalidDataException)
        { throw new InvalidOperationException("The uploaded photo could not be safely prepared for publication.", exception); }

        return await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<CommitPhotoMetadataResponse>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);
            var transitioned = await dbContext.PhotoUploadIntents
                .Where(i => i.UserId == userId && i.Id == intent.PhotoId && i.S3Key == intent.S3Key &&
                            i.State == PhotoUploadIntentState.Pending.ToString() && i.ExpiresAtUtc > DateTime.UtcNow)
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.State, PhotoUploadIntentState.Committed.ToString()), cancellationToken)
                .ConfigureAwait(false);
            if (transitioned == 0)
            {
                var currentState = await dbContext.PhotoUploadIntents.AsNoTracking()
                    .Where(i => i.UserId == userId && i.Id == intent.PhotoId)
                    .Select(i => i.State)
                    .SingleOrDefaultAsync(cancellationToken)
                    .ConfigureAwait(false);
                var replay = await dbContext.PhotoJourneyEntries.AsNoTracking()
                    .SingleOrDefaultAsync(p => p.UserId == userId && p.Id == request.PhotoId, cancellationToken)
                    .ConfigureAwait(false);
                var receipt = await PostgresPhotoPublicationReceiptStore.LoadAsync(dbContext, userId, request.PhotoId, cancellationToken).ConfigureAwait(false);
                var same = currentState == PhotoUploadIntentState.Committed.ToString() && replay is not null &&
                           receipt is not null && replay.ImagePath == receipt.Key && receipt.Sha256Checksum == sanitized.Sha256 && replay.Date == request.Date &&
                           replay.WeightKg == request.WeightKg && replay.Notes == (request.Notes ?? string.Empty) &&
                           replay.Phase == (request.Phase ?? string.Empty);
                if (!same)
                    throw new InvalidOperationException("The photo upload intent could not be committed.");
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return new CommitPhotoMetadataResponse(true, request.PhotoId, "Photo committed successfully.");
            }

            // The intent row remains locked until publication and metadata commit finish.
            // A failed transaction leaves a Pending intent whose expiry cleanup covers both keys.
            S3PhotoPublication publication;
            try { publication = await objectInspector.PublishAsync(context, sanitized, cancellationToken).ConfigureAwait(false); }
            catch (S3PhotoObjectInspectionException exception)
            { throw new InvalidOperationException("The sanitized photo could not be published.", exception); }
            if (publication.Key != PhotoObjectKeys.Published(userId, request.PhotoId) ||
                string.IsNullOrWhiteSpace(publication.VersionId) || publication.VersionId == "null" ||
                publication.Sha256Checksum != sanitized.Sha256)
                throw new InvalidOperationException("The publication receipt did not match the sanitized photo.");
            await PostgresPhotoPublicationReceiptStore.SaveAsync(dbContext, context, publication, cancellationToken).ConfigureAwait(false);

            dbContext.PhotoJourneyEntries.Add(new PhotoJourneyEntryRecord
            {
                Id = request.PhotoId,
                UserId = userId,
                Date = request.Date,
                ImagePath = publication.Key,
                WeightKg = request.WeightKg,
                Notes = request.Notes ?? string.Empty,
                Phase = request.Phase ?? string.Empty,
                CreatedAt = DateTime.UtcNow
            });

            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new CommitPhotoMetadataResponse(true, request.PhotoId, "Photo committed successfully.");
        }).ConfigureAwait(false);
    }

    private async Task<S3PhotoObjectInspection> InspectUntilAvailableAsync(
        PhotoUploadObjectContext context, DateTime expiresAtUtc, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try { return await objectInspector.InspectAsync(context, cancellationToken).ConfigureAwait(false); }
            catch (S3PhotoObjectInspectionException exception) when (
                exception.Failure is S3PhotoObjectInspectionFailure.NotFound or S3PhotoObjectInspectionFailure.Transient &&
                attempt < MaximumObjectInspectionAttempts && DateTime.UtcNow < expiresAtUtc)
            {
                var delay = TimeSpan.FromMilliseconds(InspectionRetryDelayMilliseconds * attempt);
                var remaining = expiresAtUtc - DateTime.UtcNow;
                if (remaining <= TimeSpan.Zero) throw new InvalidOperationException("The photo upload intent expired before S3 confirmation.");
                await Task.Delay(delay < remaining ? delay : remaining, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task<PhotoHistoryResponse> GetPhotoHistoryAsync(
        Guid userId,
        int limit,
        int offset,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<PhotoHistoryResponse>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            var query = dbContext.PhotoJourneyEntries.AsNoTracking()
                .Where(p => p.UserId == userId && p.ImagePath.StartsWith("published-photos/"));

            var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

            var entries = await query
                .OrderByDescending(p => p.Date)
                .ThenByDescending(p => p.CreatedAt)
                .Skip(offset)
                .Take(limit)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            await LockVisiblePhotoRowsAsync(userId, entries.Select(entry => entry.Id).ToArray(), cancellationToken).ConfigureAwait(false);
            var lockedIds = entries.Select(entry => entry.Id).ToArray();
            entries = lockedIds.Length == 0
                ? []
                : await dbContext.PhotoJourneyEntries.AsNoTracking()
                    .Where(p => p.UserId == userId && lockedIds.Contains(p.Id))
                    .ToListAsync(cancellationToken).ConfigureAwait(false);

            var expiration = TimeSpan.FromMinutes(DefaultPresignedExpirationMinutes);
            var photos = new List<PhotoItemDto>();
            foreach (var photo in entries.OrderByDescending(p => p.Date).ThenByDescending(p => p.CreatedAt))
            {
                var receipt = await PostgresPhotoPublicationReceiptStore.LoadAsync(dbContext, userId, photo.Id, cancellationToken).ConfigureAwait(false);
                if (receipt is null || photo.ImagePath != receipt.Key) continue;
                photos.Add(new PhotoItemDto(photo.Id, photo.Date, receipt.Key,
                    presignedUrlService.GeneratePresignedDownloadUrl(receipt.Key, receipt.VersionId, expiration),
                    photo.WeightKg, photo.Notes, photo.Phase, photo.CreatedAt));
            }

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new PhotoHistoryResponse(photos, totalCount);
        }).ConfigureAwait(false);

    public async Task<PhotoDownloadUrlResponse?> GetPhotoDownloadUrlAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync<PhotoDownloadUrlResponse?>(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            await LockVisiblePhotoRowsAsync(userId, [photoId], cancellationToken).ConfigureAwait(false);
            var photo = await dbContext.PhotoJourneyEntries.AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == userId && p.Id == photoId, cancellationToken)
                .ConfigureAwait(false);

            if (photo is null || photo.ImagePath != PhotoObjectKeys.Published(userId, photoId))
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }

            var expiration = TimeSpan.FromMinutes(DefaultPresignedExpirationMinutes);
            var receipt = await PostgresPhotoPublicationReceiptStore.LoadAsync(dbContext, userId, photoId, cancellationToken).ConfigureAwait(false);
            if (receipt is null || receipt.Key != photo.ImagePath)
            {
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return null;
            }
            var presignedUrl = presignedUrlService.GeneratePresignedDownloadUrl(receipt.Key, receipt.VersionId, expiration);
            var expiresAtUtc = DateTime.UtcNow.Add(expiration);

            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return new PhotoDownloadUrlResponse(photo.Id, presignedUrl, expiresAtUtc);
        }).ConfigureAwait(false);

    public async Task<bool> DeletePhotoAsync(
        Guid userId,
        Guid photoId,
        CancellationToken cancellationToken = default) =>
        await dbContext.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            dbContext.ChangeTracker.Clear();
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken)
                .ConfigureAwait(false);
            await PostgresTenantContext.SetForCurrentTransactionAsync(dbContext, userId, cancellationToken)
                .ConfigureAwait(false);
            await PhotoOwnerLock.AcquireAsync(dbContext, userId, cancellationToken).ConfigureAwait(false);

            var photo = await dbContext.PhotoJourneyEntries
                .SingleOrDefaultAsync(p => p.UserId == userId && p.Id == photoId, cancellationToken)
                .ConfigureAwait(false);

            if (photo is null)
            {
                var alreadyDeleted = await dbContext.PhotoUploadIntents.AsNoTracking().AnyAsync(
                    i => i.UserId == userId && i.Id == photoId && i.State == PhotoUploadIntentState.Deleted.ToString(),
                    cancellationToken).ConfigureAwait(false);
                await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
                return alreadyDeleted;
            }

            var intent = await dbContext.PhotoUploadIntents.SingleOrDefaultAsync(
                i => i.UserId == userId && i.Id == photoId && i.State == PhotoUploadIntentState.Committed.ToString(),
                cancellationToken).ConfigureAwait(false);
            if (intent is null || (photo.ImagePath != intent.S3Key && photo.ImagePath != PhotoObjectKeys.Published(userId, photoId)))
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            // Commit, delete, and cancellation serialize on this row; a committed tombstone
            // blocks any delayed Pending -> Committed transition from restoring visibility.
            var nowUtc = DateTime.UtcNow;
            var transitioned = await dbContext.PhotoUploadIntents
                .Where(i => i.UserId == userId && i.Id == photoId && i.State == PhotoUploadIntentState.Committed.ToString())
                .ExecuteUpdateAsync(setters => setters.SetProperty(i => i.State, "Deleted"), cancellationToken)
                .ConfigureAwait(false);
            if (transitioned == 0)
            {
                await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
                return false;
            }

            var notBeforeUtc = (intent.ExpiresAtUtc > nowUtc ? intent.ExpiresAtUtc : nowUtc) + UploadGrantClockSkewMargin;
            await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
                INSERT INTO app.photo_object_cleanups
                    (id, user_id, photo_id, s3_key, reason, not_before_utc, state, attempt_count,
                     lease_generation, created_at_utc)
                VALUES ({Guid.NewGuid()}, {userId}, {photoId}, {intent.S3Key}, 'CommittedPhoto',
                        {notBeforeUtc}, 'Pending', 0, 0, {nowUtc})
                ON CONFLICT (user_id, photo_id, reason) DO NOTHING
                """, cancellationToken).ConfigureAwait(false);

            // This transition immediately removes metadata from history and new URL issuance.
            // Cleanup waits for upload-grant safety; removing the object may invalidate an
            // already-issued GET URL before that URL's own signed expiry.
            dbContext.PhotoJourneyEntries.Remove(photo);
            await dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);

            return true;
        }).ConfigureAwait(false);

    private static string ResolveExtension(string contentType) => contentType.ToLowerInvariant().Trim() switch
    {
        "image/jpeg" or "image/jpg" => ".jpg",
        "image/png" => ".png",
        "image/heic" => ".heic",
        "image/heif" => ".heif",
        "image/webp" => ".webp",
        _ => ".jpg"
    };

    private async Task LockVisiblePhotoRowsAsync(Guid userId, Guid[] photoIds, CancellationToken cancellationToken)
    {
        if (photoIds.Length == 0) return;
        await using var command = new NpgsqlCommand("""
            SELECT id FROM app.photo_journey_entries
            WHERE user_id = @user_id AND id = ANY(@photo_ids)
            FOR KEY SHARE
            """, (NpgsqlConnection)dbContext.Database.GetDbConnection(),
            (NpgsqlTransaction)dbContext.Database.CurrentTransaction!.GetDbTransaction());
        command.Parameters.AddWithValue("user_id", userId);
        command.Parameters.AddWithValue("photo_ids", photoIds);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken)) { }
    }
}

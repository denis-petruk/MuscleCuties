using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.UseCases.Features.Profile.PhotoVault;
using Npgsql;

namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>Requires the serialized publication-columns migration; never falls back to an unversioned read.</summary>
public static class PostgresPhotoPublicationReceiptStore
{
    public static async Task SaveAsync(PostgresDbContext context, PhotoUploadObjectContext intent,
        S3PhotoPublication publication, CancellationToken cancellationToken)
    {
        PhotoObjectKeys.RequireUpload(intent.OwnerId, intent.PhotoId, intent.Key);
        if (publication.Key != PhotoObjectKeys.Published(intent.OwnerId, intent.PhotoId) ||
            string.IsNullOrWhiteSpace(publication.VersionId) || publication.VersionId == "null")
            throw new InvalidOperationException("The photo publication receipt is invalid.");
        await using var command = Command(context, """
            UPDATE app.photo_upload_intents
            SET published_s3_key = @key, published_version_id = @version, published_sha256_checksum = @checksum
            WHERE user_id = @owner AND id = @photo AND s3_key = @upload_key AND state = 'Committed'
              AND (published_s3_key IS NULL OR
                   (published_s3_key = @key AND published_version_id = @version AND published_sha256_checksum = @checksum))
            RETURNING id
            """);
        command.Parameters.AddWithValue("owner", intent.OwnerId);
        command.Parameters.AddWithValue("photo", intent.PhotoId);
        command.Parameters.AddWithValue("upload_key", intent.Key);
        command.Parameters.AddWithValue("key", publication.Key);
        command.Parameters.AddWithValue("version", publication.VersionId);
        command.Parameters.AddWithValue("checksum", publication.Sha256Checksum);
        if (await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false) is null)
            throw new InvalidOperationException("The immutable publication receipt could not be recorded.");
    }

    public static async Task<S3PhotoPublication?> LoadAsync(PostgresDbContext context, Guid ownerId, Guid photoId,
        CancellationToken cancellationToken)
    {
        await using var command = Command(context, """
            SELECT published_s3_key, published_version_id, published_sha256_checksum
            FROM app.photo_upload_intents
            WHERE user_id = @owner AND id = @photo AND state = 'Committed'
              AND published_s3_key IS NOT NULL AND published_version_id IS NOT NULL AND published_sha256_checksum IS NOT NULL
            """);
        command.Parameters.AddWithValue("owner", ownerId);
        command.Parameters.AddWithValue("photo", photoId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false)) return null;
        var receipt = new S3PhotoPublication(reader.GetString(0), reader.GetString(1), reader.GetString(2));
        if (receipt.Key != PhotoObjectKeys.Published(ownerId, photoId) ||
            string.IsNullOrWhiteSpace(receipt.VersionId) || receipt.VersionId == "null")
            throw new InvalidOperationException("The persisted photo publication receipt is invalid.");
        return receipt;
    }

    private static NpgsqlCommand Command(PostgresDbContext context, string sql)
    {
        var transaction = context.Database.CurrentTransaction
            ?? throw new InvalidOperationException("Photo publication receipts require an active tenant transaction.");
        return new NpgsqlCommand(sql, (NpgsqlConnection)context.Database.GetDbConnection(),
            (NpgsqlTransaction)transaction.GetDbTransaction());
    }
}

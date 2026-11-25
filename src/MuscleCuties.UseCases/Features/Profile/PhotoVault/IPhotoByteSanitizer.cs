namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

/// <summary>Image bytes with their metadata stripped, so location data never leaves the device.</summary>
public sealed class SanitizedPhotoBytes
{
    private readonly byte[] _bytes;

    /// <summary>Takes ownership of the re-encoded bytes, which no longer carry the original metadata.</summary>
    public SanitizedPhotoBytes(byte[] bytes, string contentType, string sha256)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _bytes = (byte[])bytes.Clone();
        ContentType = contentType;
        var calculatedHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(_bytes)).ToLowerInvariant();
        if (!string.Equals(calculatedHash, sha256, StringComparison.Ordinal))
            throw new ArgumentException("The checksum must match the supplied bytes.", nameof(sha256));
        Sha256 = calculatedHash;
    }

    /// <summary>A copy of the bytes, so a caller cannot mutate the stored image.</summary>
    public byte[] Bytes => (byte[])_bytes.Clone();
    /// <summary>Media type detected for the sanitized bytes.</summary>
    public string ContentType { get; }
    /// <summary>Hash of the sanitized bytes, used to verify the upload.</summary>
    public string Sha256 { get; }
}

/// <summary>Re-encodes uploaded bytes so EXIF is gone before they are stored.</summary>
public interface IPhotoByteSanitizer
{
    SanitizedPhotoBytes Sanitize(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default);
}

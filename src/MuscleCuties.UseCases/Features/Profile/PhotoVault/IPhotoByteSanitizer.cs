namespace MuscleCuties.UseCases.Features.Profile.PhotoVault;

public sealed class SanitizedPhotoBytes
{
    private readonly byte[] _bytes;

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

    public byte[] Bytes => (byte[])_bytes.Clone();
    public string ContentType { get; }
    public string Sha256 { get; }
}

public interface IPhotoByteSanitizer
{
    SanitizedPhotoBytes Sanitize(ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default);
}

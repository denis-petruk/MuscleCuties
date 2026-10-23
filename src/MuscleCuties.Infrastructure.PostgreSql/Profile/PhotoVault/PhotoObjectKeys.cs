namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

/// <summary>Disjoint namespaces keep upload grants away from server-published objects.</summary>
public static class PhotoObjectKeys
{
    private const int KeySegmentCount = 3;
    private const int PhotoIdSegment = 2;
    public static string Upload(Guid ownerId, Guid photoId) => $"photos/{ownerId:D}/{photoId:N}";
    public static string Published(Guid ownerId, Guid photoId) => $"published-photos/{ownerId:D}/{photoId:N}";

    public static void RequireUpload(Guid ownerId, Guid photoId, string key)
    {
        if (ownerId == Guid.Empty || photoId == Guid.Empty || key != Upload(ownerId, photoId))
            throw new ArgumentException("The object key must match its owner and upload intent.", nameof(key));
    }

    public static Guid RequireUpload(string key)
    {
        var segments = key?.Split('/');
        if (segments is not { Length: KeySegmentCount } || segments[0] != "photos" ||
            !Guid.TryParseExact(segments[1], "D", out var owner) ||
            !Guid.TryParseExact(segments[PhotoIdSegment], "N", out var photo))
            throw new ArgumentException("An owner-bound photo upload key is required.", nameof(key));
        RequireUpload(owner, photo, key!);
        return photo;
    }
}

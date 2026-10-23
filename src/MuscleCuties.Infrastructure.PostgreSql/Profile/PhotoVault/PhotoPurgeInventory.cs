namespace MuscleCuties.Infrastructure.PostgreSql.Profile.PhotoVault;

public sealed record PhotoPurgeCandidate(Guid PhotoId, string Key, DateTime NotBeforeUtc);
public sealed record PhotoPurgeTarget(Guid PhotoId, string Key, DateTime NotBeforeUtc);

/// <summary>Builds a bounded-key purge inventory without widening the authenticated owner's scope.</summary>
public static class PhotoPurgeInventory
{
    private static readonly TimeSpan UploadGrantSafetyMargin = TimeSpan.FromMinutes(5);

    public static IReadOnlyList<PhotoPurgeTarget> Build(Guid ownerId, IEnumerable<PhotoPurgeCandidate> candidates, DateTime nowUtc)
    {
        if (ownerId == Guid.Empty || nowUtc.Kind != DateTimeKind.Utc)
            throw new ArgumentException("An owner and UTC purge timestamp are required.");
        ArgumentNullException.ThrowIfNull(candidates);
        var dueByPhoto = new Dictionary<Guid, DateTime>();
        foreach (var candidate in candidates)
        {
            if (candidate.PhotoId == Guid.Empty || candidate.NotBeforeUtc.Kind != DateTimeKind.Utc ||
                (candidate.Key != PhotoObjectKeys.Upload(ownerId, candidate.PhotoId) &&
                 candidate.Key != PhotoObjectKeys.Published(ownerId, candidate.PhotoId)))
                throw new InvalidOperationException("The photo purge inventory is not bound to the authenticated owner.");
            var due = candidate.NotBeforeUtc > nowUtc ? candidate.NotBeforeUtc : nowUtc;
            if (!dueByPhoto.TryGetValue(candidate.PhotoId, out var previous) || due > previous)
                dueByPhoto[candidate.PhotoId] = due;
        }
        return dueByPhoto.SelectMany(pair => new[]
        {
            new PhotoPurgeTarget(pair.Key, PhotoObjectKeys.Upload(ownerId, pair.Key), pair.Value + UploadGrantSafetyMargin),
            new PhotoPurgeTarget(pair.Key, PhotoObjectKeys.Published(ownerId, pair.Key), pair.Value + UploadGrantSafetyMargin)
        }).ToArray();
    }
}

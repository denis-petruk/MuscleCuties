using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;

namespace MuscleCuties.Core.Services.Profile;

internal static class ProfileMediaDeletion
{
    public static async Task DeleteIfUnreferencedAsync(
        AppDatabase db, string path, int? excludedPhotoId = null, int? excludedProfileUserId = null)
    {
        if (string.IsNullOrWhiteSpace(path)) return;
        var normalized = ProfileMediaPaths.Normalize(path);
        var photoPaths = await db.PhotoJourneyEntries.AsNoTracking()
            .Where(p => excludedPhotoId == null || p.Id != excludedPhotoId)
            .Select(p => p.ImagePath).ToListAsync();
        var profilePaths = await db.UserProfiles.AsNoTracking()
            .Where(p => excludedProfileUserId == null || p.UserId != excludedProfileUserId)
            .Select(p => p.ProfileImagePath).ToListAsync();
        var snapshots = await db.UserProfileSnapshots.AsNoTracking().Select(s => s.ProfileJson).ToListAsync();
        var snapshotPaths = snapshots.Select(ProfileMediaPaths.SnapshotImagePath).OfType<string>();
        if (photoPaths.Concat(profilePaths).Concat(snapshotPaths).Where(p => !string.IsNullOrWhiteSpace(p))
            .Any(p => string.Equals(ProfileMediaPaths.Normalize(p), normalized, StringComparison.OrdinalIgnoreCase)))
            return;

        var databasePath = db.Database.GetDbConnection().DataSource;
        if (string.IsNullOrWhiteSpace(databasePath) || databasePath == ":memory:")
            throw new IOException("Photo deletion requires an app storage location.");
        databasePath = ProfileMediaPaths.Normalize(Path.GetFullPath(databasePath));
        var root = Path.GetDirectoryName(databasePath);
        if (!ProfileMediaPaths.IsWithinRoot(normalized, root) ||
            new[] { "", "-wal", "-shm", "-journal" }.Any(suffix =>
                string.Equals(normalized, databasePath + suffix, StringComparison.OrdinalIgnoreCase)))
            throw new IOException("The image reference is not a local photo.");

        // Missing files are already deleted. Access errors and directories must
        // propagate before the caller removes the last recoverable reference.
        File.Delete(normalized);
    }
}

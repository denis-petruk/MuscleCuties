#if IOS || MACCATALYST
using Foundation;
#endif

namespace MuscleCuties.UI.Services.Profile;

internal static class ProfileMediaProtection
{
    public static void Apply(string path)
    {
#if IOS || MACCATALYST
        using var url = NSUrl.FromFilename(path);
        using var excluded = NSObject.FromObject(true);
        if (!url.SetResource(NSUrl.IsExcludedFromBackupKey, excluded, out var backupError))
            throw new IOException($"Unable to exclude photo storage from backup: {backupError?.LocalizedDescription}");
#endif
#if IOS
        if (!NSFileManager.DefaultManager.SetAttributes(
                new NSFileAttributes { ProtectionKey = NSFileProtection.Complete }, path, out var protectionError))
            throw new IOException($"Unable to protect photo storage: {protectionError?.LocalizedDescription}");
#endif
        // Android stores these files in the app's private directory, with the
        // profile/vault directories excluded by data_extraction_rules.xml.
        // Desktop builds retain normal OS/user storage protection; the UI does
        // not promise individual encryption or universal backup prevention.
    }
}

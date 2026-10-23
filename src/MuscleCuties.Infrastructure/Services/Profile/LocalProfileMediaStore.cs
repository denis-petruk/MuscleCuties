namespace MuscleCuties.Core.Services.Profile;

/// <summary>
///     Stores renderable image files in app-private storage. Protection is supplied
///     by the platform; these files are deliberately not individually encrypted.
/// </summary>
public sealed class LocalProfileMediaStore(string rootDirectory, Action<string> protectPath)
{
    public async Task<string> StoreAsync(Stream source, string folder, string extension, CancellationToken ct = default)
    {
        var directory = GetDirectory(folder);
        Directory.CreateDirectory(directory);
        protectPath(directory);
        extension = extension.ToLowerInvariant();
        if (extension is not (".jpg" or ".jpeg" or ".png" or ".heic" or ".heif" or ".webp" or ".gif"))
            throw new ArgumentException("Unsupported image extension.", nameof(extension));
        var path = Path.Combine(directory, $"{folder}_{Guid.NewGuid():N}{extension}");
        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.CreateNew, Access = FileAccess.Write, Share = FileShare.None,
                Options = FileOptions.Asynchronous
            };
            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
            await using (var destination = new FileStream(path, options))
            {
                // Set backup/protection attributes while the file is still empty.
                protectPath(path);
                await source.CopyToAsync(destination, ct);
                await destination.FlushAsync(ct);
            }

            protectPath(path);
            return path;
        }
        catch
        {
            File.Delete(path);
            throw;
        }
    }

    // Preserve legacy references and bytes; apply protection in place. Missing
    // photos are tolerated, while real access/protection errors remain visible.
    public void ProtectExistingFiles()
    {
        foreach (var folder in new[] { "profile", "vault" })
        {
            var directory = GetDirectory(folder);
            if (!Directory.Exists(directory)) continue;
            protectPath(directory);
            foreach (var path in Directory.EnumerateFiles(directory))
            {
                if (!ProfileMediaPaths.IsWithinRoot(path, directory))
                    throw new IOException("A photo reference points outside its storage directory.");
                protectPath(path);
            }
        }
    }

    private string GetDirectory(string folder)
    {
        if (folder is not ("profile" or "vault"))
            throw new ArgumentException("Unknown photo storage directory.", nameof(folder));
        var directory = Path.Combine(rootDirectory, folder);
        if (!ProfileMediaPaths.IsWithinRoot(directory, rootDirectory))
            throw new IOException("Photo storage points outside app storage.");
        return directory;
    }
}

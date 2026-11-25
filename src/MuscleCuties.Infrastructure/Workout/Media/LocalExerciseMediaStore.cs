using MuscleCuties.Core.Services.ExerciseMedia;

using MuscleCuties.Infrastructure.Services.Media;

namespace MuscleCuties.Infrastructure.Workout.Media;

/// <summary>
///     Stores exercise vector graphics (SVGs) and animations (JSON Lottie) in local app storage.
///     Enforces asynchronous streaming, extension validation, and traversal prevention.
/// </summary>
public sealed class LocalExerciseMediaStore : IExerciseMediaStore
{
    private readonly Action<string>? _protectPath;

    /// <summary>Keeps downloaded exercise media under one directory, so a stale file can be replaced without touching the</summary>
    /// rest.</summary>
    public LocalExerciseMediaStore(string rootDirectory, Action<string>? protectPath = null)
    {
        RootDirectory = ExerciseMediaPaths.Normalize(rootDirectory);
        _protectPath = protectPath;
    }

    /// <summary>Directory the media files are stored under.</summary>
    public string RootDirectory { get; }

    /// <summary>Stores an exercise's vector artwork and returns the path it was written to.</summary>
    public Task<string> SaveVectorSvgAsync(Stream sourceStream, string fileName, CancellationToken ct = default)
    {
        return SaveMediaAsync(sourceStream, ExerciseMediaPaths.SvgFolder, fileName, ".svg", ct);
    }

    /// <summary>Stores an exercise's animated artwork and returns its path.</summary>
    public Task<string> SaveLottieJsonAsync(Stream sourceStream, string fileName, CancellationToken ct = default)
    {
        return SaveMediaAsync(sourceStream, ExerciseMediaPaths.LottieFolder, fileName, ".json", ct);
    }

    /// <summary>Whether a media file is already on the device, which avoids downloading it twice.</summary>
    public bool Exists(string subfolder, string fileName)
    {
        var path = GetLocalFilePath(subfolder, fileName);
        return File.Exists(path);
    }

    /// <summary>The full path a media file would have, inside the store's root.</summary>
    public string GetLocalFilePath(string subfolder, string fileName)
    {
        var sanitizedFolder = ExerciseMediaPaths.SanitizeFileName(subfolder);
        var sanitizedName = ExerciseMediaPaths.SanitizeFileName(Path.GetFileNameWithoutExtension(fileName)) +
                            Path.GetExtension(fileName);
        var directory = Path.Combine(RootDirectory, sanitizedFolder);
        var path = Path.Combine(directory, sanitizedName);

        if (!MediaPathSafety.IsWithinRoot(path, RootDirectory))
            throw new IOException("Exercise media path points outside root directory.");

        return path;
    }

    /// <summary>Deletes a media file, ignoring a path that is not inside the store.</summary>
    public Task<bool> DeleteMediaAsync(string localPath)
    {
        if (string.IsNullOrWhiteSpace(localPath) || !MediaPathSafety.IsWithinRoot(localPath, RootDirectory))
            return Task.FromResult(false);

        try
        {
            if (File.Exists(localPath))
            {
                File.Delete(localPath);
                return Task.FromResult(true);
            }
        }
        catch (IOException)
        {
            return Task.FromResult(false);
        }
        catch (UnauthorizedAccessException)
        {
            return Task.FromResult(false);
        }

        return Task.FromResult(false);
    }

    /// <summary>Writes a media file and returns its path.</summary>
    private async Task<string> SaveMediaAsync(
        Stream source,
        string folder,
        string fileName,
        string expectedExtension,
        CancellationToken ct)
    {
        var directory = Path.Combine(RootDirectory, folder);
        Directory.CreateDirectory(directory);
        _protectPath?.Invoke(directory);

        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (string.IsNullOrEmpty(extension))
            extension = expectedExtension;

        if (!extension.Equals(expectedExtension, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Expected extension '{expectedExtension}', got '{extension}'.",
                nameof(fileName));

        var sanitizedBase = ExerciseMediaPaths.SanitizeFileName(Path.GetFileNameWithoutExtension(fileName));
        var targetFileName = $"{sanitizedBase}{extension}";
        var path = Path.Combine(directory, targetFileName);

        if (!MediaPathSafety.IsWithinRoot(path, RootDirectory))
            throw new IOException("Exercise media path points outside app storage.");

        try
        {
            var options = new FileStreamOptions
            {
                Mode = FileMode.Create,
                Access = FileAccess.Write,
                Share = FileShare.None,
                Options = FileOptions.Asynchronous
            };

            if (!OperatingSystem.IsWindows())
                options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;

            await using (var destination = new FileStream(path, options))
            {
                _protectPath?.Invoke(path);
                await source.CopyToAsync(destination, ct).ConfigureAwait(false);
                await destination.FlushAsync(ct).ConfigureAwait(false);
            }

            _protectPath?.Invoke(path);
            return path;
        }
        catch
        {
            if (File.Exists(path))
                File.Delete(path);
            throw;
        }
    }
}

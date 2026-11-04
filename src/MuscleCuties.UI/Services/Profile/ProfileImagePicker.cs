using MuscleCuties.Core.Services.Profile;

namespace MuscleCuties.UI.Services.Profile;

internal static class ProfileImagePicker
{
    private static LocalProfileMediaStore CreateStore()
    {
        return new LocalProfileMediaStore(FileSystem.AppDataDirectory, ProfileMediaProtection.Apply);
    }

    public static Task ProtectExistingAsync(CancellationToken cancellationToken = default)
    {
        return Task.Run(() => CreateStore().ProtectExistingFiles(), cancellationToken);
    }

    public static Task<string> StoreAsync(Stream source, string folder, string extension,
        CancellationToken cancellationToken = default)
    {
        return CreateStore().StoreAsync(source, folder, extension, cancellationToken);
    }

    public static async Task<string?> PickAndStoreAsync(string subfolder = "profile", bool uniqueFileName = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        // Every caller keeps a single photo, so ask the platform for exactly one. The
        // multi-select replacement returns no results at all on some iOS versions, which reads
        // as a button that does nothing, so the deprecated single-photo picker is retained
        // deliberately rather than reintroducing that dead-button defect.
#pragma warning disable CS0618 // Type or member is obsolete
        var result = await MediaPicker.Default.PickPhotoAsync(new MediaPickerOptions
        {
            Title = uniqueFileName ? "Choose progress photo" : "Choose profile image"
        }).WaitAsync(cancellationToken);
#pragma warning restore CS0618
        cancellationToken.ThrowIfCancellationRequested();

        if (result is null)
            return null;

        await ProtectExistingAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        await using var source = await result.OpenReadAsync();
        cancellationToken.ThrowIfCancellationRequested();
        if (subfolder == "vault")
        {
            var sanitized = await ProgressPhotoSanitizer.ToJpegAsync(source, cancellationToken);
            await using var jpeg = new MemoryStream(sanitized, writable: false);
            return await StoreAsync(jpeg, subfolder, ProgressPhotoSanitizer.OutputExtension, cancellationToken);
        }

        cancellationToken.ThrowIfCancellationRequested();
        var extension = Path.GetExtension(result.FileName);
        if (string.IsNullOrWhiteSpace(extension)) extension = ".jpg";
        return await StoreAsync(source, subfolder, extension, cancellationToken);
    }
}

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
        var pickerTask = MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
        {
            Title = uniqueFileName ? "Choose progress photo" : "Choose profile image"
        });
        var results = await pickerTask.WaitAsync(cancellationToken);
        cancellationToken.ThrowIfCancellationRequested();
        var selectedPhotos = results.ToList();
        var result = selectedPhotos.FirstOrDefault();

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

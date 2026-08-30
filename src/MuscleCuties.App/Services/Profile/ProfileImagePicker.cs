namespace MuscleCuties.App.Services.Profile;

internal static class ProfileImagePicker
{
    public static async Task<string?> PickAndStoreAsync()
    {
        var results = await MediaPicker.Default.PickPhotosAsync(new MediaPickerOptions
        {
            Title = "Choose profile image"
        });
        var result = results.FirstOrDefault();

        if (result is null)
            return null;

        var extension = Path.GetExtension(result.FileName);
        if (string.IsNullOrWhiteSpace(extension))
            extension = ".jpg";

        var folder = Path.Combine(FileSystem.AppDataDirectory, "profile");
        Directory.CreateDirectory(folder);
        var destinationPath = Path.Combine(folder, $"profile_avatar{extension.ToLowerInvariant()}");

        await using var source = await result.OpenReadAsync();
        await using var destination = File.Create(destinationPath);
        await source.CopyToAsync(destination);

        return destinationPath;
    }
}

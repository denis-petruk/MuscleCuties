namespace MuscleCuties.App.Services.Profile;

internal static class ProfileImageFilePicker
{
    public static async Task<string?> PickAndStoreAsync()
    {
        var result = await FilePicker.Default.PickAsync(new PickOptions
        {
            PickerTitle = "Choose profile image",
            FileTypes = FilePickerFileType.Images
        });

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

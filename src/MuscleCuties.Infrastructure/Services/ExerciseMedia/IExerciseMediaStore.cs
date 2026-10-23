namespace MuscleCuties.Core.Services.ExerciseMedia;

public interface IExerciseMediaStore
{
    string RootDirectory { get; }
    Task<string> SaveVectorSvgAsync(Stream sourceStream, string fileName, CancellationToken ct = default);
    Task<string> SaveLottieJsonAsync(Stream sourceStream, string fileName, CancellationToken ct = default);
    bool Exists(string subfolder, string fileName);
    string GetLocalFilePath(string subfolder, string fileName);
    Task<bool> DeleteMediaAsync(string localPath);
}

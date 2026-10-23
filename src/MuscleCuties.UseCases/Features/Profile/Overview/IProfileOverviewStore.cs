namespace MuscleCuties.UseCases.Features.Profile.Overview;

public interface IProfileOverviewStore
{
    Task<ProfileOverviewSource> LoadAsync(CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task<string?> UpdateProfileImageAsync(string? imagePath, CancellationToken cancellationToken);
}

public interface IProfilePurgePort
{
    Task<ProfilePurgeResult> PurgeCurrentAccountAsync(CancellationToken cancellationToken);
}

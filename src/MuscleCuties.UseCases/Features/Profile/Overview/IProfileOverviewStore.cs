namespace MuscleCuties.UseCases.Features.Profile.Overview;

/// <summary>The profile overview's read.</summary>
public interface IProfileOverviewStore
{
    Task<ProfileOverviewSource> LoadAsync(CancellationToken cancellationToken);
    Task LogoutAsync(CancellationToken cancellationToken);
    Task<string?> UpdateProfileImageAsync(string? imagePath, CancellationToken cancellationToken);
}

/// <summary>The purge the account deletion path calls.</summary>
public interface IProfilePurgePort
{
    Task<ProfilePurgeResult> PurgeCurrentAccountAsync(CancellationToken cancellationToken);
}

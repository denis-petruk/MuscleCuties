using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PersonalInfo;

/// <summary>The personal-info read and versioned write.</summary>
public interface IProfilePersonalInfoStore
{
    Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken);
    Task<bool> SaveWithSnapshotAsync(SavePersonalInfoRequest request, CancellationToken cancellationToken);

    Task<ChangeAccountPasswordResult> ChangePasswordWithSnapshotAsync(
        ChangeAccountPasswordRequest request, CancellationToken cancellationToken);
}

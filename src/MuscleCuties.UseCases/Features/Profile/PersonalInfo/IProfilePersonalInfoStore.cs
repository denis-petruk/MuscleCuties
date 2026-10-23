using MuscleCuties.UseCases.Features.Profile.PersonalInfo.Contracts;

namespace MuscleCuties.UseCases.Features.Profile.PersonalInfo;

public interface IProfilePersonalInfoStore
{
    Task<LoadPersonalInfoResult> LoadAsync(CancellationToken cancellationToken);
    Task<bool> SaveWithSnapshotAsync(SavePersonalInfoRequest request, CancellationToken cancellationToken);

    Task<ChangeAccountPasswordResult> ChangePasswordWithSnapshotAsync(
        ChangeAccountPasswordRequest request, CancellationToken cancellationToken);
}

using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.UI.Services.Profile;

public sealed class CurrentProfileUserIdProvider(IAuthService authService) : ICurrentProfileUserIdProvider
{
    public Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return authService.GetCurrentUserIdAsync();
    }
}

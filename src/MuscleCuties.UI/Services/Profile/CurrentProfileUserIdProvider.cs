using MuscleCuties.Core.Services.Auth;
using MuscleCuties.UseCases.Features.Profile.UnitsDisplay;

namespace MuscleCuties.UI.Services.Profile;

/// <summary>Supplies the signed-in user's id to services that need it without depending on the auth service</summary>
/// themselves.</summary>
public sealed class CurrentProfileUserIdProvider(IAuthService authService) : ICurrentProfileUserIdProvider
{
    /// <summary>The signed-in user's id, as the auth service holds it.</summary>
    public Task<int> GetCurrentUserIdAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return authService.GetCurrentUserIdAsync();
    }
}

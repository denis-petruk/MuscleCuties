namespace MuscleCuties.Core.Services.Auth;

/// <summary>Where the app should boot or redirect, independent of MAUI navigation.</summary>
public enum StartupRoute
{
    Login,
    ProfileSetup,
    Dashboard,
    StartupError
}

public static class StartupRoutingRule
{
    /// <summary>
    ///     F07r: a stored session always means a returning user. Cached onboarding state must not
    ///     route an authenticated session into onboarding; an unreadable state is an error, not a login.
    /// </summary>
    public static StartupRoute Resolve(bool stateReadFailed, bool hasSession, bool onboardingIncomplete)
    {
        if (stateReadFailed) return StartupRoute.StartupError;
        if (hasSession) return StartupRoute.Dashboard;
        return onboardingIncomplete ? StartupRoute.ProfileSetup : StartupRoute.Login;
    }
}

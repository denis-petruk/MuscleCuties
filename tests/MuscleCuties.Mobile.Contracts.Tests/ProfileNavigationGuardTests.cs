using MuscleCuties.UI.ViewModels.Profile;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
///     Pins the "leave an edited profile page" decision that ProfileUnitsDisplayViewModel,
///     ProfilePersonalInfoViewModel and ProfileNutritionSettingsViewModel each carried privately:
///     navigate straight back when nothing changed, ask before discarding edits, and stay put when the
///     user chooses to keep editing.
/// </summary>
public sealed class ProfileNavigationGuardTests
{
    [Fact]
    public async Task CleanPageNavigatesBackWithoutPrompting()
    {
        var prompts = 0;
        var navigations = 0;

        var left = await ProfileNavigationGuard.TryNavigateBackAsync(false,
            () => { prompts++; return Task.FromResult(true); },
            () => { navigations++; return Task.CompletedTask; });

        Assert.True(left);
        Assert.Equal(0, prompts);
        Assert.Equal(1, navigations);
    }

    [Fact]
    public async Task EditedPageNavigatesBackWhenTheUserDiscards()
    {
        var navigations = 0;

        var left = await ProfileNavigationGuard.TryNavigateBackAsync(true,
            () => Task.FromResult(true),
            () => { navigations++; return Task.CompletedTask; });

        Assert.True(left);
        Assert.Equal(1, navigations);
    }

    [Fact]
    public async Task EditedPageStaysWhenTheUserKeepsEditing()
    {
        var navigations = 0;

        var left = await ProfileNavigationGuard.TryNavigateBackAsync(true,
            () => Task.FromResult(false),
            () => { navigations++; return Task.CompletedTask; });

        Assert.False(left);
        Assert.Equal(0, navigations);
    }
}

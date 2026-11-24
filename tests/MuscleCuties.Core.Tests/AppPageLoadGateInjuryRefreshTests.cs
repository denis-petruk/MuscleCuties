using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class AppPageLoadGateInjuryRefreshTests
{
    [Fact]
    public async Task InjuryChangePublishesStateMarksStaleAndForcesOneReload()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var page = new FakePage();
        var loads = 0;
        bool? published = null;

        await gate.RunAsync(() => { loads++; return Task.CompletedTask; }, page);
        await gate.RefreshAfterInjuryChangeAsync(
            true, value => published = value, () => { loads++; return Task.CompletedTask; }, page);

        Assert.True(published, "the injury state must reach the view model");
        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task InjuryChangeReloadsEvenWhenThePageIsStillFresh()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var page = new FakePage();
        var loads = 0;

        await gate.RunAsync(() => { loads++; return Task.CompletedTask; }, page);
        Assert.True(gate.HasLoaded);

        await gate.RefreshAfterInjuryChangeAsync(false, _ => { }, () => { loads++; return Task.CompletedTask; }, page);

        Assert.Equal(2, loads);
    }

    [Fact]
    public async Task InjuryChangePublishesStateBeforeASwallowedLoadFailure()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var page = new FakePage();
        bool? published = null;

        await gate.RefreshAfterInjuryChangeAsync(
            true,
            value => published = value,
            () => throw new InvalidOperationException("load failed"),
            page);

        Assert.True(published);
        Assert.True(page.IsLoadError);
        Assert.False(gate.HasLoaded);
    }

    private sealed class FakePage : IPageLoadAware
    {
        public bool IsPageLoading => false;
        public bool IsLoadError { get; set; }
        public AsyncRelayCommand LoadDataCommand { get; } = new(() => Task.CompletedTask);
    }
}

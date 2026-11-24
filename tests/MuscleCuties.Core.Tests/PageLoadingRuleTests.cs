using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// The full-page indicator belongs to the first load only: while that load is running the page has
/// nothing to show, but a refresh after a successful load must not blank what is already there.
/// Eight view models computed that rule from their own busy flag plus their own loaded flag; these
/// facts pin the one definition they now share.
/// </summary>
public sealed class PageLoadingRuleTests
{
    [Theory]
    [InlineData(true, false, true)]
    [InlineData(true, true, false)]
    [InlineData(false, false, false)]
    [InlineData(false, true, false)]
    public void OnlyABusyPageThatNeverLoadedShowsTheIndicator(bool isBusy, bool hasLoaded, bool expected)
    {
        Assert.Equal(expected, AppPageLoadGate.IsLoading(isBusy, hasLoaded));
    }

    [Fact]
    public async Task GateStopsReportingLoadingOnceTheFirstLoadCompletes()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var page = new FakePage();

        Assert.True(gate.IsLoading(isBusy: true), "before the first load the page is loading");

        await gate.RunAsync(() => Task.CompletedTask, page);

        Assert.False(gate.IsLoading(isBusy: true), "a refresh after a successful load must keep the page");
        Assert.False(gate.IsLoading(isBusy: false));
    }

    [Fact]
    public async Task GateReportsLoadingAgainAfterARefreshFails()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var page = new FakePage();

        await gate.RunAsync(() => Task.CompletedTask, page);
        Assert.False(gate.IsLoading(isBusy: true));

        await gate.RunAsync(() => throw new InvalidOperationException("refresh failed"), page, force: true);

        Assert.False(gate.HasLoaded);
        Assert.True(gate.IsLoading(isBusy: true), "a failed refresh leaves nothing on screen to keep");
    }

    private sealed class FakePage : IPageLoadAware
    {
        public bool IsPageLoading => false;
        public bool IsLoadError { get; set; }
        public AsyncRelayCommand LoadDataCommand { get; } = new(() => Task.CompletedTask);
    }
}

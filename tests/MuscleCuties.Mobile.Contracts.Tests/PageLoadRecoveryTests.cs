using CommunityToolkit.Mvvm.Input;
using MuscleCuties.UI.Services.Profile;
using MuscleCuties.UI.ViewModels.Common;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class PageLoadRecoveryTests
{
    [Fact]
    public async Task SuccessfulRetryClearsFailureEvenAfterAPreviouslySuccessfulLoad()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var page = new PageState();
        await gate.RunAsync(() => Task.CompletedTask, page);
        Assert.True(gate.HasLoaded);

        await gate.RunAsync(() => Task.FromException(new IOException()), page, force: true);
        Assert.True(page.IsLoadError);
        Assert.False(gate.HasLoaded);

        await gate.RunAsync(() => Task.CompletedTask, page);
        Assert.False(page.IsLoadError);
        Assert.True(gate.HasLoaded);
    }

    [Fact]
    public async Task ReportedLoadFailureCanBeRetriedWithoutWaitingForFreshnessWindow()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var page = new PageState();
        await gate.RunAsync(() =>
        {
            page.IsLoadError = true;
            return Task.CompletedTask;
        }, page);
        Assert.True(page.IsLoadError);

        await gate.RunAsync(() => Task.CompletedTask, page);
        Assert.False(page.IsLoadError);
        Assert.True(gate.HasLoaded);
    }

    [Fact]
    public async Task ChangesDuringLoadingRequireAnotherLoad()
    {
        using var gate = new AppPageLoadGate(AppPageLoadGate.PageFreshnessWindow);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var loading = gate.RunAsync(async () =>
        {
            entered.SetResult();
            await release.Task;
        });
        await entered.Task;
        gate.MarkStale();
        release.SetResult();
        await loading;
        Assert.False(gate.HasLoaded);

        await gate.RunAsync(() => Task.CompletedTask);
        Assert.True(gate.HasLoaded);
    }

    private sealed class PageState : IPageLoadAware
    {
        public bool IsPageLoading => false;
        public bool IsLoadError { get; set; }
        public AsyncRelayCommand LoadDataCommand { get; } = new(() => Task.CompletedTask);
    }
}

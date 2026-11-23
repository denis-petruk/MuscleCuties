using System.ComponentModel;
using MuscleCuties.UI.Controls.Shared;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
///     Pins the runtime contract of the one view-model subscription the daily check-in and both injury log controls
///     share: exactly one notification per change, no double subscription on rebind, and nothing after teardown.
/// </summary>
public sealed class ViewModelSubscriptionTests
{
    [Fact]
    public void RebindingTheSameViewModelNotifiesOncePerChange()
    {
        var changes = 0;
        var subscription = new ViewModelSubscription<TestViewModel>((_, _) => changes++);
        var viewModel = new TestViewModel();

        subscription.BindTo(viewModel);
        subscription.BindTo(viewModel);
        viewModel.Raise(nameof(TestViewModel.Value));

        Assert.Equal(1, changes);
        Assert.Same(viewModel, subscription.Current);
    }

    [Fact]
    public void RebindingAnotherViewModelStopsThePreviousOne()
    {
        var changes = 0;
        var subscription = new ViewModelSubscription<TestViewModel>((_, _) => changes++);
        var first = new TestViewModel();
        var second = new TestViewModel();

        subscription.BindTo(first);
        subscription.BindTo(second);

        first.Raise(nameof(TestViewModel.Value));
        Assert.Equal(0, changes);

        second.Raise(nameof(TestViewModel.Value));
        Assert.Equal(1, changes);
        Assert.Same(second, subscription.Current);
    }

    [Fact]
    public void TeardownSilencesTheViewModelAndRunsTheOwnerHook()
    {
        var changes = 0;
        var detaches = 0;
        var subscription = new ViewModelSubscription<TestViewModel>((_, _) => changes++, onDetached: () => detaches++);
        var viewModel = new TestViewModel();

        subscription.BindTo(viewModel);
        subscription.Detach();
        subscription.Detach();
        viewModel.Raise(nameof(TestViewModel.Value));

        Assert.Equal(0, changes);
        Assert.Null(subscription.Current);
        Assert.True(detaches >= 2, "teardown must stay idempotent and always reach the owner hook");
    }

    [Fact]
    public void ReleasingTheHandlerDetachesThroughTheSamePath()
    {
        var changes = 0;
        var subscription = new ViewModelSubscription<TestViewModel>((_, _) => changes++);
        var viewModel = new TestViewModel();

        subscription.BindTo(viewModel);
        subscription.BindTo(null);
        viewModel.Raise(nameof(TestViewModel.Value));

        Assert.Equal(0, changes);
        Assert.Null(subscription.Current);
    }

    [Fact]
    public void AttachHookRunsOnlyForARealViewModel()
    {
        var attaches = 0;
        var subscription = new ViewModelSubscription<TestViewModel>((_, _) => { }, onAttached: () => attaches++);

        subscription.BindTo(null);
        Assert.Equal(0, attaches);

        subscription.BindTo(new TestViewModel());
        Assert.Equal(1, attaches);

        subscription.BindTo(new object());
        Assert.Equal(1, attaches);
        Assert.Null(subscription.Current);
    }

    private sealed class TestViewModel : INotifyPropertyChanged
    {
        public string Value { get; set; } = string.Empty;

        public event PropertyChangedEventHandler? PropertyChanged;

        public void Raise(string propertyName) =>
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}

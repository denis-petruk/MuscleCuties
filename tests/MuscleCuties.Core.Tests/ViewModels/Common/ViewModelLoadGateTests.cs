using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.Tests.ViewModels.Common;

public class ViewModelLoadGateTests
{
    private static ViewModelLoadGate CreateGate(TimeSpan? freshness = null)
    {
        return new ViewModelLoadGate(freshness ?? TimeSpan.FromMinutes(2));
    }

    // --- RunAsync executes on first call ---

    [Fact]
    public async Task RunAsync_FirstCall_ExecutesLoadFunction()
    {
        var gate = CreateGate();
        var callCount = 0;

        await gate.RunAsync(() =>
        {
            callCount++;
            return Task.CompletedTask;
        });

        Assert.Equal(1, callCount);
    }

    // --- RunAsync skips when data is fresh ---

    [Fact]
    public async Task RunAsync_CalledTwiceWithinFreshnessWindow_SkipsSecondExecution()
    {
        var gate = CreateGate(TimeSpan.FromMinutes(5));
        var callCount = 0;

        Task LoadAsync()
        {
            callCount++;
            return Task.CompletedTask;
        }

        await gate.RunAsync(LoadAsync);
        await gate.RunAsync(LoadAsync);

        Assert.Equal(1, callCount);
    }

    // --- RunAsync re-executes after MarkStale ---

    [Fact]
    public async Task RunAsync_AfterMarkStale_ReExecutesLoadFunction()
    {
        var gate = CreateGate(TimeSpan.FromMinutes(5));
        var callCount = 0;

        Task LoadAsync()
        {
            callCount++;
            return Task.CompletedTask;
        }

        await gate.RunAsync(LoadAsync);
        Assert.Equal(1, callCount);

        gate.MarkStale();

        await gate.RunAsync(LoadAsync);
        Assert.Equal(2, callCount);
    }

    // --- force: true bypasses freshness check ---

    [Fact]
    public async Task RunAsync_ForceTrue_BypassesFreshnessCheck()
    {
        var gate = CreateGate(TimeSpan.FromMinutes(5));
        var callCount = 0;

        Task LoadAsync()
        {
            callCount++;
            return Task.CompletedTask;
        }

        await gate.RunAsync(LoadAsync);
        Assert.Equal(1, callCount);

        await gate.RunAsync(LoadAsync, force: true);
        Assert.Equal(2, callCount);
    }

    // --- Concurrent calls are serialized ---

    [Fact]
    public async Task RunAsync_ConcurrentCalls_SerializesExecution()
    {
        var gate = CreateGate(TimeSpan.FromMilliseconds(1));
        var concurrentCount = 0;
        var maxConcurrent = 0;

        async Task LoadAsync()
        {
            var current = Interlocked.Increment(ref concurrentCount);

            // Track the highest observed concurrency
            int snapshot;
            do
            {
                snapshot = maxConcurrent;
            } while (current > snapshot &&
                     Interlocked.CompareExchange(ref maxConcurrent, current, snapshot) != snapshot);

            await Task.Delay(50);
            Interlocked.Decrement(ref concurrentCount);
        }

        // Use force: true so each call actually enters the semaphore
        var tasks = Enumerable.Range(0, 5)
            .Select(_ => gate.RunAsync(LoadAsync, force: true))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(1, maxConcurrent);
    }

    // --- HasLoaded lifecycle ---

    [Fact]
    public void HasLoaded_Initially_ReturnsFalse()
    {
        var gate = CreateGate();

        Assert.False(gate.HasLoaded);
    }

    [Fact]
    public async Task HasLoaded_AfterSuccessfulLoad_ReturnsTrue()
    {
        var gate = CreateGate();

        await gate.RunAsync(() => Task.CompletedTask);

        Assert.True(gate.HasLoaded);
    }

    [Fact]
    public async Task HasLoaded_AfterMarkStale_ReturnsFalse()
    {
        var gate = CreateGate();

        await gate.RunAsync(() => Task.CompletedTask);
        Assert.True(gate.HasLoaded);

        gate.MarkStale();

        Assert.False(gate.HasLoaded);
    }
}

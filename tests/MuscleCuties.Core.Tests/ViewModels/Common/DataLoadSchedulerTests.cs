using MuscleCuties.Core.ViewModels.Common;

namespace MuscleCuties.Core.Tests.ViewModels.Common;

public class DataLoadSchedulerTests
{
    [Fact]
    public async Task RunAsync_WithValidAction_ExecutesFunction()
    {
        var executed = false;

        await DataLoadScheduler.RunAsync(() =>
        {
            executed = true;
            return Task.CompletedTask;
        });

        Assert.True(executed);
    }

    [Fact]
    public async Task RunAsyncGeneric_WithValidFunction_ReturnsResult()
    {
        var result = await DataLoadScheduler.RunAsync(() => Task.FromResult(42));

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_MultipleConcurrentCalls_AllComplete()
    {
        const int taskCount = 10;
        var completionCounter = 0;

        var tasks = Enumerable.Range(0, taskCount)
            .Select(_ => DataLoadScheduler.RunAsync(async () =>
            {
                await Task.Delay(50);
                Interlocked.Increment(ref completionCounter);
            }))
            .ToArray();

        await Task.WhenAll(tasks);

        Assert.Equal(taskCount, completionCounter);
    }

    [Fact]
    public async Task RunAsync_WhenFunctionThrows_PropagatesException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DataLoadScheduler.RunAsync(() =>
                throw new InvalidOperationException("test failure")));

        Assert.Equal("test failure", exception.Message);
    }

    [Fact]
    public async Task RunAsyncGeneric_WhenFunctionThrows_PropagatesException()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            DataLoadScheduler.RunAsync<int>(() =>
                throw new InvalidOperationException("test failure")));

        Assert.Equal("test failure", exception.Message);
    }
}

using MuscleCuties.Core.Data;
using Xunit;

namespace MuscleCuties.Core.Tests;

public class DatabaseOperationGateTests
{
    [Fact]
    public async Task RunAsync_ExecutesOnThreadPoolThread()
    {
        bool ranOnThreadPool = false;

        await DatabaseOperationGate.RunAsync(async () =>
        {
            ranOnThreadPool = Thread.CurrentThread.IsThreadPoolThread;
            await Task.CompletedTask;
            return true;
        });

        Assert.True(ranOnThreadPool, "Database operation must execute on a ThreadPool thread.");
    }

    [Fact]
    public async Task RunAsync_DoesNotBlockCallingThread()
    {
        var gate = new TaskCompletionSource<bool>();
        var operationStarted = new TaskCompletionSource<bool>();

        var dbTask = DatabaseOperationGate.RunAsync(async () =>
        {
            operationStarted.SetResult(true);
            await gate.Task;
            return 42;
        });

        // The calling thread must remain free while the operation is in flight.
        var startedBeforeCompletion = await operationStarted.Task;
        Assert.True(startedBeforeCompletion);
        Assert.False(dbTask.IsCompleted);

        gate.SetResult(true);
        var result = await dbTask;
        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_SerializesConsecutiveOperations()
    {
        var executionOrder = new List<int>();

        await DatabaseOperationGate.RunAsync(async () =>
        {
            executionOrder.Add(1);
            await Task.CompletedTask;
            return true;
        });

        await DatabaseOperationGate.RunAsync(async () =>
        {
            executionOrder.Add(2);
            await Task.CompletedTask;
            return true;
        });

        Assert.Equal([1, 2], executionOrder);
    }

    [Fact]
    public async Task RunAsync_PropagatesExceptions()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(async () =>
        {
            await DatabaseOperationGate.RunAsync<bool>(async () =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Test error");
            });
        });
    }

    [Fact]
    public async Task RunAsync_ReleasesGateAfterFailure()
    {
        // First call throws.
        try
        {
            await DatabaseOperationGate.RunAsync<bool>(async () =>
            {
                await Task.CompletedTask;
                throw new InvalidOperationException("Kaboom");
            });
        }
        catch (InvalidOperationException)
        {
            // Expected.
        }

        // Second call must succeed — the gate was properly released.
        var result = await DatabaseOperationGate.RunAsync(async () =>
        {
            await Task.CompletedTask;
            return 99;
        });

        Assert.Equal(99, result);
    }
}

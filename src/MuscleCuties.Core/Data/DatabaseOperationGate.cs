namespace MuscleCuties.Core.Data;

// SQLite's async provider can do work synchronously. Page loads use this gate
// to keep that work off the UI thread and to serialize local database access.
// External I/O may temporarily yield the gate; the same workflow reacquires it
// before it can use its DbContext again.
internal static class DatabaseOperationGate
{
    private sealed class Lease
    {
        public int Active = 1;
        public int Held = 1;
    }

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly AsyncLocal<Lease?> CurrentLease = new();

    internal static async Task<T> RunAsync<T>(Func<Task<T>> operation)
    {
        if (CurrentLease.Value is not null)
            throw new InvalidOperationException("Nested or concurrent database operations cannot share a gate lease.");

        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            return await Task.Run(async () =>
            {
                var lease = new Lease();
                CurrentLease.Value = lease;
                try
                {
                    return await operation().ConfigureAwait(false);
                }
                finally
                {
                    Volatile.Write(ref lease.Active, 0);
                    CurrentLease.Value = null;
                }
            }).ConfigureAwait(false);
        }
        finally
        {
            Gate.Release();
        }
    }

    internal static async Task<T> AwaitExternalAsync<T>(Func<Task<T>> operation)
    {
        var lease = CurrentLease.Value;
        if (lease is null)
            return await operation().ConfigureAwait(false);

        if (Volatile.Read(ref lease.Active) != 1)
            throw new InvalidOperationException("A completed database operation cannot yield its gate.");

        if (Interlocked.Exchange(ref lease.Held, 0) != 1)
            throw new InvalidOperationException("The database gate has already been yielded.");

        Gate.Release();
        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            await Gate.WaitAsync().ConfigureAwait(false);
            Volatile.Write(ref lease.Held, 1);
        }
    }
}

namespace MuscleCuties.Core.Data;

// The local storage provider can do work synchronously. Page loads use this gate
// to keep that work off the UI thread and to serialize local database access.
// External I/O may temporarily yield the gate; the same workflow reacquires it
// before it can use its data store again.
/// <summary>Serialises database work and yields the connection while an external call runs, because the provider allows</summary>
/// only one operation at a time and holding it across an HTTP call stalls everything.</summary>
internal static class DatabaseOperationGate
{
    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static readonly AsyncLocal<Lease?> CurrentLease = new();

    /// <summary>Runs one database operation under the gate.</summary>
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

    /// <summary>The gate's lease, which is what lets the external wait release and re-acquire in order.</summary>
    private sealed class Lease
    {
        public int Active = 1;
        public int Held = 1;
    }
}

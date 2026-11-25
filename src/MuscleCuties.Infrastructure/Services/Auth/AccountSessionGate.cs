namespace MuscleCuties.Core.Services.Auth;

/// <summary>Serializes local account selection changes with account deletion.</summary>
public sealed class AccountSessionGate
{
    private const int SingleActiveSessionChange = 1;

    /// <summary>The gate the app shares between services.</summary>
    public static AccountSessionGate Shared { get; } = new();

    private readonly SemaphoreSlim _semaphore = new(SingleActiveSessionChange, SingleActiveSessionChange);
    private long _version;

    /// <summary>The gate's version, bumped whenever an account switch happens, so work started under the old account can</summary>
    /// notice it is stale.</summary>
    public long Version => Interlocked.Read(ref _version);

    /// <summary>Records that the account session changed.</summary>
    public void MarkChanged() => Interlocked.Increment(ref _version);

    /// <summary>Waits for exclusive use, which is how two operations that must not overlap are kept apart.</summary>
    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_semaphore);
    }

    /// <summary>The lease returned to the caller, which releases the gate when it is disposed.</summary>
    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        /// <summary>Releases the gate's synchronization primitive.</summary>
        public void Dispose()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
        }
    }
}

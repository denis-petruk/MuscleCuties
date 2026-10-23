namespace MuscleCuties.Core.Services.Auth;

/// <summary>Serializes local account selection changes with account deletion.</summary>
public sealed class AccountSessionGate
{
    private const int SingleActiveSessionChange = 1;

    public static AccountSessionGate Shared { get; } = new();

    private readonly SemaphoreSlim _semaphore = new(SingleActiveSessionChange, SingleActiveSessionChange);
    private long _version;

    public long Version => Interlocked.Read(ref _version);

    public void MarkChanged() => Interlocked.Increment(ref _version);

    public async Task<IDisposable> EnterAsync(CancellationToken cancellationToken = default)
    {
        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
        return new Lease(_semaphore);
    }

    private sealed class Lease(SemaphoreSlim semaphore) : IDisposable
    {
        private SemaphoreSlim? _semaphore = semaphore;

        public void Dispose()
        {
            Interlocked.Exchange(ref _semaphore, null)?.Release();
        }
    }
}

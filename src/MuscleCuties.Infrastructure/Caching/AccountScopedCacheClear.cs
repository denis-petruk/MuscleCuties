namespace MuscleCuties.Infrastructure.Caching;

/// <summary>
/// Both encrypted caches clear the same way: take the gate, drop the active account, clear the stored
/// items, release. The order matters because a concurrent load must not re-adopt the old account.
/// </summary>
public static class AccountScopedCacheClear
{
    /// <summary>Clears every cached snapshot that belongs to the account.</summary>
    public static async Task RunAsync(
        SemaphoreSlim gate,
        Action resetActiveAccount,
        Func<CancellationToken, Task> clearCore,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(gate);
        ArgumentNullException.ThrowIfNull(resetActiveAccount);
        ArgumentNullException.ThrowIfNull(clearCore);

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            resetActiveAccount();
            await clearCore(cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            gate.Release();
        }
    }
}

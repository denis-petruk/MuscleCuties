using MuscleCuties.Infrastructure.Caching;
using MuscleCuties.UseCases.Features.Caching;

namespace MuscleCuties.Core.Tests;

public sealed class EncryptedLocalSnapshotCacheServiceTests
{
    private const int SampleDayOfCycle = 3;
    private const int SampleCycleLengthDays = 28;
    private const int SampleVersion = 7;

    [Fact]
    public async Task Snapshot_Survives_Relaunch_For_Same_Account_But_Not_Another_Account()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musclecuties-snapshot-test-{Guid.NewGuid():N}");
        var keyStore = new MemoryKeyStore();
        var firstAccount = Guid.NewGuid();
        var otherAccount = Guid.NewGuid();
        var snapshot = new CycleStateSnapshot(
            new DateOnly(2026, 10, 19), "Follicular", SampleDayOfCycle,
            SampleCycleLengthDays, "SyntheticTest", null, SampleVersion);

        try
        {
            using (var firstProcess = new EncryptedLocalSnapshotCacheService(directory, keyStore))
            {
                await firstProcess.ActivateAccountAsync(firstAccount);
                await firstProcess.SaveCycleStateSnapshotAsync(firstAccount, snapshot, DateTimeOffset.UtcNow);
            }

            using (var sameAccountRelaunch = new EncryptedLocalSnapshotCacheService(directory, keyStore))
            {
                await sameAccountRelaunch.ActivateAccountAsync(firstAccount);
                var restored = await sameAccountRelaunch.LoadCycleStateSnapshotAsync(firstAccount);
                Assert.NotNull(restored);
                Assert.Equal(snapshot, restored.Payload);
            }

            using (var differentAccountRelaunch = new EncryptedLocalSnapshotCacheService(directory, keyStore))
            {
                await differentAccountRelaunch.ActivateAccountAsync(otherAccount);
                await differentAccountRelaunch.ActivateAccountAsync(firstAccount);
                Assert.Null(await differentAccountRelaunch.LoadCycleStateSnapshotAsync(firstAccount));
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Switching_Accounts_Revokes_Access_To_The_Previous_Encrypted_Snapshot()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musclecuties-snapshot-switch-{Guid.NewGuid():N}");
        var keyStore = new MemoryKeyStore();
        var firstAccount = Guid.NewGuid();
        var secondAccount = Guid.NewGuid();
        var snapshot = new CycleStateSnapshot(
            new DateOnly(2026, 10, 19), "Follicular", SampleDayOfCycle,
            SampleCycleLengthDays, "SyntheticTest", null, SampleVersion);

        try
        {
            using var cache = new EncryptedLocalSnapshotCacheService(directory, keyStore);
            await cache.ActivateAccountAsync(firstAccount);
            await cache.SaveCycleStateSnapshotAsync(firstAccount, snapshot, DateTimeOffset.UtcNow);

            await cache.ActivateAccountAsync(secondAccount);
            await Assert.ThrowsAsync<UnauthorizedAccessException>(() =>
                cache.LoadCycleStateSnapshotAsync(firstAccount));
            Assert.Null(await cache.LoadCycleStateSnapshotAsync(secondAccount));

            await cache.ActivateAccountAsync(firstAccount);
            Assert.Null(await cache.LoadCycleStateSnapshotAsync(firstAccount));
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class MemoryKeyStore : ISnapshotCacheKeyStore
    {
        private byte[]? _key;

        public Task<byte[]?> ReadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_key?.ToArray());

        public Task WriteAsync(byte[] key, CancellationToken cancellationToken = default)
        {
            _key = key.ToArray();
            return Task.CompletedTask;
        }

        public Task RemoveAsync(CancellationToken cancellationToken = default)
        {
            _key = null;
            return Task.CompletedTask;
        }
    }
}

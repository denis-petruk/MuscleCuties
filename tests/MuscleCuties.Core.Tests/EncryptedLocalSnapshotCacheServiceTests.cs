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

    [Fact]
    public async Task Environment_Partitioning_Prevents_Cross_Environment_Snapshot_Leakage()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musclecuties-env-test-{Guid.NewGuid():N}");
        var keyStore = new MemoryKeyStore();
        var accountId = Guid.NewGuid();
        var snapshot = new CycleStateSnapshot(
            new DateOnly(2026, 10, 19), "Follicular", SampleDayOfCycle,
            SampleCycleLengthDays, "SyntheticTest", null, SampleVersion);

        try
        {
            // Save in Staging environment
            using (var stagingCache = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "staging"))
            {
                await stagingCache.ActivateAccountAsync(accountId);
                await stagingCache.SaveCycleStateSnapshotAsync(accountId, snapshot, DateTimeOffset.UtcNow);
            }

            // Attempt load in Beta environment: must be null due to directory and associated data partitioning
            using (var betaCache = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "beta"))
            {
                await betaCache.ActivateAccountAsync(accountId);
                var betaLoaded = await betaCache.LoadCycleStateSnapshotAsync(accountId);
                Assert.Null(betaLoaded);
            }

            // Load in Staging environment: succeeds
            using (var stagingRestore = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "staging"))
            {
                await stagingRestore.ActivateAccountAsync(accountId);
                var stagingLoaded = await stagingRestore.LoadCycleStateSnapshotAsync(accountId);
                Assert.NotNull(stagingLoaded);
                Assert.Equal(snapshot, stagingLoaded.Payload);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Legacy_V1_Snapshot_Is_Transparently_Decrypted_By_V2_Reader()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musclecuties-legacy-v1-test-{Guid.NewGuid():N}");
        var keyStore = new MemoryKeyStore();
        var accountId = Guid.NewGuid();
        var snapshot = new CycleStateSnapshot(
            new DateOnly(2026, 10, 19), "Follicular", SampleDayOfCycle,
            SampleCycleLengthDays, "SyntheticTest", null, SampleVersion);

        try
        {
            var key = System.Security.Cryptography.RandomNumberGenerator.GetBytes(32);
            await keyStore.WriteAsync(key);

            var envelope = new CachedSnapshotEnvelope<CycleStateSnapshot>(
                snapshot,
                DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.AddHours(12),
                SampleVersion);
            var plaintext = System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(envelope);
            var nonce = System.Security.Cryptography.RandomNumberGenerator.GetBytes(12);
            var ciphertext = new byte[plaintext.Length];
            var tag = new byte[16];
            var legacyAd = System.Text.Encoding.ASCII.GetBytes($"{accountId:N}:cycle-state:v1");
            using (var aes = new System.Security.Cryptography.AesGcm(key, 16))
            {
                aes.Encrypt(nonce, plaintext, ciphertext, tag, legacyAd);
            }
            var payload = new byte[12 + 16 + ciphertext.Length];
            nonce.CopyTo(payload, 0);
            tag.CopyTo(payload, 12);
            ciphertext.CopyTo(payload, 28);

            var legacyDir = Path.Combine(directory, "snapshot-cache");
            Directory.CreateDirectory(legacyDir);
            var legacyFile = Path.Combine(legacyDir, $"{accountId:N}_cycle-state.mcsnap");
            await File.WriteAllBytesAsync(legacyFile, payload);

            using (var v2Cache = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "staging"))
            {
                await v2Cache.ActivateAccountAsync(accountId);
                var restored = await v2Cache.LoadCycleStateSnapshotAsync(accountId);
                Assert.NotNull(restored);
                Assert.Equal(snapshot, restored.Payload);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Corrupted_Payload_Returns_Null_Safely_Without_Crashing()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musclecuties-corrupt-test-{Guid.NewGuid():N}");
        var keyStore = new MemoryKeyStore();
        var accountId = Guid.NewGuid();
        var snapshot = new CycleStateSnapshot(
            new DateOnly(2026, 10, 19), "Follicular", SampleDayOfCycle,
            SampleCycleLengthDays, "SyntheticTest", null, SampleVersion);

        try
        {
            using (var cache = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "local"))
            {
                await cache.ActivateAccountAsync(accountId);
                await cache.SaveCycleStateSnapshotAsync(accountId, snapshot, DateTimeOffset.UtcNow);
            }

            // Locate snapshot file and corrupt its bytes
            var cacheDir = Path.Combine(directory, "snapshot-cache_local");
            var file = Directory.EnumerateFiles(cacheDir, "*.mcsnap").First();
            var bytes = await File.ReadAllBytesAsync(file);
            // Invert bytes in the ciphertext
            for (var i = 16; i < bytes.Length; i++) bytes[i] ^= 0xFF;
            await File.WriteAllBytesAsync(file, bytes);

            using (var corruptCache = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "local"))
            {
                await corruptCache.ActivateAccountAsync(accountId);
                var result = await corruptCache.LoadCycleStateSnapshotAsync(accountId);
                Assert.Null(result);
            }
        }
        finally
        {
            if (Directory.Exists(directory))
                Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task Lost_Key_Returns_Null_Safely()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"musclecuties-lostkey-test-{Guid.NewGuid():N}");
        var keyStore = new MemoryKeyStore();
        var accountId = Guid.NewGuid();
        var snapshot = new CycleStateSnapshot(
            new DateOnly(2026, 10, 19), "Follicular", SampleDayOfCycle,
            SampleCycleLengthDays, "SyntheticTest", null, SampleVersion);

        try
        {
            using (var cache = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "local"))
            {
                await cache.ActivateAccountAsync(accountId);
                await cache.SaveCycleStateSnapshotAsync(accountId, snapshot, DateTimeOffset.UtcNow);
            }

            // Simulate key loss
            await keyStore.RemoveAsync();

            using (var lostKeyCache = new EncryptedLocalSnapshotCacheService(directory, keyStore, environmentName: "local"))
            {
                await lostKeyCache.ActivateAccountAsync(accountId);
                var result = await lostKeyCache.LoadCycleStateSnapshotAsync(accountId);
                Assert.Null(result);
            }
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

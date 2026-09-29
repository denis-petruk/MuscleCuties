using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Users;

namespace MuscleCuties.Core.Tests;

public class EncryptedConnectionPoolingTests
{
    [Fact]
    public async Task PooledConnection_ReopensEncryptedDatabase()
    {
        SQLitePCL.Batteries_V2.Init();
        var path = Path.Combine(Path.GetTempPath(), $"musclecuties-pool-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Password = "test-only-encryption-key",
            Cache = SqliteCacheMode.Private,
            Pooling = true
        }.ToString();

        try
        {
            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "CREATE TABLE test_data (value INTEGER); INSERT INTO test_data VALUES (42);";
                await command.ExecuteNonQueryAsync();
            }

            await using (var connection = new SqliteConnection(connectionString))
            {
                await connection.OpenAsync();
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT value FROM test_data";
                Assert.Equal(42L, await command.ExecuteScalarAsync());
            }

            var header = File.ReadAllBytes(path).AsSpan(0, 16);
            Assert.False(header.SequenceEqual("SQLite format 3\0"u8));
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ConcurrentSaves_CompleteWithoutDatabaseLocked()
    {
        SQLitePCL.Batteries_V2.Init();
        var path = Path.Combine(Path.GetTempPath(), $"musclecuties-writes-{Guid.NewGuid():N}.db");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Password = "test-only-encryption-key",
            Cache = SqliteCacheMode.Private,
            Pooling = true,
            DefaultTimeout = 1
        }.ToString();
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite(connectionString)
            .Options;

        try
        {
            await using (var setup = new AppDatabase(options))
                await setup.Database.EnsureCreatedAsync();

            await Task.WhenAll(Enumerable.Range(0, 12).Select(async index =>
            {
                await using var database = new AppDatabase(options);
                await database.Users.AddAsync(new User
                {
                    Email = $"parallel-{index}@example.test",
                    PasswordHash = "test"
                });
                await database.SaveChangesAsync();
            }));

            await using var verification = new AppDatabase(options);
            Assert.Equal(12, await verification.Users.CountAsync());
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            File.Delete(path);
        }
    }
}

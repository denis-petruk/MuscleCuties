using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Cycle;
using MuscleCuties.Core.Models.Entities.Users;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.ViewModels.Auth;

namespace MuscleCuties.Core.Tests;

public class CurrentUserStateTests
{
    [Fact]
    public async Task ExistingEmail_RoutesToPasswordLoginWithoutCreatingAnotherUser()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite(connection)
            .Options;
        await using var database = new AppDatabase(options);
        await database.Database.EnsureCreatedAsync();
        await database.Users.AddAsync(new User
        {
            Email = "returning@example.test",
            PasswordHash = "test"
        });
        await database.SaveChangesAsync();

        var authService = new AuthService(new UserRepository(database), new TestTokenStorage());
        string? loginEmail = null;
        var viewModel = new RegisterViewModel(
            authService,
            () => Task.CompletedTask,
            () => Task.CompletedTask,
            navigateToExistingLoginAsync: email =>
            {
                loginEmail = email;
                return Task.CompletedTask;
            });
        viewModel.Email = "  RETURNING@EXAMPLE.TEST  ";
        viewModel.Password = "unused password";
        viewModel.ConfirmPassword = "unused password";

        await viewModel.RegisterCommand.ExecuteAsync(null);

        Assert.Equal("returning@example.test", loginEmail);
        Assert.Empty(viewModel.Password);
        Assert.Empty(viewModel.ConfirmPassword);
        Assert.Equal(1, await database.Users.CountAsync());
        Assert.Empty(viewModel.ErrorMessage);
    }

    [Fact]
    public async Task ResolvesOnboardingStateAndClearsMissingUserToken()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite(connection)
            .Options;
        await using var database = new AppDatabase(options);
        await database.Database.EnsureCreatedAsync();
        var user = new User
        {
            Email = "auth@example.test",
            PasswordHash = "test",
            IsOnboardingComplete = true
        };
        await database.Users.AddAsync(user);
        await database.SaveChangesAsync();

        var storage = new TestTokenStorage();
        await storage.SetAsync("current_user_id", user.Id.ToString());
        var authService = new AuthService(new UserRepository(database), storage);
        Assert.Equal(new CurrentUserState(user.Id, true), await authService.GetCurrentUserStateAsync());

        await storage.SetAsync("current_user_id", "999999");
        var missingUserService = new AuthService(new UserRepository(database), storage);
        Assert.Null(await missingUserService.GetCurrentUserStateAsync());
        Assert.Null(await storage.GetAsync("current_user_id"));
    }

    private sealed class TestTokenStorage : ITokenStorage
    {
        private readonly Dictionary<string, string> _values = new();

        public Task<string?> GetAsync(string key) => Task.FromResult(_values.GetValueOrDefault(key));

        public Task SetAsync(string key, string value)
        {
            _values[key] = value;
            return Task.CompletedTask;
        }

        public void Remove(string key) => _values.Remove(key);

        public void RemoveAll() => _values.Clear();
    }
}

public class CyclePhaseNeighborQueryTests
{
    [Fact]
    public async Task NeighborQueries_IgnoreSameDayAndOtherUsers()
    {
        SQLitePCL.Batteries_V2.Init();
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<AppDatabase>()
            .UseSqlite(connection)
            .Options;
        await using var database = new AppDatabase(options);
        await database.Database.EnsureCreatedAsync();

        var firstUser = new User { Email = "first@example.test", PasswordHash = "test" };
        var otherUser = new User { Email = "other@example.test", PasswordHash = "test" };
        await database.Users.AddRangeAsync(firstUser, otherUser);
        await database.SaveChangesAsync();

        var targetDate = new DateTime(2026, 9, 28);
        var previous = new CyclePhaseLog
        {
            UserId = firstUser.Id,
            Phase = CyclePhase.Follicular,
            LoggedAt = targetDate.AddDays(-1).AddHours(20)
        };
        var next = new CyclePhaseLog
        {
            UserId = firstUser.Id,
            Phase = CyclePhase.Luteal,
            LoggedAt = targetDate.AddDays(1).AddHours(9)
        };
        await database.CyclePhaseLogs.AddRangeAsync(
            previous,
            new CyclePhaseLog { UserId = firstUser.Id, Phase = CyclePhase.Ovulatory, LoggedAt = targetDate.AddHours(8) },
            next,
            new CyclePhaseLog { UserId = otherUser.Id, Phase = CyclePhase.Menstrual, LoggedAt = targetDate.AddDays(-2) },
            new CyclePhaseLog { UserId = otherUser.Id, Phase = CyclePhase.Menstrual, LoggedAt = targetDate.AddDays(2) });
        await database.SaveChangesAsync();

        var repository = new CycleRepository(database);
        var actualPrevious = await repository.GetLatestPhaseLogBeforeDateAsync(firstUser.Id, targetDate);
        var actualNext = await repository.GetNextPhaseLogAfterDateAsync(firstUser.Id, targetDate);

        Assert.Equal(previous.Id, actualPrevious?.Id);
        Assert.Equal(next.Id, actualNext?.Id);
    }
}

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

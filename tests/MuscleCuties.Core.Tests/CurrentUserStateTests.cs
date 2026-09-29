using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Data;
using MuscleCuties.Core.Models.Entities.Users;
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

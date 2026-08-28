using MuscleCuties.Core.Repositories.Common;
using MuscleCuties.Core.Repositories.Cycle;
using MuscleCuties.Core.Repositories.Nutrition;
using MuscleCuties.Core.Repositories.Quiz;
using MuscleCuties.Core.Repositories.Users;
using MuscleCuties.Core.Repositories.Workout;
using MuscleCuties.Core.Services.Auth;
using MuscleCuties.Core.Services.Cycle;
using MuscleCuties.Core.Services.Nutrition;
using MuscleCuties.Core.Services.Quiz;
using NSubstitute;

namespace MuscleCuties.Core.Tests.Services.Auth;

public class AuthServiceTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;
    private readonly ITokenStorage _storage;

    public AuthServiceTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
        _storage = Substitute.For<ITokenStorage>();
    }

    private AuthService CreateService() =>
        new AuthService(new UserRepository(_fixture.Db), _storage);

    [Fact]
    public async Task RegisterAsync_NewEmail_ReturnsUserAndStoresId()
    {
        var service = CreateService();

        var user = await service.RegisterAsync("auth1@test.com", "password123");

        Assert.NotNull(user);
        Assert.Equal("auth1@test.com", user.Email);
        await _storage.Received(1).SetAsync("current_user_id", user.Id.ToString());
    }

    [Fact]
    public async Task RegisterAsync_DuplicateEmail_ReturnsNull()
    {
        var service = CreateService();
        await service.RegisterAsync("auth2@test.com", "password123");

        var result = await service.RegisterAsync("auth2@test.com", "other");

        Assert.Null(result);
    }

    [Fact]
    public async Task RegisterAsync_NormalizesEmailBeforeSaving()
    {
        var service = CreateService();

        var user = await service.RegisterAsync("  Auth-Normalized@Test.COM  ", "password123");
        var duplicate = await service.RegisterAsync("auth-normalized@test.com", "other");

        Assert.NotNull(user);
        Assert.Equal("auth-normalized@test.com", user.Email);
        Assert.Null(duplicate);
    }

    [Fact]
    public async Task LoginAsync_CorrectCredentials_ReturnsUserAndStoresId()
    {
        var service = CreateService();
        await service.RegisterAsync("auth3@test.com", "secret");

        var result = await service.LoginAsync("auth3@test.com", "secret");

        Assert.NotNull(result);
        await _storage.Received().SetAsync("current_user_id", Arg.Any<string>());
    }

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNull()
    {
        var service = CreateService();
        await service.RegisterAsync("auth4@test.com", "correct");

        var result = await service.LoginAsync("auth4@test.com", "wrong");

        Assert.Null(result);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        var service = CreateService();

        var result = await service.LoginAsync("nobody@test.com", "pass");

        Assert.Null(result);
    }

    [Fact]
    public async Task SignInWithAppleAsync_NewAppleAccount_CreatesUserAndStoresId()
    {
        var service = CreateService();
        var appleUserId = $"apple-{Guid.NewGuid():N}";
        var email = $"apple-{Guid.NewGuid():N}@privaterelay.appleid.com";

        var user = await service.SignInWithAppleAsync(new AppleSignInResult(appleUserId, email, "Apple Cutie"));

        Assert.NotNull(user);
        Assert.Equal(email, user.Email);
        Assert.Equal(appleUserId, user.AppleUserId);
        await _storage.Received(1).SetAsync("current_user_id", user.Id.ToString());
    }

    [Fact]
    public async Task SignInWithAppleAsync_MatchingEmail_LinksExistingUser()
    {
        var service = CreateService();
        var email = $"apple-link-{Guid.NewGuid():N}@test.com";
        var existingUser = await service.RegisterAsync(email, "password123");
        var appleUserId = $"apple-link-{Guid.NewGuid():N}";
        _storage.ClearReceivedCalls();

        var signedInUser = await service.SignInWithAppleAsync(new AppleSignInResult(appleUserId, email, null));
        var linkedUser = await new UserRepository(_fixture.Db).GetByAppleUserIdAsync(appleUserId);

        Assert.NotNull(existingUser);
        Assert.NotNull(signedInUser);
        Assert.NotNull(linkedUser);
        Assert.Equal(existingUser.Id, signedInUser.Id);
        Assert.Equal(existingUser.Id, linkedUser.Id);
        await _storage.Received(1).SetAsync("current_user_id", existingUser.Id.ToString());
    }

    [Fact]
    public async Task LogoutAsync_CallsRemove()
    {
        var service = CreateService();

        await service.LogoutAsync();

        _storage.Received(1).Remove("current_user_id");
    }

    [Fact]
    public async Task IsLoggedInAsync_WhenStorageHasId_ReturnsTrue()
    {
        var service = CreateService();
        _storage.GetAsync("current_user_id").Returns(Task.FromResult<string?>("1"));

        var result = await service.IsLoggedInAsync();

        Assert.True(result);
    }

    [Fact]
    public async Task IsLoggedInAsync_WhenStorageEmpty_ReturnsFalse()
    {
        var service = CreateService();
        _storage.GetAsync("current_user_id").Returns(Task.FromResult<string?>(null));

        var result = await service.IsLoggedInAsync();

        Assert.False(result);
    }

    [Fact]
    public async Task IsLoggedInAsync_WhenStoredUserMissing_ReturnsFalseAndClearsStorage()
    {
        var service = CreateService();
        _storage.GetAsync("current_user_id").Returns(Task.FromResult<string?>("999999"));

        var result = await service.IsLoggedInAsync();

        Assert.False(result);
        _storage.Received(1).Remove("current_user_id");
    }

    [Fact]
    public async Task GetCurrentUserIdAsync_WhenStoredUserExists_ReturnsId()
    {
        var service = CreateService();
        var user = await service.RegisterAsync($"current-{Guid.NewGuid():N}@test.com", "password123");
        _storage.ClearReceivedCalls();
        _storage.GetAsync("current_user_id").Returns(Task.FromResult<string?>(user!.Id.ToString()));

        var result = await service.GetCurrentUserIdAsync();

        Assert.Equal(user.Id, result);
        _storage.DidNotReceive().Remove("current_user_id");
    }

    [Fact]
    public async Task GetCurrentUserIdAsync_WhenStoredUserMissing_ReturnsZeroAndClearsStorage()
    {
        var service = CreateService();
        _storage.GetAsync("current_user_id").Returns(Task.FromResult<string?>("999998"));

        var result = await service.GetCurrentUserIdAsync();

        Assert.Equal(0, result);
        _storage.Received(1).Remove("current_user_id");
    }
}

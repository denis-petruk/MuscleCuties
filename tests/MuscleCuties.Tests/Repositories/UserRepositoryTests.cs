using MuscleCuties.Core.Models.Entities;
using MuscleCuties.Core.Repositories;

namespace MuscleCuties.Tests.Repositories;

public class UserRepositoryTests : IClassFixture<DatabaseFixture>
{
    private readonly DatabaseFixture _fixture;

    public UserRepositoryTests(DatabaseFixture fixture)
    {
        _fixture = fixture;
    }

    private UserRepository CreateRepo() => new UserRepository(_fixture.Db);

    [Fact]
    public async Task AddAsync_ValidUser_UserPersistedWithId()
    {
        var repo = CreateRepo();
        var user = new User { Email = "a@test.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };

        await repo.AddAsync(user);

        Assert.True(user.Id > 0);
    }

    [Fact]
    public async Task GetByEmailAsync_ExistingEmail_ReturnsUser()
    {
        var repo = CreateRepo();
        var user = new User { Email = "b@test.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(user);

        var result = await repo.GetByEmailAsync("b@test.com");

        Assert.NotNull(result);
        Assert.Equal("b@test.com", result.Email);
    }

    [Fact]
    public async Task GetByEmailAsync_NonExistentEmail_ReturnsNull()
    {
        var repo = CreateRepo();

        var result = await repo.GetByEmailAsync("nonexistent@test.com");

        Assert.Null(result);
    }

    [Fact]
    public async Task GetProfileAsync_AfterAddProfile_ReturnsProfile()
    {
        var repo = CreateRepo();
        var user = new User { Email = "c@test.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(user);

        var profile = new UserProfile { UserId = user.Id, Name = "Alice", DateOfBirth = new DateTime(1995, 1, 1), Height = 165, Weight = 60 };
        await repo.AddProfileAsync(profile);

        var result = await repo.GetProfileAsync(user.Id);

        Assert.NotNull(result);
        Assert.Equal("Alice", result.Name);
    }

    [Fact]
    public async Task UpdateProfileAsync_ChangedName_PersistsChange()
    {
        var repo = CreateRepo();
        var user = new User { Email = "e@test.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(user);
        var profile = new UserProfile { UserId = user.Id, Name = "Before", DateOfBirth = new DateTime(1995, 1, 1), Height = 165, Weight = 60 };
        await repo.AddProfileAsync(profile);

        profile.Name = "After";
        await repo.UpdateProfileAsync(profile);

        var result = await repo.GetProfileAsync(user.Id);
        Assert.Equal("After", result!.Name);
    }
    [Fact]
    public async Task AddSnapshotAsync_ValidSnapshot_PersistedWithId()
    {
        var repo = CreateRepo();
        var user = new User { Email = "snap1@test.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(user);

        var snapshot = new UserProfileSnapshot
        {
            UserId = user.Id,
            SnapshotReason = "Initial",
            ProfileJson = "{\"Name\":\"Alice\"}",
            CreatedAt = DateTime.UtcNow
        };
        await repo.AddSnapshotAsync(snapshot);

        Assert.True(snapshot.Id > 0);
    }

    [Fact]
    public async Task GetLatestSnapshotAsync_AfterAdd_ReturnsIt()
    {
        var repo = CreateRepo();
        var user = new User { Email = "snap2@test.com", PasswordHash = "hash", CreatedAt = DateTime.UtcNow };
        await repo.AddAsync(user);

        await repo.AddSnapshotAsync(new UserProfileSnapshot
        {
            UserId = user.Id,
            SnapshotReason = "Initial",
            ProfileJson = "{\"Name\":\"Bob\"}",
            CreatedAt = DateTime.UtcNow
        });

        var result = await repo.GetLatestSnapshotAsync(user.Id);
        Assert.NotNull(result);
        Assert.Equal("Initial", result.SnapshotReason);
    }
}

using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
/// Gate 56: the glute-focus preference is a stored profile answer, not a planning-time guess. This
/// writes the profile row, reloads it through the preview source, and proves both states.
/// </summary>
public sealed class GluteFocusPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private static readonly DateOnly SyntheticBirthDate = new(1990, 1, 1);

    [PostgresFact]
    public async Task Saved_glute_focus_survives_the_profile_row_and_the_preview_source()
    {
        var options = new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(RequireLocalTestConnectionString()).Options;
        await using var database = new PostgresDbContext(options);
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"glute-{userId:N}@example.invalid",
            Username = $"glt_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = now,
            UpdatedAt = now,
            IsOnboardingComplete = true
        });
        database.UserProfiles.Add(new UserProfileRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Name = "Synthetic",
            DateOfBirth = SyntheticBirthDate,
            Goal = "MuscleTone",
            TrainingExperienceLevel = "Beginner",
            EquipmentLevel = "FullGym",
            Height = 168m,
            Weight = 62m,
            WorkoutDaysPerWeek = 3,
            CycleLength = 28,
            UpdatedAt = now,
            GluteFocus = true
        });
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TimeZoneId = TimeZoneInfo.Utc.Id
        });
        await database.SaveChangesAsync();
        try
        {
            // Read the stored answer back from a clean context, then through the preview source.
            database.ChangeTracker.Clear();
            var stored = await database.UserProfiles.AsNoTracking()
                .SingleAsync(row => row.UserId == userId);
            Xunit.Assert.True(stored.GluteFocus);

            var focused = Xunit.Assert.IsType<MuscleCuties.UseCases.Features.Workout.Planning.StrengthPlanPreviewSourceV020>(
                await new PostgresStrengthPlanPreviewSourceV020(database).LoadAsync(userId, CancellationToken.None));
            Xunit.Assert.True(focused.GluteFocus);

            // Clearing the preference is persisted too and the source stops reporting it.
            await database.UserProfiles.Where(row => row.UserId == userId)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.GluteFocus, false));
            database.ChangeTracker.Clear();

            var cleared = Xunit.Assert.IsType<MuscleCuties.UseCases.Features.Workout.Planning.StrengthPlanPreviewSourceV020>(
                await new PostgresStrengthPlanPreviewSourceV020(database).LoadAsync(userId, CancellationToken.None));
            Xunit.Assert.False(cleared.GluteFocus);
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static string RequireLocalTestConnectionString()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal(LocalHost, parsed.Host);
        Xunit.Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        return connection!;
    }
}

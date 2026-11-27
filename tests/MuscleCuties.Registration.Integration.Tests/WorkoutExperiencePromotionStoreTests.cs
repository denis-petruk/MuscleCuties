using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>Exercises the progress store's experience and calendar decisions through the real persistence path.</summary>
public sealed class WorkoutExperiencePromotionStoreTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string DatabasePrefix = "mc_test_";
    private const int IntermediatePromotionDays = 20;
    private static readonly DateTime FirstCompletionUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [PostgresFact]
    public async Task TwentyCreditedDaysPromoteOnceAndASameDayRetryDoesNotCreditAgain()
    {
        await using var database = Open();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"promotion-{userId:N}@example.invalid",
            Username = $"prm_{userId.ToString("N")[..10]}",
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
            Name = "Promotion Tester",
            Height = 170m,
            Weight = 64m,
            Goal = "BuildMuscle",
            WeightGoalPace = "Maintain",
            TrainingExperienceLevel = "Beginner",
            WorkoutDaysPerWeek = 3,
            CycleLength = 28,
            EquipmentLevel = nameof(Equipment.FullGym),
            PreferredWorkoutActivityTypes = nameof(WorkoutActivityType.GeneralStrength),
            UpdatedAt = now
        });
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TimeZoneId = TimeZoneInfo.Utc.Id,
            TimeZoneVersion = 1,
            BaselineExperience = TrainingExperienceLevel.Beginner,
            EffectiveExperience = TrainingExperienceLevel.Beginner
        });
        var session = new WorkoutSessionLogRecord
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            WorkoutName = "Synthetic session",
            StartedAt = FirstCompletionUtc,
            CompletedAt = FirstCompletionUtc.AddMinutes(30),
            CreatedAt = FirstCompletionUtc,
            UpdatedAt = FirstCompletionUtc
        };
        database.WorkoutSessionLogs.Add(session);
        await database.SaveChangesAsync();
        try
        {
            for (var day = 0; day < IntermediatePromotionDays; day++)
                await PostgresWorkoutProgressStore.CreditPlannedSessionAsync(database, userId, session,
                    FirstCompletionUtc.AddDays(day), CancellationToken.None);

            var state = await database.WorkoutAccountPlanningStates.AsNoTracking()
                .SingleAsync(row => row.UserId == userId);
            Assert.Equal(IntermediatePromotionDays, await database.WorkoutCompletionDayCredits
                .AsNoTracking().CountAsync(row => row.UserId == userId));
            Assert.Equal(TrainingExperienceLevel.Intermediate, state.EffectiveExperience);
            Assert.Equal(1, state.ExperienceVersion);
            Assert.True(state.PromotionPending);
            Assert.Equal(nameof(TrainingExperienceLevel.Intermediate), (await database.UserProfiles.AsNoTracking()
                .SingleAsync(row => row.UserId == userId)).TrainingExperienceLevel);

            // Retrying the first day must be absorbed by the durable day credit.
            await PostgresWorkoutProgressStore.CreditPlannedSessionAsync(database, userId, session,
                FirstCompletionUtc, CancellationToken.None);
            var afterRetry = await database.WorkoutAccountPlanningStates.AsNoTracking()
                .SingleAsync(row => row.UserId == userId);
            Assert.Equal(IntermediatePromotionDays, await database.WorkoutCompletionDayCredits
                .AsNoTracking().CountAsync(row => row.UserId == userId));
            Assert.Equal(1, afterRetry.ExperienceVersion);
            Assert.Equal(TrainingExperienceLevel.Intermediate, afterRetry.EffectiveExperience);
        }
        finally
        {
            database.ChangeTracker.Clear();
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    [PostgresFact]
    public async Task LegacyProfileWithoutStatedExperienceEstablishesTheBeginnerCalendar()
    {
        await using var database = Open();
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(database, userId);
        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"legacy-calendar-{userId:N}@example.invalid",
            Username = $"lgc_{userId.ToString("N")[..10]}",
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
            Name = "Legacy Tester",
            DateOfBirth = new DateOnly(1996, 5, 1),
            Height = 170m,
            Weight = 64m,
            Goal = "MaintainHealth",
            WeightGoalPace = "Maintain",
            TrainingExperienceLevel = string.Empty,
            WorkoutDaysPerWeek = 3,
            CycleLength = 28,
            EquipmentLevel = nameof(Equipment.FullGym),
            PreferredWorkoutActivityTypes = nameof(WorkoutActivityType.GeneralStrength),
            AvailableWorkoutWeekdaysJson = "[\"Monday\",\"Wednesday\",\"Friday\"]",
            ActivitySessionDurationsJson = "{\"GeneralStrength\":45}",
            UpdatedAt = now
        });
        await database.SaveChangesAsync();
        try
        {
            var store = new PostgresWorkoutProgressStore(database);
            var progress = await store.AcceptForegroundTimeZoneAsync(userId,
                new WorkoutForegroundTimeZoneRequest("America/Toronto", 0, true), CancellationToken.None);

            Assert.Equal("Ready", progress.Status);
            Assert.Equal("America/Toronto", progress.TimeZoneId);
            Assert.Equal(nameof(TrainingExperienceLevel.Beginner), progress.ExperienceLevel);
            var state = await database.WorkoutAccountPlanningStates.AsNoTracking()
                .SingleAsync(row => row.UserId == userId);
            Assert.Equal(TrainingExperienceLevel.Beginner, state.BaselineExperience);
            Assert.Equal(TrainingExperienceLevel.Beginner, state.EffectiveExperience);

            // Generation writes the activity answers before it asks for a proposal.
            var answers = await new PostgresStrengthPlanProposalStoreV020(database).SaveAnswersAsync(userId,
                new SaveActivityAnswersRequest(0, [], null), CancellationToken.None);
            Assert.Equal(0, answers.Version);

            var generation = new CreateStrengthPlanProposalV020UseCase(
                new PreviewStrengthPlanV020UseCase(new PostgresStrengthPlanPreviewSourceV020(database)),
                new PostgresStrengthPlanProposalStoreV020(database));
            var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength,
                DateOnly.FromDateTime(DateTime.UtcNow), null, WarmupMinutes: 5, CooldownMinutes: 5)
            {
                DraftPreferences = new WorkoutPreferencesDto(
                    [(int)WorkoutActivityDto.GeneralStrength],
                    [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday, (int)DayOfWeek.Friday],
                    new Dictionary<int, int> { [(int)WorkoutActivityDto.GeneralStrength] = 45 },
                    nameof(Equipment.FullGym))
            };
            var proposal = await generation.ExecuteAsync(userId, request, CancellationToken.None);

            Assert.True(string.Equals("Feasible", proposal.Preview.Status, StringComparison.Ordinal),
                string.Join(",", proposal.Preview.LimitationCodes.Concat(proposal.Preview.AdvisoryCodes)));
            Assert.True(proposal.IsPersisted, string.Join(",", proposal.Preview.LimitationCodes));
            Assert.NotEmpty(proposal.Preview.Sessions);
        }
        finally
        {
            database.ChangeTracker.Clear();
            await database.WorkoutPlanProposalsV02.Where(row => row.UserId == userId).ExecuteDeleteAsync();
            await database.WorkoutAccountPlanningStates.Where(row => row.UserId == userId).ExecuteDeleteAsync();
            await database.UserProfiles.Where(row => row.UserId == userId).ExecuteDeleteAsync();
            await database.Users.Where(row => row.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static PostgresDbContext Open()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal(LocalHost, builder.Host);
        Assert.StartsWith(DatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options);
    }
}

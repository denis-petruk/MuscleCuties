using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class CanonicalOccurrenceAdjustmentPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string DisposableHost = "127.0.0.1";
    private const string DisposableDatabasePrefix = "mc_test_";
    private const string StrengthWorkoutType = "Strength";
    private const string SyntheticCyclePhase = "Follicular";
    private const string SyntheticCredentialType = "Password";
    private const string SyntheticPasswordHash = "synthetic-hash";
    private const string SyntheticPlanName = "Synthetic strength plan";
    private const string SyntheticDayName = "Synthetic strength day";
    private const int OriginalSets = 3;
    private const int OriginalTargetRir = 2;
    private const int OriginalOptionalSets = 2;
    private const int OriginalReps = 8;
    private const int ExpectedDeloadSets = 2;
    private const int ExpectedDeloadRir = 3;
    private const int ExpectedDeloadOptionalSets = 1;
    private const int LaterDayOffset = 8;
    private const int SyntheticUsernameIdentifierLength = 10;
    private const int SyntheticStartYear = 2026;
    private const int SyntheticStartMonth = 10;
    private const int SyntheticStartDay = 26;
    private static readonly DateOnly DeloadStart = new(SyntheticStartYear, SyntheticStartMonth, SyntheticStartDay);

    [Fact]
    public async Task ActiveDeloadChangesOnlyNewOccurrencesInWindowAndPersistsAdjustedDose()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal(DisposableHost, parsed.Host);
        Assert.StartsWith(DisposableDatabasePrefix, parsed.Database, StringComparison.Ordinal);
        await using var database = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connection).Options);
        await using var transaction = await database.Database.BeginTransactionAsync();
        var userId = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(database, userId);
        var now = DateTime.UtcNow;
        var previousPlanId = Guid.NewGuid();
        var newPlanId = Guid.NewGuid();
        var withinDayId = Guid.NewGuid();
        var laterDayId = Guid.NewGuid();
        var endExclusive = DeloadStart.AddDays(CanonicalOccurrenceAdjustmentRule.DeloadDurationDays);
        database.Users.Add(new UserRecord
        {
            Id = userId, Email = $"deload-{userId:N}@example.invalid",
            Username = $"deload_{userId.ToString("N")[..SyntheticUsernameIdentifierLength]}",
            CredentialType = SyntheticCredentialType, PasswordHash = SyntheticPasswordHash,
            CreatedAt = now, UpdatedAt = now
        });
        database.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
        {
            Id = Guid.NewGuid(), UserId = userId, TimeZoneId = TimeZoneInfo.Utc.Id,
            DeloadEvidenceThroughExclusive = DeloadStart,
            DeloadStartDate = DeloadStart, DeloadEndExclusive = endExclusive
        });
        database.WorkoutPlans.AddRange(
            new WorkoutPlanRecord { Id = previousPlanId, UserId = userId, Name = SyntheticPlanName,
                IsActive = false, CyclePhaseTarget = SyntheticCyclePhase, CreatedAt = now },
            new WorkoutPlanRecord { Id = newPlanId, UserId = userId, Name = SyntheticPlanName,
                IsActive = true, CyclePhaseTarget = SyntheticCyclePhase, CreatedAt = now });
        await database.SaveChangesAsync();
        var exerciseId = await database.WorkoutExerciseDefinitions.AsNoTracking()
            .Select(exercise => exercise.Id).FirstAsync();

        var withinDay = new WorkoutDayRecord { Id = withinDayId, UserId = userId, WorkoutPlanId = newPlanId,
            Date = DeloadStart, DayOfWeek = (int)DeloadStart.DayOfWeek,
            WorkoutType = StrengthWorkoutType, Name = SyntheticDayName };
        var laterDate = DeloadStart.AddDays(LaterDayOffset);
        var laterDay = new WorkoutDayRecord { Id = laterDayId, UserId = userId, WorkoutPlanId = newPlanId,
            Date = laterDate, DayOfWeek = (int)laterDate.DayOfWeek,
            WorkoutType = StrengthWorkoutType, Name = SyntheticDayName };
        var within = new WorkoutDayExerciseRecord { Id = Guid.NewGuid(), UserId = userId,
            WorkoutDayId = withinDayId, ExerciseId = exerciseId, Sets = OriginalSets, Reps = OriginalReps,
            OptionalSets = OriginalOptionalSets, TargetRepetitionsInReserve = OriginalTargetRir };
        var later = new WorkoutDayExerciseRecord { Id = Guid.NewGuid(), UserId = userId,
            WorkoutDayId = laterDayId, ExerciseId = exerciseId, Sets = OriginalSets, Reps = OriginalReps,
            OptionalSets = OriginalOptionalSets, TargetRepetitionsInReserve = OriginalTargetRir };

        await PostgresCanonicalOccurrenceAdjustment.ApplyAsync(database, userId, previousPlanId,
            DeloadStart, DeloadStart, TimeZoneInfo.Utc, [], [withinDay, laterDay], [within, later],
            CancellationToken.None);
        database.WorkoutDays.AddRange(withinDay, laterDay);
        database.WorkoutDayExercises.AddRange(within, later);
        await database.SaveChangesAsync();
        database.ChangeTracker.Clear();

        var adjusted = await database.WorkoutDayExercises.AsNoTracking()
            .SingleAsync(item => item.UserId == userId && item.Id == within.Id);
        var unchanged = await database.WorkoutDayExercises.AsNoTracking()
            .SingleAsync(item => item.UserId == userId && item.Id == later.Id);
        Assert.Equal(ExpectedDeloadSets, adjusted.Sets);
        Assert.Equal(ExpectedDeloadRir, adjusted.TargetRepetitionsInReserve);
        Assert.Equal(ExpectedDeloadOptionalSets, adjusted.OptionalSets);
        Assert.Equal(OriginalSets, unchanged.Sets);
        Assert.Equal(OriginalTargetRir, unchanged.TargetRepetitionsInReserve);
        Assert.Equal(OriginalOptionalSets, unchanged.OptionalSets);
        await transaction.RollbackAsync();
    }
}

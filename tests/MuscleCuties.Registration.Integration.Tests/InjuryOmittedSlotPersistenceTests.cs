using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using System.Reflection;
using Npgsql;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class InjuryOmittedSlotPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const int SyntheticExerciseId = 1;
    private const int SyntheticSets = 3;
    private const int SyntheticRepetitions = 8;
    private const int ConservativeSets = 2;
    private const int ConservativeRepetitions = 6;
    private const int ExpectedCauseCount = 2;
    private const int SyntheticUsernameSuffixLength = 10;

    [Fact]
    public async Task OmittedLineageRollsBackAndRetainsDistinctCausalEpisodes()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Assert.Equal("127.0.0.1", parsed.Host);
        Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
        var user = Guid.NewGuid();
        var plan = Guid.NewGuid();
        var origin = Guid.NewGuid();
        var firstEpisode = Guid.NewGuid();
        var secondEpisode = Guid.NewGuid();
        var now = DateTime.UtcNow;
        await SyntheticTenantSession.SetAsync(db, user);
        db.Users.Add(new UserRecord { Id = user, Email = $"omission-{user:N}@example.invalid",
            Username = $"om_{user.ToString("N")[..SyntheticUsernameSuffixLength]}", CredentialType = "Password", PasswordHash = "synthetic-hash",
            CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
        await db.SaveChangesAsync();
        try
        {
            WorkoutInjuryOmittedSlotRecord NewSlot(Guid episode) => new()
            {
                Id = Guid.NewGuid(), UserId = user, PlanId = plan, ScheduledDate = DateOnly.FromDateTime(now),
                TemplateId = "synthetic-template", TemplateVersion = "synthetic-version", SessionIndex = 0,
                SlotId = "synthetic-slot", EpisodeId = episode, OriginWorkoutDayExerciseId = origin,
                OriginalExerciseId = SyntheticExerciseId, OriginalSets = SyntheticSets, OriginalReps = SyntheticRepetitions
            };
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                db.Set<WorkoutInjuryOmittedSlotRecord>().Add(NewSlot(firstEpisode));
                await db.SaveChangesAsync();
                await transaction.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            Assert.Empty(await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking().Where(row => row.UserId == user).ToListAsync());
            db.Set<WorkoutInjuryOmittedSlotRecord>().AddRange(NewSlot(firstEpisode), NewSlot(secondEpisode));
            await db.SaveChangesAsync();
            var rows = await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking().Where(row => row.UserId == user).ToListAsync();
            Assert.Equal(ExpectedCauseCount, rows.Count);
            Assert.All(rows, row => { Assert.Equal(origin, row.OriginWorkoutDayExerciseId); Assert.Equal(SyntheticSets, row.OriginalSets); });
            var revisedPlan = Guid.NewGuid();
            var revisedDay = Guid.NewGuid();
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = revisedPlan, UserId = user, Name = "Synthetic revision",
                IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
            db.WorkoutDays.Add(new WorkoutDayRecord { Id = revisedDay, UserId = user, WorkoutPlanId = revisedPlan,
                Date = DateOnly.FromDateTime(now), DayOfWeek = (int)now.DayOfWeek,
                WorkoutType = nameof(WorkoutType.Strength), Name = "Synthetic strength" });
            await db.SaveChangesAsync();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var transfer = typeof(PostgresWorkoutPlanReconciler).GetMethod("TransferDeferredOmissionsAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(transfer);
                var task = Assert.IsAssignableFrom<Task<bool>>(transfer.Invoke(new PostgresWorkoutPlanReconciler(db),
                    [user, revisedPlan, rows, CancellationToken.None]));
                Assert.True(await task);
                var transferred = await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking()
                    .Where(row => row.UserId == user && row.PlanId == revisedPlan).ToListAsync();
                Assert.Equal(rows.Select(row => row.EpisodeId).Order(), transferred.Select(row => row.EpisodeId).Order());
                Assert.All(transferred, row => Assert.Equal(origin, row.OriginWorkoutDayExerciseId));
                await transaction.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            Assert.False(await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking().AnyAsync(row => row.PlanId == revisedPlan));
            var planWithoutStrength = Guid.NewGuid();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var transfer = typeof(PostgresWorkoutPlanReconciler).GetMethod("TransferDeferredOmissionsAsync",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(transfer);
                var task = Assert.IsAssignableFrom<Task<bool>>(transfer.Invoke(new PostgresWorkoutPlanReconciler(db),
                    [user, planWithoutStrength, rows, CancellationToken.None]));
                Assert.True(await task);
                var transferred = await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking()
                    .Where(row => row.UserId == user && row.PlanId == planWithoutStrength).ToListAsync();
                Assert.Equal(ExpectedCauseCount, transferred.Count);
                Assert.All(transferred, row => Assert.Equal(DateOnly.FromDateTime(now), row.ScheduledDate));
                await transaction.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            await using (var transaction = await db.Database.BeginTransactionAsync())
            {
                var exerciseId = await db.Exercises.AsNoTracking().OrderBy(row => row.Id).Select(row => row.Id).FirstAsync();
                var exerciseRowId = Guid.NewGuid();
                db.WorkoutInjuryEpisodes.Add(new WorkoutInjuryEpisodeRecord { Id = firstEpisode, UserId = user,
                    InjuryLogId = Guid.NewGuid(), StartedOn = DateOnly.FromDateTime(now), ClearedOn = DateOnly.FromDateTime(now),
                    ClearanceTimeZoneId = TimeZoneInfo.Utc.Id, ClearanceTimeZoneVersion = 1 });
                db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = exerciseRowId, UserId = user, WorkoutDayId = revisedDay,
                    ExerciseId = exerciseId, PlanningTemplateId = "synthetic-template", PlanningTemplateVersion = "synthetic-version",
                    PlanningSessionIndex = 0, PlanningSlotId = "synthetic-slot", Sets = ConservativeSets, Reps = ConservativeRepetitions });
                await db.SaveChangesAsync();
                var restorationType = typeof(PostgresWorkoutPlanReconciler).GetNestedType("ExactRestoration", BindingFlags.NonPublic);
                Assert.NotNull(restorationType);
                var restoration = Activator.CreateInstance(restorationType, now.DayOfWeek, "synthetic-template", "synthetic-version", 0,
                    "synthetic-slot", exerciseId, exerciseId, SyntheticSets, SyntheticRepetitions, ConservativeSets,
                    ConservativeRepetitions, false, new Dictionary<DateOnly, Guid> { [DateOnly.FromDateTime(now)] = origin },
                    new[] { firstEpisode }, new[] { new InjuryRestorationEpisode(false, DateOnly.FromDateTime(now)) });
                var mappings = Array.CreateInstance(restorationType, 1);
                mappings.SetValue(restoration, 0);
                var apply = typeof(PostgresWorkoutPlanReconciler).GetMethod("ApplyExactRestorationAsync", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.NotNull(apply);
                var task = Assert.IsAssignableFrom<Task<bool>>(apply.Invoke(new PostgresWorkoutPlanReconciler(db),
                    [user, revisedPlan, mappings, CancellationToken.None]));
                Assert.True(await task);
                var restored = await db.WorkoutDayExercises.AsNoTracking().SingleAsync(row => row.Id == exerciseRowId);
                Assert.True(restored.IsOptional);
                Assert.Equal(ConservativeSets, restored.Sets);
                Assert.Equal(ConservativeRepetitions, restored.Reps);
                Assert.Equal(exerciseId, restored.InjuryOriginalExerciseId);
                Assert.Equal(origin, restored.OriginWorkoutDayExerciseId);
                Assert.Null(restored.InjuryRestorationRequiredFromDate);
                await transaction.RollbackAsync();
            }
            db.ChangeTracker.Clear();
            db.Set<WorkoutInjuryOmittedSlotRecord>().Add(NewSlot(firstEpisode));
            await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
            db.ChangeTracker.Clear();
            Assert.Equal(ExpectedCauseCount, await db.Set<WorkoutInjuryOmittedSlotRecord>().AsNoTracking().CountAsync(row => row.UserId == user));
        }
        finally { await db.Users.Where(row => row.Id == user).ExecuteDeleteAsync(); }
    }
}

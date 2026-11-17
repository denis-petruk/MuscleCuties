using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Npgsql;
using Xunit;
using Xunit.Abstractions;

namespace MuscleCuties.Registration.Integration.Tests;

/// <summary>
/// B3 archive proof: runs the real <see cref="PostgresWorkoutPlanAndSessionStore"/> log path
/// against a database provisioned by the production <c>migrate</c> + <c>seed</c> entry points.
/// Observed per-set rows must persist rir/technique/is_observed; aggregate-only legacy rows
/// must persist is_observed = false so double progression can never consume them.
/// Local disposable only: refuses non-loopback hosts and databases outside the mc_ namespace.
/// </summary>
public sealed class ObservedSetArchiveProvenanceIntegrationTests(ITestOutputHelper output)
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LoopbackHost = "127.0.0.1";
    private const string DatabasePrefix = "mc_";
    private const int PlannedSets = 3;
    private const int PlannedReps = 10;

    private static readonly string[] EnduranceTokens =
    [
        "plank", "copenhagen", "bike", "cycle", "cycling", "ride", "run", "jog", "sprint",
        "tempo", "swim", "pool", "hiit", "interval", "yoga", "vinyasa", "mobility",
        "recovery", "breathing", "pilates", "walk"
    ];

    [PostgresFact]
    public async Task ObservedSetsPersistProvenanceWhileAggregateRowsRemainUnobserved()
    {
        var connectionString = RequireLocalTestConnectionString();
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connectionString).Options;
        await using var database = new PostgresDbContext(options);

        var userId = Guid.CreateVersion7();
        var nowUtc = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(nowUtc);
        await SyntheticTenantSession.SetAsync(database, userId);

        database.Users.Add(new UserRecord
        {
            Id = userId,
            Email = $"observed-{userId:N}@example.invalid",
            Username = $"obs_{userId.ToString("N")[..10]}",
            CredentialType = "Password",
            PasswordHash = "synthetic-hash",
            CreatedAt = nowUtc,
            UpdatedAt = nowUtc,
            IsOnboardingComplete = true
        });

        var planId = Guid.CreateVersion7();
        var dayId = Guid.CreateVersion7();
        database.WorkoutPlans.Add(new WorkoutPlanRecord
        {
            Id = planId,
            UserId = userId,
            Name = "Synthetic observed archive plan",
            IsActive = true,
            CyclePhaseTarget = "Follicular",
            CreatedAt = nowUtc
        });
        database.WorkoutDays.Add(new WorkoutDayRecord
        {
            Id = dayId,
            UserId = userId,
            WorkoutPlanId = planId,
            DayOfWeek = (int)today.DayOfWeek,
            WeekIndex = 0,
            Date = today,
            WorkoutType = "Strength",
            Name = "Synthetic observed archive"
        });

        var catalog = await database.Exercises.AsNoTracking().OrderBy(exercise => exercise.Id).ToListAsync();
        var strengthExercises = catalog
            .Where(exercise => !EnduranceTokens.Any(token =>
                exercise.Name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            .Take(2)
            .ToList();
        Assert.Equal(2, strengthExercises.Count);

        var observedExercise = strengthExercises[0];
        var legacyExercise = strengthExercises[1];
        var observedDayExercise = new WorkoutDayExerciseRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            WorkoutDayId = dayId,
            ExerciseId = observedExercise.Id,
            Sets = PlannedSets,
            Reps = PlannedReps
        };
        var legacyDayExercise = new WorkoutDayExerciseRecord
        {
            Id = Guid.CreateVersion7(),
            UserId = userId,
            WorkoutDayId = dayId,
            ExerciseId = legacyExercise.Id,
            Sets = PlannedSets,
            Reps = PlannedReps
        };
        database.WorkoutDayExercises.AddRange(observedDayExercise, legacyDayExercise);
        await database.SaveChangesAsync();

        try
        {
            var store = new PostgresWorkoutPlanAndSessionStore(database);
            await store.LogAsync(
                userId,
                new LogWorkoutSessionRequest(
                    PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayId),
                    today,
                    [
                        new WorkoutExerciseLogDto(
                            PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(observedDayExercise.Id),
                            observedExercise.Id,
                            PlannedSets,
                            PlannedReps,
                            60f)
                        {
                            ObservedSets =
                            [
                                new ObservedSetRecord(1, 12, 60m, 2, true),
                                new ObservedSetRecord(2, 11, 60m, 1, true),
                                new ObservedSetRecord(3, 10, 60m, 0, false)
                            ]
                        },
                        // Aggregate-only client: no per-set rows, so this must never become evidence.
                        new WorkoutExerciseLogDto(
                            PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(legacyDayExercise.Id),
                            legacyExercise.Id,
                            PlannedSets,
                            PlannedReps,
                            40f)
                    ]),
                CancellationToken.None);

            var archivedSets = await database.ExerciseSetLogs.AsNoTracking()
                .Where(row => row.UserId == userId)
                .OrderBy(row => row.SetNumber)
                .ToListAsync();
            var observedKey = observedExercise.Id.ToString(CultureInfo.InvariantCulture);
            var legacyKey = legacyExercise.Id.ToString(CultureInfo.InvariantCulture);
            var observedRows = archivedSets.Where(row => row.ExerciseId == observedKey)
                .OrderBy(row => row.SetNumber).ToList();
            var legacyRows = archivedSets.Where(row => row.ExerciseId == legacyKey)
                .OrderBy(row => row.SetNumber).ToList();

            foreach (var row in observedRows)
            {
                output.WriteLine(
                    $"observed: exercise={row.ExerciseName} set={row.SetNumber} reps={row.RepsCompleted} " +
                    $"load={row.WeightKg} rir={row.RirActual?.ToString(CultureInfo.InvariantCulture) ?? "null"} " +
                    $"technique={row.TechniqueAcceptable} isObserved={row.IsObserved}");
            }

            foreach (var row in legacyRows)
            {
                output.WriteLine(
                    $"legacy: exercise={row.ExerciseName} set={row.SetNumber} reps={row.RepsCompleted} " +
                    $"load={row.WeightKg} rir={row.RirActual?.ToString(CultureInfo.InvariantCulture) ?? "null"} " +
                    $"technique={row.TechniqueAcceptable} isObserved={row.IsObserved}");
            }

            Assert.Equal(PlannedSets, observedRows.Count);
            Assert.All(observedRows, row => Assert.True(row.IsObserved));
            Assert.Equal([1, 2, 3], observedRows.Select(row => row.SetNumber));
            Assert.Equal([12, 11, 10], observedRows.Select(row => row.RepsCompleted));
            Assert.Equal([2, 1, 0], observedRows.Select(row => row.RirActual));
            Assert.Equal([true, true, false], observedRows.Select(row => row.TechniqueAcceptable));
            Assert.All(observedRows, row => Assert.Equal(60m, row.WeightKg));

            Assert.Equal(PlannedSets, legacyRows.Count);
            Assert.All(legacyRows, row => Assert.False(row.IsObserved));
            Assert.All(legacyRows, row => Assert.Null(row.RirActual));
            Assert.All(legacyRows, row => Assert.True(row.TechniqueAcceptable));
            Assert.All(legacyRows, row => Assert.Equal(40m, row.WeightKg));

            var session = await database.WorkoutSessionLogs.AsNoTracking()
                .SingleAsync(row => row.UserId == userId);
            output.WriteLine($"archive session totalVolumeKg={session.TotalVolumeKg}");
            Assert.Equal(3180m, session.TotalVolumeKg);
        }
        finally
        {
            await database.Users.Where(user => user.Id == userId).ExecuteDeleteAsync();
        }
    }

    private static string RequireLocalTestConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connectionString),
            $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connectionString);
        Assert.Equal(LoopbackHost, builder.Host);
        Assert.StartsWith(DatabasePrefix, builder.Database, StringComparison.Ordinal);
        return connectionString!;
    }
}

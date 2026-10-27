using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.CheckInAndInjury;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Models.Catalog;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.UseCases.Features.CheckInAndInjury;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class InjurySuppressionLinkPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const string LocalHost = "127.0.0.1";
    private const string DatabasePrefix = "mc_test_";
    private const int FutureDays = 7;
    private const int FutureWeek = 7;
    private const int CompletePercent = 100;
    private const int SyntheticSetCount = 1;
    private const int SyntheticRepCount = 1;
    private const int InitialRevision = 1;
    private const int SessionIndex = 1;
    private const int MinimumSyntheticCatalogId = 1000000000;
    private const int MaximumSyntheticCatalogId = 2000000000;
    private const string Ruleset = "synthetic-ruleset";
    private const string Policy = "synthetic-policy";
    private const string Evidence = "{\"ownerRestrictionAssessmentAssertion\":true,\"exercise\":{\"effectiveRestrictionAssessmentStatus\":\"OwnerAssessedClearanceSubjectToExistingContraindications\",\"experienceConditions\":\"synthetic\",\"conditionsAndExclusions\":\"synthetic\"},\"resolvedDirectedSubstitutionReviews\":[]}";

    [Xunit.Theory]
    [Xunit.InlineData(true)]
    [Xunit.InlineData(false)]
    public async Task InjuryLinksOnlyUnloggedUnambiguousReviewedPrescriptions(bool knownSnapshot)
    {
        await using var db = CreateContext();
        var owner = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var future = DateOnly.FromDateTime(now).AddDays(FutureDays);
        var token = Guid.NewGuid().ToString("N");
        var seedCode = $"synthetic-{token}";
        try
        {
        await SyntheticTenantSession.SetAsync(db, owner);
        db.Users.Add(new UserRecord { Id = owner, Email = $"injury-link-{token}@example.invalid",
            Username = $"il_{token[..10]}", CredentialType = "Password", PasswordHash = "synthetic-hash",
            CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
        db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
            { Id = Guid.NewGuid(), UserId = owner, TimeZoneId = "UTC" });
        var exercise = new Exercise { Id = Random.Shared.Next(MinimumSyntheticCatalogId, MaximumSyntheticCatalogId),
            SeedCode = seedCode, Code = $"test-{token[..10]}",
            Name = "Synthetic exercise", Description = "Synthetic exercise", JointAreas = "synthetic" };
        db.Exercises.Add(exercise);
        await db.SaveChangesAsync();
        var definition = new WorkoutExerciseDefinition
        {
            Id = exercise.Id, SeedCode = seedCode, Name = "Synthetic exercise",
            Pattern = MovementPattern.SquatPattern, Required = EquipmentSet.None,
            Contraindications = InjuryFlag.Knee | InjuryFlag.Shoulder,
            TrainingCatalogVersionV020 = Ruleset, SupportedExperiencesV020Json = "[\"Beginner\"]",
            DemandTagsV020Json = "[]", DemandTagsReviewedV020 = true,
            RestrictionConflictTagsV020Json = "[]", RestrictionMappingsReviewedV020 = true,
            SupportedTimingMethodsV020Json = "[\"StraightSets\"]", ReviewedSubstituteExerciseIdsV020Json = "[]",
            ReviewEvidenceV020Json = Evidence
        };
        db.WorkoutExerciseDefinitions.Add(definition);
        await db.SaveChangesAsync();
        Xunit.Assert.Empty(WorkoutExerciseV020CatalogMapper.Map(definition).MetadataGaps);
            var planId = Guid.NewGuid();
            var proposalId = Guid.NewGuid();
            var revisionId = Guid.NewGuid();
            var freshDay = Guid.NewGuid();
            var loggedDay = Guid.NewGuid();
            var completedDay = Guid.NewGuid();
            var prescriptionIds = new[] { Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid() };
            var dates = new[] { future, future.AddDays(FutureWeek), future.AddDays(FutureWeek * 2) };
            var days = new[] { freshDay, loggedDay, completedDay };
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = planId, UserId = owner, Name = "Synthetic plan",
                IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
            db.WorkoutPlanProposalsV02.Add(new WorkoutPlanProposalV02Record { Id = proposalId, UserId = owner,
                PlanId = planId, ExpectedRevisionId = Guid.NewGuid(), RulesetVersion = Ruleset,
                TrainingPolicyVersion = Policy, Status = "Accepted", CreatedAtUtc = now,
                DecidedAtUtc = now, AcceptedRevisionId = revisionId });
            var planJson = knownSnapshot ? JsonSerializer.Serialize(new
            {
                proposal = new { sessions = new[] { new { sessionIndex = SessionIndex,
                    slots = new[] { new { exerciseId = exercise.Id } } } } },
                weekdayAssignments = new[] { new { sessionIndex = SessionIndex, weekday = (int)future.DayOfWeek } }
            }) : "{}";
            db.WorkoutPlanRevisionsV02.Add(new WorkoutPlanRevisionV02Record { Id = revisionId, UserId = owner,
                PlanId = planId, RevisionNumber = InitialRevision, SourceProposalId = proposalId,
                RulesetVersion = Ruleset, TrainingPolicyVersion = Policy, PlanJson = planJson, AcceptedAtUtc = now });
            for (var i = 0; i < days.Length; i++)
            {
                db.WorkoutDays.Add(new WorkoutDayRecord { Id = days[i], UserId = owner, WorkoutPlanId = planId,
                    Date = dates[i], DayOfWeek = (int)dates[i].DayOfWeek, WorkoutType = "Strength", Name = "Synthetic strength" });
                db.WorkoutDayExercises.Add(new WorkoutDayExerciseRecord { Id = prescriptionIds[i], UserId = owner,
                    WorkoutDayId = days[i], ExerciseId = exercise.Id, Sets = SyntheticSetCount, Reps = SyntheticRepCount });
            }
            db.WorkoutLogs.Add(new WorkoutLogRecord { Id = Guid.NewGuid(), UserId = owner, WorkoutDayId = loggedDay,
                Date = dates[1], CompletionPercent = CompletePercent, CreatedAt = now });
            db.WorkoutLogs.Add(new WorkoutLogRecord { Id = Guid.NewGuid(), UserId = owner, WorkoutDayId = completedDay,
                Date = dates[2], CompletionPercent = CompletePercent, CreatedAt = now });
            db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = Guid.NewGuid(), UserId = owner,
                PlannedWorkoutDayId = completedDay, WorkoutName = "Synthetic completed session",
                StartedAt = now, CompletedAt = now, CreatedAt = now, UpdatedAt = now });
            await db.SaveChangesAsync();
            var store = new PostgresCheckInAndInjuryStore(db, TimeProvider.System);
            foreach (var site in new[] { InjuryFlag.Knee, InjuryFlag.Shoulder })
                await store.SaveInjuryAsync(new SaveInjuryObservationRequest(owner, Guid.NewGuid(), (int)site,
                    "Acute", DateOnly.FromDateTime(now), SyntheticSetCount, DateOnly.FromDateTime(now)), CancellationToken.None);
            var links = await db.WorkoutInjuryPrescriptionLinks.AsNoTracking().Where(row => row.UserId == owner).ToArrayAsync();
            if (knownSnapshot)
            {
                Xunit.Assert.Equal(2, links.Length);
                Xunit.Assert.All(links, link => Xunit.Assert.Equal(prescriptionIds[0], link.WorkoutDayExerciseId));
                Xunit.Assert.Equal(2, links.Select(link => link.InjuryEpisodeId).Distinct().Count());
            }
            else Xunit.Assert.Empty(links);
            var persisted = await db.WorkoutDays.AsNoTracking().Where(day => day.UserId == owner).ToDictionaryAsync(day => day.Id);
            Xunit.Assert.True(persisted[freshDay].IsRemovedBySafety);
            Xunit.Assert.True(persisted[loggedDay].IsRemovedBySafety);
            Xunit.Assert.False(persisted[completedDay].IsRemovedBySafety);
        }
        finally
        {
            await db.Users.Where(row => row.Id == owner).ExecuteDeleteAsync();
            await db.WorkoutExerciseDefinitions.Where(row => row.SeedCode == seedCode).ExecuteDeleteAsync();
            await db.Exercises.Where(row => row.SeedCode == seedCode).ExecuteDeleteAsync();
        }
    }

    private static PostgresDbContext CreateContext()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection), $"Set {ConnectionVariable} to a disposable local PostgreSQL test database.");
        var builder = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal(LocalHost, builder.Host);
        Xunit.Assert.StartsWith(DatabasePrefix, builder.Database, StringComparison.Ordinal);
        return new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options);
    }
}

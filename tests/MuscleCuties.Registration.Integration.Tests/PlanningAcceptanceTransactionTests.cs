using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.ActiveSession;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.ActiveSession;
using Npgsql;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class PlanningAcceptanceTransactionTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";
    private const int SyntheticDurationMinutes = 30;
    private static readonly DateOnly SyntheticBirthDate = new(1990, 1, 1);

    // This exercises persistence with a deliberately synthetic no-strength remainder;
    // it does not claim catalog or clinical review of a real workout.
    [Xunit.Fact]
    public async Task ApplyCommitsDraftAndRevisionOnceAndPreservesPriorSession()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal("127.0.0.1", parsed.Host);
        Xunit.Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        await using var db = new PostgresDbContext(options);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var oldPlan = Guid.NewGuid();
        var oldDay = Guid.NewGuid();
        var session = Guid.NewGuid();
        var setId = Guid.NewGuid();
        await SyntheticTenantSession.SetAsync(db, user);
        try
        {
        db.Users.Add(new UserRecord { Id = user, Email = $"planning-{user:N}@example.invalid",
            Username = $"plan_{user.ToString("N")[..10]}", CredentialType = "Password", PasswordHash = "synthetic-hash",
            CreatedAt = now, UpdatedAt = now, IsOnboardingComplete = true });
        db.UserProfiles.Add(new UserProfileRecord { Id = Guid.NewGuid(), UserId = user, Name = "Synthetic",
            DateOfBirth = SyntheticBirthDate,
            Goal = "MuscleTone", TrainingExperienceLevel = "Beginner", EquipmentLevel = "FullGym",
            Height = 168m, Weight = 62m, WorkoutDaysPerWeek = 3, CycleLength = 28, UpdatedAt = now });
        db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord { Id = Guid.NewGuid(), UserId = user,
            TimeZoneId = TimeZoneInfo.Utc.Id, BaselineExperience = TrainingExperienceLevel.Beginner,
            EffectiveExperience = TrainingExperienceLevel.Beginner });
        db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = oldPlan, UserId = user, Name = "Synthetic prior plan",
            IsActive = true, CyclePhaseTarget = "Follicular", CreatedAt = now });
        db.WorkoutDays.Add(new WorkoutDayRecord { Id = oldDay, UserId = user, WorkoutPlanId = oldPlan,
            Date = today, DayOfWeek = (int)today.DayOfWeek, WorkoutType = "Cardio", Name = "Synthetic activity" });
        db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord { Id = session, UserId = user,
            PlannedWorkoutDayId = oldDay, WorkoutName = "Synthetic active session", StartedAt = now,
            CreatedAt = now, UpdatedAt = now });
        db.ExerciseSetLogs.Add(new ExerciseSetLogRecord { Id = setId, UserId = user, WorkoutSessionLogId = session,
            ExerciseName = "Synthetic movement", SetNumber = 1, RepsCompleted = 10, CreatedAt = now, UpdatedAt = now });
        await db.SaveChangesAsync();
            var source = Xunit.Assert.IsType<StrengthPlanPreviewSourceV020>(await new PostgresStrengthPlanPreviewSourceV020(db)
                .LoadAsync(user, CancellationToken.None));
            var cycling = (int)WorkoutActivityType.Cycling;
            var strength = (int)WorkoutActivityType.GeneralStrength;
            var weekdays = new[] { (int)today.DayOfWeek, (int)today.AddDays(2).DayOfWeek };
            var draft = new WorkoutPreferencesDto([strength, cycling], weekdays,
                new Dictionary<int, int> { [strength] = SyntheticDurationMinutes, [cycling] = SyntheticDurationMinutes }, "FullGym");
            var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, today, null)
                { DraftPreferences = draft };
            var schedule = new MixedActivityScheduleV020(
                [new(cycling, today.DayOfWeek, SyntheticDurationMinutes, false, 1)],
                [new(cycling, "Scheduled", null, null, null, 1, []),
                 new(strength, "Omitted", null, null, null, 0, [])], []);
            var preview = new StrengthPlanPreviewResponseV020(StrengthPlanPreviewRule.PolicyId,
                StrengthPlanPreviewRule.Version, StrengthPlanPreviewRule.RulesetVersion, "Feasible", 0, [], [], [], [], schedule)
            {
                SourceProfileVersion = source.ProfileVersion, SourceInjuryFingerprint = source.InjuryFingerprint,
                SourceActivePlanId = source.ActivePlanId, SourceActiveRevisionId = source.ActiveRevisionId,
                SourceHistoryFingerprint = source.HistoryFingerprint, SourceCatalogFingerprint = source.CatalogFingerprint,
                SourceTimeZoneVersion = source.TimeZoneVersion, SourceExperienceVersion = source.ExperienceVersion,
                SourceAnswerVersion = source.AnswerVersion
            };
            var created = await new PostgresStrengthPlanProposalStoreV020(db).SaveAsync(user, request, preview, CancellationToken.None);
            var lease = Xunit.Assert.IsType<PlanningLeaseDto>(created.Lease);
            var command = new AcceptStrengthPlanProposalRequestV020(created.ProposalId, lease.LeaseId, lease.Fence,
                created.ProposalVersion, ReplacementConfirmed: true);
            var store = new PostgresAcceptStrengthPlanProposalStoreV020(db);
            var accepted = await store.AcceptAsync(user, command, CancellationToken.None);
            Xunit.Assert.Equal("Accepted", accepted.Status);
            var replay = await store.AcceptAsync(user, command, CancellationToken.None);
            Xunit.Assert.Equal(accepted, replay);
            Xunit.Assert.Single(await db.WorkoutPlans.AsNoTracking().Where(row => row.UserId == user && row.IsActive).ToListAsync());
            Xunit.Assert.Single(await db.WorkoutPlanRevisionsV02.AsNoTracking().Where(row => row.UserId == user).ToListAsync());
            Xunit.Assert.Contains(nameof(WorkoutActivityType.Cycling), (await db.UserProfiles.AsNoTracking()
                .SingleAsync(row => row.UserId == user)).PreferredWorkoutActivityTypes);
            Xunit.Assert.False((await db.WorkoutPlans.AsNoTracking().SingleAsync(row => row.Id == oldPlan)).IsActive);
            Xunit.Assert.Equal(oldDay, (await db.WorkoutSessionLogs.AsNoTracking().SingleAsync(row => row.Id == session)).PlannedWorkoutDayId);

            var manualSessionId = Guid.NewGuid();
            var activeStore = new PostgresWorkoutActiveSessionStore(db, new PostgresIdempotencyExecutor(), new FixedTimeProvider(now));
            var legacyCommand = new StartWorkoutSessionCommand("synthetic-legacy", Guid.NewGuid(), "Legacy activity", now);
            var legacyStart = await activeStore.StartSessionAsync(user, legacyCommand, CancellationToken.None);
            var oldHash = PostgresIdempotencyExecutor.ComputeRequestHash(new { legacyCommand.SessionId,
                legacyCommand.WorkoutName, legacyCommand.StartedAtUtc, legacyCommand.Notes, legacyCommand.PlannedWorkoutDayId });
            Xunit.Assert.Equal(oldHash, (await db.CommandIdempotencyRecords.AsNoTracking().SingleAsync(row => row.UserId == user &&
                row.CommandKey == "workout.session.start:synthetic-legacy")).RequestHash);
            var legacyReplay = await activeStore.StartSessionAsync(user, legacyCommand, CancellationToken.None);
            Xunit.Assert.Equal(legacyStart.SessionId, legacyReplay.SessionId);
            await Xunit.Assert.ThrowsAsync<InvalidOperationException>(() => activeStore.StartSessionAsync(user,
                new StartWorkoutSessionCommand("synthetic-rest", Guid.NewGuid(), "Rest", now,
                    UnplannedActivity: WorkoutActivityDto.RestDay), CancellationToken.None));
            await activeStore.StartSessionAsync(user, new StartWorkoutSessionCommand("synthetic-start", manualSessionId,
                "Synthetic walk", now, UnplannedActivity: WorkoutActivityDto.Walking), CancellationToken.None);
            await activeStore.FinishSessionAsync(user, new FinishWorkoutSessionCommand("synthetic-finish", manualSessionId,
                now.AddDays(-1), SyntheticDurationMinutes, null, null), CancellationToken.None);
            var manual = await db.WorkoutSessionLogs.AsNoTracking().SingleAsync(row => row.Id == manualSessionId);
            Xunit.Assert.Equal(today, manual.UnplannedCompletionLocalDate);
            Xunit.Assert.Equal(TimeZoneInfo.Utc.Id, manual.UnplannedCompletionTimeZoneId);
            Xunit.Assert.NotEqual(now.AddDays(-1), manual.CompletedAt);
            Xunit.Assert.False(await db.WorkoutCompletionDayCredits.AsNoTracking().AnyAsync(row => row.UserId == user));
            Xunit.Assert.Null((await db.WorkoutSessionLogs.AsNoTracking().SingleAsync(row => row.Id == session)).CompletedAt);
            Xunit.Assert.Equal(session, (await db.ExerciseSetLogs.AsNoTracking().SingleAsync(row => row.Id == setId)).WorkoutSessionLogId);
            var history = await activeStore.GetPlannedSessionHistoryAsync(user,
                PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(oldDay), CancellationToken.None);
            Xunit.Assert.Contains(history, item => item.SessionId == session && item.CompletedAtUtc is null);
            var otherHistory = await activeStore.GetPlannedSessionHistoryAsync(Guid.NewGuid(),
                PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(oldDay), CancellationToken.None);
            Xunit.Assert.Empty(otherHistory);

            var current = Xunit.Assert.IsType<StrengthPlanPreviewSourceV020>(await new PostgresStrengthPlanPreviewSourceV020(db)
                .LoadAsync(user, CancellationToken.None));
            var laterPreview = preview with { SourceProfileVersion = current.ProfileVersion,
                SourceActivePlanId = current.ActivePlanId, SourceActiveRevisionId = current.ActiveRevisionId,
                SourceHistoryFingerprint = current.HistoryFingerprint, SourceInjuryFingerprint = current.InjuryFingerprint,
                SourceCatalogFingerprint = current.CatalogFingerprint, SourceTimeZoneVersion = current.TimeZoneVersion,
                SourceExperienceVersion = current.ExperienceVersion, SourceAnswerVersion = current.AnswerVersion };
            var later = await new PostgresStrengthPlanProposalStoreV020(db).SaveAsync(user, request, laterPreview, CancellationToken.None);
            var laterLease = Xunit.Assert.IsType<PlanningLeaseDto>(later.Lease);
            await db.WorkoutAccountPlanningStates.Where(row => row.UserId == user)
                .ExecuteUpdateAsync(update => update.SetProperty(row => row.AnswerVersion, row => row.AnswerVersion + 1));
            var stale = await store.AcceptAsync(user, new AcceptStrengthPlanProposalRequestV020(later.ProposalId,
                laterLease.LeaseId, laterLease.Fence, later.ProposalVersion, ReplacementConfirmed: true), CancellationToken.None);
            Xunit.Assert.Equal("Stale", stale.Status);
            Xunit.Assert.Equal(accepted.PlanId, (await db.WorkoutPlans.AsNoTracking()
                .SingleAsync(row => row.UserId == user && row.IsActive)).Id);
            Xunit.Assert.Single(await db.WorkoutPlanRevisionsV02.AsNoTracking().Where(row => row.UserId == user).ToListAsync());
            Xunit.Assert.Equal(oldDay, (await db.WorkoutSessionLogs.AsNoTracking().SingleAsync(row => row.Id == session)).PlannedWorkoutDayId);

            await new PostgresStrengthPlanProposalStoreV020(db).CloseAsync(user,
                new PlanningLeaseRequest(later.ProposalId, laterLease.LeaseId, laterLease.Fence), CancellationToken.None);
            var fresh = Xunit.Assert.IsType<StrengthPlanPreviewSourceV020>(await new PostgresStrengthPlanPreviewSourceV020(db)
                .LoadAsync(user, CancellationToken.None));
            var rollbackPreview = preview with { SourceProfileVersion = fresh.ProfileVersion,
                SourceActivePlanId = fresh.ActivePlanId, SourceActiveRevisionId = fresh.ActiveRevisionId,
                SourceHistoryFingerprint = fresh.HistoryFingerprint, SourceInjuryFingerprint = fresh.InjuryFingerprint,
                SourceCatalogFingerprint = fresh.CatalogFingerprint, SourceTimeZoneVersion = fresh.TimeZoneVersion,
                SourceExperienceVersion = fresh.ExperienceVersion, SourceAnswerVersion = fresh.AnswerVersion };
            var rollbackRequest = request with { DraftPreferences = draft with
                { AvailableWeekdays = [(int)today.DayOfWeek, (int)today.AddDays(3).DayOfWeek] } };
            var rollbackProposal = await new PostgresStrengthPlanProposalStoreV020(db)
                .SaveAsync(user, rollbackRequest, rollbackPreview, CancellationToken.None);
            var rollbackLease = Xunit.Assert.IsType<PlanningLeaseDto>(rollbackProposal.Lease);
            var beforeWeekdays = (await db.UserProfiles.AsNoTracking().SingleAsync(row => row.UserId == user)).AvailableWorkoutWeekdaysJson;
            await db.WorkoutAccountPlanningStates.Where(row => row.UserId == user).ExecuteUpdateAsync(update => update
                .SetProperty(row => row.PromotionPending, true).SetProperty(row => row.SafetyRegenerationPending, true));
            var interceptor = new ThrowAfterSaveInterceptor();
            var throwingOptions = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection)
                .AddInterceptors(interceptor).Options;
            await using var throwingDb = new PostgresDbContext(throwingOptions);
            await Xunit.Assert.ThrowsAsync<SyntheticPostWriteFailure>(() =>
                new PostgresAcceptStrengthPlanProposalStoreV020(throwingDb).AcceptAsync(user,
                    new AcceptStrengthPlanProposalRequestV020(rollbackProposal.ProposalId, rollbackLease.LeaseId,
                        rollbackLease.Fence, rollbackProposal.ProposalVersion, ReplacementConfirmed: true), CancellationToken.None));
            Xunit.Assert.True(interceptor.ObservedActiveTransaction);
            Xunit.Assert.Equal(beforeWeekdays, (await db.UserProfiles.AsNoTracking().SingleAsync(row => row.UserId == user)).AvailableWorkoutWeekdaysJson);
            Xunit.Assert.Equal(accepted.PlanId, (await db.WorkoutPlans.AsNoTracking()
                .SingleAsync(row => row.UserId == user && row.IsActive)).Id);
            Xunit.Assert.Single(await db.WorkoutPlanRevisionsV02.AsNoTracking().Where(row => row.UserId == user).ToListAsync());
            var unchangedProposal = await db.WorkoutPlanProposalsV02.AsNoTracking()
                .SingleAsync(row => row.Id == rollbackProposal.ProposalId);
            Xunit.Assert.Equal("Proposed", unchangedProposal.Status);
            Xunit.Assert.Equal(rollbackLease.LeaseId, unchangedProposal.LeaseId);
            Xunit.Assert.Equal(rollbackLease.Fence, unchangedProposal.LeaseFence);
            Xunit.Assert.Equal(rollbackLease.ExpiresAtUtc.UtcDateTime, unchangedProposal.LeaseExpiresAtUtc, TimeSpan.FromMilliseconds(1));
            var pending = await db.WorkoutAccountPlanningStates.AsNoTracking().SingleAsync(row => row.UserId == user);
            Xunit.Assert.True(pending.PromotionPending);
            Xunit.Assert.True(pending.SafetyRegenerationPending);
            Xunit.Assert.Equal(oldDay, (await db.WorkoutSessionLogs.AsNoTracking().SingleAsync(row => row.Id == session)).PlannedWorkoutDayId);
            Xunit.Assert.Equal(session, (await db.ExerciseSetLogs.AsNoTracking().SingleAsync(row => row.Id == setId)).WorkoutSessionLogId);
            await db.WorkoutAccountPlanningStates.Where(row => row.UserId == user).ExecuteUpdateAsync(update => update
                .SetProperty(row => row.TimeZoneId, "America/Toronto")
                .SetProperty(row => row.TimeZoneVersion, row => row.TimeZoneVersion + 1));
            var afterZoneChange = await db.WorkoutSessionLogs.AsNoTracking().SingleAsync(row => row.Id == manualSessionId);
            Xunit.Assert.Equal(today, afterZoneChange.UnplannedCompletionLocalDate);
            Xunit.Assert.Equal(TimeZoneInfo.Utc.Id, afterZoneChange.UnplannedCompletionTimeZoneId);

        }
        finally
        {
            await db.Users.Where(row => row.Id == user).ExecuteDeleteAsync();
        }
    }

    private sealed class FixedTimeProvider(DateTime utc) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(DateTime.SpecifyKind(utc, DateTimeKind.Utc));
    }

    private sealed class SyntheticPostWriteFailure : Exception { }

    private sealed class ThrowAfterSaveInterceptor : SaveChangesInterceptor
    {
        public bool ObservedActiveTransaction { get; private set; }
        public override ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            ObservedActiveTransaction = eventData.Context?.Database.CurrentTransaction is not null;
            throw new SyntheticPostWriteFailure();
        }
    }
}

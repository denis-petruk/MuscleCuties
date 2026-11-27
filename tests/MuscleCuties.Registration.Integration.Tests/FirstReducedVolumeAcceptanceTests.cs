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

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class FirstReducedVolumeAcceptanceTests
{
    [PostgresFact]
    public async Task First_reduced_plan_accepts_without_acknowledgment_and_replacement_cannot_replace_silently()
    {
        var connection = Environment.GetEnvironmentVariable("MC_REGISTRATION_WRITE_TEST_POSTGRES");
        Xunit.Assert.False(string.IsNullOrWhiteSpace(connection));
        var parsed = new NpgsqlConnectionStringBuilder(connection);
        Xunit.Assert.Equal("127.0.0.1", parsed.Host);
        Xunit.Assert.StartsWith("mc_test_", parsed.Database, StringComparison.Ordinal);
        await using var db = new PostgresDbContext(new DbContextOptionsBuilder<PostgresDbContext>()
            .UseNpgsql(connection).Options);
        var user = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        await SyntheticTenantSession.SetAsync(db, user);
        try
        {
            db.Users.Add(new UserRecord
            {
                Id = user,
                Email = $"reduced-{user:N}@example.invalid",
                Username = $"reduced_{user.ToString("N")[..10]}",
                CredentialType = "Password",
                PasswordHash = "synthetic-hash",
                CreatedAt = now,
                UpdatedAt = now,
                IsOnboardingComplete = true
            });
            db.UserProfiles.Add(new UserProfileRecord
            {
                Id = Guid.NewGuid(),
                UserId = user,
                Name = "Synthetic",
                DateOfBirth = new DateOnly(1990, 1, 1),
                Goal = "MuscleTone",
                TrainingExperienceLevel = "Beginner",
                EquipmentLevel = "FullGym",
                Height = 168m,
                Weight = 62m,
                WorkoutDaysPerWeek = 1,
                CycleLength = 28,
                UpdatedAt = now
            });
            db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
            {
                Id = Guid.NewGuid(),
                UserId = user,
                TimeZoneId = TimeZoneInfo.Utc.Id,
                BaselineExperience = TrainingExperienceLevel.Beginner,
                EffectiveExperience = TrainingExperienceLevel.Beginner
            });
            await db.SaveChangesAsync();

            var strength = (int)WorkoutActivityType.GeneralStrength;
            var day = today.AddDays(2).DayOfWeek;
            var draft = new WorkoutPreferencesDto([strength], [(int)day],
                new Dictionary<int, int> { [strength] = 30 }, "FullGym");
            var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, today, null)
            { DraftPreferences = draft };
            async Task<StrengthPlanProposalCreatedV020> CreateProposal()
            {
                var source = Xunit.Assert.IsType<StrengthPlanPreviewSourceV020>(
                    await new PostgresStrengthPlanPreviewSourceV020(db).LoadAsync(user, CancellationToken.None));
                var exercise = source.Catalog.First(entry =>
                    source.MuscleGroupsByExerciseId?.ContainsKey(entry.Candidate.ExerciseId) == true);
                var session = new StrengthPlanPreviewSessionV020("synthetic-template", "synthetic-version", 1, 30,
                    [new StrengthPlanSlotOutputV020("synthetic-slot", (int)StrengthMovementSlotDto.KneeDominant,
                        true, (int)StrengthPrescriptionRole.Main, false, exercise.Candidate.ExerciseId,
                        exercise.Name, null, null, 3, 8, 12, 2, 90, [])],
                    [], new Dictionary<string, string>(), day);
                var schedule = new MixedActivityScheduleV020(
                    [new(strength, day, 30, true, 1)],
                    [new(strength, "Scheduled", null, null, null, 1, [])], []);
                var preview = new StrengthPlanPreviewResponseV020(StrengthPlanPreviewRule.PolicyId,
                    StrengthPlanPreviewRule.Version, StrengthPlanPreviewRule.RulesetVersion, "Feasible", 0,
                    [session], [], [], [], schedule)
                {
                    RequiresReducedVolumeAcknowledgment = true,
                    SourceProfileVersion = source.ProfileVersion,
                    SourceInjuryFingerprint = source.InjuryFingerprint,
                    SourceActivePlanId = source.ActivePlanId,
                    SourceActiveRevisionId = source.ActiveRevisionId,
                    SourceHistoryFingerprint = source.HistoryFingerprint,
                    SourceCatalogFingerprint = source.CatalogFingerprint,
                    SourceTimeZoneVersion = source.TimeZoneVersion,
                    SourceExperienceVersion = source.ExperienceVersion,
                    SourceAnswerVersion = source.AnswerVersion,
                    SourceMuscleGroupsByExerciseId = source.MuscleGroupsByExerciseId?.ToDictionary(
                        pair => pair.Key, pair => pair.Value.ToArray())
                };
                return await new PostgresStrengthPlanProposalStoreV020(db).SaveAsync(user, request, preview, CancellationToken.None);
            }

            var first = await CreateProposal();
            var lease = Xunit.Assert.IsType<PlanningLeaseDto>(first.Lease);
            var store = new PostgresAcceptStrengthPlanProposalStoreV020(db);
            var accepted = await store.AcceptAsync(user, new AcceptStrengthPlanProposalRequestV020(first.ProposalId,
                lease.LeaseId, lease.Fence, first.ProposalVersion, ReplacementConfirmed: false,
                ReducedVolumeAcknowledged: false), CancellationToken.None);
            Xunit.Assert.Equal("Accepted", accepted.Status);
            Xunit.Assert.NotNull(accepted.PlanId);

            var replacement = await CreateProposal();
            var replacementLease = Xunit.Assert.IsType<PlanningLeaseDto>(replacement.Lease);
            var rejected = await store.AcceptAsync(user, new AcceptStrengthPlanProposalRequestV020(replacement.ProposalId,
                replacementLease.LeaseId, replacementLease.Fence, replacement.ProposalVersion,
                ReplacementConfirmed: false, ReducedVolumeAcknowledged: false), CancellationToken.None);
            Xunit.Assert.Equal("Conflict", rejected.Status);
            Xunit.Assert.Equal(accepted.PlanId, (await db.WorkoutPlans.AsNoTracking()
                .SingleAsync(row => row.UserId == user && row.IsActive)).Id);
            var unacknowledged = await store.AcceptAsync(user, new AcceptStrengthPlanProposalRequestV020(replacement.ProposalId,
                replacementLease.LeaseId, replacementLease.Fence, replacement.ProposalVersion,
                ReplacementConfirmed: true, ReducedVolumeAcknowledged: false), CancellationToken.None);
            Xunit.Assert.Equal("Conflict", unacknowledged.Status);
            var unconfirmed = await store.AcceptAsync(user, new AcceptStrengthPlanProposalRequestV020(replacement.ProposalId,
                replacementLease.LeaseId, replacementLease.Fence, replacement.ProposalVersion,
                ReplacementConfirmed: false, ReducedVolumeAcknowledged: true), CancellationToken.None);
            Xunit.Assert.Equal("Conflict", unconfirmed.Status);
            Xunit.Assert.Equal(accepted.PlanId, (await db.WorkoutPlans.AsNoTracking()
                .SingleAsync(row => row.UserId == user && row.IsActive)).Id);
            var replaced = await store.AcceptAsync(user, new AcceptStrengthPlanProposalRequestV020(replacement.ProposalId,
                replacementLease.LeaseId, replacementLease.Fence, replacement.ProposalVersion,
                ReplacementConfirmed: true, ReducedVolumeAcknowledged: true), CancellationToken.None);
            Xunit.Assert.Equal("Accepted", replaced.Status);
            Xunit.Assert.NotEqual(accepted.PlanId, replaced.PlanId);
        }
        finally
        {
            await db.Users.Where(row => row.Id == user).ExecuteDeleteAsync();
        }
    }
}

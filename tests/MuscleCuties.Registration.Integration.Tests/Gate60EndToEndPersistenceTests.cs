using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Domain.Workout.Planning.V2;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Infrastructure.PostgreSql.Account.Deletion;
using MuscleCuties.Infrastructure.PostgreSql.Models.App;
using MuscleCuties.Infrastructure.PostgreSql.Persistence;
using MuscleCuties.Infrastructure.PostgreSql.Profile.ServerProfile;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Planning;
using MuscleCuties.Infrastructure.PostgreSql.Workout.Session;
using MuscleCuties.UseCases.Features.Profile.ServerProfile;
using MuscleCuties.UseCases.Features.Profile.WorkoutPreferences;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Xunit;

namespace MuscleCuties.Registration.Integration.Tests;

public sealed class Gate60EndToEndPersistenceTests
{
    private const string ConnectionVariable = "MC_REGISTRATION_WRITE_TEST_POSTGRES";

    [PostgresFact]
    public async Task Equipment_capabilities_persist_and_unlock_gate60_exercises_only_when_confirmed()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));

        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        await using var db = new PostgresDbContext(options);
        var user = Guid.CreateVersion7();
        var now = new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc);
        var today = new DateOnly(2026, 11, 16);

        try
        {
            db.Users.Add(new UserRecord
            {
                Id = user,
                Email = $"gate60-{user:N}@example.invalid",
                Username = $"g60_{user.ToString("N")[..10]}",
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
                DateOfBirth = new DateOnly(1996, 5, 1),
                Goal = "MaintainHealth",
                TrainingExperienceLevel = "Beginner",
                EquipmentLevel = "Bodyweight",
                PreferredWorkoutActivityTypes = "GeneralStrength",
                AvailableWorkoutWeekdaysJson = "[\"Monday\",\"Wednesday\",\"Friday\"]",
                ActivitySessionDurationsJson = "{\"GeneralStrength\":60}",
                Height = 168m,
                Weight = 62m,
                WorkoutDaysPerWeek = 3,
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

            var profileStore = new PostgresProfileDataStore(db, TimeProvider.System);
            var updateCaps = new EquipmentCapabilities(HasSliders: true, HasConfirmedBandAnchor: true, HasDumbbells: false, HasBands: true);
            var write = await profileStore.UpdateWorkoutPreferencesAsync(new UpdateServerWorkoutPreferencesRequest(
                user, 1,
                [(int)WorkoutActivityDto.GeneralStrength],
                [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday, (int)DayOfWeek.Friday],
                new Dictionary<int, int> { [(int)WorkoutActivityDto.GeneralStrength] = 60 },
                EquipmentLevel: "HomeDumbbellsBands",
                EquipmentCapabilities: updateCaps), CancellationToken.None);
            Assert.Equal(ServerProfileWriteStatus.Saved, write.Status);

            var snapshot = await profileStore.LoadAsync(user, CancellationToken.None);
            Assert.NotNull(snapshot);
            Assert.Equal("HomeDumbbellsBands", snapshot!.EquipmentLevel);
            Assert.Equal(updateCaps, snapshot.EquipmentCapabilities);

            var previewSource = await new PostgresStrengthPlanPreviewSourceV020(db).LoadAsync(user, CancellationToken.None);
            Assert.NotNull(previewSource);
            Assert.NotNull(previewSource!.MuscleContributionsByExerciseId);
            Assert.Equal(updateCaps, previewSource.SavedAnswers?.EquipmentCapabilities);

            var preview = GenerationPlanV2Mapper.ForCanonicalUse(PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(
                user,
                new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, today,
                    new ActivityTrainingHistoryInputV020("strength", 0, 0m, null, true), WarmupMinutes: 10, CooldownMinutes: 5),
                previewSource));

            Assert.Equal("Feasible", preview.Status);
            var names = preview.GenerationV2!.Sessions.SelectMany(s => s.Exercises)
                .Select(e => e.ExerciseName).ToHashSet(StringComparer.Ordinal);
            Assert.Contains("Resistance Band Row", names);
            Assert.Contains("Resistance Band Chest Press", names);
            Assert.Contains("Slider Leg Curl", names);
        }
        finally
        {
            await new PostgresAccountPurgeStore(db).PurgeAccountDataAsync(user);
        }
    }

    [PostgresFact]
    public async Task Accepting_proposal_with_draft_equipment_capabilities_persists_them_and_avoids_needs_review()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));

        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        await using var db = new PostgresDbContext(options);
        var user = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        try
        {
            db.Users.Add(new UserRecord
            {
                Id = user,
                Email = $"gate60-accept-{user:N}@example.invalid",
                Username = $"g60a_{user.ToString("N")[..9]}",
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
                DateOfBirth = new DateOnly(1996, 5, 1),
                Goal = "MaintainHealth",
                TrainingExperienceLevel = "Beginner",
                EquipmentLevel = "Bodyweight",
                PreferredWorkoutActivityTypes = "GeneralStrength",
                AvailableWorkoutWeekdaysJson = "[\"Monday\",\"Wednesday\",\"Friday\"]",
                ActivitySessionDurationsJson = "{\"GeneralStrength\":60}",
                Height = 168m,
                Weight = 62m,
                WorkoutDaysPerWeek = 3,
                CycleLength = 28,
                UpdatedAt = now
            });
            db.WorkoutAccountPlanningStates.Add(new WorkoutAccountPlanningStateRecord
            {
                Id = Guid.NewGuid(),
                UserId = user,
                TimeZoneId = TimeZoneInfo.Utc.Id,
                BaselineExperience = TrainingExperienceLevel.Beginner,
                EffectiveExperience = TrainingExperienceLevel.Beginner,
                AnswerVersion = 1,
                AnswersJson = "{}"
            });
            await db.SaveChangesAsync();

            var source = await new PostgresStrengthPlanPreviewSourceV020(db).LoadAsync(user, CancellationToken.None);
            Assert.NotNull(source);

            var draft = new WorkoutPreferencesDto(
                [(int)WorkoutActivityDto.GeneralStrength],
                [(int)DayOfWeek.Monday, (int)DayOfWeek.Wednesday, (int)DayOfWeek.Friday],
                new Dictionary<int, int> { [(int)WorkoutActivityDto.GeneralStrength] = 60 },
                "HomeDumbbellsBands",
                HasSliders: true,
                HasConfirmedBandAnchor: true,
                HasDumbbells: false,
                HasBands: true);
            var request = new StrengthPlanPreviewRequestV020(WorkoutActivityDto.GeneralStrength, today, null)
            {
                DraftPreferences = draft,
                SelectedActivityTypes = new HashSet<int> { (int)WorkoutActivityDto.GeneralStrength },
                AvailableWeekdays = [DayOfWeek.Monday, DayOfWeek.Wednesday, DayOfWeek.Friday],
                ActivitySessionDurationsMinutes = new Dictionary<int, int> { [(int)WorkoutActivityDto.GeneralStrength] = 60 },
                WarmupMinutes = 10,
                CooldownMinutes = 5
            };
            var preview = PreviewStrengthPlanV020UseCase.EvaluateLoadedSource(
                user, request, source! with { DraftEquipmentCapabilities = new EquipmentCapabilities(true, true, false, true) });
            var proposalStore = new PostgresStrengthPlanProposalStoreV020(db);
            var created = await proposalStore.SaveAsync(user, request, preview, CancellationToken.None);
            var lease = Assert.IsType<PlanningLeaseDto>(created.Lease);

            var accepted = await new PostgresAcceptStrengthPlanProposalStoreV020(db).AcceptAsync(
                user,
                new AcceptStrengthPlanProposalRequestV020(created.ProposalId, lease.LeaseId, lease.Fence, created.ProposalVersion),
                CancellationToken.None);
            Assert.Equal("Accepted", accepted.Status);

            var profileAfter = await new PostgresProfileDataStore(db, TimeProvider.System).LoadAsync(user, CancellationToken.None);
            Assert.NotNull(profileAfter?.EquipmentCapabilities);
            Assert.True(profileAfter!.EquipmentCapabilities!.HasSliders);
            Assert.True(profileAfter.EquipmentCapabilities.HasConfirmedBandAnchor);
            Assert.False(profileAfter.EquipmentCapabilities.HasDumbbells);
            Assert.True(profileAfter.EquipmentCapabilities.HasBands);

            var reconcileOutcome = await new PostgresWorkoutPlanReconciler(db).ReconcileAsync(user, CancellationToken.None);
            Assert.NotEqual(WorkoutReconciliationOutcome.NeedsReview, reconcileOutcome);
        }
        finally
        {
            await new PostgresAccountPurgeStore(db).PurgeAccountDataAsync(user);
        }
    }

    [PostgresFact]
    public async Task Band_and_slider_exercises_in_session_detail_never_require_or_invent_kg_weight()
    {
        var connection = Environment.GetEnvironmentVariable(ConnectionVariable);
        Assert.False(string.IsNullOrWhiteSpace(connection));

        var options = new DbContextOptionsBuilder<PostgresDbContext>().UseNpgsql(connection).Options;
        await using var db = new PostgresDbContext(options);
        var user = Guid.CreateVersion7();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        try
        {
            db.Users.Add(new UserRecord
            {
                Id = user,
                Email = $"gate60-band-{user:N}@example.invalid",
                Username = $"g60b_{user.ToString("N")[..9]}",
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
                DateOfBirth = new DateOnly(1996, 5, 1),
                Goal = "MaintainHealth",
                TrainingExperienceLevel = "Intermediate",
                EquipmentLevel = "HomeDumbbellsBands",
                PreferredWorkoutActivityTypes = "GeneralStrength",
                AvailableWorkoutWeekdaysJson = "[\"Monday\",\"Wednesday\",\"Friday\"]",
                ActivitySessionDurationsJson = "{\"GeneralStrength\":60}",
                Height = 168m,
                Weight = 62m,
                WorkoutDaysPerWeek = 3,
                CycleLength = 28,
                UpdatedAt = now
            });
            var bandExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Code == "ENGINE_148");
            var sliderExercise = await db.Exercises.AsNoTracking().SingleAsync(e => e.Code == "ENGINE_119");
            var planId = Guid.NewGuid();
            var priorDayId = Guid.NewGuid();
            var dayId = Guid.NewGuid();
            var bandRowDayExId = Guid.NewGuid();
            var sliderDayExId = Guid.NewGuid();
            var sessionId = Guid.NewGuid();
            db.WorkoutPlans.Add(new WorkoutPlanRecord { Id = planId, UserId = user, Name = "Synthetic Plan", IsActive = true, CreatedAt = now });
            db.WorkoutDays.AddRange(
                new WorkoutDayRecord { Id = priorDayId, UserId = user, WorkoutPlanId = planId, WeekIndex = 1, DayOfWeek = (int)today.DayOfWeek, Date = today.AddDays(-7), Name = "Prior", WorkoutType = "Strength", DayOrder = 1 },
                new WorkoutDayRecord { Id = dayId, UserId = user, WorkoutPlanId = planId, WeekIndex = 1, DayOfWeek = (int)today.DayOfWeek, Date = today, Name = "Today", WorkoutType = "Strength", DayOrder = 1 });
            db.WorkoutDayExercises.AddRange(
                new WorkoutDayExerciseRecord { Id = bandRowDayExId, UserId = user, WorkoutDayId = dayId, ExerciseId = bandExercise.Id, Sets = 3, Reps = 12 },
                new WorkoutDayExerciseRecord { Id = sliderDayExId, UserId = user, WorkoutDayId = dayId, ExerciseId = sliderExercise.Id, Sets = 3, Reps = 10 });
            var priorLogId = Guid.NewGuid();
            db.WorkoutLogs.Add(new WorkoutLogRecord { Id = priorLogId, UserId = user, WorkoutDayId = priorDayId, Date = today.AddDays(-7), CompletionPercent = 100, CreatedAt = now.AddDays(-7) });
            db.WorkoutExerciseLogs.Add(new WorkoutExerciseLogRecord
            {
                Id = Guid.NewGuid(), UserId = user, WorkoutLogId = priorLogId, WorkoutDayExerciseId = bandRowDayExId,
                ExerciseId = bandExercise.Id, CompletedSets = 3, CompletedReps = 12, WeightKg = 5m, CreatedAt = now.AddDays(-7)
            });
            var generation = new GenerationSessionV2(0, today.DayOfWeek, today, (int)WorkoutActivityType.GeneralStrength, 6, 30m,
                [new GenerationExerciseV2(0, bandExercise.Id, bandExercise.Name, 1, 3, 10, 15, 2, 90, "CalibrationRequired", null, null, false, "InitialLoadCalibrationRequired"),
                 new GenerationExerciseV2(1, sliderExercise.Id, sliderExercise.Name, 0, 3, 8, 12, 2, 90, "CalibrationRequired", null, null, true, "InitialLoadCalibrationRequired")]);
            var snapshot = new PlannedPrescriptionSnapshot(dayId, "Today", "Strength", (int)WorkoutActivityType.GeneralStrength, null,
                [new PlannedExerciseSnapshot(bandRowDayExId, bandExercise.Id, 3, 1, 12, null, false),
                 new PlannedExerciseSnapshot(sliderDayExId, sliderExercise.Id, 3, 0, 10, null, false)]) { GenerationV2 = generation };
            db.WorkoutSessionLogs.Add(new WorkoutSessionLogRecord
            {
                Id = sessionId, UserId = user, PlannedWorkoutDayId = dayId, PlannedPrescriptionSnapshotJson = snapshot.Serialize(),
                WorkoutName = "Today", StartedAt = now, CreatedAt = now, UpdatedAt = now
            });
            await db.SaveChangesAsync();

            var stableDayId = PostgresWorkoutPlanAndSessionStore.ToStableWorkoutIntId(dayId);
            var detail = await new PostgresWorkoutPlanAndSessionStore(db).GetDetailAsync(user, stableDayId);

            var bandDetail = Assert.Single(detail.Exercises, e => e.ExerciseId == bandExercise.Id);
            Assert.False(bandDetail.UsesWeight);
            Assert.False(bandDetail.RequiresInitialCalibration);
            Assert.Null(bandDetail.RecommendationLoadKg);
            Assert.Equal("Workout_Recommendation_BandProgressReps", bandDetail.RecommendationCode);

            var sliderDetail = Assert.Single(detail.Exercises, e => e.ExerciseId == sliderExercise.Id);
            Assert.False(sliderDetail.RequiresInitialCalibration);
            Assert.Equal("0", sliderDetail.LoggedWeightText);
        }
        finally
        {
            await new PostgresAccountPurgeStore(db).PurgeAccountDataAsync(user);
        }
    }
}

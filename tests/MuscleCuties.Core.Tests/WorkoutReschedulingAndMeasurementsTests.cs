using System.Globalization;
using MuscleCuties.Core.Domain.Progress;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Users;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Progress.BodyMeasurements;
using MuscleCuties.UseCases.Features.Workout.Overview.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutReschedulingAndMeasurementsTests
{
    private static readonly DateOnly Today = new(2026, 11, 6);
    private static readonly DateOnly TargetEmptyDate = Today.AddDays(2);
    private static readonly Guid OwnerId = Guid.NewGuid();

    [Fact]
    public void Reschedule_EnsuresValidReschedule_ThrowsOnInvalidIdOrDate()
    {
        Assert.Throws<ArgumentException>(() =>
            WorkoutScheduleMathRule.EnsureValidReschedule(0, TargetEmptyDate));

        Assert.Throws<ArgumentException>(() =>
            WorkoutScheduleMathRule.EnsureValidReschedule(-1, TargetEmptyDate));

        Assert.Throws<ArgumentException>(() =>
            WorkoutScheduleMathRule.EnsureValidReschedule(101, new DateOnly(1969, 12, 31)));
    }

    [Fact]
    public void Reschedule_Request_ConstructsValidRecord()
    {
        var request = new RescheduleWorkoutDayRequest(101, TargetEmptyDate);
        Assert.Equal(101, request.WorkoutDayId);
        Assert.Equal(TargetEmptyDate, request.TargetDate);
    }

    [Fact]
    public void MovementNote_Validates_Length_And_Id()
    {
        Assert.True(WorkoutSessionActionRule.IsValidMovementNote(501, "Form feels strong, kept shoulder blades tucked"));
        Assert.True(WorkoutSessionActionRule.IsValidMovementNote(501, string.Empty));
        Assert.True(WorkoutSessionActionRule.IsValidMovementNote(501, null));

        // Invalid identifier
        Assert.False(WorkoutSessionActionRule.IsValidMovementNote(0, "Note"));
        Assert.False(WorkoutSessionActionRule.IsValidMovementNote(-1, "Note"));

        // Max characters: 2000
        var tooLong = new string('a', 2001);
        Assert.False(WorkoutSessionActionRule.IsValidMovementNote(501, tooLong));

        var maxAllowed = new string('a', 2000);
        Assert.True(WorkoutSessionActionRule.IsValidMovementNote(501, maxAllowed));
    }

    [Fact]
    public async Task CircumferenceMeasurements_Create_Correct_Delete_Flow()
    {
        var store = new BodyMeasurementInMemoryStore();
        var session = new TestMeasurementSession(OwnerId, Today);
        var createUseCase = new CreateBodyMeasurementUseCase(session, store);
        var correctUseCase = new CorrectBodyMeasurementUseCase(session, store);
        var deleteUseCase = new DeleteBodyMeasurementUseCase(session, store);
        var historyUseCase = new ReadBodyMeasurementHistoryUseCase(session, store);

        var id = Guid.NewGuid();
        var commandId1 = Guid.NewGuid();

        // 1. Create waist measurement (midline: no side)
        var created = await createUseCase.ExecuteAsync(new CreateBodyMeasurementInputDto(
            id,
            commandId1,
            BodyMeasurementCodes.Waist,
            BodyMeasurementCodes.NoSide,
            Today,
            75.5,
            BodyMeasurementCodes.Centimeters));

        Assert.Equal(id, created.Id);
        Assert.Equal(BodyMeasurementCodes.Waist, created.Site);
        Assert.Equal(BodyMeasurementCodes.NoSide, created.Side);
        Assert.Equal(75.5m, created.EnteredValue);
        Assert.Equal(1, created.Version);

        // 2. Correct measurement to 74.0 cm
        var commandId2 = Guid.NewGuid();
        var corrected = await correctUseCase.ExecuteAsync(new CorrectBodyMeasurementInputDto(
            id,
            commandId2,
            1,
            BodyMeasurementCodes.Waist,
            BodyMeasurementCodes.NoSide,
            Today,
            74.0,
            BodyMeasurementCodes.Centimeters));

        Assert.Equal(74.0m, corrected.EnteredValue);
        Assert.Equal(2, corrected.Version);

        // 3. Stale version correction must fail
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            correctUseCase.ExecuteAsync(new CorrectBodyMeasurementInputDto(
                id,
                Guid.NewGuid(),
                1, // Stale version!
                BodyMeasurementCodes.Waist,
                BodyMeasurementCodes.NoSide,
                Today,
                73.0,
                BodyMeasurementCodes.Centimeters)));

        // 4. Verify history readback
        var history = await historyUseCase.ExecuteAsync(new BodyMeasurementHistoryInputDto(
            Today.AddDays(-7), Today));

        Assert.Single(history.Readings);
        Assert.Equal(74.0m, history.Readings[0].EnteredValue);

        // 5. Delete measurement
        var commandId3 = Guid.NewGuid();
        await deleteUseCase.ExecuteAsync(new DeleteBodyMeasurementInputDto(id, commandId3, 2));

        var afterDelete = await historyUseCase.ExecuteAsync(new BodyMeasurementHistoryInputDto(
            Today.AddDays(-7), Today));
        Assert.Empty(afterDelete.Readings);
    }

    [Fact]
    public async Task CircumferenceMeasurements_BilateralSites_Require_Left_Or_Right()
    {
        var store = new BodyMeasurementInMemoryStore();
        var session = new TestMeasurementSession(OwnerId, Today);
        var createUseCase = new CreateBodyMeasurementUseCase(session, store);

        // Midline site (Waist) with Left side should throw
        await Assert.ThrowsAsync<ArgumentException>(() =>
            createUseCase.ExecuteAsync(new CreateBodyMeasurementInputDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                BodyMeasurementCodes.Waist,
                BodyMeasurementCodes.Left,
                Today,
                75.0,
                BodyMeasurementCodes.Centimeters)));

        // Bilateral site (UpperArm) with NoSide should throw
        await Assert.ThrowsAsync<ArgumentException>(() =>
            createUseCase.ExecuteAsync(new CreateBodyMeasurementInputDto(
                Guid.NewGuid(),
                Guid.NewGuid(),
                BodyMeasurementCodes.UpperArm,
                BodyMeasurementCodes.NoSide,
                Today,
                32.0,
                BodyMeasurementCodes.Centimeters)));

        // Bilateral site with Left succeeds
        var arm = await createUseCase.ExecuteAsync(new CreateBodyMeasurementInputDto(
            Guid.NewGuid(),
            Guid.NewGuid(),
            BodyMeasurementCodes.UpperArm,
            BodyMeasurementCodes.Left,
            Today,
            32.0,
            BodyMeasurementCodes.Centimeters));

        Assert.Equal(BodyMeasurementCodes.UpperArm, arm.Site);
        Assert.Equal(BodyMeasurementCodes.Left, arm.Side);
    }

    [Fact]
    public void WP02b_ExerciseEvidenceRanking_Orders_By_Hypertrophy_And_JointFriendliness()
    {
        var lowEvidence = CreateCatalogEntry(1, MovementPattern.SquatPattern,
            new ExerciseEvidenceRanking(HypertrophyEfficacy: 2, JointFriendliness: 3, IsCompound: true));
        var highEvidence = CreateCatalogEntry(2, MovementPattern.SquatPattern,
            new ExerciseEvidenceRanking(HypertrophyEfficacy: 5, JointFriendliness: 5, IsCompound: true));

        var input = CreateSelectionInput([
            lowEvidence,
            highEvidence,
            CreateCatalogEntry(21, MovementPattern.HipHinge, new ExerciseEvidenceRanking(3, 3, true)),
            CreateCatalogEntry(31, MovementPattern.HorizontalPush, new ExerciseEvidenceRanking(3, 3, true)),
            CreateCatalogEntry(41, MovementPattern.HorizontalPull, new ExerciseEvidenceRanking(3, 3, true))
        ]);
        var result = StrengthTemplateResolver.Resolve(input, 5);

        var kneeSlot = result.Slots.Single(s => s.SlotId == "knee-dominant");
        Assert.Equal(2, kneeSlot.ExerciseId);
        Assert.Equal("StudyBasedEvidenceRanking", kneeSlot.SelectionReason);
    }

    private static StrengthExerciseCatalogEntry CreateCatalogEntry(int id, MovementPattern pattern, ExerciseEvidenceRanking ranking)
    {
        var candidate = new ReviewedExerciseCandidate(
            id,
            "test-v2",
            pattern,
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            new HashSet<ActivityExperience> { ActivityExperience.Beginner, ActivityExperience.Established },
            new HashSet<ActivityDemandTag> { ActivityDemandTag.Mobility },
            true,
            true,
            new HashSet<ActivityDemandTag>(),
            new HashSet<ExerciseTimingMethod> { ExerciseTimingMethod.StraightSets },
            1m,
            new HashSet<int>(),
            InjuryFlag.None,
            EvidenceRanking: ranking);

        return new StrengthExerciseCatalogEntry($"Exercise {id}", candidate, []);
    }

    private static StrengthTemplateSelectionInput CreateSelectionInput(IReadOnlyList<StrengthExerciseCatalogEntry> catalog)
    {
        var template = StrengthTemplateCatalog.ForSchedule(1).Single();
        var prescriptionContext = new ActivityPrescriptionContext(
            WorkoutActivityType.GeneralStrength,
            ActivityExperience.Beginner,
            null,
            false,
            true,
            false,
            0,
            false,
            false);

        var eligibility = new ExerciseEligibilityContext(
            new HashSet<ExerciseEquipmentKind> { ExerciseEquipmentKind.Bodyweight },
            ActivityExperience.Beginner,
            new HashSet<int>(),
            new HashSet<MovementPattern>(),
            new HashSet<ActivityDemandTag>(),
            false);

        return new StrengthTemplateSelectionInput(
            template,
            WorkoutActivityType.GeneralStrength,
            UserGoal.Strength,
            prescriptionContext,
            catalog,
            eligibility,
            new Dictionary<int, int>(),
            new HashSet<int>(),
            new HashSet<int>());
    }

    private sealed class TestMeasurementSession(Guid userId, DateOnly today) : IBodyMeasurementSession
    {
        public Guid CurrentUserId => userId;
        public DateOnly Today => today;
    }

    private sealed class BodyMeasurementInMemoryStore : IBodyMeasurementStore
    {
        private sealed record Snapshot(
            Guid Id,
            BodyMeasurementSite Site,
            BodyMeasurementSide Side,
            DateOnly LocalDate,
            decimal EnteredValue,
            BodyMeasurementUnit EnteredUnit,
            int Version,
            bool IsDeleted);

        private readonly Dictionary<Guid, (Guid Owner, Snapshot Snapshot)> _readings = [];
        private readonly HashSet<Guid> _commandReceipts = [];

        public Task<BodyMeasurementOutputDto?> TryReplayCreateAsync(Guid ownerId,
            CreateBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
            Task.FromResult<BodyMeasurementOutputDto?>(null);

        public Task<BodyMeasurementOutputDto?> TryReplayCorrectionAsync(Guid ownerId,
            CorrectBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
            Task.FromResult<BodyMeasurementOutputDto?>(null);

        public Task<bool> WasDeletionAppliedAsync(Guid ownerId,
            DeleteBodyMeasurementInputDto input, CancellationToken cancellationToken) =>
            Task.FromResult(_commandReceipts.Contains(input.CommandId));

        public Task<BodyMeasurement> CreateAsync(Guid ownerId, Guid commandId,
            BodyMeasurement measurement, CancellationToken cancellationToken)
        {
            if (_readings.Values.Any(r => r.Owner == ownerId && !r.Snapshot.IsDeleted &&
                r.Snapshot.Site == measurement.Site && r.Snapshot.Side == measurement.Side &&
                r.Snapshot.LocalDate == measurement.LocalDate))
            {
                throw new InvalidOperationException("Duplicate daily slot.");
            }

            _commandReceipts.Add(commandId);
            _readings[measurement.Id] = (ownerId, ToSnapshot(measurement));
            return Task.FromResult(measurement);
        }

        public Task<BodyMeasurement?> LoadAsync(Guid ownerId, Guid id, CancellationToken cancellationToken)
        {
            if (!_readings.TryGetValue(id, out var row) || row.Owner != ownerId)
                return Task.FromResult<BodyMeasurement?>(null);

            var s = row.Snapshot;
            return Task.FromResult<BodyMeasurement?>(BodyMeasurement.Restore(
                s.Id, s.Site, s.Side, s.LocalDate, s.EnteredValue, s.EnteredUnit,
                s.Version, s.IsDeleted, s.LocalDate.AddDays(1)));
        }

        public Task<BodyMeasurement> CorrectAsync(Guid ownerId, Guid commandId,
            BodyMeasurement measurement, int expectedVersion, CancellationToken cancellationToken)
        {
            if (!_readings.TryGetValue(measurement.Id, out var row) || row.Owner != ownerId ||
                row.Snapshot.Version != expectedVersion)
            {
                throw new InvalidOperationException("Concurrent or stale measurement update.");
            }

            _commandReceipts.Add(commandId);
            _readings[measurement.Id] = (ownerId, ToSnapshot(measurement));
            return Task.FromResult(measurement);
        }

        public Task DeleteAsync(Guid ownerId, Guid commandId, BodyMeasurement measurement,
            int expectedVersion, CancellationToken cancellationToken)
        {
            if (!_readings.TryGetValue(measurement.Id, out var row) || row.Owner != ownerId ||
                row.Snapshot.Version != expectedVersion)
            {
                throw new InvalidOperationException("Concurrent or stale measurement deletion.");
            }

            _commandReceipts.Add(commandId);
            _readings[measurement.Id] = (ownerId, ToSnapshot(measurement));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<BodyMeasurement>> LoadHistoryAsync(Guid ownerId, DateOnly from,
            DateOnly through, CancellationToken cancellationToken)
        {
            IReadOnlyList<BodyMeasurement> result = _readings.Values
                .Where(r => r.Owner == ownerId && !r.Snapshot.IsDeleted &&
                            r.Snapshot.LocalDate >= from && r.Snapshot.LocalDate <= through)
                .OrderBy(r => r.Snapshot.LocalDate)
                .ThenBy(r => r.Snapshot.Site)
                .ThenBy(r => r.Snapshot.Side)
                .ThenBy(r => r.Snapshot.Id)
                .Select(r => BodyMeasurement.Restore(
                    r.Snapshot.Id, r.Snapshot.Site, r.Snapshot.Side, r.Snapshot.LocalDate,
                    r.Snapshot.EnteredValue, r.Snapshot.EnteredUnit, r.Snapshot.Version,
                    r.Snapshot.IsDeleted, r.Snapshot.LocalDate.AddDays(1)))
                .ToArray();
            return Task.FromResult(result);
        }

        private static Snapshot ToSnapshot(BodyMeasurement m) =>
            new(m.Id, m.Site, m.Side, m.LocalDate, m.EnteredValue, m.EnteredUnit, m.Version, m.IsDeleted);
    }
}

using Microsoft.EntityFrameworkCore;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.Core.Models.Entities.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Services.Workout.Planning;
using NSubstitute;

namespace MuscleCuties.Core.Tests.Services.Workout.Planning;

public class ExercisePickerServiceTests : IClassFixture<WorkoutPlanningDbFixture>
{
    private readonly WorkoutPlanningDbFixture _fixture;
    private readonly IContributionLookup _contributions;

    public ExercisePickerServiceTests(WorkoutPlanningDbFixture fixture)
    {
        _fixture = fixture;
        _contributions = Substitute.For<IContributionLookup>();
        _contributions.Contribution(Arg.Any<int>(), Arg.Any<int>()).Returns(0.8);

    }

    private ExercisePickerService CreateService() => new(_fixture.Db, _contributions);

    private static PlannedSession MakeSession(
        BlockType block = BlockType.Hypertrophy,
        MovementPattern[]? patterns = null,
        int assignedSets = 3,
        byte repsMin = 8,
        byte repsMax = 12,
        byte targetRir = 2,
        bool droppable = false,
        int slotCount = 2) =>
        new()
        {
            ArchetypeId = 1,
            ArchetypeCode = "P",
            Day = DayOfWeek.Monday,
            Variant = 1,
            Slots = Enumerable.Range(0, slotCount).Select(i => new PlannedSlot
            {
                SlotTemplateId = i + 1,
                PrimaryMuscleId = 1,
                AssignedSets = assignedSets,
                SetsMin = 2,
                SetsMax = 4,
                RepsMin = repsMin,
                RepsMax = repsMax,
                TargetRir = targetRir,
                AllowedPatterns = patterns ?? [MovementPattern.HipThrust, MovementPattern.SquatPattern],
                Droppable = droppable,
                SupersetGroup = 0,
                Block = block
            }).ToList()
        };

    [Fact]
    public async Task PickForSessionAsync_ReturnsCorrectArchetypeInfo()
    {
        var service = CreateService();
        var session = MakeSession();

        var result = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 10);

        Assert.Equal("P", result.ArchetypeCode);
        Assert.Equal(1, result.ArchetypeId);
        Assert.Equal(DayOfWeek.Monday, result.Day);
        Assert.Equal(1, result.Variant);
    }

    [Fact]
    public async Task PickForSessionAsync_ProducesExercisesForSlots()
    {
        var service = CreateService();
        var session = MakeSession(slotCount: 3);

        var result = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 10);

        Assert.True(result.Exercises.Count >= 1, "Expected at least one exercise picked");
    }

    [Fact]
    public async Task PickForSessionAsync_NoDuplicateExerciseIds()
    {
        var service = CreateService();
        var session = MakeSession(slotCount: 3);

        var result = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 10);

        var ids = result.Exercises.Select(e => e.ExerciseId).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public async Task PickForSessionAsync_SetsRpeFromRirWithCap()
    {
        var service = CreateService();
        var session = MakeSession(targetRir: 2, slotCount: 1);

        var result = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 7);

        if (result.Exercises.Count > 0)
        {
            Assert.True(result.Exercises[0].Rpe <= 7,
                $"RPE {result.Exercises[0].Rpe} exceeded cap of 7");
        }
    }

    [Fact]
    public async Task PickForSessionAsync_AppliesSetMultiplier()
    {
        var service = CreateService();
        var session = MakeSession(slotCount: 1);

        var full = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 10);
        var reduced = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 0.5, 10);

        if (full.Exercises.Count > 0 && reduced.Exercises.Count > 0)
        {
            Assert.True(reduced.Exercises[0].Sets <= full.Exercises[0].Sets,
                "Reduced multiplier should produce equal or fewer sets");
        }
    }

    [Fact]
    public async Task PickForSessionAsync_EstimatedMinutesPositive()
    {
        var service = CreateService();
        var session = MakeSession();

        var result = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 10);

        if (result.Exercises.Count > 0)
        {
            Assert.True(result.EstimatedMinutes > 0, "Estimated minutes should be positive when exercises exist");
        }
    }

    [Fact]
    public async Task PickForSessionAsync_BodyweightEquipment_FiltersCorrectly()
    {
        var service = CreateService();
        var session = MakeSession(
            patterns: [MovementPattern.AntiExtension, MovementPattern.AntiRotation],
            block: BlockType.Core,
            slotCount: 1);

        var result = await service.PickForSessionAsync(
            session, EquipmentSet.None, InjuryFlag.None, 1.0, 10);

        Assert.All(result.Exercises.Where(e => e.SlotTemplateId > 0),
            exercise => Assert.True(exercise.IsBodyweight));
    }

    [Fact]
    public async Task PickForSessionAsync_WithInjury_ExcludesContraindicated()
    {
        var service = CreateService();
        var session = MakeSession(slotCount: 2);

        var withInjury = await service.PickForSessionAsync(
            session, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.Knee, 1.0, 10);

        var selectedIds = withInjury.Exercises.Select(exercise => exercise.ExerciseId).ToList();
        var selectedExercises = await _fixture.Db.WorkoutExerciseDefinitions
            .Where(exercise => selectedIds.Contains(exercise.Id))
            .ToListAsync();

        Assert.All(selectedExercises, exercise =>
            Assert.Equal(InjuryFlag.None, exercise.Contraindications & InjuryFlag.Knee));
    }

    [Fact]
    public async Task PickForSessionAsync_DroppableSlots_SkippedWhenNoCandidates()
    {
        var service = CreateService();
        var session = new PlannedSession
        {
            ArchetypeId = 1,
            ArchetypeCode = "P",
            Day = DayOfWeek.Monday,
            Variant = 1,
            Slots =
            [
                new PlannedSlot
                {
                    SlotTemplateId = 1,
                    PrimaryMuscleId = 1,
                    AssignedSets = 3,
                    SetsMin = 2,
                    SetsMax = 4,
                    RepsMin = 8,
                    RepsMax = 12,
                    TargetRir = 2,
                    AllowedPatterns = [MovementPattern.HipThrust],
                    Droppable = false,
                    SupersetGroup = 0,
                    Block = BlockType.Hypertrophy
                },
                new PlannedSlot
                {
                    SlotTemplateId = 2,
                    PrimaryMuscleId = 99,
                    AssignedSets = 2,
                    SetsMin = 1,
                    SetsMax = 3,
                    RepsMin = 10,
                    RepsMax = 15,
                    TargetRir = 3,
                    AllowedPatterns = [MovementPattern.Climbing],
                    Droppable = true,
                    SupersetGroup = 0,
                    Block = BlockType.Accessory
                }
            ]
        };

        var result = await service.PickForSessionAsync(
            session, EquipmentSet.None, InjuryFlag.None, 1.0, 10);

        Assert.DoesNotContain(result.Exercises, e => e.Pattern == MovementPattern.Climbing);
    }

    [Fact]
    public async Task PickForSessionAsync_RestSecondsMatchBlockType()
    {
        var service = CreateService();

        var hiSession = MakeSession(block: BlockType.HighIntensity, slotCount: 1);
        var coreSession = MakeSession(
            block: BlockType.Core,
            patterns: [MovementPattern.AntiExtension, MovementPattern.AntiRotation],
            slotCount: 1);

        var hiResult = await service.PickForSessionAsync(
            hiSession, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 10);
        var coreResult = await service.PickForSessionAsync(
            coreSession, EquipmentSet.Barbell | EquipmentSet.Dumbbell, InjuryFlag.None, 1.0, 10);

        if (hiResult.Exercises.Any(e => e.SlotTemplateId > 0))
            Assert.Contains(hiResult.Exercises.Where(e => e.SlotTemplateId > 0), e => e.RestSeconds == 150);

        if (coreResult.Exercises.Any(e => e.SlotTemplateId > 0))
            Assert.Contains(coreResult.Exercises.Where(e => e.SlotTemplateId > 0), e => e.RestSeconds == 60);
    }

    [Theory]
    [InlineData(Equipment.FullGym)]
    [InlineData(Equipment.HomeDumbbellsBands)]
    [InlineData(Equipment.Bodyweight)]
    public void MapEquipment_AllValues_DoNotThrow(Equipment equipment)
    {
        var result = ExercisePickerService.MapEquipment(equipment);
        Assert.True(result >= EquipmentSet.None);
    }

    [Fact]
    public void MapEquipment_FullGym_IncludesAllFlags()
    {
        var result = ExercisePickerService.MapEquipment(Equipment.FullGym);

        Assert.True(result.HasFlag(EquipmentSet.Barbell));
        Assert.True(result.HasFlag(EquipmentSet.Dumbbell));
        Assert.True(result.HasFlag(EquipmentSet.Machines));
        Assert.True(result.HasFlag(EquipmentSet.Cables));
        Assert.True(result.HasFlag(EquipmentSet.Bands));
        Assert.True(result.HasFlag(EquipmentSet.HipThrustBench));
        Assert.True(result.HasFlag(EquipmentSet.BackExtension45));
        Assert.True(result.HasFlag(EquipmentSet.PullUpBar));
        Assert.True(result.HasFlag(EquipmentSet.Kettlebell));
    }

    [Fact]
    public void MapEquipment_Home_IncludesDumbbellBandsKettlebell()
    {
        var result = ExercisePickerService.MapEquipment(Equipment.HomeDumbbellsBands);

        Assert.True(result.HasFlag(EquipmentSet.Dumbbell));
        Assert.True(result.HasFlag(EquipmentSet.Bands));
        Assert.True(result.HasFlag(EquipmentSet.Kettlebell));
        Assert.False(result.HasFlag(EquipmentSet.Barbell));
        Assert.False(result.HasFlag(EquipmentSet.Machines));
    }

    [Fact]
    public void MapEquipment_Bodyweight_ReturnsNone()
    {
        var result = ExercisePickerService.MapEquipment(Equipment.Bodyweight);
        Assert.Equal(EquipmentSet.None, result);
    }

    [Fact]
    public void MapInjuries_EmptyList_ReturnsNone()
    {
        var result = WorkoutInjuryRules.ToFlags([]);
        Assert.Equal(InjuryFlag.None, result);
    }

    [Fact]
    public void MapInjuries_ClearedInjury_Ignored()
    {
        var injuries = new List<Injury>
        {
            new(InjuryFlag.Knee, InjuryStatus.Cleared, new DateOnly(2025, 1, 1))
        };

        var result = WorkoutInjuryRules.ToFlags(injuries);
        Assert.Equal(InjuryFlag.None, result);
    }

    [Fact]
    public void MapInjuries_AcuteInjury_MapsCorrectly()
    {
        var injuries = new List<Injury>
        {
            new(InjuryFlag.Knee, InjuryStatus.Acute, new DateOnly(2025, 6, 1))
        };

        var result = WorkoutInjuryRules.ToFlags(injuries);
        Assert.Equal(InjuryFlag.Knee, result);
    }

    [Fact]
    public void MapInjuries_MultipleActive_CombinesFlags()
    {
        var injuries = new List<Injury>
        {
            new(InjuryFlag.Shoulder, InjuryStatus.Acute, new DateOnly(2025, 6, 1)),
            new(InjuryFlag.LowBack, InjuryStatus.Recovering, new DateOnly(2025, 5, 1)),
            new(InjuryFlag.Ankle, InjuryStatus.Cleared, new DateOnly(2024, 1, 1))
        };

        var result = WorkoutInjuryRules.ToFlags(injuries);
        Assert.True(result.HasFlag(InjuryFlag.Shoulder));
        Assert.True(result.HasFlag(InjuryFlag.LowBack));
        Assert.False(result.HasFlag(InjuryFlag.Ankle));
    }
}

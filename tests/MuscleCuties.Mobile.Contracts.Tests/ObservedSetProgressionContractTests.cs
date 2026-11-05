using System.Text.Json;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class ObservedSetProgressionContractTests
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [Fact]
    public void WorkoutExerciseLogDto_BackwardCompatible_WhenObservedSetsOmitted()
    {
        const string legacyJson = """
            {
                "workoutDayExerciseId": 12,
                "exerciseId": 101,
                "completedSets": 3,
                "completedReps": 10,
                "weightKg": 60.0
            }
            """;

        var dto = JsonSerializer.Deserialize<WorkoutExerciseLogDto>(legacyJson, JsonOptions);

        Assert.NotNull(dto);
        Assert.Equal(12, dto.WorkoutDayExerciseId);
        Assert.Equal(101, dto.ExerciseId);
        Assert.Equal(3, dto.CompletedSets);
        Assert.Equal(10, dto.CompletedReps);
        Assert.Equal(60.0f, dto.WeightKg);
        Assert.Null(dto.ObservedSets);
    }

    [Fact]
    public void WorkoutExerciseLogDto_SerializesAndRoundTripsObservedSets()
    {
        var observedSets = new List<ObservedSetRecord>
        {
            new(SetIndex: 1, RepsCompleted: 12, LoadKg: 60.0m, RirActual: 2, TechniqueAcceptable: true),
            new(SetIndex: 2, RepsCompleted: 12, LoadKg: 60.0m, RirActual: 2, TechniqueAcceptable: true),
            new(SetIndex: 3, RepsCompleted: 12, LoadKg: 60.0m, RirActual: 1, TechniqueAcceptable: true)
        };

        var dto = new WorkoutExerciseLogDto(
            WorkoutDayExerciseId: 15,
            ExerciseId: 105,
            CompletedSets: 3,
            CompletedReps: 12,
            WeightKg: 60.0f,
            ObservedSets: observedSets);

        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var roundTripped = JsonSerializer.Deserialize<WorkoutExerciseLogDto>(json, JsonOptions);

        Assert.NotNull(roundTripped);
        Assert.NotNull(roundTripped.ObservedSets);
        Assert.Equal(3, roundTripped.ObservedSets.Count);
        Assert.Equal(1, roundTripped.ObservedSets[0].SetIndex);
        Assert.Equal(12, roundTripped.ObservedSets[0].RepsCompleted);
        Assert.Equal(60.0m, roundTripped.ObservedSets[0].LoadKg);
        Assert.Equal(2, roundTripped.ObservedSets[0].RirActual);
        Assert.True(roundTripped.ObservedSets[0].TechniqueAcceptable);
    }

    [Fact]
    public void DoubleProgression_ExecutesFromObservedSetActuals_ReachingNextPrescription()
    {
        // Scenario 1: Bench press 60kg for 3x12 across 2 consecutive qualifying exposures
        var week1Sets = new List<CompletedWorkingSet>
        {
            new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
            new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
            new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true)
        };
        var week2Sets = new List<CompletedWorkingSet>
        {
            new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
            new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
            new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true)
        };

        var candidate = new MuscleProgressionCandidate(
            MajorMuscleGroup.Chest,
            ExerciseId: 101,
            CurrentLoad: 60.0m,
            LoadIncrement: 2.5m,
            Category: ExerciseMovementCategory.Compound,
            RecentWeeks: new List<DoubleProgressionWeekPerformance>
            {
                new(WeekNumber: 1, WorkingSets: week1Sets),
                new(WeekNumber: 2, WorkingSets: week2Sets)
            },
            CurrentWeeklyVolume: 3,
            PreviousWeeklyVolume: 3);

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            [candidate]);

        Assert.Single(decisions);
        var decision = decisions[0];
        Assert.True(decision.LoadIncreased);
        Assert.Equal(62.5m, decision.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.StatusProgressed, decision.ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.DoubleProgressionEarned, decision.ReasonCode);
    }
}

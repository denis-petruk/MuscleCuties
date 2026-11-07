using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.UseCases.Features.Workout.Contracts;
using MuscleCuties.UseCases.Features.Workout.Session.Contracts;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

public sealed class ObservedSetCaptureAndProgressionContractTests
{
    [Fact]
    public void EditedSetRows_ProduceContiguousObservedSets()
    {
        var rows = new[]
        {
            new EditableObservedSet(1, 12, 60m, 2, true),
            new EditableObservedSet(2, 11, 60m, 1, true),
            new EditableObservedSet(3, 10, 60m, 0, false),
            new EditableObservedSet(4, 8, 40m, 4, true, IsWarmup: true)
        };

        var observed = ObservedSetInputMapping.Build(rowsEdited: true, rows);

        Assert.NotNull(observed);
        Assert.Equal(3, observed.Count);
        Assert.Equal([1, 2, 3], observed.Select(set => set.SetIndex));
        Assert.Equal([12, 11, 10], observed.Select(set => set.RepsCompleted));
        Assert.Equal([2, 1, 0], observed.Select(set => set.RirActual));
        Assert.False(observed[2].TechniqueAcceptable);
        Assert.All(observed, set => Assert.Equal(60m, set.LoadKg));
    }

    [Fact]
    public void UneditedSetRows_DoNotSendObservedSets()
    {
        var observed = ObservedSetInputMapping.Build(
            rowsEdited: false,
            [new EditableObservedSet(1, 12, 60m, 2)]);

        Assert.Null(observed);
    }

    [Fact]
    public void TimedActivity_BuildsRowLessActualPayload()
    {
        var actual = TimedActivityActualFactory.Build(WorkoutActivityDto.Yoga, 1_800);
        var request = new LogWorkoutSessionRequest(
            WorkoutDayId: 42,
            Date: new DateOnly(2026, 11, 7),
            Exercises: null,
            PlannedSessionId: Guid.NewGuid(),
            ActivityActuals: [actual]);

        Assert.Equal(WorkoutActivityDto.Yoga, actual.ActivityType);
        Assert.Equal(1_800, actual.DurationSeconds);
        Assert.Null(actual.WorkoutDayActivityId);
        Assert.Null(request.Exercises);
        Assert.Single(request.ActivityActuals!);
    }

    [Fact]
    public void PersistedObservedSets_UseRealTierAndMuscleGroupForProgression()
    {
        var sessionOne = Guid.NewGuid();
        var sessionTwo = Guid.NewGuid();
        var rows = BuildRows(sessionOne, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc))
            .Concat(BuildRows(sessionTwo, new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc)));

        var weeks = ObservedSetProgressionHistoryBuilder.Build(rows);
        var advice = StrengthProgressionAdviceRule.Resolve(
            MajorMuscleGroup.Back,
            exerciseId: 101,
            currentLoad: 60m,
            plannedReps: 10,
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            weeks);

        Assert.Equal(2, weeks.Count);
        Assert.Equal(StrengthProgressionAdviceRule.EarnedCode, advice.RecommendationCode);
        Assert.True(advice.LoadIncreased);
        Assert.Equal(62.5m, advice.PrescribedLoad);
        Assert.Equal(MajorMuscleGroup.Back, advice.Muscle);
        Assert.Equal(EffectiveExperienceTier.Tier2IntermediateOrEstablished, advice.Tier);
    }

    [Fact]
    public void AggregateOnlyHistory_ReturnsConservativeLogEachSetOutcome()
    {
        var advice = StrengthProgressionAdviceRule.Resolve(
            MajorMuscleGroup.Quads,
            exerciseId: 202,
            currentLoad: 80m,
            plannedReps: 8,
            EffectiveExperienceTier.Tier3Advanced,
            weeks: null);

        Assert.Equal(StrengthProgressionAdviceRule.LogEachSetCode, advice.RecommendationCode);
        Assert.Null(advice.PrescribedLoad);
        Assert.False(advice.LoadIncreased);
    }

    [Fact]
    public void WeekWithMissingRir_IsNotUsedAsQualifyingEvidence()
    {
        var completeWeek = Guid.NewGuid();
        var incompleteWeek = Guid.NewGuid();
        var rows = BuildRows(completeWeek, new DateTime(2026, 10, 28, 9, 0, 0, DateTimeKind.Utc))
            .Concat(BuildRows(incompleteWeek, new DateTime(2026, 11, 4, 9, 0, 0, DateTimeKind.Utc), rir: null));

        var weeks = ObservedSetProgressionHistoryBuilder.Build(rows);
        var advice = StrengthProgressionAdviceRule.Resolve(
            MajorMuscleGroup.Chest,
            exerciseId: 303,
            currentLoad: 50m,
            plannedReps: 10,
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            weeks);

        Assert.Single(weeks);
        Assert.Equal(StrengthProgressionAdviceRule.LogEachSetCode, advice.RecommendationCode);
        Assert.Null(advice.PrescribedLoad);
    }

    private static IEnumerable<ObservedSetHistoryRow> BuildRows(
        Guid sessionId,
        DateTime completedAt,
        int? rir = 2) =>
        Enumerable.Range(1, 3).Select(setNumber => new ObservedSetHistoryRow(
            sessionId,
            completedAt,
            setNumber,
            RepsCompleted: 12,
            LoadKg: 60m,
            RirActual: rir,
            TechniqueAcceptable: true));
}

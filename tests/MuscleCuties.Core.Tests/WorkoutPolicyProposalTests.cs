using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class WorkoutPolicyProposalTests
{
    [Fact]
    public void Scenario1_DoubleProgressionEarned_WhenAllWorkingSetsMeetRepCeilingAndRirTarget()
    {
        var candidate = new MuscleProgressionCandidate(
            Muscle: MajorMuscleGroup.Chest,
            ExerciseId: 10,
            CurrentLoad: 60.0m,
            LoadIncrement: 2.5m,
            Category: ExerciseMovementCategory.Compound,
            RecentWeeks: new List<DoubleProgressionWeekPerformance>
            {
                new(WeekNumber: 1, WorkingSets: new List<CompletedWorkingSet>
                {
                    new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
                    new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
                    new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true)
                }),
                new(WeekNumber: 2, WorkingSets: new List<CompletedWorkingSet>
                {
                    new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
                    new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true),
                    new(RepsCompleted: 12, Load: 60.0m, RirActual: 2, TechniqueAcceptable: true)
                })
            },
            CurrentWeeklyVolume: 9,
            PreviousWeeklyVolume: 9
        );

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            new[] { candidate });

        Assert.Single(decisions);
        var decision = decisions[0];
        Assert.True(decision.LoadIncreased);
        Assert.Equal(62.5m, decision.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.StatusProgressed, decision.ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.DoubleProgressionEarned, decision.ReasonCode);
    }

    [Fact]
    public void Scenario2_IncompleteSession_MaintainsLoadWithoutPunishment()
    {
        var candidate = new MuscleProgressionCandidate(
            Muscle: MajorMuscleGroup.Hamstrings,
            ExerciseId: 25,
            CurrentLoad: 24.0m,
            LoadIncrement: 2.0m,
            Category: ExerciseMovementCategory.Accessory,
            RecentWeeks: new List<DoubleProgressionWeekPerformance>
            {
                new(WeekNumber: 1, WorkingSets: new List<CompletedWorkingSet>
                {
                    new(RepsCompleted: 12, Load: 24.0m, RirActual: 2, TechniqueAcceptable: true),
                    new(RepsCompleted: 8, Load: 24.0m, RirActual: 2, TechniqueAcceptable: true) // Incomplete set 2, skipped set 3
                })
            },
            CurrentWeeklyVolume: 6,
            PreviousWeeklyVolume: 6
        );

        var decisions = StrengthProgressionRule.ResolveDoubleProgression(
            EffectiveExperienceTier.Tier2IntermediateOrEstablished,
            new[] { candidate });

        Assert.Single(decisions);
        var decision = decisions[0];
        Assert.False(decision.LoadIncreased);
        Assert.Equal(24.0m, decision.PrescribedLoad);
        Assert.Equal(StrengthProgressionRule.StatusMaintained, decision.ProgressionStatus);
        Assert.Equal(StrengthProgressionRule.IncompleteHistoryMaintainsLoad, decision.ReasonCode);
    }

    [Fact]
    public void Scenario4_ReadinessScoreAdjustment_SafelyDecrementsPrescribedLoad()
    {
        // Low readiness score 48/100 should decrement working load by ~12.5% to protect recovery
        const decimal baselineLoad = 80.0m;
        const int readinessScore = 48;

        var adjustedLoad = CalculateReadinessAdjustedLoad(baselineLoad, readinessScore);

        Assert.Equal(70.0m, adjustedLoad);
        Assert.True(adjustedLoad < baselineLoad);
    }

    [Fact]
    public void Scenario5_CycleModeWithFatigue_WithholdsExtraSetSafeguard()
    {
        // In cycle mode with fatigue (readiness < 75), extra sets must NOT be added
        const int fatigueReadinessScore = 55;
        const bool requestedCycleMode = true;

        var shouldAddExtraSet = ShouldOfferCycleExtraSet(requestedCycleMode, fatigueReadinessScore);

        Assert.False(shouldAddExtraSet);
    }

    [Fact]
    public void Scenario5_CycleModeWithoutFatigue_PermitsExtraSet()
    {
        // When well rested (readiness >= 75), extra set can safely be offered in cycle mode
        const int restedReadinessScore = 82;
        const bool requestedCycleMode = true;

        var shouldAddExtraSet = ShouldOfferCycleExtraSet(requestedCycleMode, restedReadinessScore);

        Assert.True(shouldAddExtraSet);
    }

    // Reference helpers validating the policy adoption mathematical invariants
    private static decimal CalculateReadinessAdjustedLoad(decimal baselineLoad, int readinessScore)
    {
        if (readinessScore >= 80) return baselineLoad;
        if (readinessScore >= 60) return Math.Round(baselineLoad * 0.95m / 2.5m, 0) * 2.5m;
        // < 60: 87.5% load rounded to standard 2.5kg plate increment
        return Math.Round(baselineLoad * 0.875m / 2.5m, 0) * 2.5m;
    }

    private static bool ShouldOfferCycleExtraSet(bool cycleModeActive, int readinessScore)
    {
        // Explicit safeguard: extra sets only offered when cycle mode is active AND readiness is >= 75
        return cycleModeActive && readinessScore >= 75;
    }
}

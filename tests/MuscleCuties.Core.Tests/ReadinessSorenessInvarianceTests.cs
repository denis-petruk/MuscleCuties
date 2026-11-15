using System.Reflection;
using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Enums.Workout;
using MuscleCuties.Core.Models.Health;
using MuscleCuties.Core.Models.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

/// <summary>
/// Ratified amendment A-8: ordinary soreness is not a training input. The readiness score that is
/// persisted with the check-in and the health evidence score must not move when only the soreness
/// answer changes.
/// </summary>
public sealed class ReadinessSorenessInvarianceTests
{
    private static readonly CyclePhase Phase = CyclePhase.Follicular;

    [Fact]
    public void Readiness_score_tier_and_breakdown_ignore_the_soreness_answer()
    {
        var none = Compute(SorenessLevel.None);
        var mild = Compute(SorenessLevel.Mild);
        var severe = Compute(SorenessLevel.Severe);

        Assert.Equal(none.Score, mild.Score);
        Assert.Equal(none.Score, severe.Score);
        Assert.Equal(none.Tier, severe.Tier);
        Assert.Equal(none.Breakdown.OrderBy(entry => entry.Key, StringComparer.Ordinal),
            severe.Breakdown.OrderBy(entry => entry.Key, StringComparer.Ordinal));
    }

    [Fact]
    public void Readiness_breakdown_reports_no_soreness_component()
    {
        var readiness = Compute(SorenessLevel.Moderate);

        Assert.DoesNotContain(readiness.Breakdown.Keys,
            key => key.Contains("soreness", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Health_readiness_evidence_cannot_carry_a_soreness_rating()
    {
        Assert.DoesNotContain(typeof(ReadinessEvidence).GetProperties(BindingFlags.Public | BindingFlags.Instance),
            property => property.Name.Contains("Soreness", StringComparison.OrdinalIgnoreCase));
    }

    private static Readiness Compute(SorenessLevel? soreness) =>
        ReadinessRule.Compute(Inputs(soreness), Phase, WorkoutPlanningConfig.CreateDefault());

    private static DailyInputs Inputs(SorenessLevel? soreness) => new(
        new DateOnly(2026, 11, 15), 6.5, 6.8, 8000, 9000,
        Energy: 3, Pain: 0, Bloating: false, WeightKg: null,
        SleepQuality: SleepQuality.Fair, Soreness: soreness);
}

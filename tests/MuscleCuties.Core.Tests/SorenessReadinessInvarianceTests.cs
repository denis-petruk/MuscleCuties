using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class SorenessReadinessInvarianceTests
{
    [Fact]
    public void Ordinary_soreness_alone_does_not_change_the_actual_readiness_prescription()
    {
        var now = new DateTimeOffset(2026, 11, 15, 12, 0, 0, TimeSpan.Zero);
        var date = DateOnly.FromDateTime(now.UtcDateTime);
        var id = Guid.Parse("33333333-3333-3333-3333-333333333333");
        CycleReadinessPrescriptionDecision Decide(decimal soreness)
        {
            var observation = new TodayCheckInObservation(date, "UTC", now, 1,
                SleepQuality: 3, Energy: 4, Soreness: soreness, Stress: null);
            var score = TodayCheckInScoreRule.Evaluate(observation, now, "UTC");
            return CycleReadinessPrescriptionRule.Evaluate(new CycleReadinessPrescriptionInput(
                CyclePhase.Unknown, PhaseReviewProvenance.Unknown, null, score,
                HasNewPainOrSafetyConcern: false,
                Exercises: [new CycleReadinessExercise(id, 2, 1, true, true)],
                SleepHours: 8m, RoutineSymptoms: false,
                Approval: PlanningReleaseApproval.CycleReadinessPrescription));
        }

        var lowSoreness = Decide(1);
        var highSoreness = Decide(5);

        Assert.Equal(lowSoreness, highSoreness);
    }
}

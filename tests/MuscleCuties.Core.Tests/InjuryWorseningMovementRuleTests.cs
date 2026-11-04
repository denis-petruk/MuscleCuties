using MuscleCuties.Core.Domain.Workout;
using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Workout;

namespace MuscleCuties.Core.Tests;

public sealed class InjuryWorseningMovementRuleTests
{
    [Theory]
    [InlineData(InjuryWorseningTrigger.NotAnswered)]
    [InlineData(InjuryWorseningTrigger.None)]
    public void Unanswered_and_none_block_no_movement(InjuryWorseningTrigger trigger) =>
        Assert.Empty(InjuryWorseningMovementRule.BlockedPatterns(trigger));

    [Fact]
    public void Overhead_reach_blocks_overhead_patterns_but_not_horizontal_push()
    {
        var blocked = InjuryWorseningMovementRule.BlockedPatterns(InjuryWorseningTrigger.OverheadReach);

        Assert.Contains(MovementPattern.VerticalPush, blocked);
        Assert.Contains(MovementPattern.VerticalPull, blocked);
        Assert.Contains(MovementPattern.LateralRaise, blocked);
        Assert.DoesNotContain(MovementPattern.HorizontalPush, blocked);
    }

    [Fact]
    public void Deep_knee_flexion_blocks_knee_dominant_patterns_but_not_hip_dominant()
    {
        var blocked = InjuryWorseningMovementRule.BlockedPatterns(InjuryWorseningTrigger.DeepKneeFlexion);

        Assert.Contains(MovementPattern.SquatPattern, blocked);
        Assert.Contains(MovementPattern.Lunge, blocked);
        Assert.Contains(MovementPattern.KneeFlexion, blocked);
        Assert.DoesNotContain(MovementPattern.HipThrust, blocked);
        Assert.DoesNotContain(MovementPattern.HipHinge, blocked);
    }

    [Fact]
    public void Clinician_restriction_expands_to_reviewed_site_contraindications_when_trigger_is_unanswered()
    {
        var answers = new[]
        {
            new InjurySafetyAnswer(InjuryWorseningTrigger.NotAnswered, ClinicianAdvisedAvoid: true,
                Site: InjuryFlag.Knee)
        };
        var catalog = new[]
        {
            new ExerciseContraindicationPattern(MovementPattern.SquatPattern, InjuryFlag.Knee),
            new ExerciseContraindicationPattern(MovementPattern.Lunge, InjuryFlag.Knee),
            new ExerciseContraindicationPattern(MovementPattern.HorizontalPush, InjuryFlag.Shoulder),
            new ExerciseContraindicationPattern(MovementPattern.HipHinge, InjuryFlag.LowBack)
        };

        var blocked = InjuryWorseningMovementRule.BlockedPatterns(answers, catalog);

        Assert.Equal(
            new[] { MovementPattern.Lunge, MovementPattern.SquatPattern }.OrderBy(pattern => pattern),
            blocked.OrderBy(pattern => pattern));
    }

    [Fact]
    public void Declined_or_unanswered_clinician_question_does_not_block_unanswered_trigger()
    {
        var catalog = new[]
        {
            new ExerciseContraindicationPattern(MovementPattern.SquatPattern, InjuryFlag.Knee)
        };

        foreach (var clinicianAnswer in new bool?[] { null, false })
        {
            var blocked = InjuryWorseningMovementRule.BlockedPatterns(
                [new InjurySafetyAnswer(InjuryWorseningTrigger.NotAnswered, clinicianAnswer,
                    InjuryFlag.Knee)],
                catalog);
            Assert.Empty(blocked);
        }
    }

    [Fact]
    public void Recorded_trigger_and_clinician_restriction_combine_without_duplicates()
    {
        var answers = new[]
        {
            new InjurySafetyAnswer(InjuryWorseningTrigger.OverheadReach, ClinicianAdvisedAvoid: true,
                Site: InjuryFlag.Shoulder)
        };
        var catalog = new[]
        {
            new ExerciseContraindicationPattern(MovementPattern.VerticalPush, InjuryFlag.Shoulder)
        };

        var blocked = InjuryWorseningMovementRule.BlockedPatterns(answers, catalog);

        Assert.Contains(MovementPattern.VerticalPush, blocked);
        Assert.Contains(MovementPattern.VerticalPull, blocked);
        Assert.Contains(MovementPattern.LateralRaise, blocked);
        Assert.Equal(blocked.Count, blocked.Distinct().Count());
    }
}

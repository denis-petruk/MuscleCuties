using MuscleCuties.Core.Domain.Workout.Planning;
using MuscleCuties.Core.Models.Enums.Cycle;
using MuscleCuties.Core.Models.Workout.Planning;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class PhaseAdjustmentRuleTests
{
    private const PhaseBaselineScale BaselineScale =
        PhaseBaselineScale.QuizMappedOneToFive;
    private const int OneSet = 1;
    private const int TwoSets = 2;
    private const int ThreeSets = 3;
    private const int OptionalAccessoryExerciseId = 101;
    private const int RequiredAccessoryExerciseId = 102;
    private const int OptionalMainExerciseId = 103;
    private const int TwentyMinutes = 20;
    private const int FifteenMinutes = 15;
    private const int TenMinutes = 10;
    private const int FiveMinutes = 5;
    private const int ThreeMinutes = 3;
    private const int LighterDiscomfort = 3;
    private const int HighEnergy = 5;

    [Theory]
    [InlineData(4, 5, PhaseAdjustmentTier.EasierDay)]
    [InlineData(1, 1, PhaseAdjustmentTier.EasierDay)]
    [InlineData(3, 5, PhaseAdjustmentTier.Lighter)]
    [InlineData(1, 2, PhaseAdjustmentTier.Lighter)]
    [InlineData(2, 3, PhaseAdjustmentTier.Maintain)]
    [InlineData(1, 5, PhaseAdjustmentTier.Maintain)]
    [InlineData(0, 5, PhaseAdjustmentTier.Unavailable)]
    [InlineData(1, 6, PhaseAdjustmentTier.Unavailable)]
    public void Tier_Uses_Exact_Ordered_Rules(int discomfort, int energy,
        PhaseAdjustmentTier expected)
    {
        Assert.Equal(expected,
            PhaseAdjustmentRule.SelectTier(new PhaseBaseline(discomfort, energy),
                BaselineScale));
    }

    [Fact]
    public void Four_Phases_Use_Their_Own_Ratings_Not_Phase_Names()
    {
        var baselines = new CyclePhaseBaselines(new(4, 5), new(3, 5),
            new(2, 3), new(1, 1));

        Assert.Equal(PhaseAdjustmentTier.EasierDay,
            PhaseAdjustmentRule.SelectTier(baselines.ForPhase(CyclePhase.Menstrual),
                BaselineScale));
        Assert.Equal(PhaseAdjustmentTier.Lighter,
            PhaseAdjustmentRule.SelectTier(baselines.ForPhase(CyclePhase.Follicular),
                BaselineScale));
        Assert.Equal(PhaseAdjustmentTier.Maintain,
            PhaseAdjustmentRule.SelectTier(baselines.ForPhase(CyclePhase.Ovulatory),
                BaselineScale));
        Assert.Equal(PhaseAdjustmentTier.EasierDay,
            PhaseAdjustmentRule.SelectTier(baselines.ForPhase(CyclePhase.Luteal),
                BaselineScale));
    }

    [Fact]
    public void Unknown_Historical_Rating_Scale_Does_Not_Assign_A_Tier()
    {
        Assert.Equal(PhaseAdjustmentTier.Unavailable,
            PhaseAdjustmentRule.SelectTier(LighterBaseline(),
                PhaseBaselineScale.Unknown));
    }

    [Fact]
    public void Lighter_Strength_Changes_Only_Explicit_Optional_Accessory_Sets()
    {
        var session = Session(ActivityModality.Strength,
            SessionTrainingStyle.General,
            exercises:
            [
                new(Guid.NewGuid(), OptionalAccessoryExerciseId, ThreeSets, true, true),
                new(Guid.NewGuid(), RequiredAccessoryExerciseId, ThreeSets, false, true),
                new(Guid.NewGuid(), OptionalMainExerciseId, ThreeSets, true, false),
                new(Guid.NewGuid(), OptionalAccessoryExerciseId + RequiredAccessoryExerciseId,
                    OneSet, true, true)
            ]);

        var preview = PhaseAdjustmentRule.Preview(Gate(session.SessionId),
            LighterBaseline(), BaselineScale, [session]);

        var change = Assert.Single(preview.FieldChanges);
        Assert.Equal(OptionalAccessoryExerciseId, change.ExerciseId);
        Assert.Equal(ThreeSets, change.AcceptedValue);
        Assert.Equal(TwoSets, change.ProposedValue);
        Assert.True(preview.RequiresStrengthDurationRecalculation);
        Assert.Equal(ThreeSets, session.Exercises[0].AcceptedSets);
    }

    [Theory]
    [InlineData(TwentyMinutes, FifteenMinutes)]
    [InlineData(TenMinutes, FiveMinutes)]
    [InlineData(FiveMinutes, FiveMinutes)]
    [InlineData(ThreeMinutes, ThreeMinutes)]
    public void Lighter_Continuous_Cardio_Never_Increases_Existing_Main_Block(
        int accepted, int expected)
    {
        var session = Session(ActivityModality.Running,
            SessionTrainingStyle.Continuous, mainMinutes: accepted);

        var preview = PhaseAdjustmentRule.Preview(Gate(session.SessionId),
            LighterBaseline(), BaselineScale, [session]);

        if (accepted == expected)
        {
            Assert.Equal(PhaseAdjustmentPreviewStatus.NoMeaningfulChange,
                preview.Status);
            Assert.Empty(preview.FieldChanges);
        }
        else
        {
            var change = Assert.Single(preview.FieldChanges);
            Assert.Equal(PhaseAdjustmentField.ContinuousMainMinutes, change.Field);
            Assert.Equal((decimal)expected, change.ProposedValue);
            Assert.Equal((decimal)accepted, change.AcceptedValue);
        }
        Assert.Equal(accepted, session.ContinuousMainMinutes);
    }

    [Fact]
    public void Continuous_Swimming_Can_Change_But_Intervals_And_Unknown_Pace_Cannot()
    {
        var continuous = Session(ActivityModality.Swimming,
            SessionTrainingStyle.Continuous, mainMinutes: TwentyMinutes);
        var intervals = Session(ActivityModality.Swimming,
            SessionTrainingStyle.Intervals, mainMinutes: TwentyMinutes);
        var distanceOnly = Session(ActivityModality.Swimming,
            SessionTrainingStyle.Continuous);
        var gate = Gate(continuous.SessionId, intervals.SessionId, distanceOnly.SessionId);

        var preview = PhaseAdjustmentRule.Preview(gate, LighterBaseline(), BaselineScale,
            [continuous, intervals, distanceOnly]);

        Assert.Equal(continuous.SessionId, Assert.Single(preview.FieldChanges).SessionId);
    }

    [Fact]
    public void Lighter_Hiit_Offers_OnlyChoicesBackedByExistingTemplates()
    {
        var hiit = Session(ActivityModality.Cycling,
            SessionTrainingStyle.Hiit);

        var preview = PhaseAdjustmentRule.Preview(Gate(hiit.SessionId),
            LighterBaseline(), BaselineScale, [hiit]);

        Assert.Empty(preview.FieldChanges);
        var choices = Assert.Single(preview.SessionChoices).Choices;
        Assert.Contains(PhaseAdjustmentChoice.KeepCurrentSession, choices);
        Assert.Contains(PhaseAdjustmentChoice.SkipFutureSession, choices);
        Assert.DoesNotContain(PhaseAdjustmentChoice.SelectEligibleEasyCardioTemplate, choices);
    }

    [Fact]
    public void Easier_Day_Offers_Choices_Without_Automatic_Reduction()
    {
        var strength = Session(ActivityModality.Strength,
            SessionTrainingStyle.General,
            exercises: [new(Guid.NewGuid(), OptionalAccessoryExerciseId, ThreeSets, true, true)]);
        var gentle = Session(ActivityModality.Yoga,
            SessionTrainingStyle.Gentle);

        var preview = PhaseAdjustmentRule.Preview(
            Gate(strength.SessionId, gentle.SessionId), new PhaseBaseline(4, 5),
            BaselineScale, [strength, gentle]);

        Assert.Empty(preview.FieldChanges);
        var choices = Assert.Single(preview.SessionChoices);
        Assert.Equal(strength.SessionId, choices.SessionId);
        Assert.Contains(PhaseAdjustmentChoice.SkipFutureSession, choices.Choices);
        Assert.Contains(PhaseAdjustmentChoice.KeepCurrentSession, choices.Choices);
    }

    [Fact]
    public void Maintain_And_Missing_Activity_Mapping_Do_Not_Prompt_For_Changes()
    {
        var climbing = Session(ActivityModality.Climbing,
            SessionTrainingStyle.Demanding);
        var gate = Gate(climbing.SessionId);

        Assert.Equal(PhaseAdjustmentPreviewStatus.NoMeaningfulChange,
            PhaseAdjustmentRule.Preview(gate, new PhaseBaseline(2, 3), BaselineScale,
                [climbing]).Status);
        Assert.Equal(PhaseAdjustmentPreviewStatus.NoMeaningfulChange,
            PhaseAdjustmentRule.Preview(gate, LighterBaseline(), BaselineScale,
                [climbing]).Status);
    }

    [Fact]
    public void Unconfirmed_Gate_Cannot_Generate_A_Change_Preview()
    {
        var session = Session(ActivityModality.Running,
            SessionTrainingStyle.Continuous, mainMinutes: TwentyMinutes);
        var gate = Gate(session.SessionId) with
        {
            Status = PhaseReviewGateStatus.NeedsPhaseConfirmation
        };

        Assert.Equal(PhaseAdjustmentPreviewStatus.Unavailable,
            PhaseAdjustmentRule.Preview(gate, LighterBaseline(), BaselineScale,
                [session]).Status);
    }

    [Fact]
    public async Task Preview_Use_Case_Uses_Accepted_Source_Without_Mutating_It()
    {
        var session = Session(ActivityModality.Running,
            SessionTrainingStyle.Continuous, mainMinutes: TwentyMinutes);
        var gateInput = new PhaseReviewGateInput(Guid.NewGuid(), Guid.NewGuid(),
            new DateOnly(2026, 10, 30), new DateTimeOffset(2026, 10, 24, 12, 0, 0,
                TimeSpan.Zero), new DateOnly(2026, 10, 24),
            new PhaseReviewContext(Guid.NewGuid(), CyclePhase.Menstrual,
                PhaseReviewProvenance.UserConfirmed, null), LighterBaseline(),
            PhaseBaselineScale.QuizMappedOneToFive,
            null, null, null, false, true,
            [new PhaseReviewSession(session.SessionId, new DateOnly(2026, 10, 25),
                new DateTimeOffset(2026, 10, 25, 15, 0, 0, TimeSpan.Zero),
                PlannedSessionState.Planned, false, false)],
            new HashSet<PhaseReviewDismissalKey>());
        var source = new SyntheticSource(new ActivePlanPhaseAdjustmentSourceV010(
            gateInput, [session]));

        var result = await new PreviewActivePlanPhaseAdjustmentV010UseCase(source)
            .ExecuteAsync(Guid.NewGuid());

        Assert.Equal(PhaseReviewGateStatus.ReadyForAdjustmentPreview, result.Gate.Status);
        Assert.Single(result.Adjustment.FieldChanges);
        Assert.Equal(TwentyMinutes, session.ContinuousMainMinutes);
        Assert.Equal(1, source.LoadCount);
    }

    private static PhaseBaseline LighterBaseline() => new(LighterDiscomfort, HighEnergy);

    private static PhaseReviewGateResult Gate(params Guid[] sessionIds) =>
        new(PhaseReviewGateStatus.ReadyForAdjustmentPreview,
            new DateOnly(2026, 10, 24), new DateOnly(2026, 10, 30),
            sessionIds, null);

    private static PhaseAdjustmentSession Session(ActivityModality modality,
        SessionTrainingStyle style,
        IReadOnlyList<PhaseAdjustmentExercise>? exercises = null,
        decimal? mainMinutes = null) =>
        new(Guid.NewGuid(), modality, style,
            exercises ?? Array.Empty<PhaseAdjustmentExercise>(), mainMinutes);

    private sealed class SyntheticSource(ActivePlanPhaseAdjustmentSourceV010 snapshot)
        : IActivePlanPhaseAdjustmentSourceV010
    {
        public int LoadCount { get; private set; }

        public Task<ActivePlanPhaseAdjustmentSourceV010?> LoadAsync(Guid userId, string? timeZoneId,
            CancellationToken cancellationToken)
        {
            LoadCount++;
            return Task.FromResult<ActivePlanPhaseAdjustmentSourceV010?>(snapshot);
        }
    }
}

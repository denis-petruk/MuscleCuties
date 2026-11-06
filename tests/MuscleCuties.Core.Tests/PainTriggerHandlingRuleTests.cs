using MuscleCuties.Core.Domain.Workout.Planning;
using Xunit;

namespace MuscleCuties.Core.Tests;

public sealed class PainTriggerHandlingRuleTests
{
    private static readonly List<PainTriggerAlternativeCandidate> ChestCandidates =
    [
        new(ExerciseId: 101, ExerciseName: "Barbell Bench Press", Region: PrimaryMuscleRegion.Chest, InvolvesProvokingPattern: true),
        new(ExerciseId: 102, ExerciseName: "Dumbbell Floor Press", Region: PrimaryMuscleRegion.Chest, InvolvesProvokingPattern: false),
        new(ExerciseId: 103, ExerciseName: "Machine Chest Press", Region: PrimaryMuscleRegion.Chest, InvolvesProvokingPattern: false),
        new(ExerciseId: 201, ExerciseName: "Barbell Back Squat", Region: PrimaryMuscleRegion.Quadriceps, InvolvesProvokingPattern: false)
    ];

    [Fact]
    public void PainZero_NormalExecutionWithoutStopping()
    {
        var report = new PainTriggerReport(
            ExerciseId: 101,
            ExerciseName: "Barbell Bench Press",
            PainScore: 0,
            ReportDate: new DateOnly(2026, 11, 5),
            AffectedRegion: PrimaryMuscleRegion.Chest);

        var result = PainTriggerHandlingRule.Evaluate(report, ChestCandidates);

        Assert.Equal(PainTriggerDisposition.Substituted, result.Disposition);
        Assert.Equal(101, result.ReplacementExerciseId);
        Assert.False(result.ProvokingExerciseStopped);
        Assert.Equal(PainTriggerHandlingRule.ReasonNoPainReported, result.ReasonCode);
    }

    [Fact]
    public void SeverePain_WithholdsImmediatelyAndRecommendsClinicalEvaluation()
    {
        var report = new PainTriggerReport(
            ExerciseId: 101,
            ExerciseName: "Barbell Bench Press",
            PainScore: 8, // >= 7
            ReportDate: new DateOnly(2026, 11, 5),
            AffectedRegion: PrimaryMuscleRegion.Chest);

        var result = PainTriggerHandlingRule.Evaluate(report, ChestCandidates);

        Assert.Equal(PainTriggerDisposition.SeverePainRequiresClinicalAttention, result.Disposition);
        Assert.Null(result.ReplacementExerciseId);
        Assert.True(result.ProvokingExerciseStopped);
        Assert.Equal(PainTriggerHandlingRule.ReasonSeverePainWithheld, result.ReasonCode);
        Assert.Contains("severe pain", result.AdvisoryText, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ModeratePain_SubstitutesWithSafeAlternativeAvoidingProvokingPattern()
    {
        var report = new PainTriggerReport(
            ExerciseId: 101,
            ExerciseName: "Barbell Bench Press",
            PainScore: 4, // 1 to 6
            ReportDate: new DateOnly(2026, 11, 5),
            AffectedRegion: PrimaryMuscleRegion.Chest);

        var result = PainTriggerHandlingRule.Evaluate(report, ChestCandidates);

        Assert.Equal(PainTriggerDisposition.Substituted, result.Disposition);
        Assert.Equal(102, result.ReplacementExerciseId); // Dumbbell Floor Press
        Assert.Equal("Dumbbell Floor Press", result.ReplacementExerciseName);
        Assert.True(result.ProvokingExerciseStopped);
        Assert.Equal(PainTriggerHandlingRule.ReasonSubstitutedSafeAlternative, result.ReasonCode);
    }

    [Fact]
    public void ModeratePain_WithholdsWhenNoSafeStrengthAlternativeExists()
    {
        var report = new PainTriggerReport(
            ExerciseId: 101,
            ExerciseName: "Barbell Bench Press",
            PainScore: 4,
            ReportDate: new DateOnly(2026, 11, 5),
            AffectedRegion: PrimaryMuscleRegion.Chest);

        // All chest alternatives involve the provoking pattern
        var restrictedCandidates = new List<PainTriggerAlternativeCandidate>
        {
            new(ExerciseId: 101, ExerciseName: "Barbell Bench Press", Region: PrimaryMuscleRegion.Chest, InvolvesProvokingPattern: true),
            new(ExerciseId: 105, ExerciseName: "Incline Bench Press", Region: PrimaryMuscleRegion.Chest, InvolvesProvokingPattern: true),
            new(ExerciseId: 201, ExerciseName: "Barbell Back Squat", Region: PrimaryMuscleRegion.Quadriceps, InvolvesProvokingPattern: false)
        };

        var result = PainTriggerHandlingRule.Evaluate(report, restrictedCandidates);

        Assert.Equal(PainTriggerDisposition.Withheld, result.Disposition);
        Assert.Null(result.ReplacementExerciseId);
        Assert.True(result.ProvokingExerciseStopped);
        Assert.Equal(PainTriggerHandlingRule.ReasonWithheldNoSafeAlternative, result.ReasonCode);
        Assert.Contains("no safe strength alternative", result.AdvisoryText, StringComparison.OrdinalIgnoreCase);
    }
}

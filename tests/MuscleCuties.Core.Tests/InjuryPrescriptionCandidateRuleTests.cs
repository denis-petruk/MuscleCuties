using MuscleCuties.Core.Domain.Workout.Planning;

namespace MuscleCuties.Core.Tests;

public sealed class InjuryPrescriptionCandidateRuleTests
{
    private const int OriginalId = 101;
    private const int AlternativeId = 102;
    private const string Slot = "synthetic-slot";
    private static readonly DateOnly Clearance = DateOnly.Parse("2026-10-01");
    private static readonly DateOnly WithinWindow = DateOnly.Parse("2026-10-08");
    private static readonly DateOnly AfterWindow = DateOnly.Parse("2026-10-09");

    private static InjuryPrescriptionCandidateEvidence Evidence() => new(
        OriginalId, Slot, OriginalId, Slot, true, [new(false, Clearance)], false,
        true, true, true, true, false, WithinWindow);

    [Fact]
    public void RestoredOriginalUsesOriginalClearanceAndOptionality()
    {
        var evidence = Evidence();
        Assert.Equal(InjuryPrescriptionCandidateDisposition.OriginalOptional, InjuryPrescriptionCandidateRule.Evaluate(evidence));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.OriginalRequired,
            InjuryPrescriptionCandidateRule.Evaluate(evidence with { EffectiveDate = AfterWindow }));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.OriginalOptional,
            InjuryPrescriptionCandidateRule.Evaluate(evidence with { EffectiveDate = AfterWindow, OriginalWasOptional = true }));
    }

    [Fact]
    public void ActiveOrUnknownEpisodesBlockOriginalButReviewedAlternativeCanBeUsed()
    {
        var evidence = Evidence() with { Episodes = [new(true, null)] };
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked, InjuryPrescriptionCandidateRule.Evaluate(evidence));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Alternative,
            InjuryPrescriptionCandidateRule.Evaluate(evidence with { ProposedExerciseId = AlternativeId }));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked,
            InjuryPrescriptionCandidateRule.Evaluate(evidence with { ProposedExerciseId = AlternativeId, CurrentRestrictionsPermit = false }));
    }

    [Fact]
    public void OmissionNeedsCompleteReviewAndVerifiedNoSafeCandidate()
    {
        var missing = Evidence() with { ProposedExerciseId = null, ProposedSlotId = null };
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked, InjuryPrescriptionCandidateRule.Evaluate(missing));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Omitted,
            InjuryPrescriptionCandidateRule.Evaluate(missing with { NoSafeCandidateVerified = true }));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked,
            InjuryPrescriptionCandidateRule.Evaluate(missing with { NoSafeCandidateVerified = true, EpisodeSetComplete = false }));
    }

    [Fact]
    public void UnknownOrContradictoryClearanceBlocksAlternativeAndOmission()
    {
        var alternative = Evidence() with { ProposedExerciseId = AlternativeId, Episodes = [new(false, null)] };
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked, InjuryPrescriptionCandidateRule.Evaluate(alternative));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked,
            InjuryPrescriptionCandidateRule.Evaluate(alternative with { Episodes = [new(true, Clearance)] }));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked,
            InjuryPrescriptionCandidateRule.Evaluate(alternative with { ProposedExerciseId = null, NoSafeCandidateVerified = true }));
    }

    [Fact]
    public void OverlappingClearancesKeepOriginalOptionalUntilLastWindowEnds()
    {
        var evidence = Evidence() with
        {
            Episodes = [new(false, Clearance), new(false, WithinWindow)],
            EffectiveDate = AfterWindow
        };
        Assert.Equal(InjuryPrescriptionCandidateDisposition.OriginalOptional, InjuryPrescriptionCandidateRule.Evaluate(evidence));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked,
            InjuryPrescriptionCandidateRule.Evaluate(evidence with { Episodes = [new(false, Clearance), new(true, null)] }));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked,
            InjuryPrescriptionCandidateRule.Evaluate(evidence with { DoseReviewedAndValid = false }));
        Assert.Equal(InjuryPrescriptionCandidateDisposition.Blocked,
            InjuryPrescriptionCandidateRule.Evaluate(evidence with { ProposedSlotId = "different-slot" }));
    }
}

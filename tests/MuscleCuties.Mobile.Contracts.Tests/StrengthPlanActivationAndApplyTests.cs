using MuscleCuties.UI.ViewModels.Workout;
using MuscleCuties.UseCases.Features.Workout.Planning;
using Xunit;

namespace MuscleCuties.Mobile.Contracts.Tests;

/// <summary>
/// View-model-level decisions of the planning page (G-01, G-02, U-04/U-05). The page view model
/// delegates activation, replacement confirmation, the Apply acceptance and the disabled-Apply reason
/// to <see cref="StrengthPlanGenerationDecisions"/>; these tests drive it with real transport DTOs.
/// </summary>
public sealed class StrengthPlanActivationAndApplyTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReplacementStaysInPreviewAfterGeneration(bool userRequested)
    {
        var proposal = Proposal(Preview(), replaces: true);

        Assert.False(StrengthPlanGenerationDecisions.MayActivateWithoutApply(
            proposal, userRequested, missingRequiredAnswers: false, hasOptionalOmissions: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void FirstFeasiblePlanKeepsTheAutoApplyException(bool userRequested)
    {
        Assert.True(StrengthPlanGenerationDecisions.MayActivateWithoutApply(
            Proposal(Preview()), userRequested, missingRequiredAnswers: false, hasOptionalOmissions: false));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ReducedVolumeFirstPlanAutoAppliesWithoutClaimingAcknowledgment(bool userRequested)
    {
        var proposal = Proposal(Preview() with { RequiresReducedVolumeAcknowledgment = true });

        Assert.True(StrengthPlanGenerationDecisions.MayActivateWithoutApply(
            proposal, userRequested, missingRequiredAnswers: false, hasOptionalOmissions: false));
        var acceptance = StrengthPlanGenerationDecisions.BuildAcceptance(proposal, proposal.Lease!, fromApplyTap: false);
        Assert.False(acceptance.ReducedVolumeAcknowledged);
        Assert.False(acceptance.ReplacementConfirmed);
    }

    [Fact]
    public void Automatically_applied_reduced_volume_does_not_claim_user_acknowledgment()
    {
        var proposal = Proposal(Preview() with { RequiresReducedVolumeAcknowledgment = true });
        var request = StrengthPlanGenerationDecisions.BuildAcceptance(proposal, proposal.Lease!, fromApplyTap: false);
        Assert.False(request.ReducedVolumeAcknowledged);
        Assert.False(request.ReplacementConfirmed);
    }

    [Fact]
    public async Task DecliningAnUndisclosedReplacementKeepsTheActivePlan()
    {
        var proposal = Proposal(Preview(), replaces: true);
        var asked = 0;

        var proceed = await StrengthPlanGenerationDecisions.ConfirmReplacementAsync(proposal,
            replacementDisclosedUpFront: false, _ => { asked++; return Task.FromResult(false); },
            () => "summary");

        Assert.False(proceed);
        Assert.Equal(1, asked);
    }

    [Fact]
    public async Task ApplyOnADisclosedReplacementIsTheAcceptanceAndConfirmsReplacement()
    {
        var proposal = Proposal(Preview(), replaces: true);

        var proceed = await StrengthPlanGenerationDecisions.ConfirmReplacementAsync(proposal,
            replacementDisclosedUpFront: true, _ => throw new InvalidOperationException("no second prompt"),
            () => "summary");

        Assert.True(proceed);
        Assert.True(StrengthPlanGenerationDecisions.BuildAcceptance(proposal, proposal.Lease!).ReplacementConfirmed);
    }

    [Fact]
    public async Task MissingConfirmationSurfaceNeverReplacesSilently()
    {
        Assert.False(await StrengthPlanGenerationDecisions.ConfirmReplacementAsync(
            Proposal(Preview(), replaces: true), false, null, () => "summary"));
    }

    [Theory]
    [InlineData("Limited")]
    [InlineData("InvalidInput")]
    [InlineData("RequiredAnswersMissing")]
    [InlineData("StrengthStyleChoiceRequired")]
    [InlineData("SomeFutureStatus")]
    public void EveryNonFeasibleStatusHasAVisibleApplyReason(string status)
    {
        var preview = Preview(status) with { LimitationCodes = ["StrengthSessionsExceedAvailableWeekdays"] };

        var reason = StrengthPlanGenerationDecisions.ApplyUnavailableReason(preview, Proposal(preview), false, false);

        Assert.False(string.IsNullOrWhiteSpace(reason));
    }

    [Fact]
    public void NonFeasibleWithoutCodesStillExplainsItself()
    {
        var preview = Preview("Limited");

        Assert.Equal("Workout_Generation_LimitationGeneral",
            StrengthPlanGenerationDecisions.ApplyUnavailableReason(preview, Proposal(preview), false, false));
    }

    [Fact]
    public void FeasibleWeekWithoutStrengthSessionsExplainsTheDisabledApply()
    {
        var preview = Preview() with { Sessions = [] };

        Assert.Equal(StrengthPlanGenerationDecisions.NoStrengthSessionsKey,
            StrengthPlanGenerationDecisions.ApplyUnavailableReason(preview, Proposal(preview), false, false));
    }

    [Fact]
    public void UnsavedLeaselessAndChangedPreviewsEachHaveAReason()
    {
        var preview = Preview();
        Assert.Equal(StrengthPlanGenerationDecisions.PreviewNotSavedKey,
            StrengthPlanGenerationDecisions.ApplyUnavailableReason(preview, Proposal(preview) with { IsPersisted = false }, false, false));
        Assert.Equal(StrengthPlanGenerationDecisions.PreviewLeaseUnavailableKey,
            StrengthPlanGenerationDecisions.ApplyUnavailableReason(preview, Proposal(preview) with { Lease = null }, false, false));
        Assert.Equal(StrengthPlanGenerationDecisions.PlanChangedKey,
            StrengthPlanGenerationDecisions.ApplyUnavailableReason(preview, Proposal(preview), invalidated: true, false));
        Assert.Equal(string.Empty,
            StrengthPlanGenerationDecisions.ApplyUnavailableReason(preview, Proposal(preview), false, false));
    }

    [Fact]
    public void WithheldSessionsAreDisclosed()
    {
        var one = Preview() with { AdvisoryCodes = ["SessionWithheld:MC-FB-2D-B"] };
        var two = Preview() with { AdvisoryCodes = ["SessionWithheld:MC-FB-2D-A", "SessionWithheld:MC-FB-2D-B"] };

        Assert.Equal("Workout_Generation_SessionWithheldOne", StrengthPlanGenerationDisplayMapper.SessionWithheldNote(one));
        Assert.Equal("Workout_Generation_SessionWithheldManyFormat", StrengthPlanGenerationDisplayMapper.SessionWithheldNote(two));
        Assert.Equal(string.Empty, StrengthPlanGenerationDisplayMapper.SessionWithheldNote(Preview()));
    }

    [Fact]
    public void Automatic_reduced_volume_without_coverage_codes_has_a_visible_notice()
    {
        var preview = Preview() with { RequiresReducedVolumeAcknowledgment = true };
        var notes = StrengthPlanGenerationDisplayMapper.PostAcceptanceNotices(preview);
        Assert.Contains("Workout_Generation_ReducedVolumeNotice", notes);
    }

    [Theory]
    [InlineData("OneDayFullBodyHasLimitedWeeklyCoverage")]
    [InlineData("StrengthDaysInsufficientForMinimumCoverage")]
    [InlineData("PrimaryRegionCoverageBelowMinimum_Chest")]
    [InlineData("PrimaryRegionCoverageBelowPreferred_Glutes")]
    [InlineData("LowerBodyCoverageBelowMinimum_Hamstrings")]
    public void Coverage_disclosures_are_visible_for_advisories_and_limitations(string code)
    {
        var withAdvisory = Preview() with { AdvisoryCodes = [code] };
        var withLimitation = Preview("Limited") with { LimitationCodes = [code] };
        Assert.NotEmpty(StrengthPlanGenerationDisplayMapper.CoverageNotes(withAdvisory));
        Assert.NotEmpty(StrengthPlanGenerationDisplayMapper.CoverageNotes(withLimitation));
    }

    [Theory]
    [InlineData("WeeklyCoverageBelowTarget_DirectGluteDays", "Workout_Generation_BelowTargetDirectGluteDays")]
    [InlineData("WeeklyCoverageMissing_GluteFocusHipAbduction", "Workout_Generation_MissingGluteFocusAbduction")]
    [InlineData("WeeklyMovementCoverageMissing_KneeExtension", "Workout_Generation_MissingKneeExtension")]
    [InlineData("WeeklyMovementCoverageMissing_KneeFlexion", "Workout_Generation_MissingKneeFlexion")]
    [InlineData("WeeklyMovementCoverageMissing_HipExtension", "Workout_Generation_MissingHipExtension")]
    [InlineData("WeeklyMovementCoverageUnknown", "Workout_Generation_MovementCoverageUnknown")]
    public void Weekly_movement_gaps_are_shown_in_preview_and_after_automatic_apply(string code, string expectedKey)
    {
        var preview = Preview() with { AdvisoryCodes = [code] };
        Assert.Contains(expectedKey, StrengthPlanGenerationDisplayMapper.CoverageNotes(preview));
        Assert.Contains(expectedKey, StrengthPlanGenerationDisplayMapper.PostAcceptanceNotices(preview));
    }

    [Fact]
    public void Automatically_applied_reduced_volume_plan_has_a_visible_compromise_notice()
    {
        var preview = Preview() with { RequiresReducedVolumeAcknowledgment = true };
        Assert.NotEmpty(StrengthPlanGenerationDisplayMapper.CoverageNotes(preview));
    }

    [Fact]
    public void Coverage_disclosure_identifies_each_known_affected_muscle_group()
    {
        var preview = Preview() with { AdvisoryCodes = ["PrimaryRegionCoverageBelowMinimum_Chest", "LowerBodyCoverageBelowMinimum_Glutes"] };
        var notes = StrengthPlanGenerationDisplayMapper.CoverageNotes(preview);
        Assert.Contains(notes, note => note.Contains("Workout_Generation_CoverageMuscle_Chest"));
        Assert.Contains(notes, note => note.Contains("Workout_Generation_CoverageMuscle_Glutes"));
    }

    [Fact]
    public void Manual_apply_of_first_reduced_volume_plan_records_the_actual_acknowledgment()
    {
        var proposal = Proposal(Preview() with { RequiresReducedVolumeAcknowledgment = true });
        var request = StrengthPlanGenerationDecisions.BuildAcceptance(proposal, proposal.Lease!, fromApplyTap: true);
        Assert.True(request.ReducedVolumeAcknowledged);
    }

    [Fact]
    public void Automatically_applied_partial_plan_discloses_a_withheld_session()
    {
        var preview = Preview() with { AdvisoryCodes = ["SessionWithheld:MC-FB-2D-B"] };
        Assert.Contains("Workout_Generation_SessionWithheldOne", StrengthPlanGenerationDisplayMapper.PostAcceptanceNotices(preview));
    }

    [Fact]
    public void Automatically_applied_partial_plan_names_an_omitted_activity()
    {
        var activity = new MixedActivityScheduleDecisionV020(3, "Omitted", null, 1, 1, 0, []);
        var preview = Preview() with { MixedActivitySchedule = new MixedActivityScheduleV020([], [activity], []) };
        var display = StrengthPlanGenerationDisplayMapper.ToMixedActivityDecisionDisplay(activity);
        Assert.Contains(StrengthPlanGenerationDisplayMapper.PostAcceptanceNotices(preview),
            note => note.Contains(display.Activity) && note.Contains(display.Status));
    }

    private static StrengthPlanProposalCreatedV020 Proposal(StrengthPlanPreviewResponseV020 preview,
        bool replaces = false) =>
        new(Guid.Parse("22222222-2222-2222-2222-222222222222"), preview, IsPersisted: true,
            new PlanningLeaseDto(Guid.Parse("33333333-3333-3333-3333-333333333333"), 7,
                DateTimeOffset.UnixEpoch.AddYears(60)),
            ProposalVersion: 3, ReplacesExistingPlan: replaces);

    private static StrengthPlanPreviewResponseV020 Preview(string status = "Feasible") =>
        new("MC-WORKOUT-POLICY", "0.2.0", "MC-PLANNING-0.2", status, 1,
            [new StrengthPlanPreviewSessionV020("MC-FB-2D-A", "v1", 0, 45m,
                Array.Empty<StrengthPlanSlotOutputV020>(), Array.Empty<string>(),
                new Dictionary<string, string>(), DayOfWeek.Monday)],
            Array.Empty<string>(), Array.Empty<StrengthExerciseReviewConditionV020>(), Array.Empty<string>());
}

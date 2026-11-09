using MuscleCuties.UI.Services.Localization;
using MuscleCuties.UseCases.Features.Workout.Planning;

namespace MuscleCuties.UI.ViewModels.Workout;

/// <summary>
/// The planning page's activation and Apply-availability decisions, kept free of MAUI so they can be
/// exercised directly. The view model delegates to these members; it does not repeat their logic.
/// </summary>
internal static class StrengthPlanGenerationDecisions
{
    internal const string FeasibleStatus = "Feasible";
    internal const string PlanChangedKey = "Workout_Generation_PlanChanged";
    internal const string PreviewNotSavedKey = "Workout_Generation_PreviewNotSaved";
    internal const string PreviewLeaseUnavailableKey = "Workout_Generation_PreviewLeaseUnavailable";
    internal const string NoStrengthSessionsKey = "Workout_Generation_ApplyUnavailableNoStrengthSessions";
    internal const string NoSafeStrengthContentRemainingKey =
        "Workout_Generation_Reason_NoSafeStrengthContentRemaining";
    private const string NoSafeStrengthStyleFitCode = "NoSafeStrengthStyleFit";

    /// <summary>
    /// G-01/G-02: whether a just-generated proposal may be applied without an Apply tap. Only the
    /// first-plan exception qualifies; a replacement or a reduced-volume plan always stays in the
    /// preview until the user taps Apply plan.
    /// </summary>
    public static bool MayActivateWithoutApply(StrengthPlanProposalCreatedV020? proposal, bool userRequested,
        bool missingRequiredAnswers, bool hasOptionalOmissions)
    {
        if (proposal is null)
            return false;

        var preview = proposal.Preview;
        var feasible = string.Equals(preview.Status, FeasibleStatus, StringComparison.Ordinal);
        var reducedVolume = preview.RequiresReducedVolumeAcknowledgment();
        return userRequested
            ? WorkoutPlanningFlow.CanAutoActivateRequestedPlan(
                userRequested: true,
                replacesExistingPlan: proposal.ReplacesExistingPlan,
                feasible: feasible,
                persisted: proposal.IsPersisted,
                hasLease: proposal.Lease is not null,
                missingRequiredAnswers: missingRequiredAnswers,
                styleChoiceRequired: preview.StrengthStyleChoiceRequired,
                reducedVolume: reducedVolume)
            : WorkoutPlanningFlow.CanAutomaticallyApplyFirstPlan(proposal.ReplacesExistingPlan, feasible,
                proposal.IsPersisted, missingRequiredAnswers, preview.StrengthStyleChoiceRequired,
                hasOptionalOmissions, reducedVolume);
    }

    /// <summary>
    /// Apply is the user's acceptance. A replacement whose consequence was not disclosed on an
    /// initiating Generate/Regenerate is stated once more before it is sent; declining keeps the
    /// active plan and sends nothing.
    /// </summary>
    public static async Task<bool> ConfirmReplacementAsync(StrengthPlanProposalCreatedV020 proposal,
        bool replacementDisclosedUpFront, Func<string, Task<bool>>? confirmReplacementAsync,
        Func<string> replacementSummary)
    {
        if (!WorkoutPlanningFlow.RequiresReplacementConfirmation(proposal.ReplacesExistingPlan,
                replacementDisclosedUpFront))
            return true;

        return confirmReplacementAsync is not null && await confirmReplacementAsync(replacementSummary());
    }

    /// <summary>Everything the Apply tap accepts for the visible preview and its live lease.</summary>
    public static AcceptStrengthPlanProposalRequestV020 BuildAcceptance(StrengthPlanProposalCreatedV020 proposal,
        PlanningLeaseDto lease)
    {
        var acceptance = WorkoutPlanningFlow.AcceptanceAcknowledgments(
            proposal.ReplacesExistingPlan,
            proposal.Preview.RequiresReducedVolumeAcknowledgment());
        return new AcceptStrengthPlanProposalRequestV020(
            proposal.ProposalId, lease.LeaseId, lease.Fence, proposal.ProposalVersion,
            ReplacementConfirmed: acceptance.ReplacementConfirmed,
            ReducedVolumeAcknowledged: acceptance.ReducedVolumeAcknowledged);
    }

    /// <summary>
    /// U-04/U-05: the reason Apply is unavailable for the visible preview, in the user's language, or
    /// an empty string when nothing about the preview blocks Apply. Every disabled-Apply state that a
    /// person can see has a reason; transient states (busy, already applied) are not reasons.
    /// </summary>
    public static string ApplyUnavailableReason(StrengthPlanPreviewResponseV020? preview,
        StrengthPlanProposalCreatedV020? proposal, bool invalidated, bool uncertainAcceptance)
    {
        if (preview is null)
            return string.Empty;

        if (invalidated && !uncertainAcceptance)
            return UiTextCatalog.Get(PlanChangedKey);

        if (!string.Equals(preview.Status, FeasibleStatus, StringComparison.Ordinal))
            return StrengthPlanGenerationDisplayMapper.GetPreviewLimitationMessage(preview);

        if (proposal is not { IsPersisted: true })
            return UiTextCatalog.Get(PreviewNotSavedKey);

        if (proposal.Lease is null)
            return UiTextCatalog.Get(PreviewLeaseUnavailableKey);

        if (preview.Sessions.Count == 0)
            return UiTextCatalog.Get(preview.AdvisoryCodes.Contains(NoSafeStrengthStyleFitCode)
                ? NoSafeStrengthContentRemainingKey
                : NoStrengthSessionsKey);

        return string.Empty;
    }
}

public static class StrengthPlanPreviewExtensions
{
    private static readonly HashSet<string> ReducedVolumeCodes = new(StringComparer.OrdinalIgnoreCase)
    {
        "ReducedVolume",
        "VolumeReduction",
        "HighVolumeConservativeStart"
    };

    public static bool RequiresReducedVolumeAcknowledgment(this StrengthPlanPreviewResponseV020? preview)
    {
        if (preview is null)
            return false;

        if (preview.RequiresReducedVolumeAcknowledgment)
            return true;

        if (preview.AdvisoryCodes.Any(code => ReducedVolumeCodes.Contains(code)))
            return true;

        if (preview.LimitationCodes.Any(code => ReducedVolumeCodes.Contains(code)))
            return true;

        if (preview.MixedActivitySchedule?.InputReasonCodes.Any(code => ReducedVolumeCodes.Contains(code)) == true)
            return true;

        return preview.MixedActivitySchedule?.Activities.Any(
            decision => decision.ReasonCodes.Any(code => ReducedVolumeCodes.Contains(code))) == true;
    }
}
